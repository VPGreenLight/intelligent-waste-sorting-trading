# ĐẶC TẢ HỢP ĐỒNG API CHI TIẾT (API CONTRACT SPECIFICATION)
## PHÂN HỆ M-12 / FE-14: GÓI DỊCH VỤ NỘI BỘ & TÍCH HỢP VIETQR (`IN-SYSTEM SERVICE PACKAGES & VIETQR INTEGRATION`)

* **Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform (`SEP490_G141`)
* **Phiên bản tài liệu:** `v1.0.0` (Cập nhật đồng bộ với mã nguồn `Ecolink.WebApi` / `.NET 10`)
* **Use Cases bao phủ:**
  * `U-16`: Citizen Purchase In-System Services via VietQR
  * `R-07`: Recycler Purchase In-System Services via VietQR
  * `A-10`: Configure In-System Service Fees
  * `A-11`: Manage Financial Reconciliation
* **Controllers liên quan:** `ServicePaymentController`, `ServicePackagesController`, `AdminReconciliationController`
* **Phạm vi Endpoint:** `15` REST APIs (`STT 115` – `STT 129` trong [00_api_inventory_and_endpoint_statistics.md](./00_api_inventory_and_endpoint_statistics.md))
* **Tiêu chuẩn phản hồi:** Chuẩn hóa theo `ApiResponse<T>` trong [00_api_overview_and_response_contract.md](./00_api_overview_and_response_contract.md) (`camelCase`, Enum serialize dạng chuỗi qua `JsonStringEnumConverter`).

---

## 1. TỔNG QUAN KIẾN TRÚC & MA TRẬN API PHÂN HỆ `M-12`

### 1.1. Mô hình Thuê bao Dịch vụ Nội bộ 3 Cấp bậc (Good – Better – Best)

Để tách bạch hoàn toàn hoạt động thu mua phế liệu trực tiếp ngoài đời thực (không giữ tiền hộ / không escrow theo `BR-DSP-04`) khỏi nguồn thu SaaS của nền tảng, EcoLink vận hành 3 gói dịch vụ chuẩn (`CanonicalServicePackages`):

| Mã gói (`packageCode`) | Tên hiển thị | Đối tượng (`targetRole`) | Yêu cầu KYB (`requiresVerifiedKyb`) | Đơn giá mặc định (`priceVnd`) | Chu kỳ (`durationDays`) | Đặc quyền (`features`) |
|:---|:---|:---|:---:|:---:|---:|:---|
| **`BASIC`** | Basic (Mặc định) | `All` | `false` | `0 VNĐ` (`isDefaultFree = true`) | `0` (Vĩnh viễn) | `adFree = false`, `dailyPointMultiplier = 1.0`, các tính năng AI/Bản đồ/Bàn giao cơ bản |
| **`PREMIUM`** | Premium (Tắt quảng cáo) | `All` (`Citizen` & `Recycler`) | `false` | `59,000 VNĐ` | `30 ngày` | `adFree = true`, `ecoPlusBadge = true`, `dailyPointMultiplier = 1.2` |
| **`PRO`** | Pro (Tích xanh & Ưu tiên bản đồ) | `Recycler` | `true` (`VerificationStatus.Verified`) | `1,000,000 VNĐ` | `365 ngày` | `adFree = true`, `ecoPlusBadge = true`, `verifiedBlueBadge = true`, `featuredMapMarker = true`, `mapPriorityBoost = 100`, `dailyPointMultiplier = 1.5` |

### 1.2. Kiến trúc Quy tắc Thanh toán, Cộng dồn & Đối soát (`BR-PAY-01`..`BR-PAY-06`)

1. **Tạo đơn & VietQR Động (`BR-PAY-01`, `BR-PAY-03`):**
   * Khách hàng gửi `packageId` hoặc `packageCode` (tương thích ngược với `amount`/`description`). Máy chủ luôn lấy giá trị thực từ `service_packages.price_vnd` và chặn tạo QR cho gói `IsDefaultFree` (`BASIC - 0đ`).
   * Mã đơn hàng chuẩn có dạng `ECOyyyyMMddXXXX` (hoặc `PayOSOrderCode`), nội dung chuyển khoản chuẩn `ECOLINK <orderCode>` và hạn QR mặc định **15 phút** (`QrExpiresAt = utcNow + 15m`).
   * Hỗ trợ 2 chế độ sinh QR: gọi PayOS API v2 khi đủ `PayOS:ClientId/ApiKey/ChecksumKey`, hoặc tự động fallback sang chuẩn **NAPAS 24/7 EMVCo (`img.vietqr.io` + CRC16-CCITT `0x1021`)**.
