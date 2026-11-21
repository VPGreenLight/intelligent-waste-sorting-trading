# TỔNG QUAN KIẾN TRÚC API, KHUNG PHẢN HỒI CHUẨN & HỆ THỐNG MÃ THÔNG BÁO
**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phiên bản API:** `v1` (`/api/v1/...`) | **Phiên bản tài liệu:** `v2.6.0` (`2026-11-20`)  
**Chuẩn thiết kế:** RESTful API + SignalR WebSocket + Clean Architecture (`ApiResponse<T>`, `PagedResult<T>`)  

---

## 1. DANH MỤC TÀI LIỆU API THEO MODULE

Tài liệu đặc tả hợp đồng API của hệ thống EcoLink được chuẩn hóa tại thư mục `documents/api contact/`, chia thành các phân hệ (Module) độc lập dưới đây:

| STT | File Tài liệu | Module / Phân hệ | Base Route | Mô tả chính |
|:---:|:---|:---|:---|:---|
| **STAT** | [`00_api_inventory_and_endpoint_statistics.md`](./00_api_inventory_and_endpoint_statistics.md) | **API Inventory & Statistical Dashboard** | `All Routes` | Bảng tổng hợp quy mô **184 API endpoints** (**184 đã triển khai – 100.0%**, 0 dự kiến). |
| **00** | [`00_api_overview_and_response_contract.md`](./00_api_overview_and_response_contract.md) | **Overview & Response Contract** | `/api/v1/*` | Khung JSON `ApiResponse<T>`, phân trang `PagedResult<T>`, xuất CSV/XLSX ẩn danh PII, xác thực JWT / `[HasPermission]` / Webhook, HTTP Status Codes và bảng tra cứu `MSG-*` / `BR-*`. |
| **01** | [`01_module_authentication_api.md`](./01_module_authentication_api.md) | **Module 1: Authentication & Account (`M-01` / `AUTH`)** | `/api/v1/auth` | **14 APIs (`STT 001–014`):** Đăng ký `Citizen`/`Recycler`, Đăng nhập (chống Brute-force), Google OAuth 2.0, Làm mới Token (chống Replay Attack), Quên & Đặt lại mật khẩu OTP qua Gmail SMTP và `/vietqr-token`. |
| **02** | [`02_module_recycler_onboarding_api.md`](./02_module_recycler_onboarding_api.md) | **Module 2: Recycler Onboarding & Verification (`M-02` / `U-17` & `S-04`)** | `/api/v1/recycler-applications` | **6 APIs (`STT 015–020`):** Tải chứng từ/ảnh cơ sở lên Cloudflare R2, Người dân (`Citizen`) nộp đơn nâng cấp lên Cơ sở thu gom (`Recycler`) kèm tọa độ PostGIS, `Staff/Admin` thẩm định (Duyệt / Từ chối). |
| **03** | [`03_module_ai_waste_classification_api.md`](./03_module_ai_waste_classification_api.md) | **Module 3: AI Waste Classification & Rule Engine (`M-03` & `AIS`)** | `/api/v1/waste-scan`<br>`/api/v1/waste-categories`<br>`/api/classify` | **11 Core + 11 AI APIs (`STT 021–031`, `174–184`):** Nhận diện rác đa mô hình, kiểm tra ngưỡng OOD (60%), lịch sử quét AI cá nhân (`U-01`/`U-03`), CRUD/Toggle danh mục rác (`A-05`), phát hành Rule Engine đồng bộ AI Service (`A-06`) và báo cáo AI nhận diện sai. |
| **04** | [`04_module_recycler_profile_pricing_availability_api.md`](./04_module_recycler_profile_pricing_availability_api.md) | **Module 4: Recycler Profile, Pricing & Availability (`M-04` / `R-01` & `R-02`)** | `/api/v1/recycler` | **9 APIs (`STT 032–040`):** Quản lý hồ sơ cơ sở thu gom, cấu hình bảng giá thu mua (`VND/kg`, khối lượng tối thiểu, lưu nguyên tử hàng loạt), bật/tắt trạng thái `Online`/`Offline` và lịch hoạt động theo tuần. |
| **05** | [`05_module_location_based_recycler_discovery_api.md`](./05_module_location_based_recycler_discovery_api.md) | **Module 5: Location-Based Recycler Discovery (`M-05` / `U-05`)** | `/api/v1/recyclers`<br>`/api/v1/geocoding`<br>`/api/v1/trade/partners` | **4 APIs (`STT 041–044`):** Tìm kiếm cơ sở thu gom đã xác minh (`Verified`) và đang `Online` theo bán kính GPS (ưu tiên ghim `PRO`), lọc loại phế liệu & từ khóa, Geocoding Proxy và gợi ý dự phòng (`MSG-MTH-01`). |
| **06** | [`06_module_handover_request_management_api.md`](./06_module_handover_request_management_api.md) | **Module 6: Handover Request Management (`M-06` / `U-06`, `U-07`, `R-03`, `R-04`)** | `/api/v1/handover-requests`<br>`/api/v1/recycler/handover-requests` | **10 APIs (`STT 045–054`):** Người dân tạo/sửa/hủy yêu cầu bàn giao phế liệu (`HND-yyyyMMdd-NNNNNN`), Cơ sở chấp nhận/từ chối/hủy lịch hẹn, ẩn liên hệ trước khi chốt (`BR-HND-08`), cảnh báo hủy muộn và tự hết hạn 24h (`JOB-03`). |
| **07** | [`07_module_handover_completion_and_receipt_api.md`](./07_module_handover_completion_and_receipt_api.md) | **Module 7: Handover Completion & Transaction Receipt (`M-07` / `R-06`, `U-09`, `U-12`)** | `/api/v1/recycler/handover-requests/{id}/receipt`<br>`/api/v1/handover-requests/{id}/receipt`<br>`/api/v1/transactions`<br>`/api/v1/recycler/transactions` | **8 APIs (`STT 055–062`):** Xem trước & phát hành biên nhận số kèm ảnh đồng hồ cân (`RCP-yyyy-NNNNNN`), cảnh báo lệch khối lượng $> 20\%$ (`MSG-HND-04`), người dân xác nhận hoàn tất & cộng điểm xanh (`BR-GAM-01`), tự hoàn tất sau 48h (`JOB-01`) và lịch sử giao dịch. |
| **08** | [`08_module_in_app_realtime_negotiation_chatbox_api.md`](./08_module_in_app_realtime_negotiation_chatbox_api.md) | **Module 8: In-App Real-Time Negotiation Chatbox (`M-08` / `U-08` & `R-05`)** | `/api/v1/trade/orders/{orderId}`<br>`/hubs/negotiation-chat` | **8 APIs (`STT 063–070`):** Phòng chat thương lượng thời gian thực SignalR theo đơn bàn giao (`BR-NEG-01`), gửi tin nhắn `Text`/`Image` (Cloudflare R2), gửi phản đề xuất giá/khối lượng/khung giờ (`Superseded`), chốt điều khoản & khóa lịch hẹn. |
| **09** | [`09_module_account_staff_system_governance_analytics_api.md`](./09_module_account_staff_system_governance_analytics_api.md) | **Module 9: Account, Staff & System Governance / Analytics (`M-15` / `FE-17`, `FE-18`)** | `/api/v1/admin/analytics`<br>`/api/v1/admin/staff`<br>`/api/v1/staff/accounts`<br>`/api/v1/staff/attendance`<br>`/api/v1/notifications` | **23 APIs (`STT 150–172` = 22 REST + 1 WS Hub):** KPI điều hành & biểu đồ (`BR-ADM-07`), xuất CSV/XLSX ẩn danh PII (`BR-ADM-06`), Audit Trail bất biến (`BR-ADM-05`), khóa/vô hiệu hóa mềm (`BR-ADM-03`, `BR-ADM-04`), Staff & 7 mã quyền RBAC (`BR-ADM-01`, `BR-ADM-02`), chấm công ca trực (`BR-GAM-09`) và Hệ thống Thông báo realtime SignalR (`/hubs/notifications`, `SU-04`). |
| **10** | [`10_module_in_system_service_packages_vietqr_api.md`](./10_module_in_system_service_packages_vietqr_api.md) | **Module 10: In-System Service Packages & VietQR (`M-12` / `FE-14`)** | `/api/v1/service-packages`<br>`/api/v1/service-payments`<br>`/api/v1/admin/service-packages`<br>`/api/v1/admin/reconciliations` | **15 APIs (`STT 115–129`):** Danh mục 3 gói chuẩn (`BASIC` 0đ, `PREMIUM` 59k/30d, `PRO` 1tr/365d yêu cầu KYB `Verified`), tạo VietQR động 15p & hủy đơn chờ, Webhook idempotent + phân loại 5 kịch bản bất thường (`BR-PAY-02`, `BR-PAY-06`), cộng dồn & nâng bậc tức thì (`BR-PAY-04`), quản trị giá & Grandfathering (`BR-PAY-05`), đối soát & xuất CSV/XLSX ẩn danh PII. |
| **11** | [`11_module_ai_misclassification_feedback_dataset_curation_api.md`](./11_module_ai_misclassification_feedback_dataset_curation_api.md) | **Module 11: AI Misclassification Feedback & Dataset Curation (`M-13` / `FE-15`)** | `/api/v1/waste-scan`<br>`/api/v1/staff/ai-feedback`<br>`/api/v1/staff/ai-labeling`<br>`/api/v1/staff/ai-datasets` | **10 Core + 2 AI APIs (`STT 130–139`, `183–184`, nâng cấp `021, 024`):** Lưu vết phiên quét `SHA-256` + tự động thu thập ca OOD `< 60%`, báo cáo phân loại sai có liên kết `scanId` & lịch sử cá nhân (`U-04`), Staff thẩm định 1 chạm `ApproveAndLabel` / `ApproveToQueue` / `Reject` (`S-01`), gán nhãn YOLO & hàng loạt nguyên tử (`S-02`), đóng gói phiên bản Dataset **Sticky Stratified Split 70/20/10** chống rò rỉ (`BR-CLS-07`), xuất ZIP `YOLO`/`ImageFolder` & đồng bộ nạp nóng FastAPI (`S-03`). |
| **12** | [`12_module_rating_review_complaint_resolution_api.md`](./12_module_rating_review_complaint_resolution_api.md) | **Module 12: Rating, Review & Complaint Resolution (`M-11` / `FE-10`)** | `/api/v1/handover-requests/{id}/reviews`<br>`/api/v1/recyclers/{id}/reviews`<br>`/api/v1/reviews`<br>`/api/v1/complaints`<br>`/api/v1/staff/complaints` | **12 APIs (`STT 103–114`):** Đánh giá `1–5` sao đơn `Completed` trong `14 ngày` (`BR-RVW-01`), tự xóa vĩnh viễn (`BR-RVW-03`), báo cáo & kiểm duyệt đánh giá vi phạm + tính lại `AverageRating` tức thì; khiếu nại 2 chiều `Citizen`/`Recycler` (`BR-DSP-01`), tải bằng chứng R2 (`0..5` ảnh), hợp nhất `DisputeAsync`, Hồ sơ Trọng tài 360° (`ScaleProofPhotoUrls` + chat bất biến `BR-DSP-06`), thu hồi đồng bộ `UserPointWallet` (`>= 0`) & cảnh cáo `AccountModerationLog` (`BR-ADM-05`). |
| **13** | [`13_module_gamification_and_rewards_api.md`](./13_module_gamification_and_rewards_api.md) | **Module 13: Gamification & Rewards (`M-10` / `FE-13`)** | `/api/v1/gamification`<br>`/api/v1/staff/gifts`<br>`/api/v1/staff/eco-facts`<br>`/api/v1/admin/gamification` | **27 APIs (`STT 076–102`):** Hồ sơ Sống Xanh dữ liệu thật, Điểm danh hằng ngày `ICT` + Streak & trần điểm phi giao dịch (`U-13`), Ví điểm & Sổ cái idempotent (`user_point_wallets`), Đổi quà nguyên tử & nhập mã E-Voucher (`U-14`, `S-10`), Cây Ảo & Eco Fact (`U-15`, `S-09`), Cấu hình quy đổi & Chu kỳ Bảng xếp hạng + Chốt thưởng Top 3 (`A-07`, `A-08`). |
| **14** | [`14_module_blog_and_content_management_api.md`](./14_module_blog_and_content_management_api.md) | **Module 14: Blog & Content Management (`M-14` / `FE-16`)** | `/api/v1/blogs`<br>`/api/v1/staff/blogs`<br>`/api/v1/admin/blogs` | **10 APIs (`STT 140–149`):** Cổng Blog công khai lọc thẻ tag & đọc theo slug tiếng Việt (`G-01`), Staff `[MANAGE_BLOG]` tải ảnh Cloudflare R2 (`upload-media`), soạn nháp, nộp duyệt & thu hồi (`S-08`), Admin phê duyệt xuất bản (`Approved`) hoặc từ chối (`Draft`) kèm góp ý bắt buộc & ghi `AuditLog` (`A-09`, `BR-ADM-08`). |

