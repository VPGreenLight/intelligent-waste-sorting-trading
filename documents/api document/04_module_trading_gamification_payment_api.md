# TÀI LIỆU ĐẶC TẢ API – MODULE 4: KẾT NỐI ĐỊA LÝ (POSTGIS), GIAO DỊCH BÀN GIAO, GAMIFICATION & THANH TOÁN NỘI BỘ
**Mã phân hệ:** `MTH` (Tìm kiếm Recycler), `HND` (Bàn giao & Giao dịch), `GAM` (Gamification & Phần thưởng), `PAY` (Thanh toán dịch vụ nội bộ VietQR / PayOS)  
**Controllers:** [`TradeController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/TradeController.cs), [`GamificationController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/GamificationController.cs)  
**Services thực thi:** [`GeoMatchingService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GeoMatchingService.cs), [`TradeTransactionService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/TradeTransactionService.cs), [`GamificationService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GamificationService.cs), [`PaymentService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PaymentService.cs), [`PayOsService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PayOsService.cs)  
**DTOs:** [`TradeAndGamificationDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/TradeAndGamificationDtos.cs), [`PaymentDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/PaymentDtos.cs), [`PayOsDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/PayOsDtos.cs)

---

## 1. TỔNG QUAN DANH SÁCH ENDPOINTS

| STT | Phân hệ | Phương thức | Endpoint URI | Quyền truy cập | Chức năng nghiệp vụ | Mã thông báo / Quy tắc |
|:---:|:---:|:---:|:---|:---:|:---|:---|
| 1 | `MTH` | `GET` | `/api/v1/trade/partners/nearby` | Public / `Citizen` | Tìm kiếm các Cơ sở thu gom (`Recycler` đã duyệt) gần vị trí GPS người dùng trong bán kính `radiusKm`, lọc theo loại rác và sắp xếp theo khoảng cách tăng dần. | `MSG-MTH-01`, `MSG-MTH-02`, `BR-MTH-04`, `BR-MTH-05` |
| 2 | `HND` | `POST` | `/api/v1/trade/orders` | `Citizen` | Tạo yêu cầu hẹn bàn giao rác tới cơ sở thu gom kèm khung giờ đề xuất (`scheduledAt`) và sinh mã QR bảo mật (`qrCodeValue`). | `MSG-HND-01`, `MSG-NEG-04`, `BR-HND-01`, `BR-NEG-04` |
| 3 | `HND` / `GAM` | `POST` | `/api/v1/trade/receipts/confirm-qr` | `Recycler` / `Citizen` | Quét mã QR xác nhận biên nhận bàn giao, chốt khối lượng thực tế (`actualWeightKg`), tính tổng tiền mặt và tự động cộng Điểm Xanh (`pointsAwarded`). | `MSG-HND-03`, `MSG-GAM-01`, `BR-HND-04`, `BR-GAM-01` |
| 4 | `GAM` | `GET` | `/api/v1/gamification/profile/{userId:guid}` | Authenticated | Xem hồ sơ sinh thái (`EcoProfileDto`): Điểm Xanh hiện tại, cấp độ Cây ảo (`treeLevel`), kinh nghiệm (`treeExperience`) và tổng số kg rác tái chế. | `MSG-GAM-06`, `BR-GAM-06`, `BR-GAM-08` |
| 5 | `GAM` | `GET` | `/api/v1/gamification/leaderboard` | Public / Authenticated | Xem bảng xếp hạng đóng góp sống xanh (`Top N` người dùng có tổng điểm cao nhất). | `BR-GAM-07` |

---

## 2. CHI TIẾT CÁC API TÌM KIẾM ĐỊA LÝ & GIAO DỊCH BÀN GIAO (`/api/v1/trade`)

---

### 2.1. Tìm Điểm Thu Gom Gần Nhất theo GPS (`GET /api/v1/trade/partners/nearby`)

* **Mô tả:** Tính khoảng cách địa lý (Haversine / PostGIS `ST_DWithin`) từ tọa độ GPS hiện tại của người dùng (`lat`, `lng`) đến các cơ sở thu gom đã được phê duyệt (`VerificationStatus = Verified`, `ActiveStatus = Available`), lọc theo danh mục rác chấp nhận (`category`) trong phạm vi bán kính `radiusKm` và sắp xếp từ gần nhất đến xa nhất (`BR-MTH-04`).
* **Quy tắc bảo mật vị trí (`BR-MTH-05`):** Tọa độ GPS của người dùng chỉ dùng để tính khoảng cách trên Server, không lưu trữ hay chia sẻ cho Recycler khi tìm kiếm.

