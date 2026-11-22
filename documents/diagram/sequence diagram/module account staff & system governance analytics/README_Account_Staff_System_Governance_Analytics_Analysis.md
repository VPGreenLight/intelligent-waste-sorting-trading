# PHÂN TÍCH SƠ ĐỒ THIẾT KẾ MODULE 9: QUẢN TRỊ TÀI KHOẢN, NHÂN SỰ, PHÂN QUYỀN & GIÁM SÁT HỆ THỐNG (`A-01`..`A-04`, `S-06`, `S-07`, `S-11`)

**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** `ADM` / `AUTH` / `GAM` – Kiểm soát Vận hành Trung tâm & Quản trị Hệ thống (*Account, Staff & System Governance / Analytics* — Module `M-15` / `FE-17`, `FE-18`)  
**Use Cases trọng tâm:**
- `A-01: View Dashboard & Analytics`
- `A-02: Export Data Reports & Audit Log`
- `A-03: Manage Staff Accounts`
- `A-04: Manage Staff Permissions`
- `S-06: View User & Recycler Accounts`
- `S-07: Lock / Delete Accounts`
- `S-11: Staff Daily Check-in`

---

## 1. Danh mục Sơ đồ Thiết kế trong Module

| STT | Loại sơ đồ | Đường dẫn File | Mô tả nội dung |
|:---:|:---:|:---|:---|
| **01** | **Class Diagram** | [`module_account_staff_system_governance_analytics_clean_architecture_class_diagram_ultra_compact.mmd`](../../class%20diagram/module%20account%20staff%20&%20system%20governance%20analytics/module_account_staff_system_governance_analytics_clean_architecture_class_diagram_ultra_compact.mmd) | Sơ đồ lớp kiến trúc 4 tầng Clean Architecture (`AdminAnalyticsController`, `StaffAccountsController`, `AdminStaffController`, `StaffAttendanceController`, `PermissionAuthorizationHandler`, `AccountStatusGuardMiddleware`, 5 Application Services, `AuditLogImmutabilityInterceptor`, `AuditLog`, `AccountModerationLog`, `StaffAttendance`, `StaffProfile`, `StaffPermissionAssignment`). |
| **02** | **Sequence Diagram 01** | [`01_a01_a02_executive_kpi_analytics_anonymized_export_and_immutable_audit_trail.mmd`](./01_a01_a02_executive_kpi_analytics_anonymized_export_and_immutable_audit_trail.mmd) | Luồng tổng hợp KPI điều hành & biểu đồ từ giao dịch `Completed` + doanh thu VietQR `Paid` (`BR-ADM-07`), xuất báo cáo CSV/XLSX ẩn danh hóa PII (`BR-ADM-06`) và bảo vệ tính bất biến của bảng `audit_logs` (`BR-ADM-05`). |
| **03** | **Sequence Diagram 02** | [`02_s06_s07_staff_account_inspection_lock_unlock_and_soft_deactivation.mmd`](./02_s06_s07_staff_account_inspection_lock_unlock_and_soft_deactivation.mmd) | Luồng kiểm soát quyền `[HasPermission("MANAGE_ACCOUNTS")]`, tra cứu hồ sơ Citizen/Recycler, tạm khóa/mở khóa thu hồi `RefreshToken` + xóa cache JWT tức thời (`AccountStatusGuardMiddleware` – `BR-ADM-03`) và vô hiệu hóa mềm bảo toàn biên nhận (`BR-ADM-04`). |
| **04** | **Sequence Diagram 03** | [`03_a03_a04_admin_staff_provisioning_rbac_permission_matrix_and_instant_cache_invalidation.mmd`](./03_a03_a04_admin_staff_provisioning_rbac_permission_matrix_and_instant_cache_invalidation.mmd) | Luồng `Admin` khởi tạo nhân sự với `0` quyền mặc định (`BR-ADM-01`), chuẩn hóa ma trận 7 mã quyền RBAC, xóa cache tức thời (`BR-ADM-02`, `MSG-ADM-01`) và chặn tự khóa chính mình. |
| **05** | **Sequence Diagram 04** | [`04_s11_staff_daily_shift_checkin_checkout_and_greenpoints_wallet_isolation.mmd`](./04_s11_staff_daily_shift_checkin_checkout_and_greenpoints_wallet_isolation.mmd) | Luồng chấm công ca trực hàng ngày theo múi giờ UTC+7, thưởng chuỗi 7 ngày (`+10`/`+15`), tách biệt hoàn toàn khỏi ví điểm công dân (`BR-GAM-08`, `BR-GAM-09`), kết thúc ca đếm tác vụ vận hành và báo cáo chuyên cần Admin. |

---

## 2. Bảng Đối chiếu Quy tắc Nghiệp vụ (`BR-*`) & Mã Thông báo (`MSG-*`)