---

## 2. ĐỊA CHỈ MÁY CHỦ (BASE URLS)

| Dịch vụ | Môi trường Local | Swagger / OpenAPI UI | Health Check |
|:---|:---|:---|:---|
| **Core Business Web API (.NET 10)** | `http://localhost:5000` | `http://localhost:5000/swagger` | `GET /health` |
| **SignalR Negotiation Hub (WebSocket)** | `ws://localhost:5000/hubs/negotiation-chat` | — | — |
| **SignalR Notifications Hub (WebSocket)** | `ws://localhost:5000/hubs/notifications` | — | — |
| **Dedicated AI Vision Service (FastAPI)** | `http://127.0.0.1:8000` | `http://127.0.0.1:8000/docs` | `GET /health` |

---

## 3. CƠ CHẾ XÁC THỰC & PHÂN QUYỀN (AUTHENTICATION, RBAC & GUARD)

### 3.1. Truyền Access Token qua Header (REST) hoặc Query String (SignalR WebSocket)
Các API yêu cầu xác thực (`[Authorize]`) bắt buộc đính kèm JWT Access Token trong HTTP Header:

```http
Authorization: Bearer <eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...>
```

Đối với kết nối thời gian thực **SignalR WebSocket** tại `/hubs/negotiation-chat` và `/hubs/notifications`, client có thể truyền JWT qua Header hoặc qua Query Parameter `?access_token=<jwt_token>`.

