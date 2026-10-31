# TỔNG QUAN KIẾN TRÚC API, KHUNG PHẢN HỒI CHUẨN & HỆ THỐNG MÃ THÔNG BÁO
**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phiên bản API:** `v1` (`/api/v1/...`)  
**Chuẩn thiết kế:** RESTful API + Clean Architecture (`ApiResponse<T>`, `PagedResult<T>`)  

---

## 1. DANH MỤC TÀI LIỆU API THEO MODULE

Tài liệu đặc tả API của hệ thống EcoLink được chia thành các phân hệ (Module) độc lập dưới đây:

| STT | File Tài liệu | Module / Phân hệ | Base Route | Mô tả chính |
|:---:|:---|:---|:---|:---|
| **00** | [`00_api_overview_and_response_contract.md`](./00_api_overview_and_response_contract.md) | **Overview & Response Contract** | `/api/v1/*` | Khung JSON `ApiResponse<T>`, phân trang `PagedResult<T>`, xác thực JWT, HTTP Status Codes và bảng tra cứu `MSG-*` / `BR-*`. |
| **01** | [`01_module_authentication_api.md`](./01_module_authentication_api.md) | **Module 1: Authentication & Account (`AUTH`)** | `/api/v1/auth` | Đăng ký tài khoản `Citizen`, Đăng nhập (chống Brute-force), Google OAuth 2.0, Làm mới Token (chống Replay Attack), Quên & Đặt lại mật khẩu qua Gmail SMTP. |
| **02** | [`02_module_recycler_onboarding_api.md`](./02_module_recycler_onboarding_api.md) | **Module 2: Recycler Onboarding & Verification (`MTH` / `U-17` & `S-04`)** | `/api/v1/recycler-applications` | Tải chứng từ/ảnh cơ sở lên Cloudflare R2, Người dân (`Citizen`) nộp đơn nâng cấp lên Cơ sở thu gom (`Recycler`) kèm tọa độ PostGIS, `Staff/Admin` thẩm định (Duyệt / Từ chối). |
| **03** | [`03_module_ai_waste_classification_api.md`](./03_module_ai_waste_classification_api.md) | **Module 3: AI Waste Classification & Rule Engine (`CLS` & `SRT`)** | `/api/v1/waste-scan`<br>`/api/v1/classify` | Nhận diện rác đa mô hình (`yolov8_detect`, `mobilenet_v3`, `efficientnet_b0`, `yolov8n`), kiểm tra ngưỡng OOD (60%), tra cứu hướng dẫn xử lý Rule Engine và báo cáo AI nhận diện sai. |
| **04** | [`04_module_trading_gamification_payment_api.md`](./04_module_trading_gamification_payment_api.md) | **Module 4: Geo-Matching, Trading, Gamification & Payment (`MTH`, `HND`, `GAM`, `PAY`)** | `/api/v1/trade`<br>`/api/v1/gamification` | Tìm kiếm điểm thu mua gần nhất theo GPS, tạo đơn hẹn bàn giao rác, quét QR chốt biên nhận, tích điểm xanh & Cây ảo, bảng xếp hạng và thanh toán VietQR / PayOS. |

---

## 2. ĐỊA CHỈ MÁY CHỦ (BASE URLS)

| Dịch vụ | Môi trường Local | Swagger / OpenAPI UI | Health Check |
|:---|:---|:---|:---|
| **Core Business Web API (.NET 10)** | `http://localhost:5000` | `http://localhost:5000/swagger` | `GET /health` |
| **Dedicated AI Vision Service (FastAPI)** | `http://127.0.0.1:8000` | `http://127.0.0.1:8000/docs` | `GET /health` |

---

## 3. CƠ CHẾ XÁC THỰC & PHÂN QUYỀN (AUTHENTICATION & RBAC)

### 3.1. Truyền Access Token qua Header
Các API yêu cầu xác thực (`[Authorize]`) bắt buộc đính kèm JWT Access Token trong HTTP Header:

```http
Authorization: Bearer <eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...>
```

