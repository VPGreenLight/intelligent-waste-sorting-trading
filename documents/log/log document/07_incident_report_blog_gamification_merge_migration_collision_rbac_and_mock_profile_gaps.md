# BÁO CÁO SỰ CỐ KỸ THUẬT HỢP NHẤT NHÁNH `gamification-&-reward` & `giang` (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-GAM-07`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Gamification & Rewards (`M-10` / `FE-13`), Blog & Content Management (`M-14` / `FE-16`), Rating/Complaint Clawback (`M-11`), Staff Governance (`M-15`), Frontend SPA (`ecolink-web`)  
**Thời điểm ghi nhận:** 18/11/2026 – 19/11/2026 (Quá trình hợp nhất nhánh `gamification-&-reward` vào `develop` và nhánh `giang` vào `thang`)  
**Mức độ nghiêm trọng:** Nghiêm trọng (Critical – Chứa xung đột EF Migration phá hủy bảng `audit_logs`, lỗ hổng phân quyền `[Authorize]`, dữ liệu mock tĩnh ở Profile & FE, và hàm SQL không tương thích InMemory Unit Test)

---

## 1. DANH SÁCH 9 LỖI, THIẾU SÓT KỸ THUẬT ĐÃ PHÁT HIỆN & KHẮC PHỤC

### 1.1. Lỗi 1: Xung đột Git đa tệp trên Backend khi merge nhánh `gamification-&-reward` vào `develop`
* **Hiện tượng:** Khi kéo nhánh `gamification-&-reward` của Hoàng vào `develop`, GitHub Desktop báo 3 điểm xung đột nặng: `ApplicationDbContextModelSnapshot.cs` (**17 conflicts**), `DependencyInjection.cs` và `data/stats.json`.
* **Biện pháp khắc phục:** Hợp nhất toàn bộ `DbSet` + `ModelBuilder` cho cả hai nhánh (giữ trọn vẹn các bảng của `M-11..M-15` trên `develop` cùng 11 thực thể mới của `M-10`/`M-14`), giữ đủ đăng ký DI cho mọi phân hệ và cộng gộp thống kê AI.

### 1.2. Lỗi 2 (Critical): Migration `20261112125714_BlogContent.cs` tạo trùng bảng `audit_logs` & xóa cột `"Id"`
* **Hiện tượng:** Trong migration `20261112125714_BlogContent.cs` từ nhánh `gamification-&-reward`, EF Core chứa lệnh `CreateTable("audit_logs")` và `DropColumn("Id")` xung đột trực tiếp với bảng `audit_logs` đã được tạo hoàn chỉnh trước đó tại `20261116025750_AddModule15GovernanceAndAnalytics.cs`. Nếu chạy `dotnet ef database update` lên `ecolink_db`, PostgreSQL sẽ báo lỗi `relation "audit_logs" already exists` và làm sập toàn bộ `IAuditLogService` / `AuditLogImmutabilityInterceptor`.
* **Biện pháp khắc phục:** Vá sạch `20261112125714_BlogContent.cs` (gỡ bỏ toàn bộ thao tác đụng bảng `audit_logs`, chỉ giữ tạo bảng `blog_posts` và đồng bộ `notifications`), sau đó kiểm chứng `dotnet ef database update` thành công với `0` pending migrations trên `ecolink_db`.

### 1.3. Lỗi 3: Trùng lặp cụm `StaffShift` với `M-15` và bỏ qua `IAuditLogService`
* **Hiện tượng:** Nhánh `gamification-&-reward` tự tạo thêm cụm `StaffShiftController`, `StaffShiftService`, `StaffShiftLog` (`/api/v1/staff/shift/*`) trùng hoàn toàn chức năng chấm công ca trực `S-11` đã có sẵn tại `StaffAttendanceController` (`M-15`). Đồng thời `GamificationConfigService` và `BlogPostService` ghi thẳng `_db.AuditLogs.Add(...)` bỏ qua `IAuditLogService` nên bị thiếu `ActorRole`, `ActorEmail`, `IpAddress`, `UserAgent`.
* **Biện pháp khắc phục:** Gỡ bỏ cụm `StaffShift` thừa khỏi code & API Tests (dùng thống nhất `StaffAttendanceController` với `/api/v1/staff/attendance/*`), tích hợp `IAuditLogService` chuẩn vào `BlogPostService`, `GamificationConfigService` và `LeaderboardService`.