* **Thời hạn Access Token:** `30 phút` (cấu hình tại `JwtSettings:AccessTokenExpirationMinutes`).
* **Thời hạn Refresh Token:** `7 ngày` (lưu tự động trong `HttpOnly Cookie` tên `refreshToken` hoặc truyền qua body).

### 3.2. Các Vai trò Hệ thống (User Roles)
* `Guest`: Khách vãng lai chưa đăng nhập (chỉ gọi được các API công khai như đăng ký, đăng nhập, quét rác AI, tìm vựa bản đồ, tra cứu quy tắc tĩnh).
* `Citizen` (`User`): Người dân đã đăng ký tài khoản (quét rác AI, tìm điểm thu gom, tạo đơn bàn giao, thương lượng qua Chatbox `U-08`, tích điểm xanh, mua gói `PREMIUM` qua VietQR `U-16`, nộp hồ sơ xin nâng cấp lên `Recycler`).
* `Recycler`: Đối tác / Cơ sở thu mua phế liệu đã được `Staff` phê duyệt hồ sơ qua Module 2 (quản lý hồ sơ & bảng giá thu mua `R-01`, bật/tắt trạng thái mở cửa `R-02`, thương lượng & chốt khung giờ thu gom `R-05`, quét QR xác nhận khối lượng, mua gói `PRO` Tích xanh & Ghim nổi bật bản đồ `R-07`).
* `Staff`: Nhân viên vận hành hệ thống với **0 quyền mặc định** khi mới tạo (`BR-ADM-01`), được `Admin` gán chi tiết trong danh mục 7 mã quyền chuẩn tại bảng phân quyền động (`BR-ADM-02`): thẩm định hồ sơ `Recycler`, xử lý khiếu nại, kiểm duyệt đánh giá, gán nhãn Dataset, quản lý kho quà/Blog, khóa/mở khóa tài khoản người dùng (`S-06`, `S-07`), chấm công ca trực (`S-11`).
* `Admin`: Quản trị viên toàn quyền (quản lý chính sách Rule Engine, danh mục rác, tài khoản nhân viên & phân quyền RBAC `A-03`/`A-04`, cấu hình gói dịch vụ `A-10`, đối soát dòng tiền VietQR `A-11`, Dashboard KPI, xuất báo cáo & xem Audit Log bất biến `A-01`/`A-02`).