2. **Idempotency & Xử lý Webhook 5 Kịch bản (`BR-PAY-02`, `BR-PAY-06`):**
   * Cổng `POST /api/v1/service-payments/webhook` xác thực chữ ký HMAC/Header Secret (`X-VietQr-Webhook-Secret`, `signature`) và tính khóa chống trùng `IdempotencyKey = SHA256(gatewaySource | refToken | resolvedOrderCode)`.
   * Phân loại tự động:
     1. `Matched` (`received == expected` trong hạn QR) -> Đánh dấu `Paid`, kích hoạt gói tức thì, vô hiệu hóa cache `IMemoryCache` (`sub_entitlements:{userId}`).
     2. `DuplicateIgnored` (`IdempotencyKey` đã tồn tại hoặc đơn đã `Paid`) -> Trả `200 OK` (`ALREADY_PROCESSED`), không kích hoạt lặp (`BR-PAY-02`).
     3. `Underpaid` (`received < expected`) -> Giữ nguyên `Pending`, gán `ReconciliationStatus = Underpaid`, `AnomalyType = Underpaid`, ghi vào sổ cái Admin (`ANOMALY_FLAGGED`).
     4. `Overpaid` (`received > expected` trong hạn) -> Vẫn kích hoạt gói ngay để đảm bảo UX (`OrderStatus = Paid`), đồng thời đánh dấu `ReconciliationStatus = Overpaid` chờ Admin đối soát hoàn tiền dư.
     5. `LateTransfer` (đơn `Expired`/`Cancelled` hoặc quá `QrExpiresAt`) -> Chuyển thành `Expired` (nếu đang `Pending`), gán `ReconciliationStatus = LateTransfer`, KHÔNG tự kích hoạt gói, chuyển hàng đợi Admin.
     6. `UnmatchedReference` (không khớp `OrderCode` / `GatewayOrderCode` nào) -> Lưu bản ghi `PaymentWebhookLog` với `MatchedOrderId = null` (`WH-...`), gán `UnmatchedReference` vào sổ cái Admin.
3. **Cơ chế Thời hạn Cộng dồn & Nâng bậc (`BR-PAY-04`):**
   * Gia hạn **cùng `PackageCode`**: Nối tiếp từ `MAX(utcNow, latestSamePackageExpiry)` thêm `DurationDaysSnapshot`.
   * Nâng bậc **khác `PackageCode`** (ví dụ `PREMIUM` -> `PRO`): Gói mới `PRO` khởi chạy tức thì từ `utcNow` để mở khóa Tích xanh & Ghim nổi bật bản đồ (`GeoMatchingService` truy vấn `ISubscriptionEntitlementService.GetBatchEntitlementsAsync`), sắp xếp theo thứ tự `IsFeatured DESC -> HasVerifiedBadge DESC -> DistanceKm ASC`.
4. **Grandfathering (`BR-PAY-05`):**
   * Khi Admin chỉnh sửa giá hoặc quyền lợi (`A-10`), chỉ các lượt mua mới bị ảnh hưởng; các `ServiceOrder` và `UserSubscription` đang hiệu lực giữ nguyên `FeaturesSnapshot` và `DurationDaysSnapshot` lúc mua. Gói `BASIC` không thể bị tắt (`IsActive = false`).

### 1.3. Danh mục 15 API Phân hệ `M-12` (`STT 115` – `STT 129`)

| STT | Phương thức | Tuyến đường Endpoint | Controller Action | RBAC Role | Use Case |
|:---:|:---:|:---|:---|:---|:---|
| **115** | `POST` | `/api/v1/service-payments/create` | `CreateServicePayment` | `User`, `Citizen`, `Recycler`, `Collector`, `Admin` | `U-16`, `R-07` |
| **116** | `GET` | `/api/v1/service-payments/orders/{orderCode}` | `GetOrderStatus` | Authenticated (Chủ đơn / `Admin`) | `U-16`, `R-07` |
| **117** | `GET` | `/api/v1/service-payments/orders` | `GetAllOrders` | Authenticated | `U-16`, `R-07` |
| **118** | `POST` | `/api/v1/service-payments/webhook` | `ReceivePaymentWebhook` | Public (`AllowAnonymous`, Gateway Secret/HMAC) | `U-16`, `R-07` |
| **119** | `POST` | `/api/v1/service-payments/simulate-transfer/{orderCode}` | `SimulateTransfer` | Public (`AllowAnonymous`, Dev/Test Sandbox) | `U-16`, `R-07` |
| **120** | `GET` | `/api/v1/service-packages` | `GetAvailablePackages` | Authenticated | `U-16`, `R-07` |
| **121** | `GET` | `/api/v1/service-packages/subscriptions/me` | `GetMySubscriptions` | Authenticated | `U-16`, `R-07` |
| **122** | `POST` | `/api/v1/service-payments/orders/{orderCode}/cancel` | `CancelPendingOrder` | Authenticated (Chủ đơn) | `U-16`, `R-07` |
| **123** | `GET` | `/api/v1/admin/service-packages` | `GetAllServicePackages` | `Admin` | `A-10` |
| **124** | `POST` | `/api/v1/admin/service-packages` | `CreateServicePackage` | `Admin` | `A-10` |
| **125** | `PUT` | `/api/v1/admin/service-packages/{packageId:guid}` | `UpdateServicePackage` | `Admin` | `A-10` |
| **126** | `PATCH` | `/api/v1/admin/service-packages/{packageId:guid}/status` | `TogglePackageStatus` | `Admin` | `A-10` |
| **127** | `GET` | `/api/v1/admin/reconciliations/ledger` | `GetPaymentLedger` | `Admin` | `A-11` |
| **128** | `PATCH` | `/api/v1/admin/reconciliations/orders/{orderCode}/resolve` | `ResolvePaymentAnomaly` | `Admin` | `A-11` |
| **129** | `GET` | `/api/v1/admin/reconciliations/export` | `ExportReconciliationReport` | `Admin` | `A-11` |

