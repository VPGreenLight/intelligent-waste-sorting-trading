# TÀI LIỆU ĐẶC TẢ API – MODULE 1: TÀI KHOẢN & XÁC THỰC (AUTHENTICATION & ACCOUNT MANAGEMENT)
**Mã phân hệ:** `AUTH`  
**Controller:** [`AuthController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/AuthController.cs)  
**Service thực thi:** [`AuthService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AuthService.cs), [`JwtTokenService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/JwtTokenService.cs), [`GmailSmtpEmailService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GmailSmtpEmailService.cs)  
**DTOs:** [`AuthDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/AuthDtos.cs)  
**Base Route:** `/api/v1/auth`

---

## 1. TỔNG QUAN DANH SÁCH ENDPOINTS

| STT | Phương thức | Endpoint URI | Quyền truy cập | Chức năng nghiệp vụ | Mã thông báo chính |
|:---:|:---:|:---|:---:|:---|:---|
| 1 | `POST` | `/api/v1/auth/register` | Public (`Guest`) | Đăng ký tài khoản Người dân (`Citizen`) kèm khởi tạo Hồ sơ Sinh thái (`UserEcoProfile`). | `MSG-AUTH-08`, `MSG-AUTH-01`, `MSG-GEN-01` |
| 2 | `POST` | `/api/v1/auth/login` | Public (`Guest`) | Đăng nhập bằng Username/Email & Mật khẩu (tích hợp khóa tạm thời 15 phút sau 5 lần sai). | `MSG-GEN-01`, `MSG-AUTH-01`, `MSG-AUTH-03`, `MSG-AUTH-04` |
| 3 | `POST` | `/api/v1/auth/google` | Public (`Guest`) | Đăng nhập / Đăng ký tự động bằng Google OAuth 2.0 `idToken`. | `MSG-AUTH-09`, `MSG-AUTH-01`, `MSG-AUTH-04` |
| 4 | `POST` | `/api/v1/auth/refresh-token` | Public (Cookie/Body) | Làm mới cặp JWT Access Token & Refresh Token (Token Rotation & phát hiện Replay Attack). | `MSG-GEN-01`, `MSG-NTF-01`, `MSG-AUTH-10`, `MSG-AUTH-04` |
| 5 | `POST` | `/api/v1/auth/forgot-password` | Public (`Guest`) | Yêu cầu gửi email đặt lại mật khẩu qua Gmail SMTP (chống dò quét email & giới hạn tần suất 60s). | `MSG-AUTH-05`, `MSG-GEN-01` |
| 6 | `POST` | `/api/v1/auth/reset-password` | Public (`Guest`) | Đặt lại mật khẩu mới bằng Token bảo mật dùng 1 lần và thu hồi toàn bộ phiên đăng nhập cũ. | `MSG-GEN-01`, `MSG-AUTH-06`, `MSG-AUTH-01` |
| 7 | `GET` | `/api/v1/auth/me` | Authenticated (`Bearer`) | Lấy thông tin chi tiết tài khoản hiện tại và chỉ số Điểm Xanh (`GreenPoints`, `TreeLevel`). | `200 OK` / `401 Unauthorized` |
| 8 | `GET` | `/api/v1/auth/demo-users` | Public (Dev/Test) | Lấy danh sách tài khoản mẫu (`citizen`, `collector`, `admin`) để kiểm thử nhanh. | `200 OK` |

> [!NOTE]
> **Lưu ý về phân tách nghiệp vụ Đăng ký (`Guest` $\rightarrow$ `Citizen` vs. `Citizen` $\rightarrow$ `Recycler`):**
> * Endpoint `POST /api/v1/auth/register` và `POST /api/v1/auth/google` chỉ khởi tạo tài khoản với vai trò **`Citizen`** (`User`).
> * Để đăng ký trở thành Cơ sở thu gom (**`Recycler`**), người dùng cần đăng nhập tài khoản `Citizen` và thực hiện quy trình nộp hồ sơ nâng cấp tại [**Module 2: Recycler Onboarding & Verification (`02_module_recycler_onboarding_api.md`)**](./02_module_recycler_onboarding_api.md).
> * Endpoint `POST /api/v1/auth/register-recycler` trong `AuthController` chỉ được giữ lại để tương thích ngược (Legacy Alias) cho dữ liệu seed nội bộ.

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

* **Mô tả:** Xác thực thông tin đăng nhập bằng `username` hoặc `email` kết hợp mật khẩu. Nếu thành công, trả về `AuthResponseDto` và tự động đính kèm cookie `refreshToken` (`HttpOnly`, `Strict`, `7 ngày`).
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
* **Header `Set-Cookie`:** `refreshToken=<token>; expires=...; path=/; secure; samesite=strict; httponly`
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
* **Header `Set-Cookie`:** `refreshToken=<token>; httponly; samesite=strict`
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

* **Mô tả:** Giải mã JWT Bearer Token, truy vấn thông tin mới nhất của người dùng từ CSDL kèm theo điểm sinh thái (`CurrentGreenPoints`, `TreeLevel`, `TotalRecycledKg`).

#### a. Request Parameters

| Tên tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mô tả |
|:---|:---:|:---:|:---:|:---|
| `Authorization` | Header | `string` | **Có*** | Định dạng: `Bearer <accessToken>`. |
| `token` | Query | `string?` | Không* | Hỗ trợ truyền trực tiếp `?token=<accessToken>` khi test nhanh trên trình duyệt. |

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
  "totalRecycledKg": 12.5
}
```

#### c. Các Thông báo Trả về

| HTTP Status | Điều kiện kích hoạt | Nội dung phản hồi |
|:---:|:---|:---|
| **`200 OK`** | Token hợp lệ và tài khoản đang hoạt động (`Active`) | Đối tượng JSON `UserInfoDto`. |
| **`401 Unauthorized`** | Không đính kèm Header `Authorization` và không có query `token` | `{ "message": "Thiếu Authorization Header hoặc query param 'token'. Vui lòng đính kèm Bearer token." }` |
| **`401 Unauthorized`** | Token sai chữ ký, đã hết hạn hoặc tài khoản bị vô hiệu hóa | `{ "message": "Token không hợp lệ hoặc đã hết hạn." }` |