### 3.3. Guard Tức thời, Phân quyền Động `[HasPermission]` & Bảo mật Webhook
1. **Kiểm tra trạng thái tức thời (`AccountStatusGuardMiddleware`):** Sau lớp xác thực JWT, mọi request được đối chiếu trực tiếp với snapshot trạng thái tài khoản (`auth_snapshot:{userId}` trên `IMemoryCache`). Khi bị khóa (`Suspended`), vô hiệu hóa mềm (`Deactivated`) hoặc bị `Admin` thu hồi quyền, cache bị xóa tức thì và mọi JWT còn hạn trả về `403 Forbidden` (`MSG-AUTH-04`) hoặc `MSG-ADM-04`.
2. **Phân quyền chi tiết theo tác vụ (`[HasPermission]` & `PermissionAuthorizationHandler`):** Với các route của Nhân viên (`Staff`), tài khoản `Admin` được đi qua mặc định; tài khoản `Staff` bắt buộc phải sở hữu mã quyền tương ứng (ví dụ `MANAGE_ACCOUNTS` khi truy cập `/api/v1/staff/accounts/*`).
3. **Xác thực Cổng Thanh toán VietQR & Điều kiện Gói Thuê bao (`M-12`):**
   * Endpoint `POST /api/v1/service-payments/webhook` mở công khai cho Payment Gateway nhưng bắt buộc xác thực chữ ký PayOS HMAC-SHA256 hoặc header `X-VietQr-Webhook-Secret` khi cấu hình bí mật hoạt động (sai chữ ký bị ghi `AuditLog` từ chối và trả `400 Bad Request` – `MSG-PAY-05`).
   * Gói thương mại `PRO` (`1,000,000 VNĐ / 365 ngày`) yêu cầu tài khoản có `Role = Recycler` và hồ sơ KYB đạt `VerificationStatus.Verified` (`BR-PAY-03`, chặn bằng `MSG-PAY-06`).

> [!IMPORTANT]
> **Ranh giới nghiệp vụ nâng cấp vai trò (`Guest` $\rightarrow$ `Citizen` $\rightarrow$ `Recycler`):**
> * Khi đăng ký mới tại Module 1 (`POST /api/v1/auth/register` hoặc `POST /api/v1/auth/google`), tài khoản tạo ra mặc định mang vai trò **`Citizen`** (`User`).
> * Để trở thành **`Recycler`**, người dùng có tài khoản `Citizen` phải nộp hồ sơ đăng ký cơ sở thu gom tại Module 2 (`POST /api/v1/recycler-applications`) và được `Staff/Admin` phê duyệt (`PUT /api/v1/recycler-applications/{userId}/verification`).
> * Sau khi được duyệt lên **`Recycler`**, đối tác truy cập Module 4 (`/api/v1/recycler/*`) để thiết lập bảng giá phế liệu (`R-01`) và bật trạng thái `Online` (`R-02`) trước khi xuất hiện trên bản đồ tìm kiếm của người dân (`U-05`), đồng thời đủ điều kiện đăng ký gói `PRO` tại Module 10 (`R-07`).