---

## 2. ĐẶC TẢ CHI TIẾT CÁC ENDPOINTS (`STT 115` – `STT 129`)

### 2.1. Nhóm Thanh toán VietQR, Lịch sử Đơn hàng & Webhook (`ServicePaymentController`)

#### [STT 115] Khởi tạo đơn thanh toán dịch vụ & Mã VietQR động
* **Endpoint:** `POST /api/v1/service-payments/create`
* **RBAC:** `Authorize(Roles = "User,Citizen,Recycler,Collector,Admin")`
* **Use Case / Business Rules:** `U-16`, `R-07` | `BR-PAY-01`, `BR-PAY-03`
* **Request Headers:** `Authorization: Bearer <jwt_token>`
* **Request Body (`CreateServicePaymentRequest`):**
  ```json
  {
    "packageId": "51000001-0000-0000-0000-000000000002",
    "packageCode": "PREMIUM",
    "returnUrl": "https://ecolink.vn/payment/success",
    "cancelUrl": "https://ecolink.vn/payment/cancel"
  }
  ```
  > *Ghi chú:* Nếu truyền `packageId` hoặc `packageCode`, máy chủ bỏ qua mọi giá trị `amount` từ client và lấy trực tiếp từ `service_packages`. Với luồng tương thích cũ không truyền gói, `amount <= 0` trả về `400 Bad Request` (`MSG-PAY-03`). Nếu chọn gói `PRO` mà tài khoản không phải `Recycler` hoặc chưa `VerificationStatus == Verified`, trả về `403`/`400` tương ứng (`BR-PAY-03`).
* **Response `200 OK` (`MSG-PAY-01`):**
  ```json
  {
    "success": true,
    "messageCode": "MSG-PAY-01",
    "message": "Quét mã VietQR này để hoàn tất thanh toán dịch vụ EcoLink.",
    "data": {
      "id": "8e12a901-7410-4b23-89c1-112233445566",
      "userId": "3f9a1c10-0000-0000-0000-000000000001",
      "packageId": "51000001-0000-0000-0000-000000000002",
      "packageCode": "PREMIUM",
      "packageName": "Premium (Tắt quảng cáo)",
      "durationDays": 30,
      "orderCode": "ECO202611170001",
      "amount": 59000,
      "paidAmount": null,
      "description": "Thanh toán gói Premium (Tắt quảng cáo)",
      "transferContent": "ECOLINK ECO202611170001",
      "bankCode": "MB",
      "bankName": "MB Bank",
      "accountNo": "0123456789",
      "accountName": "ECOLINK PLATFORM",
      "qrCodeUrl": "https://img.vietqr.io/image/MB-0123456789-compact2.png?amount=59000&addInfo=ECOLINK%20ECO202611170001&accountName=ECOLINK%20PLATFORM",
      "qrPayload": "00020101021238570010A00000072701270006970422011001234567890208QRIBFTTA53037045405590005802VN62260822ECOLINK ECO2026111700016304ABCD",
      "checkoutUrl": null,
      "status": "PENDING",
      "reconciliationStatus": "PendingMatch",
      "anomalyType": "None",
      "anomalyNote": null,
      "createdAt": "2026-11-17T01:00:00Z",
      "expiresAt": "2026-11-17T01:15:00Z",
      "paidAt": null,
      "transactionReference": null,
      "activatedSubscription": null
    }
  }
  ```
* **Error Responses:**
  * `400 Bad Request` (`MSG-PAY-03` / `MSG-GEN-01`): Gói `BASIC` miễn phí không thể thanh toán, gói ngừng mở bán (`IsActive == false`), hoặc `amount <= 0`.
  * `403 Forbidden`: Tài khoản không thuộc `TargetRole` của gói hoặc cơ sở chưa đạt `Verified` KYB khi mua gói `PRO`.
  * `401 Unauthorized`: Thiếu hoặc sai JWT Token.

