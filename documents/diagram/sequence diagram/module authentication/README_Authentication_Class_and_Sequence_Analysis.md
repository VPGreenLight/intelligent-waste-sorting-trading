# PHÂN TÍCH CLASS DIAGRAM & SEQUENCE DIAGRAM – MODULE AUTHENTICATION VÀ MODULE RECYCLER ONBOARDING & VERIFICATION

Tài liệu này tổng hợp kiến trúc **Class Diagram** (1 bản Ultra-Compact tổng hợp cho toàn bộ mỗi Module) và **Sequence Diagram** (tách nhỏ theo từng chức năng/Use Case) của 2 phân hệ:
1. **Module Authentication**: Quản lý định danh, xác thực và phiên đăng nhập. Khi khách vãng lai (**Guest**) đăng ký mới, hệ thống chỉ khởi tạo tài khoản `User` (`Role = Citizen`) kèm `UserEcoProfile`.
2. **Module Recycler Onboarding & Verification (`U-17` & `S-04`)**: Xử lý luồng người dùng đã xác thực (`Role = Citizen`) nộp hồ sơ nâng cấp kèm chứng từ lên **Cloudflare R2** (`U-17`) và được **Staff/Admin** thẩm định phê duyệt nâng cấp lên `Role = Recycler` (`S-04`).

---

## PHẦN 1: MODULE AUTHENTICATION

### 1.1. Sơ đồ Lớp Tổng hợp Rút gọn (Ultra-Compact Class Diagram)
- **File:** [module1_auth_clean_architecture_class_diagram_ultra_compact.mmd](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/class%20diagram/module%20authentication/module1_auth_clean_architecture_class_diagram_ultra_compact.mmd)
- **Cấu trúc 4 tầng Clean Architecture:**
  - **Presentation Layer**: `AuthController` (6 hành động cốt lõi: `Register`, `Login`, `GoogleLogin`, `RefreshToken`, `ForgotPassword`, `ResetPassword`).
  - **Application Layer (Ports)**: `IAuthService` (Inbound Port), `ITokenService` & `IEmailService` (Outbound Ports).
  - **Infrastructure Layer (Adapters)**: `AuthService`, `JwtTokenService`, `GmailSmtpEmailService`, `ApplicationDbContext`.
  - **Domain Layer (Entities)**: `User` (`Role = Citizen`), `UserEcoProfile` (quan hệ `1--1`), `RefreshToken` (`1--0..*`), `PasswordResetToken` (`1--0..*`).

### 1.2. Danh sách Sequence Diagrams theo Chức năng (5 sơ đồ tách nhỏ)

| STT | Tên file sơ đồ (`.mmd`) | Chức năng / Use Case | Quy tắc nghiệp vụ (Business Rules) được mô tả |
| :-: | :--- | :--- | :--- |
| **1** | [01_citizen_and_recycler_registration.mmd](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20authentication/01_citizen_and_recycler_registration.mmd) | **Đăng ký Tài khoản Người dân (Guest $\rightarrow$ Citizen)** | - `BR-AUTH-01`: Kiểm tra tính duy nhất của `Email` và `Username`.<br>- `BR-AUTH-02`: Khởi tạo `User` (`Role = Citizen`, `Status = Active`) và tự động tạo `UserEcoProfile` (`TreeLevel = 1`, `CurrentGreenPoints = 0`). |
| **2** | [02_login_and_bruteforce_lockout.mmd](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20authentication/02_login_and_bruteforce_lockout.mmd) | **Đăng nhập Hệ thống & Khóa tạm thời chống Brute-force** | - `BR-AUTH-03`: Xác thực mật khẩu `BCrypt.Verify()`, cấp JWT Access Token (30 phút) & Refresh Token (7 ngày).<br>- `BR-AUTH-04`: Tự động khóa tài khoản **15 phút** (`Status = Locked`) khi nhập sai **5 lần liên tiếp**. |
| **3** | [03_google_oauth2_authentication.mmd](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20authentication/03_google_oauth2_authentication.mmd) | **Xác thực & Tự động Khởi tạo Tài khoản qua Google OAuth 2.0** | - `BR-AUTH-07`: Kiểm tra chữ ký số RSA của Google ID Token.<br>- `BR-AUTH-02`: Tự động tạo mới `User` (`Role = Citizen`) + `UserEcoProfile` nếu đăng nhập lần đầu. |
| **4** | [04_refresh_token_rotation_and_replay_detection.mmd](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20authentication/04_refresh_token_rotation_and_replay_detection.mmd) | **Làm mới Access Token (Token Rotation) & Chống Replay Attack** | - `BR-AUTH-05`: Xoay vòng Refresh Token (`ReplacedByTokenHash`) và thu hồi toàn bộ phiên của User nếu phát hiện tái sử dụng token cũ (`IsRevoked == true`). |
| **5** | [05_forgot_and_reset_password_smtp.mmd](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20authentication/05_forgot_and_reset_password_smtp.mmd) | **Quên mật khẩu (Rate-limit) & Đặt lại mật khẩu qua Gmail SMTP** | - `BR-AUTH-06` & `BR-AUTH-09`: Chống dò quét Email, giới hạn 1 mail/60s, OTP hiệu lực 15 phút dùng 1 lần, thu hồi toàn bộ `RefreshToken` sau khi đổi mật khẩu. |