---

## 4. KHUNG PHẢN HỒI CHUẨN (`ApiResponse<T>`, `PagedResult<T>` & XUẤT TỆP BÁO CÁO)

Toàn bộ API JSON của Core Backend đều sử dụng `camelCase`, Enum serialize dạng chuỗi (`JsonStringEnumConverter`), và được bọc trong lớp `ApiResponse<T>`:

### 4.1. Cấu trúc `ApiResponse<T>`

| Tên trường | Kiểu dữ liệu | Bắt buộc | Mô tả |
|:---|:---|:---:|:---|
| `success` | `boolean` | Có | `true` nếu yêu cầu xử lý thành công (`2xx`), `false` nếu thất bại (`4xx`, `5xx`). |
| `messageCode` | `string?` | Không | Mã thông báo chuẩn hóa theo từng phân hệ (VD: `MSG-AUTH-08`, `MSG-MTH-03`, `MSG-NEG-03`, `MSG-PAY-01`, `MSG-ADM-01`, `MSG-GEN-01`). |
| `message` | `string?` | Không | Thông báo tiếng Việt thân thiện để hiển thị trực tiếp lên giao diện (Toast / Inline message). |
| `data` | `T?` | Không | Dữ liệu nghiệp vụ trả về (Object, Array, hoặc `PagedResult<T>`). Trả về `null` khi gặp lỗi. |
| `errors` | `string[]?` | Không | Danh sách chi tiết các lỗi xác thực đầu vào (FluentValidation / ModelState). Trả về `null` khi thành công. |
| `timestamp` | `string (ISO 8601)` | Có | Thời điểm phản hồi từ Server theo múi giờ chuẩn UTC (VD: `"2026-11-17T08:15:30.123Z"`). |

#### Ví dụ Phản hồi Thành công (`200 OK` / `201 Created`):
```json
{
  "success": true,
  "messageCode": "MSG-MTH-03",
  "message": "Hồ sơ cơ sở của bạn đang được xem xét và sẽ xuất hiện trong tìm kiếm sau khi được duyệt.",
  "data": {
    "userId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "facilityName": "Cơ Sở Phế Liệu Xanh Thủ Đức",
    "verificationStatus": "Pending"
  },
  "errors": null,
  "timestamp": "2026-11-17T08:15:30Z"
}
```

#### Ví dụ Phản hồi Lỗi Nghiệp vụ (`400 Bad Request` - `BusinessRuleException`):
```json
{
  "success": false,
  "messageCode": "MSG-AUTH-03",
  "message": "Bạn đã nhập sai quá nhiều lần. Vui lòng thử lại sau 15 phút.",
  "data": null,
  "errors": null,
  "timestamp": "2026-11-17T08:16:00Z"
}
```

#### Ví dụ Phản hồi Lỗi Validation Đầu vào (`400 Bad Request` - FluentValidation / ModelBinding):
```json
{
  "success": false,
  "messageCode": "MSG-GEN-01",
  "message": "Dữ liệu đầu vào không hợp lệ.",
  "data": null,
  "errors": [
    "FacilityName: Tên cơ sở thu gom không được để trống.",
    "Latitude: Vĩ độ (Latitude) phải nằm trong khoảng từ -90 đến 90."
  ],
  "timestamp": "2026-11-17T08:17:00Z"
}
```

---

### 4.2. Cấu trúc Danh sách Phân trang (`PagedResult<T>`)

Các API truy vấn danh sách có phân trang trả về đối tượng `PagedResult<T>` bên trong trường `data`:

| Tên trường | Kiểu dữ liệu | Mô tả |
|:---|:---|:---|
| `items` | `T[]` | Mảng các bản ghi của trang hiện tại. |
| `pageNumber` | `integer` | Chỉ số trang hiện tại (bắt đầu từ `1`). |
| `pageSize` | `integer` | Số lượng bản ghi tối đa trên mỗi trang (mặc định `10` hoặc `20`, tối đa `100`). |
| `totalRecords` | `integer` | Tổng số bản ghi thỏa mãn điều kiện lọc trong CSDL. |
| `totalPages` | `integer` | Tổng số trang được tính tự động $\lceil \text{totalRecords} / \text{pageSize} \rceil$. |
| `hasPreviousPage` | `boolean` | `true` nếu `pageNumber > 1`. |
| `hasNextPage` | `boolean` | `true` nếu `pageNumber < totalPages`. |

---

### 4.3. Quy chuẩn Xuất Tệp Nhị phân CSV / XLSX (`FileContentResult`)

Các API xuất báo cáo quản trị (`/api/v1/admin/reports/export` và `/api/v1/admin/reconciliations/export`) trả về luồng tệp trực tiếp (`200 OK`) kèm Metadata Header và che mờ PII bắt buộc (`BR-ADM-06`):