---

#### [STT 116] Tra cứu trạng thái đơn hàng thanh toán theo mã đơn
* **Endpoint:** `GET /api/v1/service-payments/orders/{orderCode}`
* **RBAC:** `Authorize` (Chủ đơn hoặc `Admin` — kiểm tra IDOR theo `MSG-ADM-04`)
* **Use Case / Business Rules:** `U-16`, `R-07` | `BR-PAY-01`
* **Path Parameters:** `orderCode` (string, VD: `ECO202611170001` hoặc `GatewayOrderCode` dạng số)
* **Response `200 OK` (`MSG-GEN-01`):** Trả về `ApiResponse<PayInOrderDto>`; tự động quét quá hạn `SweepOrderExpiryIfNeededAsync` — nếu đang `PENDING` và `utcNow > qrExpiresAt` thì cập nhật thành `EXPIRED`. Khi đơn ở trạng thái `PAID`, trường `activatedSubscription` điền đầy đủ `ActiveSubscriptionDto` (`startsAt`, `expiresAt`, `remainingDays`, `features`).
* **Error Responses:** `403 Forbidden` (`MSG-ADM-04` khi người dùng khác tra cứu đơn không phải của mình), `404 Not Found`.

---

#### [STT 117] Xem danh sách lịch sử đơn hàng thanh toán của tôi
* **Endpoint:** `GET /api/v1/service-payments/orders`
* **RBAC:** `Authorize`
* **Use Case / Business Rules:** `U-16`, `R-07`
* **Query Parameters:** `status` (optional: `PENDING`, `PAID`, `EXPIRED`, `CANCELLED`), `pageNumber` (`int`, mặc định `1`), `pageSize` (`int`, mặc định `20`, giới hạn `1..100`).
* **Response `200 OK` (`MSG-GEN-01`):** `ApiResponse<PagedResult<PayInOrderDto>>` gồm `items`, `totalCount`, `pageNumber`, `pageSize`, `totalPages`.

---

#### [STT 122] Hủy đơn thanh toán VietQR đang chờ (`Pending`)
* **Endpoint:** `POST /api/v1/service-payments/orders/{orderCode}/cancel`
* **RBAC:** `Authorize` (Chỉ chủ đơn)
* **Use Case / Business Rules:** `U-16`, `R-07` | `BR-PAY-01`
* **Response `200 OK` (`MSG-PAY-03`):**
  ```json
  {
    "success": true,
    "messageCode": "MSG-PAY-03",
    "message": "Đã hủy mã đơn thanh toán đang chờ. Bạn có thể khởi tạo mã VietQR mới bất kỳ lúc nào.",
    "data": {
      "orderCode": "ECO202611170001",
      "status": "CANCELLED"
    }
  }
  ```
* **Error Responses:** `409 Conflict` (`MSG-PAY-03`) nếu đơn đã ở trạng thái `Paid`, `403 Forbidden` nếu không phải chủ đơn, `404 Not Found`.

---

#### [STT 118] Webhook nhận biến động số dư VietQR / PayOS
* **Endpoint:** `POST /api/v1/service-payments/webhook`
* **RBAC:** `AllowAnonymous` (Bảo vệ bằng `X-VietQr-Webhook-Secret` hoặc HMAC-SHA256 PayOS `signature`)
* **Use Case / Business Rules:** `U-16`, `R-07` | `BR-PAY-02`, `BR-PAY-06`
* **Request Headers:** `X-VietQr-Webhook-Secret: <secret>` (nếu cấu hình `VietQr:WebhookSecret`)
* **Request Body (`VietQrWebhookPayload`):**
  ```json
  {
    "orderCode": "ECO202611170001",
    "amount": 59000,
    "description": "ECOLINK ECO202611170001",
    "reference": "FT263210099881",
    "transactionDateTime": "2026-11-17T01:02:10Z",
    "signature": null,
    "data": null
  }
  ```
* **Response `200 OK` (`MSG-PAY-02`):**
  * Giao dịch khớp đủ (`Matched`): `data.status = "PAID"`, `data.reconciliationStatus = "Matched"`.
  * Giao dịch trùng (`DuplicateIgnored` / đã thanh toán): `data.status = "ALREADY_PROCESSED"`.
  * Bất thường (`Underpaid` / `Overpaid` / `LateTransfer` / `UnmatchedReference`): `data.status = "ANOMALY_FLAGGED"`, trừ `Overpaid` kích hoạt gói ngay nên trả về `data.status = "PAID"`, `data.reconciliationStatus = "Overpaid"`.
* **Error Responses:**
  * `400 Bad Request` (`MSG-PAY-05`): Chữ ký webhook không hợp lệ (`INVALID_SIGNATURE` được ghi log audit từ chối) hoặc `amount <= 0`.