* **Thời hạn Access Token:** `30 phút` (cấu hình tại `JwtSettings:AccessTokenExpirationMinutes`).
* **Thời hạn Refresh Token:** `7 ngày` (lưu tự động trong `HttpOnly Cookie` tên `refreshToken` hoặc truyền qua body).

### 3.2. Các Vai trò Hệ thống (User Roles)
* `Guest`: Khách vãng lai chưa đăng nhập (chỉ gọi được các API công khai như đăng ký, đăng nhập, tra cứu quy tắc tĩnh).
* `Citizen` (`User`): Người dân đã đăng ký tài khoản (quét rác AI, tìm điểm thu gom, tạo đơn bàn giao, tích điểm xanh, nộp hồ sơ xin nâng cấp lên `Recycler`).
* `Recycler`: Đối tác / Cơ sở thu mua phế liệu đã được `Staff` phê duyệt hồ sơ qua Module 2 (nhận đơn bàn giao, quét QR xác nhận khối lượng).
* `Staff`: Nhân viên vận hành hệ thống (thẩm định hồ sơ `Recycler`, duyệt phản hồi ảnh AI sai, xử lý khiếu nại).
* `Admin`: Quản trị viên toàn quyền (quản lý chính sách Rule Engine, danh mục rác, tài khoản nhân viên, báo cáo).

> [!IMPORTANT]
> **Ranh giới nghiệp vụ nâng cấp vai trò (`Guest` $\rightarrow$ `Citizen` $\rightarrow$ `Recycler`):**
> * Khi đăng ký mới tại Module 1 (`POST /api/v1/auth/register` hoặc `POST /api/v1/auth/google`), tài khoản tạo ra mặc định mang vai trò **`Citizen`** (`User`).
> * Để trở thành **`Recycler`**, người dùng có tài khoản `Citizen` phải nộp hồ sơ đăng ký cơ sở thu gom tại Module 2 (`POST /api/v1/recycler-applications`) và được `Staff/Admin` phê duyệt (`PUT /api/v1/recycler-applications/{userId}/verification`).

---

## 4. KHUNG PHẢN HỒI CHUẨN (`ApiResponse<T>` & `PagedResult<T>`)

Toàn bộ API của Core Backend đều trả về dữ liệu JSON theo định dạng `camelCase` được bọc trong lớp `ApiResponse<T>`:

### 4.1. Cấu trúc `ApiResponse<T>`