### 1.4. Lỗi 4 (Bảo mật): Phân quyền cứng `[Authorize(Roles = "Staff")]` bỏ qua `[HasPermission]` và chặn `Admin`
* **Hiện tượng:** `StaffBlogController` và `StaffGamificationController` ban đầu dùng `[Authorize(Roles = "Staff")]` khiến: (a) tài khoản `Admin` bị từ chối `403 Forbidden` khi thao tác nghiệp vụ Staff; (b) bất kỳ `Staff` nào không có quyền chuyên trách vẫn truy cập được kho quà và bài viết Blog (vi phạm `BR-ADM-01`/`BR-ADM-02` *"Staff mặc định 0 quyền"*).
* **Biện pháp khắc phục:** Chuyển toàn bộ hai controller sang phân quyền hạt mịn `[HasPermission(StaffPermissions.ManageBlog)]` và `[HasPermission(StaffPermissions.ManageInventory)]` (cho phép cả `Admin` và `Staff` được cấp đúng mã quyền trong `AdminStaffController`), đồng thời bổ sung `MANAGE_BLOG` & `MANAGE_INVENTORY` vào danh mục quyền chuẩn.

### 1.5. Lỗi 5: Đổi tài khoản seed `staff` thành `blogstaff` làm gãy toàn bộ `EcoLink.ApiTests`
* **Hiện tượng:** Nhánh nhánh mới sửa `Support.cs`, `BlogTests.cs` và `GamificationTests.cs` dùng tài khoản cứng `"blogstaff" / "citizen123"` thay cho tài khoản seed chuẩn `"staff" / "staff123"`, gây lỗi đăng nhập hàng loạt khi chạy bộ test.
* **Biện pháp khắc phục:** Khôi phục chuẩn `"staff" / "staff123"` trong `Support.cs`, `BlogTests.cs` và `GamificationTests.cs`.

### 1.6. Lỗi 6: `GamificationController.GetProfile` trả mock tĩnh, thiếu `A-08` (`LeaderboardCycle`) & thiếu clawback ví điểm thật
* **Hiện tượng:**
  1. `GamificationController.GetProfile` và `GetLeaderboard` gọi `GamificationService` đang dùng `ConcurrentDictionary` + mảng 5 người giả `"Nguyễn Văn Xanh"` thay vì đọc từ PostgreSQL.
  2. Chưa triển khai Use Case `A-08` (**Configure Leaderboard Cycles**: tạo/bật/chốt chu kỳ Bảng xếp hạng tuần/tháng/quý và tự động trao thưởng Top 3).
  3. Khi Staff phán quyết `CancelDisputedOrder` tại `M-11` (`ComplaintService.ResolveComplaintAsync`), hệ thống chỉ trừ điểm trên bảng legacy `user_eco_profiles` mà chưa trừ trong ví điểm thật `user_point_wallets` và chưa ghi bút toán âm `PointTransaction`, tạo lỗ hổng người dùng vẫn còn nguyên điểm trong ví để đổi quà sau khi bị hủy đơn gian lận!
* **Biện pháp khắc phục:** Nối `GamificationService` trực tiếp sang PostgreSQL (`user_point_wallets`, `virtual_tree_progress`, `handover_receipts`, `ai_scan_histories`, `daily_checkin_logs`), triển khai đầy đủ `LeaderboardCycle` + `LeaderboardService` + Migration `20261118205051_AddLeaderboardCyclesModule.cs`, và tích hợp `ClawBackPointsSafeAsync` + `IPointsService.AwardHandoverPointsAsync` vào `ComplaintService.cs`.

### 1.7. Lỗi 7: Lệch đường dẫn Route với `00_api_inventory` & thiếu endpoint Upload ảnh Blog lên R2
* **Hiện tượng:** Nhánh `gamification-&-reward` đặt route `/api/v1/blog-posts`, `/api/v1/check-in`, `/api/v1/virtual-tree`, `/api/v1/points/*`, `/api/v1/gifts` lệch so với hợp đồng chuẩn trong [`00_api_inventory_and_endpoint_statistics.md`](../../api%20contact/00_api_inventory_and_endpoint_statistics.md) (`/api/v1/blogs`, `/api/v1/gamification/*`, `/api/v1/admin/blogs/pending`, `/api/v1/admin/blogs/{id}/review`), và chưa có endpoint tải ảnh bìa/minh họa bài viết lên Cloudflare R2 (`STT 142`).
* **Biện pháp khắc phục:** Chuẩn hóa toàn bộ route trong `PublicBlogController`, `StaffBlogController`, `AdminBlogController`, `UserGamificationController`, `PointsAndRewardsController`, `StaffGamificationController`, `AdminGamificationController` theo dạng **Canonical Route + Alias Route tương thích ngược**, đồng thời bổ sung `POST /api/v1/staff/blogs/upload-media` sử dụng `IFileStorageService` (`CloudflareR2StorageService`).