---

#### [STT 119] Giả lập chuyển khoản VietQR (Môi trường Sandbox / Kiểm thử)
* **Endpoint:** `POST /api/v1/service-payments/simulate-transfer/{orderCode}`
* **RBAC:** `AllowAnonymous` (Chặn `403 Forbidden` trên môi trường Production trừ khi bật `VietQr:EnableSimulator = true`)
* **Use Case / Business Rules:** `U-16`, `R-07`
* **Request Body (`SimulateTransferRequest`):**
  ```json
  {
    "scenario": "Exact",
    "simulatedAmount": null,
    "simulatedDescription": null
  }
  ```
  *Giá trị `scenario` hỗ trợ:* `Exact` (mặc định), `Underpaid` (`50%` giá trị), `Overpaid` (`+20,000 VNĐ`), `LateTransfer` (đưa đơn sang hết hạn rồi bắn webhook), `Unmatched` (xóa mã đơn khỏi nội dung để tạo webhook mồ côi).
* **Response `200 OK` (`MSG-PAY-02`):** Trả về `ApiResponse<PayInOrderDto>` cập nhật trạng thái đơn thanh toán ngay sau khi đi qua pipeline Webhook (`reconciliationStatus`, `paidAmount`, `activatedSubscription`).
* **Error Responses:** `403 Forbidden` (Production tắt Simulator), `404 Not Found` (`orderCode` không tồn tại).

---

### 2.2. Nhóm Danh mục Gói & Quyền lợi Thuê bao Người dùng (`ServicePackagesController`)

#### [STT 120] Lấy bảng giá 3 gói dịch vụ khả dụng cho người dùng
* **Endpoint:** `GET /api/v1/service-packages`
* **RBAC:** `Authorize`
* **Use Case / Business Rules:** `U-16`, `R-07`
* **Query Parameters:** `targetRole` (optional: `All`, `User`, `Recycler`)
* **Response `200 OK` (`MSG-GEN-01`):** `ApiResponse<IReadOnlyList<ServicePackageCatalogDto>>`, tự đánh giá:
  * `isEligible` (`bool`): `false` kèm `ineligibleReason` nếu Citizen hoặc Recycler chưa `Verified` KYB xem gói `PRO`.
  * `isCurrentPlan` (`bool`) & `currentActiveExpiresAt` (`DateTime?`).

---

#### [STT 121] Xem bậc gói hiện tại, đặc quyền & lịch sử thuê bao của tôi
* **Endpoint:** `GET /api/v1/service-packages/subscriptions/me`
* **RBAC:** `Authorize`
* **Use Case / Business Rules:** `U-16`, `R-07` | `BR-PAY-04`
* **Response `200 OK` (`MSG-GEN-01`):**
  ```json
  {
    "success": true,
    "messageCode": "MSG-GEN-01",
    "data": {
      "currentTier": "Pro",
      "entitlements": {
        "currentTier": "Pro",
        "isAdFree": true,
        "hasEcoPlusBadge": true,
        "hasVerifiedBlueBadge": true,
        "isFeaturedMapMarker": true,
        "dailyPointMultiplier": 1.5,
        "mapPriorityBoost": 100
      },
      "activeSubscriptions": [
        {
          "subscriptionId": "a1b2c3d4-0000-0000-0000-000000000001",
          "packageId": "51000002-0000-0000-0000-000000000001",
          "packageCode": "PRO",
          "packageName": "Pro (Tích xanh & Ưu tiên bản đồ)",
          "sourceOrderCode": "ECO202611170002",
          "startsAt": "2026-11-17T01:05:00Z",
          "expiresAt": "2027-11-17T01:05:00Z",
          "remainingDays": 365,
          "status": "Active",
          "features": {
            "adFree": true,
            "ecoPlusBadge": true,
            "verifiedBlueBadge": true,
            "featuredMapMarker": true,
            "mapPriorityBoost": 100,
            "dailyPointMultiplier": 1.5
          }
        }
      ],
      "pastSubscriptions": []
    }
  }
  ```

---

### 2.3. Nhóm Quản trị Danh mục Gói & Biểu phí Admin (`A-10` — `ServicePackagesController`)

#### [STT 123] Tra cứu toàn bộ danh mục gói dịch vụ & thống kê doanh thu (Admin)
* **Endpoint:** `GET /api/v1/admin/service-packages`
* **RBAC:** `Authorize(Roles = "Admin")`
* **Use Case / Business Rules:** `A-10`
* **Query Parameters:** `search` (string), `targetRole` (`All|User|Recycler`), `isActive` (`bool`)
* **Response `200 OK` (`MSG-GEN-01`):** `ApiResponse<IReadOnlyList<AdminServicePackageDto>>` kèm `activeSubscriberCount`, `totalPaidOrders`, `totalRevenueVnd`.