| Thành phần phản hồi | Giá trị chuẩn hóa |
|:---|:---|
| `Content-Type` | `text/csv; charset=utf-8` *(kèm UTF-8 BOM cho Excel)* hoặc `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` *(OpenXML `.xlsx`)* |
| `Content-Disposition` | `attachment; filename="ecolink-...-YYYY-MM-DD.{csv\|xlsx}"` |
| `X-EcoLink-Message-Code` | `MSG-ADM-02` |
| `X-EcoLink-Row-Count` | Tổng số dòng dữ liệu đã xuất *(số nguyên dạng chuỗi)* |
| **Che mờ PII (`PiiMaskingHelper`)** | Email được che dạng `ab***@domain.com`, Số điện thoại được che dạng `091****678`, và mọi lượt xuất đều ghi vào bảng bất biến `audit_logs` (`BR-ADM-05`). |

---

## 5. BẢNG ÁNH XẠ HTTP STATUS CODE & XỬ LÝ NGOẠI LỆ (`GlobalExceptionMiddleware`)

| HTTP Status | Loại Exception trong Backend | Mã `messageCode` mặc định | Ý nghĩa & Trường hợp xảy ra |
|:---:|:---|:---|:---|
| **`200 OK`** | — | `MSG-GEN-01` / `MSG-[MOD]-XX` | Truy vấn (`GET`), cập nhật (`PUT`/`PATCH`) hoặc thực thi tác vụ (`POST` login, scan AI, hủy đơn chờ, thanh toán/giả lập VietQR) thành công. |
| **`201 Created`** | — | `MSG-AUTH-08` / `MSG-MTH-03` / `MSG-NEG-01` / `MSG-PAY-07` | Khởi tạo tài nguyên mới thành công (đăng ký tài khoản, nộp hồ sơ Recycler, tạo nhân viên `Staff`, điểm danh ca trực, tạo gói dịch vụ mới `A-10`). |
| **`400 Bad Request`** | `BusinessRuleException`<br>`ValidationException` | `MSG-[MOD]-XX`<br>`MSG-GEN-01` | Dữ liệu đầu vào sai định dạng (FluentValidation) hoặc vi phạm quy tắc nghiệp vụ (`BR-*`, chữ ký webhook `MSG-PAY-05`, chưa duyệt KYB `MSG-PAY-06`). |
| **`401 Unauthorized`** | `UnauthorizedAccessException` | `MSG-UNAUTHORIZED`<br>`MSG-NTF-01` | Thiếu JWT Bearer Token, Token hết hạn, sai chữ ký, hoặc Refresh Token không hợp lệ. |
| **`403 Forbidden`** | `ForbiddenAccessException`<br>ASP.NET Authorization / Guard | `MSG-ADM-04` / `MSG-AUTH-04`<br>`MSG-NEG-FORBIDDEN` | Vai trò/quyền `[HasPermission]` không hợp lệ, tài khoản đang bị khóa/vô hiệu hóa (`MSG-AUTH-04`), hoặc cố truy cập tài nguyên của người khác (IDOR trên phòng chat / đơn hàng / đơn VietQR). |
| **`404 Not Found`** | `NotFoundException` | `MSG-NOT-FOUND` | Không tìm thấy bản ghi theo ID hoặc mã đơn/webhook yêu cầu. |
| **`409 Conflict`** | `ConflictException` | `MSG-PAY-03` / `MSG-PAY-08`<br>`MSG-GEN-01` | Xung đột trạng thái tài nguyên: hủy đơn VietQR đã `Paid` (`BR-PAY-01`), tạo trùng `packageCode` (`BR-PAY-05`), hoặc đối soát lặp lại bản ghi đã `Resolved`/`Refunded` (`BR-PAY-06`). |
| **`500 Internal Server Error`** | `Exception` (Unhandled) | `MSG-SYS-500` | Lỗi hệ thống hoặc kết nối dịch vụ ngoài. Hệ thống ẩn chi tiết kỹ thuật và trả về thông báo an toàn. |

---

## 6. BẢNG TRA CỨU MÃ THÔNG BÁO HỆ THỐNG (`MSG-*`) DÙNG CHUNG