| Mã Quy tắc / Thông báo | Nội dung Đặc tả | Thành phần Thực thi trong Sơ đồ |
|:---:|:---|:---|
| **`BR-ADM-01`** | Chỉ `Admin` được phép tạo/đình chỉ tài khoản nội bộ `Staff`. Tài khoản `Staff` mới khởi tạo mặc định không có bất kỳ quyền nghiệp vụ nào (Least Privilege). | `AdminStaffController` (`[Authorize(Roles = "Admin")]`) & `StaffManagementService.CreateStaffAccountAsync` |
| **`BR-ADM-02`** | Phân quyền RBAC chi tiết theo 7 mã tác vụ (`REVIEW_AI`, `CURATE_DATASET`, `VERIFY_RECYCLER`, `HANDLE_COMPLAINT`, `MANAGE_ACCOUNTS`, `MANAGE_BLOG`, `MANAGE_INVENTORY`). Cấp/thu hồi quyền có hiệu lực **tức thời**. | `HasPermissionAttribute`, `PermissionAuthorizationHandler`, `MemoryAuthStateCacheInvalidator` |
| **`BR-ADM-03`** | Mọi thao tác khóa/mở khóa hoặc thay đổi trạng thái nhân sự đều bắt buộc lý do rõ ràng ($\ge 10$ ký tự), cấm leo thang đặc quyền (`Staff` khóa `Staff`/`Admin`), cấm tự khóa mình, thu hồi `RefreshToken` và chặn JWT ngay lập tức. | `AccountGovernanceService`, `StaffManagementService`, `AccountStatusGuardMiddleware` |
| **`BR-ADM-04`** | Vô hiệu hóa tài khoản dùng cơ chế Soft-Deactivate (`UserStatus.Deactivated`), tuyệt đối không xóa vật lý bản ghi để bảo toàn tham chiếu tới lịch sử biên nhận (`HandoverReceipts`). | `AccountGovernanceService.DeactivateAccountAsync` |
| **`BR-ADM-05`** | Mọi hành động nhạy cảm (xuất báo cáo, khóa/vô hiệu hóa, tạo/sửa Staff, phân quyền, điểm danh) đều ghi vào `audit_logs` ở chế độ chỉ ghi thêm (Append-Only); cấm tuyệt đối `UPDATE`/`DELETE`. | `AuditLogService` & `AuditLogImmutabilityInterceptor` |
| **`BR-ADM-06`** | Toàn bộ tệp CSV/XLSX trích xuất dữ liệu hệ thống phải che dấu thông tin định danh cá nhân (Email, SĐT). | `PiiMaskingHelper` & `TabularReportExportHelper` trong `AdminAnalyticsService.ExportSystemReportAsync` |
| **`BR-ADM-07`** | Số liệu KPI và biểu đồ điều hành chỉ tổng hợp từ các giao dịch đã hoàn tất (`Completed`), cơ sở đã xác minh (`Verified`), và đơn VietQR đã thanh toán (`Paid`). | `AdminAnalyticsService.GetExecutiveKpiSummaryAsync` & `GetAnalyticsChartsAsync` |
| **`BR-GAM-08`** | Điểm chuyên cần nhận việc (`WorkPointsEarned`) của nhân sự nội bộ tách biệt hoàn toàn với ví điểm xanh công dân (`UserEcoProfile.CurrentGreenPoints`). | `StaffAttendanceService.CheckInAsync` |
| **`BR-GAM-09`** | Nhân viên chỉ được điểm danh tối đa 1 lần/ngày theo múi giờ Việt Nam (UTC+7); duy trì 7 ngày liên tiếp thưởng `+15` điểm (các ngày 1–6 thưởng `+10`), bỏ lỡ sẽ quay về Ngày 1. | `StaffAttendanceService.CheckInAsync` & `CheckOutAsync` |
| **`MSG-ADM-01`** | *"Cập nhật quyền nhân viên thành công."* | `AdminStaffController.UpdateStaffPermissions` |
| **`MSG-ADM-02`** | Mã xác nhận xuất báo cáo ẩn danh thành công qua header `X-EcoLink-Message-Code`. | `AdminAnalyticsController.ExportSystemReport` |
| **`MSG-ADM-03`** | *"Trạng thái tài khoản đã được cập nhật và người dùng đã được thông báo."* | `StaffAccountsController` & `AdminStaffController.UpdateStaffStatus` |
| **`MSG-ADM-04`** | *"Bạn không có quyền thực hiện thao tác này."* | `PermissionAuthorizationHandler` & `AccountGovernanceService` |
| **`MSG-AUTH-04`** | *"Tài khoản của bạn đã bị tạm đình chỉ hoặc ngừng kích hoạt."* | `AccountStatusGuardMiddleware` |