---

#### [STT 124] Tạo gói dịch vụ thương mại mới (Admin)
* **Endpoint:** `POST /api/v1/admin/service-packages`
* **RBAC:** `Authorize(Roles = "Admin")`
* **Use Case / Business Rules:** `A-10` | `BR-PAY-05`, `BR-ADM-05`
* **Request Body (`UpsertServicePackageRequest`):**
  ```json
  {
    "packageCode": "RECYCLER_BOOST_90",
    "packageName": "Gói Đẩy Tin Cơ Sở 90 Ngày",
    "targetRole": "Recycler",
    "description": "Gói mở rộng đẩy tin khu vực trong 90 ngày.",
    "requiresVerifiedKyb": true,
    "priceVnd": 299000,
    "durationDays": 90,
    "displayOrder": 40,
    "isActive": true,
    "features": {
      "adFree": true,
      "ecoPlusBadge": false,
      "verifiedBlueBadge": true,
      "featuredMapMarker": true,
      "mapPriorityBoost": 50,
      "dailyPointMultiplier": 1.3
    }
  }
  ```
* **Validation (`UpsertServicePackageRequestValidator`):**
  * `packageCode`: không rỗng, độ dài `3..50` ký tự, khớp `^[A-Z0-9_]{3,50}$`.
  * `packageName`: không rỗng, độ dài `2..150` ký tự; `targetRole` thuộc `User | Recycler | All`.
  * `priceVnd >= 2000` VNĐ và `durationDays` từ `1` đến `730` ngày (với gói trả phí; riêng mã `BASIC` bắt buộc `priceVnd == 0` và `durationDays == 0`).
* **Response `201 Created` (`MSG-PAY-07`):** Trả về `AdminServicePackageDto` của gói vừa tạo kèm bản ghi `AuditLog` (`CREATE_SERVICE_PACKAGE`).
* **Error Responses:** `400 Bad Request` (`MSG-PAY-04`), `409 Conflict` (trùng `packageCode`), `403 Forbidden` (Staff/User/Recycler).

---

#### [STT 125] Cập nhật biểu phí, chu kỳ & đặc quyền gói dịch vụ (Admin)
* **Endpoint:** `PUT /api/v1/admin/service-packages/{packageId:guid}`
* **RBAC:** `Authorize(Roles = "Admin")`
* **Use Case / Business Rules:** `A-10` | `BR-PAY-05`, `BR-ADM-05`
* **Request Body:** `UpsertServicePackageRequest` (nếu là gói `BASIC` miễn phí mặc định, hệ thống chỉ cho phép sửa tên/mô tả/thứ tự hiển thị và tự động bảo lưu `priceVnd == 0`, `durationDays == 0`, `targetRole == All` theo `BR-PAY-05`; nếu gói đã phát sinh `ServiceOrder` thì chặn đổi `packageCode`).
* **Response `200 OK` (`MSG-PAY-07`):** Ghi `AuditLog` (`UPDATE_SERVICE_PACKAGE`), chỉ áp dụng cho đơn mua mới (`Grandfathering` giữ nguyên mọi thuê bao đang hoạt động).
* **Error Responses:** `400 Bad Request` (`BR-PAY-05` / `MSG-PAY-04`) khi cố đổi `packageCode` của gói đã có đơn hàng hoặc dữ liệu không hợp lệ, `404 Not Found`.

---

#### [STT 126] Bật/tắt trạng thái mở bán gói dịch vụ (Admin)
* **Endpoint:** `PATCH /api/v1/admin/service-packages/{packageId:guid}/status`
* **RBAC:** `Authorize(Roles = "Admin")`
* **Use Case / Business Rules:** `A-10` | `BR-PAY-05`, `BR-ADM-05`
* **Request Body (`ToggleServicePackageStatusRequest`):** `{ "isActive": false, "reason": "Tạm đóng bảo trì gói mở rộng." }`
* **Response `200 OK` (`MSG-PAY-07`):** Ghi `AuditLog` (`TOGGLE_SERVICE_PACKAGE_STATUS`).
* **Error Responses:** `400 Bad Request` (`BR-PAY-05`) nếu cố chuyển `IsActive = false` đối với gói mặc định `BASIC`, `404 Not Found`.

---

### 2.4. Nhóm Đối soát Tài chính VietQR & Xuất Bảng kê (`A-11` — `AdminReconciliationController`)