| Mã Thông Báo | Phân hệ | Loại hiển thị | Nội dung thông báo chuẩn |
|:---|:---:|:---:|:---|
| **`MSG-GEN-01`** | Hệ thống chung | Inline / Toast | Thao tác thành công / Dữ liệu đầu vào không hợp lệ (kèm mảng `errors`). |
| **`MSG-NOT-FOUND`** | Hệ thống chung | Inline | Không tìm thấy dữ liệu yêu cầu trong hệ thống. |
| **`MSG-UNAUTHORIZED`** | Hệ thống chung | Toast | Không xác định được định danh người dùng từ Token hoặc phiên không hợp lệ. |
| **`MSG-SYS-500`** | Hệ thống chung | Toast | Đã xảy ra sự cố trong quá trình xử lý hệ thống. Vui lòng thử lại sau. |
| **`MSG-AUTH-04`** | Xác thực & Bảo vệ phiên (`AUTH`) | Inline / Guard | Tài khoản này hiện không còn hoạt động. Vui lòng liên hệ bộ phận hỗ trợ. |
| **`MSG-NTF-01`** | Thông báo / Phiên | Toast | Phiên làm việc của bạn đã hết hạn. Vui lòng đăng nhập lại. |
| **`MSG-MTH-03`** | Hồ sơ & Bản đồ (`MTH`) | Inline / Banner | Hồ sơ cơ sở của bạn đang được xem xét và sẽ xuất hiện trong tìm kiếm sau khi được duyệt. |
| **`MSG-MTH-04`** | Hồ sơ & Bản đồ (`MTH`) | Toast / Email | Hồ sơ cơ sở của bạn đã được duyệt và hiện hiển thị với người dùng. |
| **`MSG-MTH-05`** | Hồ sơ & Bản đồ (`MTH`) | Inline / Banner | Hồ sơ cơ sở của bạn chưa được duyệt kèm lý do từ chối. Vui lòng cập nhật và gửi lại. |
| **`MSG-MTH-06`** | Hồ sơ & Bản đồ (`MTH`) | Toast / Inline | Vui lòng thêm ít nhất một loại phế liệu nhận mua và một vị trí hợp lệ trước khi hoạt động. |
| **`MSG-HND-01`** | Bàn giao & Biên nhận (`HND`) | Toast | Yêu cầu bàn giao của bạn đã được gửi. |
| **`MSG-HND-02`** | Bàn giao & Biên nhận (`HND`) | Toast | Yêu cầu bàn giao đã được chấp nhận. |
| **`MSG-HND-03`** | Bàn giao & Biên nhận (`HND`) | Toast | Giao dịch `#{receipt_code}` đã được đánh dấu hoàn tất. |
| **`MSG-HND-04`** | Bàn giao & Biên nhận (`HND`) | Warning Banner | Khối lượng xác nhận chênh lệch so với thỏa thuận hơn 20%. Vui lòng kiểm tra trước khi xác nhận. |
| **`MSG-HND-05`** | Bàn giao & Biên nhận (`HND`) | Toast | Yêu cầu bàn giao / Lịch thu gom đã được hủy. |
| **`MSG-HND-06`** | Bàn giao & Biên nhận (`HND`) | Warning Toast | Đã hủy. Việc hủy trong vòng 4 giờ trước lịch hẹn được ghi nhận là hủy muộn. |
| **`MSG-NEG-01`** | Thương lượng (`NEG`) | Toast | Yêu cầu trao đổi của bạn đã được gửi đến `{facility_name}`. |
| **`MSG-NEG-02`** | Thương lượng (`NEG`) | In-app Notification | Bạn có tin nhắn mới từ `{sender_name}`. |
| **`MSG-NEG-03`** | Thương lượng (`NEG`) | Toast | `{party_name}` đã chấp nhận đề nghị của bạn. |
| **`MSG-NEG-04`** | Thương lượng (`NEG`) | Toast | Bạn có yêu cầu bàn giao mới kèm khung giờ đề xuất từ `{user_name}`. |
| **`MSG-PAY-01`** | Gói dịch vụ & VietQR (`PAY`) | Inline / Modal | Quét mã VietQR này để hoàn tất thanh toán dịch vụ EcoLink. |
| **`MSG-PAY-02`** | Gói dịch vụ & VietQR (`PAY`) | Toast / Poll | Thanh toán dịch vụ của bạn đã được xác nhận thành công. |
| **`MSG-PAY-03`** | Gói dịch vụ & VietQR (`PAY`) | Inline / Toast | Mã thanh toán hết hạn, số tiền tạo đơn không hợp lệ, đã hủy đơn chờ hoặc từ chối hủy đơn đã thanh toán. |
| **`MSG-PAY-05`** | Gói dịch vụ & VietQR (`PAY`) | Security / Toast | Chữ ký bảo mật hoặc payload Webhook VietQR / PayOS không hợp lệ (`400 Bad Request`). |
| **`MSG-PAY-06`** | Gói dịch vụ & VietQR (`PAY`) | Inline / Guard | Cơ sở thu mua cần hoàn tất xác minh hồ sơ KYB trước khi đăng ký gói `PRO` Tích xanh (`BR-PAY-03`). |
| **`MSG-PAY-07`** | Quản trị Gói dịch vụ (`A-10`) | Toast (Admin) | Thao tác tạo mới, cập nhật hoặc bật/tắt trạng thái mở bán gói dịch vụ thành công. |
| **`MSG-PAY-08`** | Đối soát Tài chính (`A-11`) | Toast (Admin) | Đối soát thủ công giao dịch VietQR bất thường thành công hoặc từ chối xử lý trùng/sai điều kiện (`409`). |
| **`MSG-CLS-06`** | Phản hồi Lỗi AI (`U-04`) | Toast / Inline | Cảm ơn bạn đã phản hồi! Dữ liệu đã được chuyển tới Nhân viên vận hành để kiểm chứng và cải thiện mô hình AI. |
| **`MSG-CLS-07`** | Thẩm định & Gán nhãn (`S-01`, `S-02`) | Toast (Staff) | Đã cập nhật kết quả thẩm định và chuẩn hóa nhãn Ground-Truth cho mẫu ảnh AI. |
| **`MSG-CLS-08`** | Đóng gói Dataset (`S-03`) | Toast (Staff) | Đã đóng gói phiên bản Dataset `'vX.Y.Z'` theo thuật toán Sticky Stratified Split `70/20/10` (`BR-CLS-07`). |
| **`MSG-RVW-01`** | Đánh giá Cơ sở (`U-10`) | Toast | Cảm ơn bạn đã đánh giá! Điểm uy tín của cơ sở thu mua đã được cập nhật. |
| **`MSG-RVW-02`** | Đánh giá Cơ sở (`U-10`) | Inline / Guard | Bạn chỉ có thể đánh giá mỗi đơn giao dịch hoàn tất một lần trong thời hạn quy định (`BR-RVW-01`). |
| **`MSG-RVW-03`** | Xóa / Kiểm duyệt Đánh giá (`U-10`) | Toast | Đánh giá đã được cập nhật trạng thái và tính lại điểm sao trung bình của cơ sở (`BR-RVW-03`). |
| **`MSG-DSP-01`** | Nộp Khiếu nại (`U-11`) | Toast | Khiếu nại của bạn đã được gửi và đang được xem xét (`BR-DSP-01`). |
| **`MSG-DSP-02`** | Trọng tài Khiếu nại (`S-05`) | Toast / Notify | Khiếu nại của bạn đã được giải quyết: `{resolutionOutcome}` (`BR-DSP-04`). |
| **`MSG-DSP-03`** | Nộp Khiếu nại (`U-11`) | Inline / Guard | Bạn không thể khiếu nại giao dịch này nữa (quá thời hạn 7 ngày hoặc trạng thái không hợp lệ). |
| **`MSG-DSP-04`** | Chống trùng / Đóng lặp (`U-11`, `S-05`) | Inline (`409`) | Đã có một khiếu nại đang mở hoặc đang xử lý cho giao dịch này / Phiếu đã giải quyết trước đó. |
| **`MSG-ADM-01`** | Quản trị Hệ thống (`ADM`) | Toast (Staff/Admin) | Cập nhật trạng thái tài khoản / phân quyền RBAC thành công. |
| **`MSG-ADM-02`** | Quản trị Hệ thống (`ADM`) | Header / Toast (Admin) | Xuất báo cáo hệ thống / bảng kê đối soát thành công (`X-EcoLink-Message-Code: MSG-ADM-02`). |
| **`MSG-ADM-03`** | Quản trị Hệ thống (`ADM`) | Toast (Admin) | Tạo tài khoản nhân viên hoặc vô hiệu hóa mềm tài khoản thành công. |
| **`MSG-ADM-04`** | Quản trị Hệ thống (`ADM`) | Inline / Guard | Bạn không có quyền thực hiện thao tác này (`403 Forbidden` cho IDOR / RBAC). |
| **`MSG-ADM-05`** | Quản trị Blog (`A-09`) | Toast (Admin) | Bài viết `'{title}'` đã được duyệt và xuất bản (`BR-ADM-08`). |
| **`MSG-ADM-06`** | Biên tập Blog (`S-08`) | Toast (Staff) | Bài viết đã được gửi Admin xét duyệt (`Draft -> Pending_Approval`). |
| **`MSG-GAM-02`** | Đổi Quà (`U-14`) | Toast | Đổi quà thành công. Mã đổi thưởng / E-Voucher đã được cấp (`BR-GAM-04`). |
| **`MSG-GAM-03`** | Ví Điểm (`GAM`) | Inline / Guard | Bạn không đủ điểm để đổi quà hoặc nâng cấp cây (`BR-GAM-03`). |
| **`MSG-GAM-04`** | Kho Quà (`U-14`) | Inline / Guard | Quà này đã hết hàng hoặc không còn khả dụng (`BR-GAM-04`). |
| **`MSG-GAM-05`** | Điểm Danh Ngày (`U-13`) | Inline / Guard | Bạn đã đạt giới hạn điểm phi giao dịch trong ngày (`DailyNonTransactionCap`, `BR-GAM-02`). |
| **`MSG-GAM-06`** | Cây Ảo (`U-15`) | Toast / Modal | Cây của bạn đã lên cấp mới kèm mở khóa thẻ kiến thức `Eco Fact` (`BR-GAM-06`). |
| **`MSG-GAM-08`** | Điểm Danh Ngày (`U-13`) | Toast | Điểm danh thành công! Bạn nhận được `{points}` điểm (chuỗi: `{streak}` ngày) (`BR-GAM-09`). |