| Tên trường | Kiểu dữ liệu | Bắt buộc | Mô tả |
|:---|:---|:---:|:---|
| `success` | `boolean` | Có | `true` nếu yêu cầu xử lý thành công (`2xx`), `false` nếu thất bại (`4xx`, `5xx`). |
| `messageCode` | `string?` | Không | Mã thông báo chuẩn hóa theo từng phân hệ (VD: `MSG-AUTH-08`, `MSG-MTH-03`, `MSG-CLS-01`, `MSG-GEN-01`). |
| `message` | `string?` | Không | Thông báo tiếng Việt thân thiện để hiển thị trực tiếp lên giao diện (Toast / Inline message). |
| `data` | `T?` | Không | Dữ liệu nghiệp vụ trả về (Object, Array, hoặc `PagedResult<T>`). Trả về `null` khi gặp lỗi. |
| `errors` | `string[]?` | Không | Danh sách chi tiết các lỗi xác thực đầu vào (FluentValidation / ModelState). Trả về `null` khi thành công. |
| `timestamp` | `string (ISO 8601)` | Có | Thời điểm phản hồi từ Server theo múi giờ chuẩn UTC (VD: `"2026-10-30T08:15:30.123Z"`). |

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
  "timestamp": "2026-10-30T08:15:30Z"
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
  "timestamp": "2026-10-30T08:16:00Z"
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
  "timestamp": "2026-10-30T08:17:00Z"
}
```

---

### 4.2. Cấu trúc Danh sách Phân trang (`PagedResult<T>`)

Các API truy vấn danh sách có phân trang trả về đối tượng `PagedResult<T>` bên trong trường `data`:

| Tên trường | Kiểu dữ liệu | Mô tả |
|:---|:---|:---|
| `items` | `T[]` | Mảng các bản ghi của trang hiện tại. |
| `pageNumber` | `integer` | Chỉ số trang hiện tại (bắt đầu từ `1`). |
| `pageSize` | `integer` | Số lượng bản ghi tối đa trên mỗi trang (mặc định `10`, tối đa `100`). |
| `totalRecords` | `integer` | Tổng số bản ghi thỏa mãn điều kiện lọc trong CSDL. |
| `totalPages` | `integer` | Tổng số trang được tính tự động $\lceil \text{totalRecords} / \text{pageSize} \rceil$. |
| `hasPreviousPage` | `boolean` | `true` nếu `pageNumber > 1`. |
| `hasNextPage` | `boolean` | `true` nếu `pageNumber < totalPages`. |

---

## 5. BẢNG ÁNH XẠ HTTP STATUS CODE & XỬ LÝ NGOẠI LỆ (`GlobalExceptionMiddleware`)

| HTTP Status | Loại Exception trong Backend | Mã `messageCode` mặc định | Ý nghĩa & Trường hợp xảy ra |
|:---:|:---|:---|:---|
| **`200 OK`** | — | `MSG-GEN-01` / `MSG-[MOD]-XX` | Truy vấn (`GET`), cập nhật (`PUT`) hoặc thực thi tác vụ (`POST` login, scan AI) thành công. |
| **`201 Created`** | — | `MSG-AUTH-08` / `MSG-MTH-03` | Khởi tạo tài nguyên mới thành công (`POST` đăng ký tài khoản, nộp hồ sơ nâng cấp Recycler). |
| **`400 Bad Request`** | `BusinessRuleException`<br>`ValidationException` | `MSG-[MOD]-XX`<br>`MSG-GEN-01` | Dữ liệu đầu vào sai định dạng (FluentValidation) hoặc vi phạm quy tắc nghiệp vụ (`BR-*`). |
| **`401 Unauthorized`** | `UnauthorizedAccessException` | `MSG-UNAUTHORIZED`<br>`MSG-NTF-01` | Thiếu JWT Bearer Token, Token hết hạn, sai chữ ký, hoặc Refresh Token không hợp lệ. |
| **`403 Forbidden`** | ASP.NET Authorization | — | Đã đăng nhập nhưng vai trò (`Role`) không có quyền truy cập endpoint (VD: `Citizen` gọi API của `Staff`). |
| **`404 Not Found`** | `NotFoundException` | `MSG-NOT-FOUND` | Không tìm thấy bản ghi theo ID yêu cầu (VD: chưa nộp hồ sơ Recycler, không tìm thấy người dùng). |
| **`500 Internal Server Error`** | `Exception` (Unhandled) | `MSG-SYS-500` | Lỗi hệ thống hoặc kết nối dịch vụ ngoài. Hệ thống ẩn chi tiết kỹ thuật và trả về thông báo an toàn. |

---

## 6. BẢNG TRA CỨU MÃ THÔNG BÁO HỆ THỐNG (`MSG-*`) DÙNG CHUNG

| Mã Thông Báo | Phân hệ | Loại hiển thị | Nội dung thông báo chuẩn |
|:---|:---:|:---:|:---|
| **`MSG-GEN-01`** | Hệ thống chung | Inline / Toast | Thao tác thành công / Dữ liệu đầu vào không hợp lệ (kèm mảng `errors`). |
| **`MSG-NOT-FOUND`** | Hệ thống chung | Inline | Không tìm thấy dữ liệu yêu cầu trong hệ thống. |
| **`MSG-UNAUTHORIZED`** | Hệ thống chung | Toast | Không xác định được định danh người dùng từ Token hoặc phiên không hợp lệ. |
| **`MSG-SYS-500`** | Hệ thống chung | Toast | Đã xảy ra sự cố trong quá trình xử lý hệ thống. Vui lòng thử lại sau. |
| **`MSG-NTF-01`** | Thông báo / Phiên | Toast | Phiên làm việc của bạn đã hết hạn. Vui lòng đăng nhập lại. |