#### [STT 127] Truy vấn sổ cái dòng tiền VietQR & Lọc giao dịch bất thường (Admin)
* **Endpoint:** `GET /api/v1/admin/reconciliations/ledger`
* **RBAC:** `Authorize(Roles = "Admin")`
* **Use Case / Business Rules:** `A-11` | `BR-PAY-06`
* **Query Parameters (`ReconciliationLedgerQuery`):**
  * `search` (mã đơn `ECO...`, mã webhook `WH-...`, mã NH `FT...`, tên người nộp)
  * `reconciliationStatus` (`PendingMatch`, `Matched`, `Underpaid`, `Overpaid`, `LateTransfer`, `UnmatchedReference`, `Resolved`, `Refunded`)
  * `anomalyType` (`None`, `Underpaid`, `Overpaid`, `LateTransfer`, `UnmatchedReference`)
  * `onlyAnomalies` (`bool` — nếu `true`, chỉ trả về các giao dịch sai lệch/bất thường)
  * `fromDate` / `toDate` (`YYYY-MM-DD`, UTC)
  * `pageNumber`, `pageSize`
* **Response `200 OK` (`MSG-GEN-01`):** `ApiResponse<ReconciliationLedgerResponseDto>` gồm `summary` (`totalMatchedRevenueVnd`, `pendingOrdersCount`, `openAnomaliesCount`, `totalDiscrepancyVnd`, `totalRefundedVnd`) và `items` (`PagedResult<ReconciliationLedgerItemDto>`, tổng hợp cả `ServiceOrder` và `PaymentWebhookLog` mồ côi).

---

#### [STT 128] Đối soát thủ công giao dịch VietQR bất thường (Admin)
* **Endpoint:** `PATCH /api/v1/admin/reconciliations/orders/{orderCode}/resolve`
* **RBAC:** `Authorize(Roles = "Admin")`
* **Use Case / Business Rules:** `A-11` | `BR-PAY-06`, `BR-ADM-05`
* **Path Parameters:** `orderCode` — chấp nhận cả mã đơn (`ECO...`) và mã webhook mồ côi (`WH-...`).
* **Request Body (`ResolvePaymentAnomalyRequest`):**
  ```json
  {
    "action": "ApproveAndActivate",
    "resolutionBankRef": "FT26321999111",
    "resolutionNote": "Khách chuyển bù đủ 19.000đ qua MB Bank, kích hoạt gói PREMIUM.",
    "targetOrderCode": null
  }
  ```
* **Validation & Luồng xử lý (`ResolvePaymentAnomalyRequestValidator`):**
  * `action` bắt buộc thuộc `ReconciliationAction`: `ApproveAndActivate`, `ManualMatchOrder`, `MarkRefunded`, `MarkExcessRefunded` (dành cho đơn `Overpaid`), `CloseAnomaly`.
  * `resolutionNote` bắt buộc, độ dài `10..500` ký tự.
  * Nếu `action == ManualMatchOrder`, bắt buộc nhập `targetOrderCode` (áp dụng khi `orderCode` là bản ghi webhook mồ côi `WH-...`; hệ thống kiểm tra `ReceivedAmountVnd >= targetOrder.ExpectedAmountVnd` và đơn đích chưa `Paid`).
  * Nếu `action` là `MarkRefunded` hoặc `MarkExcessRefunded`, bắt buộc nhập `resolutionBankRef` (mã tham chiếu giao dịch hoàn tiền ngân hàng).
  * Nếu bản ghi đã ở trạng thái `Resolved` hoặc `Refunded` (trừ trường hợp đóng bất thường đơn `Refunded`), trả `409 Conflict` (`BR-PAY-06` / `MSG-PAY-08`).
* **Response `200 OK` (`MSG-PAY-08`):** Trả về `ReconciliationLedgerItemDto` đã cập nhật, kích hoạt thuê bao nếu chọn `ApproveAndActivate`/`ManualMatchOrder`, vô hiệu hóa cache và ghi `AuditLog` (`RESOLVE_PAYMENT_ANOMALY`).

---

#### [STT 129] Xuất bảng kê đối soát dòng tiền VietQR ẩn danh PII (Admin)
* **Endpoint:** `GET /api/v1/admin/reconciliations/export`
* **RBAC:** `Authorize(Roles = "Admin")`
* **Use Case / Business Rules:** `A-11` | `BR-ADM-06`, `BR-ADM-05`
* **Query Parameters (`ReconciliationExportQuery`):** `format` (`csv` mặc định hoặc `xlsx`), `reconciliationStatus`, `anomalyOnly`, `fromDate`, `toDate`.
* **Response `200 OK` (`FileContentResult`):**
  * `Content-Type`: `text/csv; charset=utf-8` (UTF-8 BOM) hoặc `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` (OpenXML).
  * Custom Headers: `X-EcoLink-Message-Code: MSG-ADM-02`, `X-EcoLink-Row-Count: <int>`.
  * Bảo mật (`BR-ADM-06`): Tự động che mờ email & số điện thoại người nộp tiền qua `PiiMaskingHelper.MaskEmail` / `MaskPhone` và ghi nhận `AuditLog` (`EXPORT_RECONCILIATION`).