#### a. Request Parameters (Query String)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mặc định / Ràng buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `lat` | Query | `double` | **Có** | `-90.0 <= lat <= 90.0` | Vĩ độ GPS hiện tại của người dùng (VD: `10.7769`). |
| `lng` | Query | `double` | **Có** | `-180.0 <= lng <= 180.0` | Kinh độ GPS hiện tại của người dùng (VD: `106.7009`). |
| `category` | Query | `string?` | Không | `"plastic"`, `"paper"`, `"cardboard"`, `"metal"`, `"glass"` | Lọc các cơ sở có thu mua loại phế liệu cụ thể. Nếu bỏ trống sẽ lấy tất cả. |
| `radiusKm` | Query | `double` | Không | Mặc định: `10.0` (km) | Bán kính tìm kiếm tối đa tính bằng kilômét (`km`). |

##### Ví dụ Request URL:
```http
GET /api/v1/trade/partners/nearby?lat=10.7769&lng=106.7009&category=plastic&radiusKm=10
```

#### b. Response Parameters (`IEnumerable<CollectionPartnerDto>`)

| Trường | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `id` | `Guid` | Mã định danh của Cơ sở thu mua. |
| `facilityName` | `string` | Tên cơ sở thu mua phế liệu. |
| `address` | `string` | Địa chỉ kinh doanh của cơ sở. |
| `latitude` | `double` | Vĩ độ của cơ sở. |
| `longitude` | `double` | Kinh độ của cơ sở. |
| `distanceKm` | `double` | Khoảng cách từ vị trí người dùng đến cơ sở (km, làm tròn 2 chữ số thập phân). |
| `contactPhone` | `string` | Số điện thoại liên hệ của cơ sở. |
| `materials` | `AcceptedMaterialDto[]` | Danh sách các loại phế liệu nhận thu mua kèm đơn giá tham khảo (`pricePerKg`) và khối lượng tối thiểu (`minimumQuantityKg`). |

##### Ví dụ Response Thành công (`200 OK`):
```json
[
  {
    "id": "11111111-1111-1111-1111-111111111111",
    "facilityName": "Vựa Phế Liệu Xanh Quận 1",
    "address": "123 Nguyễn Thị Minh Khai, Phường Bến Thành, Quận 1, TP.HCM",
    "latitude": 10.7725,
    "longitude": 106.692,
    "distanceKm": 1.09,
    "contactPhone": "0901234567",
    "materials": [
      {
        "categoryCode": "plastic",
        "categoryName": "Nhựa tái chế",
        "pricePerKg": 7000,
        "minimumQuantityKg": 1.0
      },
      {
        "categoryCode": "paper",
        "categoryName": "Giấy báo & Carton",
        "pricePerKg": 4500,
        "minimumQuantityKg": 2.0
      },
      {
        "categoryCode": "metal",
        "categoryName": "Kim loại / Vỏ lon",
        "pricePerKg": 25000,
        "minimumQuantityKg": 0.5
      }
    ]
  }
]
```

#### c. Các Thông báo Nghiệp vụ Liên quan (`MTH`)

| Mã thông báo | Quy tắc (`BR`) | Ngữ cảnh hiển thị trên Client | Nội dung thông báo chuẩn |
|:---:|:---:|:---|:---|
| `MSG-MTH-01` | `BR-MTH-04` | Khi mảng kết quả trả về rỗng (`[]`) | `"Không tìm thấy cơ sở thu mua rác gần bạn."` |
| `MSG-MTH-02` | `BR-MTH-05` | Khi người dùng từ chối cấp quyền GPS trên trình duyệt/điện thoại | `"Cần quyền truy cập vị trí để tìm cơ sở thu mua gần bạn. Vui lòng bật trong phần cài đặt."` |

---

### 2.2. Tạo Yêu cầu Hẹn Bàn giao Rác (`POST /api/v1/trade/orders`)

* **Mô tả:** Người dùng (`Citizen`) gửi yêu cầu bàn giao phế liệu đến một Cơ sở thu gom (`partnerId`) kèm theo khung giờ thu gom đề xuất (`scheduledAt` – `BR-NEG-04`) và khối lượng ước tính (`estimatedWeightKg`). Hệ thống khởi tạo đơn ở trạng thái `"Pending"` và sinh mã định danh QR bảo mật (`qrCodeValue`) để đối soát khi bàn giao thực tế.

