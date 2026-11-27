# TÀI LIỆU ĐẶC TẢ API – MODULE 1: TÀI KHOẢN & XÁC THỰC (AUTHENTICATION & ACCOUNT MANAGEMENT)
**Mã phân hệ:** `AUTH`  
**Controller:** [`AuthController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/AuthController.cs)  
**Service thực thi:** [`AuthService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AuthService.cs), [`JwtTokenService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/JwtTokenService.cs), [`GmailSmtpEmailService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GmailSmtpEmailService.cs)  
**DTOs:** [`AuthDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/AuthDtos.cs)  
**Base Route:** `/api/v1/auth`

---

## 1. TỔNG QUAN DANH SÁCH ENDPOINTS (`M-01` – 14 APIs)

> Tham chiếu Inventory: [`00_api_inventory_and_endpoint_statistics.md`](./00_api_inventory_and_endpoint_statistics.md) (`STT 001` $\rightarrow$ `014`).

| STT | Inventory ID | Phương thức | Endpoint URI | Quyền truy cập | Chức năng nghiệp vụ | Mã thông báo chính |
|:---:|:---:|:---:|:---|:---:|:---|:---|
| 1 | `001` | `POST` | `/api/v1/auth/register` | Public (`Guest`) | Đăng ký tài khoản Người dân (`Citizen`) kèm khởi tạo Hồ sơ Sinh thái (`UserEcoProfile`). | `MSG-AUTH-08`, `MSG-AUTH-01`, `MSG-GEN-01` |
| 2 | `002` | `POST` | `/api/v1/auth/register-recycler` | Public (Legacy) | Đăng ký nhanh tài khoản Recycler kèm tọa độ GPS & hồ sơ cơ sở (Legacy Compatibility). | `MSG-GEN-01`, `MSG-AUTH-01` |
| 3 | `003` | `POST` | `/api/v1/auth/login` | Public (`Guest`) | Đăng nhập bằng Username/Email & Mật khẩu (khóa tạm thời 15 phút sau 5 lần sai). | `MSG-GEN-01`, `MSG-AUTH-01`, `MSG-AUTH-03`, `MSG-AUTH-04` |
| 4 | `004` | `GET` | `/api/v1/auth/google-config` | Public (`Guest`) | Trả về cấu hình công khai `{ enabled, clientId }` của Google OAuth 2.0 cho Frontend. | `MSG-GEN-01` |
| 5 | `005` | `POST` | `/api/v1/auth/google` | Public (`Guest`) | Đăng nhập / Đăng ký tự động bằng Google OAuth 2.0 `idToken`. | `MSG-AUTH-09`, `MSG-AUTH-01`, `MSG-AUTH-04` |
| 6 | `006` | `POST` | `/api/v1/auth/refresh-token` | Public (Cookie/Body) | Làm mới cặp JWT Access Token & Refresh Token (Token Rotation & phát hiện Replay Attack). | `MSG-GEN-01`, `MSG-NTF-01`, `MSG-AUTH-10`, `MSG-AUTH-04` |
| 7 | `007` | `POST` | `/api/v1/auth/forgot-password` | Public (`Guest`) | Yêu cầu gửi email đặt lại mật khẩu qua Gmail SMTP (chống dò quét email & giới hạn tần suất 60s). | `MSG-AUTH-05`, `MSG-GEN-01` |
| 8 | `008` | `POST` | `/api/v1/auth/reset-password` | Public (`Guest`) | Đặt lại mật khẩu mới bằng Token bảo mật dùng 1 lần và thu hồi toàn bộ phiên đăng nhập cũ. | `MSG-GEN-01`, `MSG-AUTH-06`, `MSG-AUTH-01` |
| 9 | `009` | `PUT` | `/api/v1/auth/profile` | Authenticated (`Bearer`/`Cookie`) | Cập nhật hồ sơ cá nhân (`fullName`, `phoneNumber`, `avatarUrl`) của tài khoản đang đăng nhập (`SU-01`). | `MSG-GEN-01`, `MSG-NTF-01` |
| 10 | `010` | `POST` | `/api/v1/auth/change-password` | Authenticated (`Bearer`/`Cookie`) | Đổi mật khẩu đang đăng nhập và thu hồi các Refresh Token trên thiết bị khác (`SU-02`). | `MSG-GEN-01`, `MSG-NTF-01` |
| 11 | `011` | `POST` | `/api/v1/auth/logout` | Public / Cookie | Thu hồi Refresh Token hiện tại và xóa toàn bộ HttpOnly Cookies (`accessToken`, `refreshToken`, `ecolink_session_active`). | `MSG-GEN-01` |
| 12 | `012` | `GET` | `/api/v1/auth/me` | Authenticated (`Bearer`/`Cookie`/`Query`) | Lấy thông tin chi tiết tài khoản hiện tại, Điểm Xanh (`CurrentGreenPoints`, `TreeLevel`) và gói dịch vụ (`CurrentTier`). | `200 OK` / `401 Unauthorized` |
| 13 | `013` | `GET` | `/api/v1/auth/demo-users` | Public (Dev/Test) | Lấy danh sách tài khoản mẫu (`citizen`, `recycler`, `staff`, `admin`) để kiểm thử nhanh. | `200 OK` |
| 14 | `014` | `POST` | `/api/v1/auth/vietqr-token` | Public (Partner Basic Auth) | Sinh Bearer mock token `24h` theo chuẩn Basic Auth cho webhook/đối tác VietQR / TingoPay. | `200 OK` / `401 Unauthorized` |

> [!NOTE]
> **Cơ chế Xác thực Kép (`Dual Auth`: HttpOnly Cookie + Bearer JWT):**
> * `AuthController.SetAuthCookies` đính kèm `refreshToken` (`HttpOnly`, `SameSite=Lax`, `Path=/`, `7 ngày`).
> * Khi request có header `X-EcoLink-Auth-Mode`, `Origin`, hoặc đang mang cookie `accessToken` / `ecolink_session_active` (trừ khi gửi `X-EcoLink-No-Cookie: 1` hoặc `true`), server đính kèm thêm `accessToken` (`HttpOnly`, `SameSite=Lax`, hết hạn theo `expiresAt` hoặc `30 phút`) và `ecolink_session_active="1"` (`HttpOnly=false`, `7 ngày`) để trình duyệt SPA xác thực tự động mà không lưu JWT trong `localStorage`.
> * **Đăng ký tài khoản:** `POST /api/v1/auth/register` và `POST /api/v1/auth/google` khởi tạo vai trò **`Citizen`** (`User`). Để nâng cấp trở thành Cơ sở thu gom (**`Recycler`**), người dùng đăng nhập và nộp hồ sơ tại [**Module 2 (`02_module_recycler_onboarding_api.md`)**](./02_module_recycler_onboarding_api.md) (`POST /api/v1/auth/register-recycler` chỉ giữ để tương thích ngược).

---

## 2. CHI TIẾT TỪNG API ENDPOINT

---

### 2.1. Đăng ký tài khoản Người dân (`POST /api/v1/auth/register`)

* **Mô tả:** Cho phép khách (`Guest`) tạo tài khoản mới với vai trò mặc định là `Citizen` (`Status = Active`), tự động băm mật khẩu bằng `BCrypt`, khởi tạo bảng `UserEcoProfiles` (`CurrentGreenPoints = 0`, `TreeLevel = 1`), và cấp ngay cặp `AccessToken` + `RefreshToken`.
* **Quy tắc nghiệp vụ áp dụng:** `BR-AUTH-01` (Mỗi email và username là duy nhất trên toàn hệ thống).

#### a. Request Parameters (`application/json` – `RegisterUserRequestDto`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc / Định dạng | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `username` | Body | `string` | **Có** | `3 - 50` ký tự, duy nhất | Tên đăng nhập của người dùng (lưu chữ thường). |
| `email` | Body | `string` | **Có** | Chuẩn Email, duy nhất | Địa chỉ email dùng để đăng nhập và nhận thông báo bảo mật. |
| `password` | Body | `string` | **Có** | `6 - 100` ký tự | Mật khẩu tài khoản (sẽ được băm bằng `BCrypt.HashPassword`). |
| `fullName` | Body | `string` | **Có** | Tối đa `100` ký tự | Họ và tên hiển thị của người dùng. |
| `phoneNumber` | Body | `string?` | Không | Chuẩn số điện thoại | Số điện thoại liên hệ của người dùng. |

##### Ví dụ Request Body:
```json
{
  "username": "nguyenvana",
  "email": "nguyenvana@gmail.com",
  "password": "Password@123",
  "fullName": "Nguyễn Văn A",
  "phoneNumber": "0901234567"
}
```

#### b. Response Parameters (`ApiResponse<AuthResponseDto>`)

| Trường trong `data` | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `success` | `boolean` | Trạng thái xác thực (`true`). |
| `accessToken` | `string` | Chuỗi JWT Access Token (algorithm `HS256`, hết hạn sau 30 phút). |
| `token` | `string` | Trường alias tương thích ngược (giá trị giống hệt `accessToken`). |
| `refreshToken` | `string` | Chuỗi Refresh Token ngẫu nhiên 64-byte Base64 (hết hạn sau 7 ngày). |
| `tokenType` | `string` | Luôn là `"Bearer"`. |
| `expiresAt` | `string (ISO 8601)` | Thời điểm hết hạn của `accessToken` (UTC). |
| `user` | `UserInfoDto` | Thông tin chi tiết của người dùng vừa tạo (xem bảng `UserInfoDto` bên dưới). |
| `message` | `string` | Thông điệp kết quả đăng ký. |

##### Cấu trúc đối tượng `UserInfoDto`:
| Trường | Kiểu dữ liệu | Mô tả |
|:---|:---:|:---|
| `id` | `Guid` | Mã định danh duy nhất của tài khoản (`users.id`). |
| `username` | `string` | Tên đăng nhập. |
| `email` | `string` | Địa chỉ email. |
| `fullName` | `string` | Họ và tên đầy đủ. |
| `phoneNumber` | `string?` | Số điện thoại liên hệ. |
| `avatarUrl` | `string?` | Đường dẫn ảnh đại diện. |
| `role` | `string` | Vai trò hiện tại: `"Citizen"`, `"Recycler"`, `"Staff"`, hoặc `"Admin"`. |
| `status` | `string` | Trạng thái tài khoản: `"Active"`, `"PendingVerification"`, `"Suspended"`, `"Deactivated"`. |
| `isGoogleAuth` | `boolean` | `true` nếu tài khoản liên kết Google OAuth, ngược lại `false`. |
| `currentGreenPoints` | `integer` | Số dư Điểm Xanh hiện có (`0` khi mới tạo). |
| `treeLevel` | `integer` | Cấp độ Cây ảo hiện tại (`1` khi mới tạo). |
| `totalRecycledKg` | `decimal` | Tổng khối lượng rác tái chế đã bàn giao thành công (`0.0`). |

##### Ví dụ Response Thành công (`201 Created`):
```json
{
  "success": true,
  "messageCode": "MSG-AUTH-08",
  "message": "Tạo tài khoản thành công! Vui lòng kiểm tra email để xác minh tài khoản.",
  "data": {
    "success": true,
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "8f9d2a1b4c6e7f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e...",
    "tokenType": "Bearer",
    "expiresAt": "2026-10-30T08:45:00Z",
    "user": {
      "id": "b15a2e81-91f2-4a8e-8c11-2a3b4c5d6e7f",
      "username": "nguyenvana",
      "email": "nguyenvana@gmail.com",
      "fullName": "Nguyễn Văn A",
      "phoneNumber": "0901234567",
      "avatarUrl": null,
      "role": "Citizen",
      "status": "Active",
      "isGoogleAuth": false,
      "currentGreenPoints": 0,
      "treeLevel": 1,
      "totalRecycledKg": 0
    },
    "message": "Tạo tài khoản thành công! Vui lòng kiểm tra email để xác minh tài khoản.",
    "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
  },
  "errors": null,
  "timestamp": "2026-10-30T08:15:00Z"
}
```

#### c. Các Thông báo Trả về (Response Messages & Error Codes)

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`201 Created`** | `MSG-AUTH-08` | — | Đăng ký thành công | `"Tạo tài khoản thành công! Vui lòng kiểm tra email để xác minh tài khoản."` |
| **`400 Bad Request`** | `MSG-AUTH-01` | `BR-AUTH-01` | Trùng `username` hoặc `email` đã tồn tại trong CSDL | `"Tên đăng nhập hoặc địa chỉ Email này đã được sử dụng trên hệ thống."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `MSG-AUTH-02` | Bỏ trống trường bắt buộc, mật khẩu `< 6` ký tự, hoặc email sai định dạng | `"Dữ liệu đầu vào không hợp lệ."` *(kèm chi tiết từng trường trong mảng `errors`)* |

---

### 2.2. Đăng nhập hệ thống (`POST /api/v1/auth/login`)

* **Mô tả:** Xác thực thông tin đăng nhập bằng `username` hoặc `email` kết hợp mật khẩu. Nếu thành công, trả về `AuthResponseDto` và gọi `SetAuthCookies(result)` đính kèm cookie `refreshToken` (`HttpOnly`, `SameSite=Lax`, `7 ngày`); nếu là luồng Web/SPA (`Origin`, `X-EcoLink-Auth-Mode`, hoặc có sẵn cookie) và không có header `X-EcoLink-No-Cookie: 1`/`true`, server đính kèm thêm cookie `accessToken` (`HttpOnly`, `SameSite=Lax`) và `ecolink_session_active=1`.
* **Quy tắc nghiệp vụ áp dụng:**
  * `BR-AUTH-03`: Thông báo lỗi chung, không tiết lộ tài khoản có tồn tại hay mật khẩu bị sai.
  * `BR-AUTH-04`: Nhập sai mật khẩu liên tiếp **5 lần** sẽ bị khóa tạm thời **15 phút** (`LockoutEnd`).
  * `BR-AUTH-05`: Tài khoản ở trạng thái `Suspended` hoặc `Deactivated` bị chặn đăng nhập ngay cả khi nhập đúng mật khẩu.

#### a. Request Parameters (`application/json` – `LoginRequestDto`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `usernameOrEmail` | Body | `string` | **Có** | Không rỗng | Tên đăng nhập hoặc địa chỉ Email của tài khoản. |
| `password` | Body | `string` | **Có** | Không rỗng | Mật khẩu đăng nhập. |

##### Ví dụ Request Body:
```json
{
  "usernameOrEmail": "citizen",
  "password": "citizen123"
}
```

#### b. Response Parameters
* **Header `Set-Cookie` (mặc định trừ khi gửi `X-EcoLink-No-Cookie: 1`/`true`):**
  * `refreshToken=<token>; expires=<+7d>; path=/; samesite=lax; httponly` *(thêm `secure` khi HTTPS)*
  * Khi request từ Web/SPA (`Origin`, `X-EcoLink-Auth-Mode` hoặc có cookie phiên): bổ sung `accessToken=<jwt>; expires=<expiresAt/+30m>; path=/; samesite=lax; httponly` và `ecolink_session_active=1; expires=<+7d>; path=/; samesite=lax`
* **Body JSON:** `ApiResponse<AuthResponseDto>` (cấu trúc giống mục 2.1).

#### c. Các Thông báo Trả về (Response Messages & Error Codes)

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `MSG-GEN-01` | — | Đăng nhập hợp lệ | `"Đăng nhập thành công."` |
| **`400 Bad Request`** | `MSG-AUTH-01` | `BR-AUTH-03` | Không tìm thấy tài khoản, mật khẩu sai (lần 1–4), hoặc tài khoản chỉ dùng Google OAuth | `"Sai tên đăng nhập hoặc mật khẩu. Vui lòng kiểm tra lại."` |
| **`400 Bad Request`** | `MSG-AUTH-03` | `BR-AUTH-04` | Nhập sai lần thứ 5 liên tiếp hoặc đang trong thời gian bị khóa tạm thời | `"Bạn đã nhập sai quá nhiều lần. Vui lòng thử lại sau 15 phút."` *(hoặc số phút còn lại)* |
| **`400 Bad Request`** | `MSG-AUTH-04` | `BR-AUTH-05` | Tài khoản có `Status = Suspended` hoặc `Deactivated` | `"Tài khoản này hiện không còn hoạt động. Vui lòng liên hệ bộ phận hỗ trợ."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `MSG-AUTH-02` | Để trống `usernameOrEmail` hoặc `password` | `"Dữ liệu đầu vào không hợp lệ."` |

---

### 2.3. Đăng nhập / Đăng ký qua Google OAuth 2.0 (`POST /api/v1/auth/google`)

* **Mô tả:** Xác minh chữ ký số của `idToken` trực tiếp với máy chủ Google (`GoogleJsonWebSignature.ValidateAsync`) theo `ClientId` đã cấu hình.
  * Nếu email chưa tồn tại: Tự động khởi tạo tài khoản mới với vai trò `Citizen` (`BR-AUTH-07`). Nếu `email_verified = true`, kích hoạt `Status = Active` ngay (`BR-AUTH-02`); nếu `false`, đặt `Status = PendingVerification`.
  * Nếu email đã tồn tại: Tự động liên kết `GoogleSubjectId` vào tài khoản hiện có (`BR-AUTH-01`).

#### a. Request Parameters (`application/json` – `GoogleAuthRequestDto`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `idToken` | Body | `string` | **Có** | Chuỗi JWT từ Google Identity Services | Google ID Token nhận được sau khi người dùng chọn tài khoản Google ở Frontend. |

##### Ví dụ Request Body:
```json
{
  "idToken": "eyJhbGciOiJSUzI1NiIsImtpZCI6IjFlOWdkazcifQ..."
}
```

#### b. Response Parameters
* **Header `Set-Cookie`:** Tương tự mục `2.2` qua `SetAuthCookies(result)` (`refreshToken` `HttpOnly; SameSite=Lax`, cộng thêm `accessToken` `HttpOnly` và `ecolink_session_active=1` cho luồng Web/SPA trừ khi gửi `X-EcoLink-No-Cookie: 1`/`true`).
* **Body JSON:** `ApiResponse<AuthResponseDto>` với `user.isGoogleAuth = true`.

#### c. Các Thông báo Trả về (Response Messages & Error Codes)

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `MSG-AUTH-09` | `BR-AUTH-02`, `BR-AUTH-07` | Xác thực Google thành công | `"Xác thực qua Google thành công."` *(hoặc `"Email của bạn đã được xác minh."`)* |
| **`400 Bad Request`** | `MSG-AUTH-01` | `BR-AUTH-07` | `idToken` rỗng, giả mạo, sai `ClientId`, hoặc đã hết hạn | `"Google ID Token không hợp lệ hoặc đã hết hạn."` |
| **`400 Bad Request`** | `MSG-AUTH-04` | `BR-AUTH-05` | Tài khoản gắn với email Google đó đã bị khóa/đình chỉ | `"Tài khoản này hiện không còn hoạt động. Vui lòng liên hệ bộ phận hỗ trợ."` |

---

### 2.4. Làm mới Access Token (`POST /api/v1/auth/refresh-token`)

* **Mô tả:** Thu hồi Refresh Token hiện tại và xoay vòng cấp cặp `AccessToken` + `RefreshToken` mới (Token Rotation).
* **Cơ chế chống tấn công Tái sử dụng Token (Replay Attack Detection – `BR-AUTH-05`):** Nếu phát hiện một `refreshToken` đã bị thu hồi (`IsRevoked = true`) nhưng vẫn được gửi lên lại, hệ thống lập tức thu hồi **toàn bộ** các Refresh Token đang còn hiệu lực của tài khoản đó và buộc đăng nhập lại.

#### a. Request Parameters (`RefreshTokenRequestDto` hoặc `Cookie`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|
| `refreshToken` | Body JSON | `string?` | Tùy chọn* | Chuỗi Refresh Token (dành cho Mobile App hoặc Postman). |
| `refreshToken` | Cookie | `string?` | Tùy chọn* | Cookie `HttpOnly` tự động trình duyệt gửi kèm (nếu Body không truyền). |

*(Lưu ý: Bắt buộc phải có `refreshToken` ở ít nhất 1 trong 2 vị trí: Body hoặc Cookie).*

##### Ví dụ Request Body:
```json
{
  "refreshToken": "8f9d2a1b4c6e7f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e..."
}
```

#### b. Response Parameters
* **Header `Set-Cookie`:** Xoay vòng cập nhật `refreshToken` (và `accessToken` + `ecolink_session_active=1` nếu là luồng Web/SPA qua `SetAuthCookies`).
* **Body JSON:** `ApiResponse<AuthResponseDto>` chứa cặp `accessToken` và `refreshToken` mới.

#### c. Các Thông báo Trả về (Response Messages & Error Codes)

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `MSG-GEN-01` | — | Xoay vòng và cấp mới Token thành công | `"Làm mới phiên đăng nhập thành công."` |
| **`401 Unauthorized`** | `MSG-NTF-01` | — | Không truyền `refreshToken` trong cả Body lẫn Cookie | `"Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại."` |
| **`400 Bad Request`** | `MSG-NTF-01` | — | `refreshToken` không tồn tại trong CSDL hoặc đã quá hạn 7 ngày | `"Phiên làm việc của bạn đã hết hạn. Vui lòng đăng nhập lại."` |
| **`400 Bad Request`** | `MSG-AUTH-10` | `BR-AUTH-05` | Phát hiện `refreshToken` đã bị sử dụng trước đó (Token Replay Attack) | `"Phát hiện bất thường bảo mật: Phiên đăng nhập đã được sử dụng trước đó. Toàn bộ phiên đã bị đăng xuất."` |
| **`400 Bad Request`** | `MSG-AUTH-04` | `BR-AUTH-05` | Tài khoản chủ sở hữu bị `Suspended` / `Deactivated` | `"Tài khoản này hiện không còn hoạt động."` |

---

### 2.5. Yêu cầu đặt lại mật khẩu (`POST /api/v1/auth/forgot-password`)

* **Mô tả:** Sinh mã Token đặt lại mật khẩu dùng 1 lần (hết hạn sau **15 phút**), lưu bản băm `SHA-256` vào bảng `password_reset_tokens` và gửi liên kết khôi phục qua Gmail SMTP (`ecolinksupport@gmail.com`).
* **Quy tắc bảo mật (`BR-AUTH-06` & `BR-AUTH-09`):**
  * Luôn trả về cùng một phản hồi `200 OK` (`MSG-AUTH-05`) dù email có tồn tại trong hệ thống hay không để chống dò quét danh sách email (Account Enumeration Prevention).
  * Giới hạn tần suất: Nếu cùng một email yêu cầu lại trong vòng **60 giây**, hệ thống âm thầm bỏ qua việc gửi lại email nhưng vẫn trả về `200 OK` đồng nhất.

#### a. Request Parameters (`application/json` – `ForgotPasswordRequestDto`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `email` | Body | `string` | **Có** | Định dạng Email hợp lệ | Địa chỉ email cần nhận liên kết đặt lại mật khẩu. |

##### Ví dụ Request Body:
```json
{
  "email": "nguyenvana@gmail.com"
}
```

#### b. Response Parameters (`ApiResponse<object>`)
```json
{
  "success": true,
  "messageCode": "MSG-AUTH-05",
  "message": "Nếu email này tồn tại trong hệ thống, liên kết đặt lại mật khẩu đã được gửi.",
  "data": null,
  "errors": null,
  "timestamp": "2026-10-30T08:25:00Z"
}
```

#### c. Các Thông báo Trả về (Response Messages & Error Codes)

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `MSG-AUTH-05` | `BR-AUTH-06`, `BR-AUTH-09` | Email hợp lệ về mặt cú pháp (dù có tồn tại trong DB hay không) | `"Nếu email này tồn tại trong hệ thống, liên kết đặt lại mật khẩu đã được gửi."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `MSG-AUTH-02` | Trường `email` để trống hoặc sai định dạng email | `"Dữ liệu đầu vào không hợp lệ."` |

---

### 2.6. Đặt lại mật khẩu mới bằng Token (`POST /api/v1/auth/reset-password`)

* **Mô tả:** Xác thực mã `token` đặt lại mật khẩu (so khớp băm `SHA-256`, kiểm tra `IsUsed == false` và chưa quá hạn 15 phút), cập nhật mật khẩu mới bằng `BCrypt`, mở khóa tài khoản nếu đang bị khóa tạm thời, và **thu hồi toàn bộ các phiên Refresh Token hiện có** của tài khoản (`BR-AUTH-06`).

#### a. Request Parameters (`application/json` – `ResetPasswordRequestDto`)

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Ràng buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|:---|
| `email` | Body | `string` | **Có** | Định dạng Email | Email của tài khoản cần đặt lại mật khẩu. |
| `token` | Body | `string` | **Có** | Không rỗng | Mã Token bảo mật nhận được từ liên kết trong email. |
| `newPassword` | Body | `string` | **Có** | `6 - 100` ký tự | Mật khẩu mới. |
| `confirmPassword` | Body | `string?` | Không | Phải khớp với `newPassword` (nếu truyền) | Mật khẩu xác nhận lại. |

##### Ví dụ Request Body:
```json
{
  "email": "nguyenvana@gmail.com",
  "token": "4a8b9c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b",
  "newPassword": "NewSecurePassword@2026",
  "confirmPassword": "NewSecurePassword@2026"
}
```

#### b. Response Parameters (`ApiResponse<object>`)
```json
{
  "success": true,
  "messageCode": "MSG-GEN-01",
  "message": "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.",
  "data": null,
  "errors": null,
  "timestamp": "2026-10-30T08:30:00Z"
}
```

#### c. Các Thông báo Trả về (Response Messages & Error Codes)

| HTTP Status | Mã `messageCode` | Quy tắc (`BR`) | Điều kiện kích hoạt | Nội dung `message` trả về |
|:---:|:---:|:---:|:---|:---|
| **`200 OK`** | `MSG-GEN-01` | `BR-AUTH-06` | Đặt lại mật khẩu và hủy toàn bộ phiên cũ thành công | `"Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới."` |
| **`400 Bad Request`** | `MSG-AUTH-06` | `BR-AUTH-06` | Email không tồn tại, `token` sai, `token` đã được sử dụng (`IsUsed = true`), hoặc đã quá 15 phút | `"Liên kết này không hợp lệ hoặc đã hết hạn. Vui lòng yêu cầu liên kết mới."` |
| **`400 Bad Request`** | `MSG-AUTH-01` | — | `confirmPassword` không khớp với `newPassword` | `"Mật khẩu xác nhận không khớp với mật khẩu mới."` |
| **`400 Bad Request`** | `MSG-GEN-01` | `MSG-AUTH-02` | Thiếu `email`, `token` hoặc `newPassword` ngắn hơn 6 ký tự | `"Dữ liệu đầu vào không hợp lệ."` |

---

### 2.7. Lấy thông tin người dùng hiện tại (`GET /api/v1/auth/me`)

* **Mô tả:** Đọc JWT từ Header `Authorization`, Query `?token=` hoặc HttpOnly Cookie `accessToken`, truy vấn thông tin mới nhất của người dùng từ CSDL kèm theo điểm sinh thái (`CurrentGreenPoints`, `TreeLevel`, `TotalRecycledKg`) và trạng thái gói dịch vụ (`CurrentTier`, `IsAdFree`, `HasVerifiedBadge`, `IsFeaturedMapMarker`).

#### a. Request Parameters

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|
| `Authorization` | Header | `string?` | Tùy chọn* | Định dạng: `Bearer <accessToken>`. |
| `accessToken` | Cookie | `string?` | Tùy chọn* | HttpOnly Cookie tự động đính kèm trên trình duyệt Web/SPA (`credentials: include`). |
| `token` | Query | `string?` | Không* | Hỗ trợ truyền trực tiếp `?token=<accessToken>` khi kiểm thử nhanh. |

*(Lưu ý: Phải cung cấp Token ở ít nhất 1 trong 3 vị trí: Header `Authorization`, HttpOnly Cookie `accessToken` hoặc Query `token`.)*

#### b. Response Parameters (`UserInfoDto`)

##### Ví dụ Response Thành công (`200 OK`):
```json
{
  "id": "10000000-0000-0000-0000-000000000001",
  "username": "citizen",
  "email": "citizen@ecolink.vn",
  "fullName": "Nguyễn Văn Xanh (Người dân)",
  "phoneNumber": "0901112222",
  "avatarUrl": null,
  "role": "Citizen",
  "status": "Active",
  "isGoogleAuth": false,
  "currentGreenPoints": 150,
  "treeLevel": 2,
  "totalRecycledKg": 12.5,
  "currentTier": "Basic",
  "isAdFree": false,
  "hasVerifiedBadge": false,
  "isFeaturedMapMarker": false
}
```

#### c. Các Thông báo Trả về

| HTTP Status | Điều kiện kích hoạt | Nội dung phản hồi |
|:---:|:---|:---|
| **`200 OK`** | Token hợp lệ và tài khoản đang hoạt động (`Active`) | Đối tượng JSON `UserInfoDto`. |
| **`401 Unauthorized`** | Thiếu đồng thời Header `Authorization`, Cookie `accessToken` và query `token` | `{ "message": "Thiếu Authorization Header, HttpOnly Cookie hoặc query param 'token'." }` |
| **`401 Unauthorized`** | Token sai chữ ký, đã hết hạn hoặc tài khoản bị vô hiệu hóa | `{ "message": "Token không hợp lệ hoặc đã hết hạn." }` |

---

### 2.8. Cập nhật hồ sơ cá nhân (`PUT /api/v1/auth/profile`)

* **Inventory ID:** `009` | **Yêu cầu quyền:** `[Authorize]` (`Bearer` hoặc HttpOnly Cookie `accessToken`, `SU-01`).
* **Request Body (`UpdateUserProfileRequestDto`):**
  * `fullName` (`string`, bắt buộc, `2..100` ký tự)
  * `phoneNumber` (`string?`, chuẩn số điện thoại)
  * `avatarUrl` (`string?`, tối đa `500` ký tự)
* **Response (`200 OK`):** `ApiResponse<UserInfoDto>` (`messageCode: "MSG-GEN-01"`, `message: "Đã lưu hồ sơ cá nhân thành công."`). Trả `401 Unauthorized` (`MSG-NTF-01`) nếu phiên không hợp lệ.

---

### 2.9. Đổi mật khẩu khi đang đăng nhập (`POST /api/v1/auth/change-password`)

* **Inventory ID:** `010` | **Yêu cầu quyền:** `[Authorize]` (`SU-02`).
* **Request Body (`ChangePasswordRequestDto`):**
  * `currentPassword` (`string`, bắt buộc)
  * `newPassword` (`string`, bắt buộc, `6..100` ký tự)
  * `confirmPassword` (`string?`)
* **Mô tả:** Xác thực `currentPassword`, cập nhật mật khẩu mới và tự động thu hồi toàn bộ Refresh Token của các phiên đăng nhập trên thiết bị khác (giữ lại phiên tương ứng với cookie `refreshToken` hiện tại nếu có).
* **Response (`200 OK`):** `ApiResponse<object?>` (`messageCode: "MSG-GEN-01"`, `message: "Đã đổi mật khẩu thành công. Các phiên đăng nhập trên thiết bị khác đã được đăng xuất."`).

---

### 2.10. Đăng xuất & Xóa HttpOnly Cookies (`POST /api/v1/auth/logout`)

* **Inventory ID:** `011` | **Yêu cầu quyền:** Public / Cookie.
* **Request Body (`LogoutRequestDto?`, tùy chọn):** `{ "refreshToken": "..." }`. Nếu Body bỏ trống, hệ thống đọc từ cookie `refreshToken`.
* **Mô tả:** Thu hồi Refresh Token hiện tại và gọi `ClearAuthCookies()` xóa sạch cả 3 cookie (`accessToken`, `refreshToken`, `ecolink_session_active`) với `Path=/` và `SameSite=Lax`.
* **Response (`200 OK`):** `ApiResponse<object?>` (`messageCode: "MSG-GEN-01"`, `message: "Đã đăng xuất thành công."`).

---

### 2.11. Các Endpoint Phụ trợ & Tương thích (`STT 002`, `004`, `013`, `014`)

* **`GET /api/v1/auth/google-config` (`STT 004`, Public):** Trả về `ApiResponse<object>` chứa `{ "enabled": true|false, "clientId": "..." }` từ `GoogleAuth:ClientId` (bỏ qua các giá trị rỗng hoặc placeholder `mock`).
* **`POST /api/v1/auth/register-recycler` (`STT 002`, Public Legacy):** Tiếp nhận `RegisterRecyclerRequestDto` (`username`, `email`, `password`, `fullName`, `phoneNumber`, `facilityName`, `representativeName`, `businessAddress`, `latitude`, `longitude`, `operatingHours`, `contactPhone`, `description`, `recyclerType` [`"Facility"` | `"Individual"`], `legalPermitUrl`, `businessPhotoUrl`, `businessPhotoUrls`, `identityCardFrontUrl`, `identityCardBackUrl`), đồng thời gọi `SetAuthCookies(result)`. Luồng nâng cấp chuẩn của hệ thống vẫn đi qua Module 2 (`POST /api/v1/recycler-applications`).
* **`GET /api/v1/auth/demo-users` (`STT 013`, Public Dev/Test):** Trả về danh sách tài khoản mẫu (`citizen / citizen123`, `recycler / recycler123`, `staff / staff123`, `admin / admin123`).
* **`POST /api/v1/auth/vietqr-token` (`STT 014`, Basic Auth):** Kiểm tra Header `Authorization: Basic ...`; nếu thiếu trả `401`, nếu hợp lệ trả `{ "access_token", "token_type": "Bearer", "expires_in": 86400, "message" }`.