### 1.8. Lỗi 8: Hàm EF Core Relational-only làm gãy `InMemory` Unit Tests & lỗi nạp `Npgsql.dll` trong PowerShell 5.1
* **Hiện tượng:**
  1. 6 service (`GamificationConfigService`, `PointsService`, `VirtualTreeService`, `EcoFactService`, `GiftService`, `BlogPostService`) gọi trực tiếp `ExecuteSqlInterpolatedAsync`, `ExecuteUpdateAsync`, `ExecuteDeleteAsync`, và `EF.Functions.ILike`. Khi kiểm thử đơn vị qua `TestDbContextFactory.Create()` (`Microsoft.EntityFrameworkCore.InMemory`), EF Core ném `InvalidOperationException` vì InMemory provider không hỗ trợ các phép toán Relational này.
  2. Khi viết script smoke test E2E bằng Windows PowerShell 5.1 (`.NET Framework 4.8`), lệnh nạp `Npgsql.dll` (.NET 10) trực tiếp báo lỗi `Cannot find type [Npgsql.NpgsqlConnection]`.
  3. Ngoài ra, toàn bộ 10 trang Frontend tương ứng (`Blog.tsx`, `Rewards.tsx`, `Leaderboard.tsx`, `Gamification.tsx`, `BlogEditor.tsx`, `EcoFacts.tsx`, `Gifts.tsx`, `BlogReview.tsx`, `admin/Gamification.tsx`, `admin/Leaderboard.tsx`) ban đầu đang gắn mock tĩnh (`ops.ts`, `rewards.ts`, `insights.ts`).
* **Biện pháp khắc phục:**
  1. Bổ sung nhánh kiểm tra `if (_db.Database.IsRelational()) { ... } else { /* tracked entities + SaveChangesAsync / Contains(StringComparison.OrdinalIgnoreCase) */ }` trong cả 6 service — giữ nguyên 100% lệnh SQL nguyên tử trên PostgreSQL đồng thời cho phép `302 / 302` Unit Tests (bao gồm `22` test `M-10` và `20` test `M-14`) chạy xanh hoàn toàn trên InMemory.
  2. Chuyển thao tác kiểm đếm và dọn dữ liệu test DB sang công cụ `.NET 10` (`DbTool`) thay vì nạp assembly .NET 10 trong PowerShell 5.1.
  3. Viết `src/lib/blogApi.ts`, `src/lib/gamificationApi.ts`, tạo `BlogDetail.tsx` (`/blog/:slug`) và đấu nối đồng bộ toàn bộ màn hình FE (`4/4` Frontend gates PASS & E2E smoke test PASS 100%).

### 1.9. Lỗi 9: Xung đột giao diện `src/components/Leaderboard.tsx` khi merge nhánh `giang` vào `thang` (`ecolink-web`)
* **Hiện tượng:** Khi ghép nhánh `giang` (chỉnh lại thiết kế card `Leaderboard.tsx` với `LaurelIcon` dải ruy-băng, 3 podium avatar tròn kích thước lớn không khung viền, huy chương phẳng gold/silver/bronze và icon `ecolink-coin.png`) vào nhánh `thang` (đã đấu nối `hasLiveBackendAuth()`, `fetchLeaderboard({ top: 100 })`, `cycle` và `myEntry`), Git phát hiện 3 vùng xung đột trong `Leaderboard.tsx` và 1 xung đột nhật ký ở `docs/2026-11-18.md`.
* **Biện pháp khắc phục:** Giải quyết xung đột bằng cách giữ **100% giao diện mới từ `giang`** kết hợp **100% luồng dữ liệu thật + fallback `hasLiveBackendAuth()` + null-safe guard `person?: BoardRow` từ `thang`**, đồng thời bảo toàn nhật ký cả 2 nhánh và xác minh lại toàn bộ `typecheck`, `check`, `check:routes` (`8885 bytes html`), `build` (`2201 modules transformed`).