#### a. Request Parameters (`application/json` – `CreateTradeOrderRequest`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `userId` | Body | `Guid` | **Có** | `Guid` hợp lệ | ID tài khoản của người dân (`Citizen`) tạo yêu cầu. |
| `partnerId` | Body | `Guid` | **Có** | `Guid` hợp lệ | ID của cơ sở thu gom (`Recycler`) nhận yêu cầu. |
| `scheduledAt` | Body | `string (ISO 8601)` | **Có** | Thời gian trong tương lai | Khung giờ hẹn bàn giao / thu gom đề xuất (`BR-NEG-04`). |
| `estimatedWeightKg` | Body | `decimal` | **Có** | `> 0` | Khối lượng phế liệu ước tính tính bằng kilômét (`kg`). |
| `note` | Body | `string?` | Không | Tối đa `500` ký tự | Ghi chú tình trạng phế liệu hoặc hướng dẫn đường đi. |

##### Ví dụ Request Body:
```json
{
  "userId": "10000000-0000-0000-0000-000000000001",
  "partnerId": "11111111-1111-1111-1111-111111111111",
  "scheduledAt": "2026-10-31T09:30:00Z",
  "estimatedWeightKg": 5.5,
  "note": "Gồm 3kg chai nhựa PET đã rửa sạch và 2.5kg bìa carton xếp gọn."
}
```

#### b. Response Parameters (`TradeOrderDto`)

| Trường trong `order` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `id` | `Guid` | Mã định danh duy nhất của đơn bàn giao (`TradeOrderId`). |
| `userId` | `Guid` | ID người dân gửi yêu cầu. |
| `partnerId` | `Guid` | ID cơ sở thu gom. |
| `status` | `string` | Trạng thái đơn: `"Pending"` (`BR-HND-02`). |
| `scheduledAt` | `string (ISO 8601)` | Thời điểm hẹn thu gom đề xuất. |
| `estimatedWeightKg` | `decimal` | Khối lượng ước tính (`kg`). |
| `qrCodeValue` | `string` | Chuỗi mã bảo mật QR (VD: `"ECOLINK_QR_A1B2C3D4"`) dùng để quét khi bàn giao. |

##### Ví dụ Response Thành công (`200 OK`):
```json
{
  "success": true,
  "order": {
    "id": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "userId": "10000000-0000-0000-0000-000000000001",
    "partnerId": "11111111-1111-1111-1111-111111111111",
    "status": "Pending",
    "scheduledAt": "2026-10-31T09:30:00Z",
    "estimatedWeightKg": 5.5,
    "qrCodeValue": "ECOLINK_QR_7C9E6679"
  },
  "message": "Tạo đơn hẹn bàn giao rác thành công. Vui lòng đưa mã QR cho bên thu gom quét khi mang rác tới."
}
```

#### c. Các Thông báo Nghiệp vụ Liên quan (`HND` & `NEG`)

| Mã thông báo | Quy tắc (`BR`) | Ngữ cảnh | Nội dung thông báo chuẩn |
|:---:|:---:|:---|:---|
| `MSG-HND-01` | `BR-HND-01` | Người dân tạo yêu cầu bàn giao thành công | `"Yêu cầu bàn giao của bạn đã được gửi."` |
| `MSG-NEG-04` | `BR-NEG-04` | Thông báo gửi tới Recycler khi có yêu cầu mới kèm khung giờ | `"Bạn có yêu cầu bàn giao mới kèm khung giờ đề xuất từ {user_name}."` |

---

### 2.3. Xác nhận Biên nhận Bàn giao qua Quét Mã QR (`POST /api/v1/trade/receipts/confirm-qr`)

* **Mô tả:** Khi hai bên gặp nhau và cân phế liệu thực tế, cơ sở thu gom quét mã QR của đơn hàng (`qrSecurityCode`), nhập khối lượng cân thực tế (`actualWeightKg`) và đơn giá thống nhất (`unitPrice`). Hệ thống:
  1. Kiểm tra tính hợp lệ của `tradeOrderId` và `qrSecurityCode`.
  2. Tính tổng tiền thanh toán thực tế: $\text{TotalAmountCash} = \text{ActualWeightKg} \times \text{UnitPrice}$ (`BR-HND-05`).
  3. Chuyển trạng thái đơn sang `"Completed"` (`BR-HND-04`, `BR-HND-07`).
  4. Tự động tính và cộng Điểm Xanh (`PointsAwarded`) cùng kinh nghiệm Cây ảo cho người dùng (`BR-GAM-01`, `BR-GAM-08`).