---

## 3. MA TRẬN BUSINESS RULES & SYSTEM MESSAGES ÁP DỤNG

| Mã | Nhóm | Nội dung thực thi trên `M-12` | Ánh xạ trong Source Code |
|:---|:---|:---|:---|
| **`BR-PAY-01`** | Rule | Thanh toán gói nội bộ thực hiện qua VietQR động gắn mã đơn duy nhất (`ECO...`), thời hạn 15 phút, chặn sửa giá từ client. | `PaymentService.CreateOrderAsync` |
| **`BR-PAY-02`** | Rule | Webhook thanh toán xử lý idempotent theo `SHA256(source\|ref\|order)` và không kích hoạt lặp cho cùng `SourceOrderId`. | `PaymentService.ProcessWebhookCoreAsync`, `ServicePackageHelpers.ActivateSubscriptionForOrderAsync` |
| **`BR-PAY-03`** | Rule | Không tạo QR cho gói `BASIC` miễn phí; kiểm tra đúng `TargetRole` và điều kiện `Verified` KYB cho gói `PRO`. | `PaymentService.CreateOrderAsync`, `ServicePackageService.EvaluateEligibility` |
| **`BR-PAY-04`** | Rule | Gia hạn cùng `PackageCode` cộng dồn nối tiếp từ `MAX(now, oldExpiresAt)`; nâng bậc khác gói kích hoạt tức thì tại `now`. | `ServicePackageHelpers.ActivateSubscriptionForOrderAsync` |
| **`BR-PAY-05`** | Rule | Thay đổi biểu phí/cấu hình gói của Admin chỉ áp dụng đơn mới (Grandfathering snapshot); không thể vô hiệu hóa hoặc sửa giá gói `BASIC`. | `ServicePackageService.UpdatePackageAsync`, `TogglePackageStatusAsync` |
| **`BR-PAY-06`** | Rule | Phân loại 5 trạng thái bất thường (`Underpaid`, `Overpaid`, `LateTransfer`, `UnmatchedReference`, `DuplicateIgnored`) và cho phép Admin đối soát thủ công một lần (`Resolved`/`Refunded`). | `PaymentService.ProcessWebhookCoreAsync`, `AdminReconciliationService.ResolvePaymentAnomalyAsync` |
| **`BR-ADM-05`** | Rule | Ghi `AuditLog` bất biến cho thao tác tạo/sửa/ẩn gói (`A-10`), từ chối sai chữ ký webhook, giải quyết sai lệch và xuất bảng kê (`A-11`). | `AuditLogService.LogAsync`, `AuditLogImmutabilityInterceptor` |
| **`BR-ADM-06`** | Rule | Xuất file CSV/XLSX bảng kê đối soát bắt buộc che mờ email/SĐT (`PiiMaskingHelper`). | `AdminReconciliationService.ExportReconciliationReportAsync` |
| **`MSG-PAY-01`** | Message | *"Quét mã VietQR này để hoàn tất thanh toán dịch vụ EcoLink."* | `ServicePaymentController.CreateServicePayment` |
| **`MSG-PAY-02`** | Message | *"Thanh toán dịch vụ của bạn đã được xác nhận thành công."* | `ServicePaymentController.ReceivePaymentWebhook`, `SimulateTransfer` |
| **`MSG-PAY-03`** | Message | *"Mã thanh toán này đã hết hạn. Vui lòng tạo mã mới."* / Thông báo hủy đơn chờ hoặc số tiền không hợp lệ. | `ServicePaymentController.CreateServicePayment`, `CancelPendingOrder` |
| **`MSG-PAY-05`** | Message | Chữ ký bảo mật webhook hoặc payload PayOS/VietQR không hợp lệ (`400 Bad Request`). | `PaymentService.VerifyWebhookSignature`, `ServicePaymentController` |
| **`MSG-PAY-07`** | Message | Thao tác cấu hình/tạo/cập nhật/bật-tắt gói dịch vụ bởi Admin thành công (`200`/`201`). | `ServicePackagesController` (`A-10`) |
| **`MSG-PAY-08`** | Message | Đối soát thủ công hoàn tất hoặc từ chối do sai điều kiện/đã xử lý (`200`/`409`). | `AdminReconciliationController.ResolvePaymentAnomaly`, `AdminReconciliationService` |
| **`MSG-ADM-02`** | Header | `X-EcoLink-Message-Code: MSG-ADM-02` (*"Xuất báo cáo thành công."*) khi xuất CSV/XLSX. | `AdminReconciliationController.ExportReconciliationReport` |
| **`MSG-ADM-04`** | Error | *"Bạn không có quyền thực hiện thao tác này."* (`403 Forbidden` khi IDOR hoặc sai vai trò). | `PaymentService.GetOrderAsync`, `[Authorize(Roles = "Admin")]` |
