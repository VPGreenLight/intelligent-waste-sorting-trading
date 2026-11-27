# REPORT 5.3 – SYSTEM TEST: MODULE 01: AUTHENTICATION, SESSION SECURITY & ACCOUNT LIFECYCLE (M-01)

## 1. Workflow Summary Header (Theo mẫu Sheet `M01_Auth` – `SEP490_G141`)

| Workflow | `Module 01: Authentication, Session Security & Account Lifecycle (M-01)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử hệ thống End-to-End cho luồng Đăng ký Công dân (/register), Đăng nhập Dual Auth HttpOnly Cookie + Bearer JWT & Quick-Login 7 vai trò (/login), Khóa Brute-Force 5 lần sai (BR-AUTH-04), Google OAuth 2.0, Quên/Đặt lại mật khẩu OTP (/forgot-password), Cập nhật hồ sơ & Đổi mật khẩu (/user/profile) giữa ecolink-web, Ecolink.WebApi và PostgreSQL. | | | |
| **Number of TCs** | **10** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 9 | 1 | 0 | 0 |
| *Round 2* | 10 | 0 | 0 | 0 |
| *Round 3* | 0 | 0 | 10 | 0 |

---

## 2. System Test Case Specification (`STC-AUTH-001` $\rightarrow$ `STC-AUTH-010`)

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Scenario 1.1: Đăng ký Tài khoản Công dân (/register - U-01)** | | | | | | | | | |
| **STC-AUTH-001** | Đăng ký tài khoản Công dân mới hợp lệ trên trang /register | 1. Mở trang /register trên trình duyệt.<br>2. Nhập đầy đủ Username, Email mới, Họ tên, SĐT và Mật khẩu hợp lệ.<br>3. Nhấn nút 'Đăng ký tài khoản'. | - Giao diện hiển thị thông báo thành công (MSG-AUTH-02) và chuyển hướng vào cổng người dân /user.<br>- Thiết lập cookie phiên và tạo đồng thời bản ghi users (Role=Citizen) + user_eco_profiles (TreeLevel=1, 0 điểm). | Username và Email chưa tồn tại trong PostgreSQL. | Passed | Passed | Pending | N/A | Happy path U-01 (/register) |
| **STC-AUTH-002** | Đăng ký Công dân bỏ trống trường SĐT tùy chọn | 1. Mở /register.<br>2. Điền Username, Email, Họ tên, Mật khẩu; bỏ trống ô Số điện thoại.<br>3. Nhấn 'Đăng ký tài khoản'. | - Hệ thống cho phép đăng ký thành công (201 Created), tài khoản có PhoneNumber = null.<br>- Người dùng đăng nhập tự động thành công. | Username và Email chưa tồn tại. | Passed | Passed | Pending | N/A | Trường tùy chọn trên UI |
| **STC-AUTH-003** | Chặn đăng ký trùng Email hoặc Username (BR-AUTH-01) | 1. Mở /register.<br>2. Nhập email 'citizen@ecolink.vn' hoặc username 'citizen' đã tồn tại.<br>3. Nhấn 'Đăng ký tài khoản'. | - Hiển thị lỗi rõ ràng từ mã MSG-AUTH-01 ngay trên form, không tạo bản ghi mới trong DB. | Tài khoản citizen đã được seed trong ecolink_db. | Passed | Passed | Pending | N/A | BR-AUTH-01 |
| **Scenario 1.2: Đăng nhập Dual Auth, Quick-Login & Brute-Force Lockout (/login - U-02)** | | | | | | | | | |
| **STC-AUTH-004** | Đăng nhập bằng Username/Email chuẩn và giữ nguyên phiên làm việc khi tải lại trang (F5) | 1. Mở /login.<br>2. Nhập 'citizen' / 'citizen123' (hoặc email viết hoa).<br>3. Nhấn 'Đăng nhập', sau đó bấm F5 tải lại trình duyệt. | - Điều hướng về cổng /user.<br>- Sau khi nhấn F5, GET /api/v1/auth/me xác thực qua HttpOnly cookie vẫn giữ nguyên trạng thái đăng nhập. | Tài khoản citizen đang Active. | Failed | Passed | Pending | **INC-2026-AUTH-10** | Dual Auth Cookie + JWT \| Round 1 Failed (INC-2026-AUTH-10): vòng lặp /api/v1/auth/me & RequireRole đá phiên cookie -> đã vá Round 2 Passed |
| **STC-AUTH-005** | Kiểm tra 7 nút Quick-Login điều hướng đúng cổng theo vai trò | 1. Mở /login và lần lượt nhấn các nút Quick-Login: Người dân (citizen), Công dân VIP (citizen_vip), Vựa EcoGreen (recycler), Vựa PRO (recycler_pro), Nhân viên (staff), Hỗ trợ (staff_support), Quản trị (admin). | - Citizen vào /user; Recycler vào /recycler; Staff vào /staff; Admin vào /admin.<br>- Thanh Sidebar hiển thị đúng menu theo role và quyền RBAC. | Môi trường Development bật quick-login từ /api/v1/auth/demo-users. | Passed | Passed | Pending | N/A | Council Demo Quick-Login |
| **STC-AUTH-006** | Chống dò tìm tài khoản (User Enumeration - BR-AUTH-03) khi sai mật khẩu hoặc sai tên đăng nhập | 1. Nhập username 'citizen' với mật khẩu sai.<br>2. Nhập username không tồn tại 'ghost_999' với mật khẩu bất kỳ. | - Cả 2 trường hợp đều hiển thị cùng một thông báo lỗi chung (MSG-AUTH-01), không tiết lộ tài khoản có tồn tại hay không. | Đang ở trang /login. | Passed | Passed | Pending | N/A | BR-AUTH-03 |
| **Scenario 1.3: Google OAuth 2.0 & Phục hồi Mật khẩu OTP (/forgot-password - U-03)** | | | | | | | | | |
| **STC-AUTH-007** | Hiển thị và kích hoạt luồng đăng nhập Google OAuth trên /login | 1. Mở /login.<br>2. Kiểm tra trạng thái tải /api/v1/auth/google-config.<br>3. Thực hiện xác thực với Google ID Token hợp lệ. | - Khi GoogleAuth được bật, hiển thị nút 'Đăng nhập bằng Google'; khi đăng nhập lần đầu tự tạo Citizen (IsGoogleAuth=true) và vào cổng /user. | GoogleAuth:ClientId hợp lệ trong cấu hình. | Passed | Passed | Pending | N/A | BR-AUTH-02 |
| **STC-AUTH-008** | Gửi yêu cầu quên mật khẩu và kiểm tra giới hạn tần suất 60 giây (BR-AUTH-09) | 1. Mở /forgot-password.<br>2. Nhập email tài khoản test và bấm 'Gửi mã khôi phục'.<br>3. Bấm gửi lại ngay trong vòng 20 giây. | - Lần 1: Thông báo gửi email khôi phục thành công (MSG-AUTH-08), lưu token hạn 15 phút.<br>- Lần 2 (< 60s): Rate limit chặn spam email thứ hai. | Tài khoản tồn tại trong DB. | Passed | Passed | Pending | N/A | BR-AUTH-09 Rate Limit |
| **Scenario 1.4: Quản lý Hồ sơ, Đổi mật khẩu & Đăng xuất an toàn (/user/profile)** | | | | | | | | | |
| **STC-AUTH-009** | Cập nhật thông tin cá nhân trên trang Hồ sơ (/user/profile) | 1. Đăng nhập tài khoản citizen.<br>2. Vào mục 'Hồ sơ & thông báo' (/user/profile).<br>3. Sửa Họ tên và SĐT, bấm 'Lưu thay đổi' rồi tải lại trang. | - Thông báo cập nhật thành công; tên hiển thị ở Sidebar và Header cập nhật ngay và giữ nguyên sau khi F5. | Đã đăng nhập tài khoản Citizen. | Passed | Passed | Pending | N/A | PUT /api/v1/auth/profile |
| **STC-AUTH-010** | Đổi mật khẩu trên /user/profile (hoặc /change-password) và thu hồi phiên thiết bị khác | 1. Mở trang Đổi mật khẩu.<br>2. Nhập đúng mật khẩu hiện tại và mật khẩu mới hợp lệ.<br>3. Nhấn xác nhận. | - Thông báo đổi mật khẩu thành công.<br>- Phiên hiện tại giữ lại nhưng Refresh Token của phiên cũ trên thiết bị khác bị thu hồi. | Đã đăng nhập bằng tài khoản có mật khẩu cục bộ. | Passed | Passed | Pending | N/A | POST /api/v1/auth/change-password |