#### a. Request Parameters (`application/json` – `ConfirmQrReceiptRequest`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `tradeOrderId` | Body | `Guid` | **Có** | `Guid` đơn hàng đang mở | ID của đơn hẹn bàn giao rác. |
| `qrSecurityCode` | Body | `string` | **Có** | Khớp với `order.qrCodeValue` | Chuỗi mã bảo mật đọc được từ mã QR của đơn hàng. |
| `actualWeightKg` | Body | `decimal` | **Có** | `> 0` | Khối lượng phế liệu cân thực tế (`kg`). |
| `unitPrice` | Body | `decimal` | **Có** | `>= 0` | Đơn giá thu mua thực tế trên mỗi kg (`VNĐ/kg`). |

##### Ví dụ Request Body:
```json
{
  "tradeOrderId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
  "qrSecurityCode": "ECOLINK_QR_7C9E6679",
  "actualWeightKg": 6.0,
  "unitPrice": 7000
}
```

#### b. Response Parameters (`TransactionReceiptDto`)

| Trường trong `receipt` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `receiptId` | `Guid` | Mã định danh biên nhận giao dịch hoàn tất. |
| `tradeOrderId` | `Guid` | Mã đơn hẹn bàn giao liên kết. |
| `actualWeightKg` | `decimal` | Khối lượng thực tế đã chốt (`kg`). |
| `totalAmountCash` | `decimal` | Tổng số tiền mặt thanh toán (`VNĐ`). |
| `pointsAwarded` | `integer` | Số Điểm Xanh (`GreenPoints`) được hệ thống thưởng cho giao dịch. |
| `completedAt` | `string (ISO 8601)` | Thời điểm hoàn tất biên nhận (UTC). |

##### Ví dụ Response Thành công (`200 OK`):
```json
{
  "success": true,
  "receipt": {
    "receiptId": "f47ac10b-58cc-4372-a567-0e02b2c3d479",
    "tradeOrderId": "7c9e6679-7425-40de-944b-e07fc1f90ae7",
    "actualWeightKg": 6.0,
    "totalAmountCash": 42000,
    "pointsAwarded": 60,
    "completedAt": "2026-10-31T09:45:00Z"
  },
  "message": "Giao dịch thành công! Đã chốt 6.0kg rác, thanh toán 42,000 VNĐ và cộng 60 Điểm Xanh!"
}
```

#### c. Các Thông báo Trả về

| HTTP Status | Mã thông báo (`MSG`) | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung thông báo |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `MSG-HND-03` + `MSG-GAM-01` | `BR-HND-04`, `BR-GAM-01` | Quét QR hợp lệ, chốt biên nhận và cộng điểm thành công | `"Giao dịch thành công! Đã chốt {ActualWeightKg}kg rác, thanh toán {TotalAmountCash} VNĐ và cộng {PointsAwarded} Điểm Xanh!"` |
| **`404 Not Found`** | `MSG-SRT-02` | `BR-SRT-05` | Không tìm thấy `tradeOrderId` hoặc mã `qrSecurityCode` không khớp | `"Không tìm thấy đơn hàng hoặc mã QR không hợp lệ."` |

---

## 3. CHI TIẾT CÁC API GAMIFICATION & BẢNG XẾP HẠNG (`/api/v1/gamification`)

---

### 3.1. Xem Hồ sơ Sinh thái & Cấp độ Cây Ảo (`GET /api/v1/gamification/profile/{userId:guid}`)

* **Mô tả:** Lấy thông tin tích lũy Điểm Xanh, cấp độ Cây ảo (`TreeLevel`), điểm kinh nghiệm hiện tại (`TreeExperience`), số điểm cần để lên cấp tiếp theo (`ExperienceToNextLevel`) và tổng khối lượng rác đã tái chế của người dùng (`BR-GAM-06`, `BR-GAM-08`).

#### a. Request Parameters

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|
| `userId` | Path (Route) | `Guid` | **Có** | Mã định danh của người dùng cần xem hồ sơ sinh thái. |

