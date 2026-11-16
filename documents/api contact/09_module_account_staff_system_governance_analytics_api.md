# TÀI LIỆU ĐẶC TẢ API – MODULE 9: QUẢN TRỊ TÀI KHOẢN, NHÂN SỰ, PHÂN QUYỀN & GIÁM SÁT HỆ THỐNG (ACCOUNT, STAFF & SYSTEM GOVERNANCE / ANALYTICS)

**Mã phân hệ:** `ADM` / `AUTH` / `GAM` (`Use Cases A-01`, `A-02`, `A-03`, `A-04`, `S-06`, `S-07`, `S-11` — Module API `M-15` / Feature `FE-17`, `FE-18`)  
**Controllers:** [`AdminAnalyticsController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/AdminAnalyticsController.cs), [`StaffAccountsController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/StaffAccountsController.cs), [`AdminStaffController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/AdminStaffController.cs), [`StaffAttendanceController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/StaffAttendanceController.cs)  
**Security & Interceptors:** [`PermissionAuthorization.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Security/PermissionAuthorization.cs) (`HasPermissionAttribute`, `PermissionAuthorizationHandler`, `AccountStatusGuardMiddleware`), [`AuditLogImmutabilityInterceptor.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Data/Interceptors/AuditLogImmutabilityInterceptor.cs)  
**Services thực thi:** [`AdminAnalyticsService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AdminAnalyticsService.cs), [`AccountGovernanceService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AccountGovernanceService.cs), [`StaffManagementService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/StaffManagementService.cs), [`StaffAttendanceService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/StaffAttendanceService.cs), [`AuditLogService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AuditLogService.cs)  
**Validators & Constants:** [`GovernanceValidators.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/GovernanceValidators.cs), [`StaffPermissions.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Constants/StaffPermissions.cs)  
**Entities:** [`GovernanceEntities.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/GovernanceEntities.cs) (`AuditLog`, `AccountModerationLog`, `StaffAttendance`), [`User.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/User.cs), [`AuthEntities.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/AuthEntities.cs) (`StaffProfile`, `StaffPermissionAssignment`)  
**DTOs:** [`GovernanceDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/GovernanceDtos.cs)  
**Base Routes:** `/api/v1/admin/*`, `/api/v1/staff/accounts/*`, `/api/v1/staff/attendance/*`

---

## 1. TỔNG QUAN DANH SÁCH ENDPOINTS (19 REST APIS — `STT 150–168`)

| STT | Inventory ID | Phương thức | Endpoint URI | Quyền truy cập (`[Authorize]` / `[HasPermission]`) | Use Case | Chức năng nghiệp vụ | Quy tắc / Mã thông báo |
|:---:|:---:|:---:|:---|:---|:---:|:---|:---|
| 1 | `STT 150` | `GET` | `/api/v1/admin/analytics/kpi-summary` | `Admin` | `A-01` | Tổng hợp KPI điều hành (Citizen tích cực, Recycler xác minh, sản lượng kg/tấn, doanh thu VietQR, chất lượng AI, tồn đọng). | `BR-ADM-07`, `MSG-GEN-01` |
| 2 | `STT 151` | `GET` | `/api/v1/admin/analytics/charts` | `Admin` | `A-01` | Dữ liệu biểu đồ xu hướng theo tháng, cơ cấu nhóm phế liệu và hiệu năng AI theo nhóm. | `BR-ADM-07`, `MSG-GEN-01` |
| 3 | `STT 152` | `GET` | `/api/v1/admin/reports/export` | `Admin` | `A-02` | Trích xuất báo cáo `csv` (UTF-8 BOM) hoặc `xlsx` che PII và tự động ghi nhật ký `EXPORT_REPORT`. | `BR-ADM-05`, `BR-ADM-06`, `MSG-ADM-02` |
| 4 | `STT 153` | `GET` | `/api/v1/admin/audit-logs` | `Admin` | `A-02` | Tra cứu nhật ký kiểm toán bất biến (Append-Only Audit Trail, chỉ đọc). | `BR-ADM-05`, `MSG-GEN-01` |
| 5 | `STT 154` | `GET` | `/api/v1/staff/accounts` | `Staff` (`MANAGE_ACCOUNTS`) hoặc `Admin` | `S-06` | Danh sách tài khoản Người dân & Cơ sở thu gom (lọc vai trò, trạng thái, cảnh cáo, từ khóa). | `BR-ADM-02`, `BR-ADM-03`, `MSG-GEN-01` |
| 6 | `STT 155` | `GET` | `/api/v1/staff/accounts/{userId:guid}` | `Staff` (`MANAGE_ACCOUNTS`) hoặc `Admin` | `S-06` | Xem chi tiết hồ sơ tài khoản, thống kê giao dịch và lịch sử kiểm duyệt vi phạm. | `BR-ADM-02`, `MSG-GEN-01` |
| 7 | `STT 156` | `PATCH` | `/api/v1/staff/accounts/{userId:guid}/lock` | `Staff` (`MANAGE_ACCOUNTS`) hoặc `Admin` | `S-07` | Tạm khóa (`Suspended`) hoặc mở khóa (`Active`), bắt buộc lý do $\ge 10$ ký tự, thu hồi Refresh Token, xóa cache JWT, đẩy Recycler về `Offline`. | `BR-ADM-03`, `BR-ADM-05`, `MSG-ADM-03` |
| 8 | `STT 157` | `DELETE` | `/api/v1/staff/accounts/{userId:guid}` | `Staff` (`MANAGE_ACCOUNTS`) hoặc `Admin` | `S-07` | Vô hiệu hóa mềm (`Deactivated`) kèm biên bản lý do $\ge 10$ ký tự, bảo toàn 100% biên nhận lịch sử. | `BR-ADM-03`, `BR-ADM-04`, `MSG-ADM-03` |
| 9 | `STT 158` | `GET` | `/api/v1/admin/staff` | `Admin` | `A-03` | Tra cứu danh sách nhân viên vận hành nội bộ, phòng ban, trạng thái công tác và tập quyền. | `BR-ADM-01`, `MSG-GEN-01` |
| 10 | `STT 159` | `POST` | `/api/v1/admin/staff` | `Admin` | `A-03` | Khởi tạo tài khoản Nhân viên vận hành (`201 Created`), mặc định `0` quyền nếu không truyền `initialPermissions`, cấp mã `NV-xxx`. | `BR-ADM-01`, `BR-ADM-05`, `MSG-GEN-01` |
| 11 | `STT 160` | `PUT` | `/api/v1/admin/staff/{staffId:guid}` | `Admin` | `A-03` | Cập nhật họ tên, SĐT, phòng ban, chức danh và tùy chọn cấp lại mật khẩu tạm (thu hồi token & xóa cache). | `BR-ADM-01`, `BR-ADM-05`, `MSG-GEN-01` |
| 12 | `STT 161` | `PATCH` | `/api/v1/admin/staff/{staffId:guid}/status` | `Admin` | `A-03` | Đình chỉ (`Suspended`), ngừng kích hoạt (`Deactivated`) hoặc mở lại (`Active`) nhân viên kèm lý do $\ge 10$ ký tự; chặn tự khóa mình. | `BR-ADM-03`, `BR-ADM-05`, `MSG-ADM-03` |
| 13 | `STT 162` | `GET` | `/api/v1/admin/permissions/catalog` | `Admin` | `A-04` | Trả về danh mục chuẩn 7 mã quyền RBAC (`REVIEW_AI`..`MANAGE_INVENTORY`) và legacy alias Frontend. | `BR-ADM-02`, `MSG-GEN-01` |
| 14 | `STT 163` | `GET` | `/api/v1/admin/staff/{staffId:guid}/permissions` | `Admin` | `A-04` | Xem chi tiết tập quyền RBAC đang gán cho một nhân viên cụ thể kèm người cấp & thời điểm. | `BR-ADM-02`, `MSG-GEN-01` |
| 15 | `STT 164` | `PUT` | `/api/v1/admin/staff/{staffId:guid}/permissions` | `Admin` | `A-04` | Cập nhật ma trận quyền RBAC, chuẩn hóa 2 chiều mã quyền, xóa `IMemoryCache` tức thời và ghi Audit Log diff. | `BR-ADM-02`, `BR-ADM-05`, `MSG-ADM-01` |
| 16 | `STT 165` | `POST` | `/api/v1/staff/attendance/check-in` | `Staff`, `Admin` | `S-11` | Điểm danh bắt đầu ca trực (`201 Created`), giới hạn 1 lần/ngày (UTC+7), chuỗi 7 ngày (`+10`/`+15` điểm nhận việc), tách biệt khỏi `GreenPoints`. | `BR-GAM-08`, `BR-GAM-09`, `MSG-GEN-01` |
| 17 | `STT 166` | `POST` | `/api/v1/staff/attendance/check-out` | `Staff`, `Admin` | `S-11` | Kết thúc ca trực, tính `workedMinutes`, thống kê tác vụ vận hành từ `audit_logs` trong ca và lưu ghi chú bàn giao. | `BR-GAM-09`, `MSG-GEN-01` |
| 18 | `STT 167` | `GET` | `/api/v1/staff/attendance/me` | `Staff`, `Admin` | `S-11` | Tra cứu trạng thái chấm công hôm nay, chuỗi chuyên cần và lịch sử ca trực theo tháng/năm của bản thân. | `BR-GAM-09`, `MSG-GEN-01` |
| 19 | `STT 168` | `GET` | `/api/v1/admin/staff/attendance` | `Admin` | `S-11`, `A-03` | Báo cáo chuyên cần và nhật ký ca trực hàng ngày của toàn bộ nhân viên vận hành (có phân trang & lọc). | `BR-GAM-09`, `MSG-GEN-01` |

---

## 2. CHI TIẾT TỪNG NHÓM REST API ENDPOINTS

### Nhóm 1: Điều hành KPI, Xuất Báo cáo Ẩn danh & Nhật ký Kiểm toán (`AdminAnalyticsController` – `A-01`, `A-02`)

#### 2.1. Chỉ số Điều hành Hệ thống (`GET /api/v1/admin/analytics/kpi-summary`)
* **Query Parameters (`ExecutiveKpiQueryRequest`):**
  * `period` (`string`, mặc định `"30d"`): `"7d" | "30d" | "90d" | "all"`.
  * `fromDate` (`DateOnly?`), `toDate` (`DateOnly?`): khoảng thời gian tùy chỉnh (ưu tiên nếu cả hai cùng có giá trị).
* **Nguyên tắc tính (`BR-ADM-07`):** Chỉ tính đơn có `HandoverRequests.Status == Completed`, cơ sở `VerificationStatus == Verified && User.Status == Active`, và doanh thu gói `ServiceOrders` ở trạng thái `Paid`.
* **Ví dụ Response (`200 OK` – `ApiResponse<ExecutiveKpiSummaryDto>`):**
```json
{
  "success": true,
  "messageCode": "MSG-GEN-01",
  "message": "Tải chỉ số điều hành hệ thống thành công.",
  "data": {
    "periodLabel": "30 ngày qua",
    "fromDate": "2026-10-18",
    "toDate": "2026-11-16",
    "activeCitizensCount": 1280,
    "verifiedRecyclersCount": 42,
    "completedHandoversCount": 315,
    "totalRecycledWeightKg": 4285.5,
    "totalRecycledTonnage": 4.286,
    "grossTradeValueVnd": 31250000,
    "vietQrServiceRevenueVnd": 11770000,
    "aiBenchmark": {
      "aiServiceOnline": true,
      "totalScans": 1850,
      "overallAccuracyPercent": 93.4,
      "oodRatePercent": 4.2,
      "falseReportCount": 19
    },
    "operationalBacklog": {
      "pendingAiFeedbackCount": 7,
      "pendingRecyclerKybCount": 3,
      "openComplaintsCount": 2,
      "pendingBlogPostsCount": 0
    }
  },
  "errors": null,
  "timestamp": "2026-11-16T09:00:00Z"
}
```

#### 2.2. Dữ liệu Biểu đồ Thống kê (`GET /api/v1/admin/analytics/charts`)
* **Query Parameters:** `months` (`int`, mặc định `6`, tự động kẹp biên `Math.Clamp(months, 1, 12)`).
* **Response (`200 OK` – `ApiResponse<AnalyticsChartsDto>`):** Trả về 3 mảng: `monthlyHandoverTrend` (theo tháng `yyyy-MM`), `wasteCategoryBreakdown` (tỷ trọng % khối lượng tái chế từ đơn `Completed`), và `aiQualityByGroup` (số lượt quét, báo sai, độ chính xác từng nhóm).

#### 2.3. Trích xuất Báo cáo CSV/XLSX Ẩn danh PII (`GET /api/v1/admin/reports/export`)
* **Query Parameters (`ReportExportQueryRequest`):**
  * `dataset` (`string`, mặc định `"accounts"`): `"accounts"` (hoặc `"users"`), `"handovers"`, `"vietqr_revenues"` (hoặc `"cashflow"`), `"ai_feedback"`, `"audit_logs"`.
  * `format` (`string`, mặc định `"csv"`): `"csv"` (UTF-8 có BOM `0xEF 0xBB 0xBF`) hoặc `"xlsx"` (OpenXML `.xlsx`).
  * `fromDate` (`DateOnly?`), `toDate` (`DateOnly?`).
* **Bảo mật & Kiểm toán:**
  * Mọi trường Email / SĐT trong tệp xuất đều qua `PiiMaskingHelper` (`du***@email.vn`, `091****678` – **`BR-ADM-06`**).
  * Tự động ghi 1 bản ghi `"EXPORT_REPORT"` vào bảng bất biến `audit_logs` (**`BR-ADM-05`**).
* **Response Headers (`200 OK` binary stream):**
  * `Content-Type`: `text/csv; charset=utf-8` hoặc `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
  * `X-EcoLink-Message-Code`: `MSG-ADM-02`
  * `X-EcoLink-Row-Count`: Số dòng dữ liệu xuất ra.

#### 2.4. Tra cứu Nhật ký Kiểm toán Bất biến (`GET /api/v1/admin/audit-logs`)
* **Query Parameters (`AuditLogFilterRequest`):** `keyword`, `action`, `entityType`, `actorUserId`, `fromDate`, `toDate`, `pageNumber` (`>= 1`), `pageSize` (`1..100`).
* **Ghi chú:** Chỉ cung cấp API `GET` (Read-Only). Toàn bộ thao tác `UPDATE`/`DELETE` trên entity `AuditLog` đều bị `AuditLogImmutabilityInterceptor` chặn bằng `InvalidOperationException` (`BR-ADM-05`).

---

### Nhóm 2: Kiểm duyệt Tài khoản Citizen & Recycler (`StaffAccountsController` – `S-06`, `S-07`)

> **Kiểm soát truy cập:** `[HasPermission(StaffPermissions.ManageAccounts)]` — Cho phép `Admin` hoặc `Staff` có mã quyền `MANAGE_ACCOUNTS`. Nếu `Staff` không có quyền $\rightarrow$ `403 Forbidden` (`BR-ADM-02`, `MSG-ADM-04`). Tự động loại trừ tài khoản `Staff` và `Admin` khỏi danh sách bên ngoài (`BR-ADM-03`).

#### 2.5. Danh sách Tài khoản Kiểm duyệt (`GET /api/v1/staff/accounts`)
* **Query (`AccountGovernanceFilterRequest`):** `keyword`, `role` (`User` | `Recycler`), `status` (`Active` | `Suspended` | `Locked` | `Deactivated`), `minWarnings`, `pageNumber`, `pageSize` (`1..100`).

#### 2.6. Chi tiết Hồ sơ & Lịch sử Kiểm duyệt (`GET /api/v1/staff/accounts/{userId:guid}`)
* **Response (`200 OK` – `ManagedAccountDetailDto`):** Bao gồm thông tin cơ bản, `activityStats` (`totalHandoverRequests`, `completedDealsCount`, `cancelledLateCount`, `totalRecycledKg`, `greenPoints`, `treeLevel`) và tối đa 20 bản ghi `moderationHistory` mới nhất. Trả `404` nếu không tồn tại hoặc `targetUser` là `Staff`/`Admin`.

#### 2.7. Tạm khóa hoặc Mở khóa Tài khoản (`PATCH /api/v1/staff/accounts/{userId:guid}/lock`)
* **Request Body (`LockOrUnlockAccountRequest`):**
```json
{
  "isLocked": true,
  "reason": "Tái phạm gian lận cân đo > 5kg tại điểm hẹn bàn giao",
  "incrementWarning": true,
  "relatedComplaintId": null
}
```
* **Validation (`LockOrUnlockAccountValidator`):** `reason` không rỗng, `10..500` ký tự (`400 Bad Request` nếu vi phạm).
* **Side Effects (`BR-ADM-03`, `BR-ADM-05`):**
  * Chặn tự khóa chính mình (`actorUserId == targetUserId`) hoặc khóa `Staff`/`Admin` (`403 Forbidden`, `MSG-ADM-04`).
  * Chuyển `User.Status` sang `Suspended` (khi khóa) hoặc `Active` (khi mở khóa + reset `FailedLoginAttempts` & `LockoutUntil`).
  * Thu hồi toàn bộ `RefreshTokens` chưa revoke, đặt `RecyclerProfile.ActiveStatus = Offline`, xóa `IMemoryCache` key `auth_snapshot:{userId}` (`AccountStatusGuardMiddleware` chặn ngay cả Access Token còn hạn với `403 MSG-AUTH-04`), ghi `AccountModerationLog` và `AuditLog` (`LOCK_ACCOUNT` / `UNLOCK_ACCOUNT`).
  * Trả về `200 OK` với `messageCode = "MSG-ADM-03"`.

#### 2.8. Vô hiệu hóa Mềm Tài khoản (`DELETE /api/v1/staff/accounts/{userId:guid}`)
* **Request Body (`DeactivateAccountRequest`):** `{ "reason": "Tài khoản lạm dụng chính sách..." }` (`10..500` ký tự).
* **Side Effects (`BR-ADM-04`):** Cập nhật `User.Status = UserStatus.Deactivated` (không xóa vật lý dòng trong DB để bảo toàn khóa ngoại tới `HandoverReceipts`), thu hồi `RefreshTokens`, đặt Recycler về `Offline`, xóa cache tức thời và ghi `DEACTIVATE_ACCOUNT` vào `audit_logs` kèm `preservedCompletedDeals`. Trả `200 OK` (`MSG-ADM-03`).

---

### Nhóm 3: Quản lý Nhân sự Vận hành & Ma trận Phân quyền RBAC (`AdminStaffController` – `A-03`, `A-04`)

#### 2.9. Danh sách & Khởi tạo Nhân viên (`GET` / `POST /api/v1/admin/staff`)
* **`POST /api/v1/admin/staff` (`CreateStaffAccountRequest`):**
  * `fullName` (`3..100` ký tự, bắt buộc), `email` (định dạng Email, tối đa `255` ký tự, bắt buộc), `username` (`<= 50`), `phoneNumber`, `initialPassword` (`>= 8` ký tự nếu truyền, mặc định `"Staff@2026!Eco"`), `staffCode` (tự sinh `"NV-001"`, `"NV-002"`... nếu bỏ trống), `department`, `position`, `initialPermissions` (`List<string>?` — mặc định `[]` theo nguyên tắc Least-Privilege **`BR-ADM-01`**).
  * Response: `201 Created` trả về `StaffAccountDto`, đồng thời ghi Audit Log `"CREATE_STAFF"`.

#### 2.10. Cập nhật Hồ sơ & Trạng thái Nhân sự (`PUT /api/v1/admin/staff/{staffId:guid}`, `PATCH /api/v1/admin/staff/{staffId:guid}/status`)
* **`PUT /api/v1/admin/staff/{staffId:guid}` (`UpdateStaffAccountRequest`):** Cập nhật `fullName`, `phoneNumber`, `department`, `position`. Nếu truyền `resetTemporaryPassword` (`>= 8` ký tự), hệ thống băm BCrypt mật khẩu mới, thu hồi `RefreshTokens`, xóa cache và ghi Audit Log `"UPDATE_STAFF"`.
* **`PATCH /api/v1/admin/staff/{staffId:guid}/status` (`UpdateStaffStatusRequest`):**
  * `status`: `"Active" | "Suspended" | "Deactivated"`, `reason`: `10..500` ký tự.
  * Chặn Admin tự khóa chính mình (`403 Forbidden`, `MSG-ADM-04`). Trả về `200 OK` (`MSG-ADM-03`).

#### 2.11. Danh mục Quyền & Phân quyền Chi tiết (`GET .../permissions/catalog`, `GET` / `PUT /api/v1/admin/staff/{staffId:guid}/permissions`)
* **7 Mã quyền RBAC chuẩn (`StaffPermissions`):**

| Mã chuẩn (`Code`) | Alias Frontend (`LegacyAlias`) | Nhãn (`Label`) |
|:---|:---|:---|
| `REVIEW_AI` | `ai.review` | Đánh giá ảnh AI (`S-01`, `S-02`) |
| `CURATE_DATASET` | `ai.dataset` | Cập nhật tập dữ liệu (`S-03`) |
| `VERIFY_RECYCLER` | `partner.verify` | Thẩm định hồ sơ đối tác (`S-04`) |
| `HANDLE_COMPLAINT` | `complaint.handle` | Xử lý khiếu nại (`S-05`) |
| `MANAGE_ACCOUNTS` | `account.manage` | Quản lý tài khoản (`S-06`, `S-07`) |
| `MANAGE_BLOG` | `blog.write` | Soạn bài Blog (`S-08`) |
| `MANAGE_INVENTORY` | `gift.manage` | Quản lý kho quà (`S-09`, `S-10`) |

* **`PUT /api/v1/admin/staff/{staffId:guid}/permissions` (`UpdateStaffPermissionsRequest`):**
```json
{
  "permissions": ["MANAGE_ACCOUNTS", "ai.review"],
  "reason": "Phân công trực kiểm duyệt tài khoản và ảnh AI ca sáng"
}
```
* **Hành vi (`BR-ADM-02`, `MSG-ADM-01`):** Chuẩn hóa alias sang mã in hoa, chặn mã lạ (`400 Bad Request`), đồng bộ bảng `staff_permission_assignments`, gọi `IAuthStateCacheInvalidator.InvalidateUserAuthState(staffId)` để cấp/thu hồi quyền **tức thời**, ghi diff `oldValues`/`newValues` vào `audit_logs`, và trả về `200 OK` với `messageCode = "MSG-ADM-01"`.

---

### Nhóm 4: Điểm danh Ca trực Hàng ngày (`StaffAttendanceController` – `S-11`)

#### 2.12. Điểm danh Đầu ca (`POST /api/v1/staff/attendance/check-in`)
* **Request (`StaffCheckInRequest?`):** `{ "shiftNote": "Nhận ca sáng..." }` (`<= 500` ký tự).
* **Hành vi (`BR-GAM-08`, `BR-GAM-09`):**
  * Tính ngày làm việc theo múi giờ **UTC+7** (`VietnamOffset`).
  * Nếu đã điểm danh trong ngày $\rightarrow$ `400 Bad Request` (`BusinessRuleException` ruleCode `"BR-GAM-09"`).
  * Liên tiếp ngày hôm qua $\rightarrow$ tăng `streakDay = (prevStreak % 7) + 1`; đứt quãng $\rightarrow$ reset `streakDay = 1`. Ngày `1..6` thưởng `+10` điểm nhận việc (`workPointsEarned`), ngày `7` thưởng `+15` điểm nhận việc.
  * **Tuyệt đối không cộng** vào `UserEcoProfiles.CurrentGreenPoints` (`BR-GAM-08`).
  * Response: `201 Created` trả về `StaffAttendanceStatusDto`, ghi Audit Log `"STAFF_CHECK_IN"`.

#### 2.13. Kết thúc Ca trực & Tra cứu Chấm công (`POST /check-out`, `GET /me`, `GET /admin/staff/attendance`)
* **`POST /api/v1/staff/attendance/check-out`:** Bắt buộc đã `check-in` và chưa `check-out` hôm nay (`400 BR-GAM-09` nếu vi phạm). Ghi nhận `workedMinutes`, đếm `tasksCompletedSnapshot` từ `audit_logs` trong ca và ghi `"STAFF_CHECK_OUT"`.
* **`GET /api/v1/staff/attendance/me`:** Tham số `month`, `year`; trả trạng thái hôm nay, chuỗi chuyên cần và lịch sử tháng.
* **`GET /api/v1/admin/staff/attendance`:** Chỉ `Admin`; tham số `fromDate`, `toDate`, `staffId`, `pageNumber`, `pageSize`; trả về `PagedResult<AdminStaffAttendanceItemDto>`.

---

## 3. BẢNG MÃ LỖI & MÃ THÔNG BÁO CHUẨN TRONG MODULE 9

| HTTP Status | `messageCode` | Quy tắc | Trường hợp xảy ra |
|:---:|:---|:---:|:---|
| `200` / `201` | `MSG-GEN-01` | — | Tải KPI, charts, danh sách, khởi tạo Staff, chấm công ca trực thành công. |
| `200` | `MSG-ADM-01` | `BR-ADM-02` | Admin cập nhật ma trận quyền RBAC cho Staff thành công (*"Cập nhật quyền nhân viên thành công."*). |
| `200` | `MSG-ADM-02` | `BR-ADM-05`, `BR-ADM-06` | Header `X-EcoLink-Message-Code` xác nhận trích xuất báo cáo hệ thống che PII thành công. |
| `200` | `MSG-ADM-03` | `BR-ADM-03`, `BR-ADM-04` | Khóa, mở khóa, vô hiệu hóa tài khoản hoặc đổi trạng thái Staff thành công. |
| `400` | `MSG-GEN-01` | `BR-ADM-03`, `BR-ADM-06`, `BR-GAM-09` | Thiếu/ngắn lý do (`< 10` ký tự), sai mã quyền RBAC, sai định dạng export, hoặc điểm danh trùng trong ngày. |
| `401` | `MSG-AUTH-03` | — | Chưa đăng nhập hoặc JWT Bearer token không hợp lệ. |
| `403` | `MSG-ADM-04` | `BR-ADM-02`, `BR-ADM-03` | `Staff` thiếu quyền tương ứng trong `staff_permission_assignments`, tự khóa mình, hoặc tác động tài khoản `Staff`/`Admin` qua endpoint kiểm duyệt bên ngoài. |
| `403` | `MSG-AUTH-04` | `BR-AUTH-05`, `BR-ADM-03` | JWT còn hạn nhưng tài khoản vừa bị chuyển sang `Suspended`/`Deactivated` và bị `AccountStatusGuardMiddleware` chặn tức thời. |
| `404` | `MSG-GEN-01` | — | Không tìm thấy tài khoản `User` hoặc `StaffProfile` mục tiêu. |