---

## PHẦN 2: MODULE RECYCLER ONBOARDING & VERIFICATION

### 2.1. Sơ đồ Lớp Tổng hợp Rút gọn (Ultra-Compact Class Diagram)
- **File:** [module_recycler_onboarding_clean_architecture_class_diagram_ultra_compact.mmd](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/class%20diagram/module%20recycler%20onboarding%20&%20verification/module_recycler_onboarding_clean_architecture_class_diagram_ultra_compact.mmd)
- **Cấu trúc 4 tầng Clean Architecture:**
  - **Presentation Layer**: `RecyclerApplicationsController` (Upload chứng từ R2, Gửi hồ sơ nâng cấp `U-17`, Xem trạng thái cá nhân, Lọc/Xem chi tiết & Thẩm định hồ sơ `S-04`).
  - **Application Layer (Ports)**: `IRecyclerOnboardingService` (Inbound Port), `IFileStorageService` & `IEmailService` (Outbound Ports).
  - **Infrastructure Layer (Adapters)**: `RecyclerOnboardingService`, `CloudflareR2StorageService` (AWS SigV4), `GmailSmtpEmailService`, `ApplicationDbContext`.
  - **Domain Layer (Entities)**: `User` (`Citizen` $\rightarrow$ `Recycler`) và `RecyclerProfile` (PostGIS `Point` SRID 4326, `LegalPermitUrl`, `BusinessPhotoUrl`, `VerificationStatus`, `RejectionReason`).

### 2.2. Danh sách Sequence Diagrams theo Chức năng (2 sơ đồ tách nhỏ)

| STT | Tên file sơ đồ (`.mmd`) | Chức năng / Use Case | Quy tắc nghiệp vụ (Business Rules) được mô tả |
| :-: | :--- | :--- | :--- |
| **1** | [01_u17_citizen_apply_for_recycler_upgrade.mmd](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20recycler%20onboarding%20&%20verification/01_u17_citizen_apply_for_recycler_upgrade.mmd) | **`U-17`: Người dân (`Citizen`) tải chứng từ lên Cloudflare R2 & Nộp đơn nâng cấp** | - Kiểm tra tệp đính kèm ($\le 5\text{MB}$, `.jpg/.png/.webp/.pdf`) và ký số AWS SigV4 đẩy lên **Cloudflare R2**.<br>- `BR-MTH-01`: Lưu tọa độ PostGIS `Point(Lon, Lat)` SRID 4326, đặt `VerificationStatus = Pending`, `ActiveStatus = Offline` (`MSG-MTH-03`). Hỗ trợ nộp lại (`Re-apply`) khi từng bị từ chối. |
| **2** | [02_s04_staff_verify_recycler_application.mmd](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20recycler%20onboarding%20&%20verification/02_s04_staff_verify_recycler_application.mmd) | **`S-04`: Nhân viên (`Staff`/`Admin`) thẩm định Phê duyệt hoặc Từ chối hồ sơ** | - Khi **`Approved`**: Cập nhật `VerificationStatus = Verified`, **nâng cấp `User.Role` từ `Citizen` lên `Recycler`** và gửi email thông báo (`MSG-MTH-04`).<br>- Khi **`Rejected` (`BR-MTH-02`)**: Bắt buộc nhập `RejectionReason`, giữ nguyên `Role = Citizen`, đặt `VerificationStatus = Rejected` và gửi email nêu rõ lý do (`MSG-MTH-05`). |