#### b. Response Parameters (`EcoProfileDto`)

| Trường | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `userId` | `Guid` | ID người dùng. |
| `currentGreenPoints` | `integer` | Số dư Điểm Xanh hiện có (dùng để đổi Voucher – `BR-GAM-03`). |
| `totalLifetimePoints` | `integer` | Tổng Điểm Xanh tích lũy từ khi tạo tài khoản. |
| `treeLevel` | `integer` | Cấp độ phát triển của Cây ảo (chỉ tăng một chiều – `BR-GAM-06`). |
| `treeExperience` | `integer` | Điểm kinh nghiệm (EXP) hiện tại của Cây ảo. |
| `experienceToNextLevel` | `integer` | Mốc EXP cần đạt để Cây ảo lên cấp độ tiếp theo (`MSG-GAM-06`). |
| `totalWasteScanned` | `integer` | Tổng số lần người dùng đã quét phân loại rác bằng AI. |
| `totalRecycledKg` | `decimal` | Tổng khối lượng rác tái chế đã bàn giao hoàn tất (`kg`). |

##### Ví dụ Response Thành công (`200 OK`):
```json
{
  "userId": "10000000-0000-0000-0000-000000000001",
  "currentGreenPoints": 210,
  "totalLifetimePoints": 350,
  "treeLevel": 3,
  "treeExperience": 70,
  "experienceToNextLevel": 100,
  "totalWasteScanned": 18,
  "totalRecycledKg": 18.5
}
```

---

### 3.2. Xem Bảng Xếp Hạng Sống Xanh (`GET /api/v1/gamification/leaderboard`)

* **Mô tả:** Trả về danh sách `Top N` người dùng có thành tích đóng góp sống xanh cao nhất. Để bảo vệ quyền riêng tư theo `BR-GAM-07`, bảng xếp hạng chỉ hiển thị thứ hạng (`rank`), tên hiển thị (`fullName`), tổng điểm (`totalPoints`) và cấp độ cây (`treeLevel`), không lộ email hay số điện thoại.

#### a. Request Parameters (Query String)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mặc định | Mô tả |
|:---|:---:|:---:|:---:|:---:|:---|
| `top` | Query | `integer` | Không | `10` | Số lượng thứ hạng đứng đầu muốn lấy (VD: `top=10`). |

#### b. Ví dụ Response Thành công (`200 OK` – `IEnumerable<LeaderboardEntryDto>`):
```json
[
  {
    "rank": 1,
    "fullName": "Nguyễn Thị Thu Xanh",
    "totalPoints": 1250,
    "treeLevel": 8
  },
  {
    "rank": 2,
    "fullName": "Trần Minh Bảo Vệ",
    "totalPoints": 980,
    "treeLevel": 6
  },
  {
    "rank": 3,
    "fullName": "Lê Hoàng Tái Chế",
    "totalPoints": 740,
    "treeLevel": 5
  }
]
```

---

## 4. ĐẶC TẢ DỊCH VỤ THANH TOÁN NỘI BỘ VIETQR & PAYOS (`PAY` – `BR-PAY-01`, `BR-PAY-02`)

Phân hệ Thanh toán dịch vụ nội bộ ([`PaymentService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PaymentService.cs) và [`PayOsService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PayOsService.cs)) cung cấp cơ chế tạo mã **VietQR động chuẩn EMVCo Napas 24/7** và xử lý **Webhook bất đồng bộ có tính Idempotent** cho các gói dịch vụ của EcoLink (`VERIFIED_BADGE` – Huy hiệu Tích xanh, `NO_ADS` – Tắt quảng cáo, `MAP_BOOST` – Ưu tiên hiển thị bản đồ).

### 4.1. Khởi tạo Đơn Thanh toán Dịch vụ Nội bộ (`CreatePayInRequest` $\rightarrow$ `PayInOrderDto`)

#### a. Cấu trúc Request (`CreatePayInRequest`)

| Tên tham số | Kiểu dữ liệu | Bắt buộc | Mặc định | Mô tả |
|:---|:---:|:---:|:---|:---|
| `packageCode` | `string` | **Có** | `"VERIFIED_BADGE"` | Mã gói dịch vụ nội bộ: `"VERIFIED_BADGE"`, `"NO_ADS"`, hoặc `"MAP_BOOST"`. |
| `amount` | `decimal` | **Có** | `50000` | Số tiền thanh toán (`VNĐ`). |
| `description` | `string` | **Có** | `"Thanh toan goi Tich Xanh EcoLink"` | Nội dung mô tả đơn hàng. |
| `userId` | `Guid?` | Không | `null` | ID tài khoản `User` hoặc `Recycler` đăng ký mua gói. |

#### b. Cấu trúc Response (`PayInOrderDto` – Mã thông báo `MSG-PAY-01`)

| Trường | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `orderCode` | `string` | Mã đơn hàng duy nhất (VD: `"ECO10304821"`). |
| `amount` | `decimal` | Số tiền cần chuyển khoản (`VNĐ`). |
| `description` | `string` | Mô tả gói dịch vụ. |
| `transferContent` | `string` | Nội dung chuyển khoản bắt buộc để hệ thống đối soát tự động (trùng với `orderCode`). |
| `bankCode` / `bankName` | `string` | Ngân hàng thụ hưởng (VD: `"TPB"` / `"TPBank"`). |
| `accountNo` / `accountName` | `string` | Số tài khoản và tên chủ tài khoản nhận thanh toán. |
| `qrCodeUrl` | `string` | Đường dẫn ảnh mã VietQR động (`https://img.vietqr.io/image/...`). |
| `status` | `string` | Trạng thái đơn: `"PENDING"`, `"PAID"`, `"CANCELLED"`, `"EXPIRED"`. |
| `createdAt` / `expiresAt` | `string (ISO 8601)` | Thời điểm tạo và thời điểm hết hạn mã QR (sau **15 phút** – `MSG-PAY-03`). |

---

### 4.2. Xử lý Webhook Xác nhận Thanh toán Tự động (`VietQrWebhookPayload` $\rightarrow$ `WebhookResponseDto`)

* **Quy tắc Idempotent (`BR-PAY-01`, `BR-PAY-02`):** Khi nhận thông báo biến động số dư từ cổng thanh toán VietQR / PayOS, hệ thống đối chiếu `orderCode` (hoặc bóc tách mã `ECOxxxx` từ `description`) và kiểm tra trạng thái đơn:
  * Nếu đơn đang ở trạng thái `"PENDING"`: Chuyển sang `"PAID"`, ghi nhận `paidAt`, `transactionReference` và trả về `status = "PAID"` (`MSG-PAY-02`).
  * Nếu đơn đã ở trạng thái `"PAID"` trước đó: Không kích hoạt trùng lặp dịch vụ mà trả về ngay `status = "ALREADY_PAID"`, `success = true` (`BR-PAY-02`).

#### a. Cấu trúc Payload Webhook (`VietQrWebhookPayload`)

| Tên tham số | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `orderCode` | `string?` | Mã đơn hàng đối soát (nếu cổng truyền trực tiếp). |
| `amount` | `decimal` | Số tiền thực nhận (`VNĐ`). |
| `description` | `string?` | Nội dung chuyển khoản thực tế từ ngân hàng (chứa mã `ECOxxxx`). |
| `reference` | `string?` | Mã tham chiếu giao dịch ngân hàng (`FT...`). |
| `transactionDateTime` | `string?` | Thời gian giao dịch ngân hàng. |
| `signature` | `string?` | Chữ ký bảo mật HMAC kiểm tra tính toàn vẹn của Webhook. |

#### b. Bảng Mã Thông báo Phân hệ Thanh toán (`PAY`)

| Mã thông báo | Quy tắc (`BR`) | Ngữ cảnh | Nội dung thông báo chuẩn |
|:---:|:---:|:---|:---|
| **`MSG-PAY-01`** | `BR-PAY-01` | Hiển thị ngay dưới mã QR khi khởi tạo đơn `PENDING` | `"Quét mã VietQR này để hoàn tất thanh toán dịch vụ EcoLink."` |
| **`MSG-PAY-02`** | `BR-PAY-01`, `BR-PAY-02` | Khi Webhook xác nhận thanh toán thành công (`status = "PAID"`) | `"Thanh toán dịch vụ của bạn đã được xác nhận thành công."` |
| **`MSG-PAY-03`** | `BR-PAY-01` | Khi quá 15 phút (`expiresAt`) mà đơn vẫn chưa được thanh toán | `"Mã thanh toán này đã hết hạn. Vui lòng tạo mã mới."` |

