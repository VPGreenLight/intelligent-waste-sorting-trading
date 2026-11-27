# TÀI LIỆU TRA CỨU TRUY VẾT MÃ GIT COMMIT & TRÌNH CHIẾU GIT DIFF (MINH CHỨNG 14 SỰ CỐ)

**Mục đích:** Cung cấp bảng truy vết đầy đủ từ phiên bản mã nguồn gây lỗi (`Buggy Commit` — Round 1 `Failed`) sang phiên bản đã khắc phục (`Fixed Commit` — Round 2 `Passed`) cho toàn bộ **14 báo cáo sự cố (`INC-2026-HND-01` → `INC-2026-VAL-14`)**, kèm bộ lệnh trình chiếu trực tiếp theo **Cách 1 (`git show` / Git Diff)** giúp minh chứng nhanh, trực quan và **an toàn 100%** (không làm thay đổi working tree hay gián đoạn bản Demo đang chạy).

**Hai Repository liên quan:**
- **Backend & Tài liệu (`BE`):** `c:\Projects\EcoLink\intelligent-waste-sorting-trading`
- **Frontend Web (`FE`):** `c:\Projects\EcoLink\ecolink-web`

---

## 1. Bảng Truy vết Mã Git Commit (`Buggy Commit` → `Fixed Commit`) của toàn bộ 14 Sự cố

| STT | Mã Sự cố | File Báo cáo Log | Repo | Phiên bản Lỗi (`Buggy Commit`) | Phiên bản Đã Vá (`Fixed Commit`) | Nội dung Commit Vá lỗi trên Git |
| :-: | :--- | :--- | :---: | :---: | :---: | :--- |
| 1 | **`INC-2026-HND-01`** | [01_incident_report_handover_requests_relation_not_exist.md](./01_incident_report_handover_requests_relation_not_exist.md) | Backend | `88a68b06`<br>*(09/11/2026)* | **`32aa1a84`**<br>*(09/11/2026)* | `fix(db): ignore inherited id on recycler profile and sync migrations` — Áp dụng migration tạo bảng `handover_requests` và đồng bộ EF Core snapshot cho `JOB-03`. |
| 2 | **`INC-2026-GEO-02`** | [02_incident_report_nearby_recyclers_postgis_and_seeding_500.md](./02_incident_report_nearby_recyclers_postgis_and_seeding_500.md) | Backend | `13760848`<br>*(09/11/2026)* | **`32aa1a84`**<br>*(09/11/2026)* | `fix(db): ignore inherited id on recycler profile and sync migrations` — Sửa lỗi HTTP `500` trên `/api/v1/recyclers/nearby` (PostGIS & seeding khu vực Hòa Lạc). |
| 3 | **`INC-2026-DB-03`** | [03_incident_report_missing_column_recycler_profiles_id_and_unapplied_migrations.md](./03_incident_report_missing_column_recycler_profiles_id_and_unapplied_migrations.md) | Backend | `13760848`<br>*(09/11/2026)* | **`32aa1a84`**<br>*(09/11/2026)* | `fix(db): ignore inherited id on recycler profile and sync migrations` — Thêm `builder.Ignore(r => r.Id)` trên `RecyclerProfileConfiguration` (Shared PK `user_id`) tránh lỗi SQL `42703: column r.Id does not exist`. |
| 4 | **`INC-2026-HND-04`** *(Đã ghi hash trong log)* | [04_incident_report_handover_sequence_merge_conflicts_and_unit_test_regression.md](./04_incident_report_handover_sequence_merge_conflicts_and_unit_test_regression.md) | Backend | `72a67446`<br>*(11/11/2026)* | **`8519e7b1`** & **`2c26e166`**<br>*(11/11/2026)* | `fix(handover): support in-memory fallback and D4 format for sequence code generation` — Kiểm tra `_db.Database.IsRelational()` trước khi gọi PostgreSQL `nextval(...)` và giữ định dạng mã `D4` (`HND-yyyyMMdd-NNNN`, `RCP-yyyy-NNNN`). |
| 5 | **`INC-2026-DI-05`** *(Đã ghi hash trong log)* | [05_incident_report_missing_di_registrations_and_api_integration_test_alignment.md](./05_incident_report_missing_di_registrations_and_api_integration_test_alignment.md) | Backend | `2c26e166`<br>*(11/11/2026)* | **`2cc9b221`**<br>*(11/11/2026)* | `fix(backend): register missing onboarding and storage DI services and align API integration tests` — Bổ sung đăng ký `IFileStorageService` (`CloudflareR2StorageService`) & `IRecyclerOnboardingService` vào DI Container và đồng bộ ApiTests. |
| 6 | **`INC-2026-TRD-06`** | [06_incident_report_trade_order_recycler_profile_pk_silent_catch_and_inquiry_chat_lock.md](./06_incident_report_trade_order_recycler_profile_pk_silent_catch_and_inquiry_chat_lock.md) | Backend & Frontend | `9ae1bf1d` (BE)<br>`197ece8` (FE) | **`dc0d2cdb`** (BE)<br>**`831632d`** (FE)<br>*(19/11/2026)* | `fix(trade): resolve order persistence and inquiry chat sync` & `test(trade): cover inquiry draft upgrade and recycler profile lookup` — Sửa tra cứu `RecyclerProfile` theo `UserId`, gỡ nuốt ngoại lệ im lặng và cho phép gửi tin nhắn chat inquiry trước khi chốt đơn. |
| 7 | **`INC-2026-GAM-07`** *(Đã ghi nhánh trong log)* | [07_incident_report_blog_gamification_merge_migration_collision_rbac_and_mock_profile_gaps.md](./07_incident_report_blog_gamification_merge_migration_collision_rbac_and_mock_profile_gaps.md) | Backend & Frontend | `9ae1bf1d` (BE)<br>`4ade7f5` (FE) | **`551a57e9`** & **`d79a169b`** (BE)<br>**`2bdfa01`** (FE)<br>*(19/11/2026)* | `feat(gam): integrate gamification, leaderboard cycles and blog modules` & `feat(gam): connect gamification, leaderboard and blog portals to api` — Đồng bộ migration sau merge, kiểm tra RBAC/controller và nối API thật thay dữ liệu giả. |
| 8 | **`INC-2026-RCP-08`** *(Đã ghi hash trong log)* | [08_incident_report_handover_receipt_dto_mismatch_404_and_silent_offline_fallback.md](./08_incident_report_handover_receipt_dto_mismatch_404_and_silent_offline_fallback.md) | Backend & Frontend | `72552443` (BE)<br>`4ade7f5` (FE) | **`ec287f0e`** (BE)<br>**`00e26e3`** (FE)<br>*(19/11/2026)* | `fix(trade): sync handover orders with receipts and R2 proof upload` & `fix(trade): add 30-day slots, sync receipts, and fix onboarding flow` — Đồng bộ DTO biên nhận, upload chứng từ R2 và xử lý lỗi API tường minh thay offline fallback im lặng. |
| 9 | **`INC-2026-ONB-09`** | [09_incident_report_recycler_onboarding_wrong_endpoint_msg_auth_01_and_offline_fallback.md](./09_incident_report_recycler_onboarding_wrong_endpoint_msg_auth_01_and_offline_fallback.md) | Frontend | `4ade7f5`<br>*(19/11/2026)* | **`00e26e3`**<br>*(19/11/2026)* | `fix(trade): add 30-day slots, sync receipts, and fix onboarding flow` — Sửa luồng nâng cấp `/join-recycler` gọi đúng `POST /api/v1/recycler-applications`, hiển thị lỗi đúng `MSG-AUTH-01` thay vì rơi vào offline demo giả. |
| 10 | **`INC-2026-AUTH-10`** | [10_incident_report_auth_me_infinite_loop_cookie_session_and_require_role.md](./10_incident_report_auth_me_infinite_loop_cookie_session_and_require_role.md) | Backend & Frontend | `e1c11dac` (BE)<br>`065e8b9` (FE) | **`31b6241c`** (BE)<br>**`a046f00`** & **`5f7903f`** (FE)<br>*(22–23/11/2026)* | `feat(trade): upgrade tree care, ai detector, chat and role sync` & `fix(infra): disable vite 8 dev websocket and client auto-reload` — Khắc phục vòng lặp vô hạn `/api/v1/auth/me` bằng `sessionIdentity` trong `RequireRole.tsx` + chống ghi trùng `localStorage` trong `session.ts`, đồng bộ `User.Role` từ DB và tắt auto-reload HMR Vite 8. |
| 11 | **`INC-2026-TST-11`** | [11_incident_report_unit_test_msbuild_msb3027_locked_webapi_binaries.md](./11_incident_report_unit_test_msbuild_msb3027_locked_webapi_binaries.md) | Backend | Môi trường Build Windows *(WebApi đang chạy)* | **`87b74b79`** *(23/11/2026)* & **`0de13a80`** *(24/11/2026)* | Lỗi khóa file `.dll` (`MSB3027` / `MSB3021`) khi chạy `Run All Tests` lúc `Ecolink.WebApi.exe` đang chạy nền. Khắc phục bằng quy trình chạy `dotnet test <Project>.csproj` độc lập và `-p:OutDir=...` tách thư mục build. |
| 12 | **`INC-2026-TST-12`** | [12_incident_report_flaky_unit_test_utc004_static_demo_seed_flag_collision.md](./12_incident_report_flaky_unit_test_utc004_static_demo_seed_flag_collision.md) | Backend | `57d982ac` / `27f3e9a9`<br>*(22–23/11/2026)* | **`31b6241c`** *(23/11/2026)* & **`0de13a80`** *(24/11/2026)* | `feat(trade): upgrade tree care...` (`isolate UTC004 geo coordinates`) — Cô lập tọa độ Đà Nẵng (`16.0544, 108.2022`) trong `ServicePackagePaymentTests.UTC004` tránh đụng độ cờ tĩnh `_demoSeedChecked` Hòa Lạc khi chạy song song. |
| 13 | **`INC-2026-TST-13`** *(Đã ghi hash trong log)* | [13_incident_report_unit_test_ai_category_normalizer_legacy_6_vs_10_yolo26_classes.md](./13_incident_report_unit_test_ai_category_normalizer_legacy_6_vs_10_yolo26_classes.md) | Backend | `27f3e9a9`<br>*(23/11/2026)* | **`87b74b79`**<br>*(23/11/2026)* | `fix(trade): harden validator null checks and sync ai tests` — Đồng bộ bộ `AiCategoryNormalizerTests` trong `PureLogicTests.cs` từ 6 lớp cũ sang 10 lớp YOLO26n. |
| 14 | **`INC-2026-VAL-14`** *(Đã ghi hash trong log)* | [14_incident_report_fluentvalidation_null_collection_500_exception_without_cascade_stop.md](./14_incident_report_fluentvalidation_null_collection_500_exception_without_cascade_stop.md) | Backend | `27f3e9a9`<br>*(23/11/2026)* | **`87b74b79`** *(23/11/2026)* & **`0de13a80`** *(24/11/2026)* | `fix(trade): harden validator null checks and sync ai tests` — Bổ sung `RuleLevelCascadeMode = CascadeMode.Stop` & kiểm tra `!= null` cho 5 FluentValidation Validators (tránh `ArgumentNullException` HTTP 500) và hoàn thiện nối validator ở `0de13a80`. |

---

## 2. Cách 1: Trình chiếu trực tiếp Git Diff của Commit Vá lỗi cho toàn bộ 14 Lỗi

> [!TIP]
> **Nguyên tắc trình bày trước Hội đồng (Nhanh, Trực quan, An toàn 100%):**
> - **Không** chạy `git checkout` trực tiếp trên thư mục code đang chạy Demo (tránh hỏng trạng thái server Backend/Frontend/DB đang chạy).
> - Mở một cửa sổ PowerShell riêng, chạy lệnh `git show <commit_hash> -- <đường_dẫn_file>` dưới đây để chiếu thẳng phần chênh lệch:
>   - Các dòng bắt đầu bằng dấu **`-` (màu đỏ)**: chính là **đoạn code gốc bị lỗi ở Round 1 (`Failed`)**.
>   - Các dòng bắt đầu bằng dấu **`+` (màu xanh)**: chính là **đoạn code đã được sửa ở Round 2 (`Passed`)**.
> - Có thể chạy `git show <commit_hash> --stat` trước nếu giảng viên muốn xem tổng quan toàn bộ danh sách file thay đổi trong commit đó.

---

### 2.1. `INC-2026-HND-01` — Bảng `handover_requests` chưa tồn tại trong PostgreSQL (`42P01`)
- **Tóm tắt lỗi (Round 1) → Cách vá (Round 2):** Entity `HandoverRequest` và `HandoverRequestService` đã có trong nhánh sau merge, nhưng EF Core Migration (`AddPendingModelChanges`) chưa được tạo/đồng bộ nên PostgreSQL thiếu bảng `handover_requests` (`42P01`). Commit `32aa1a84` bổ sung `ApplicationDbContext.cs` + file migration `20261109154426_AddPendingModelChanges.cs` và đồng bộ schema DB.
- **Lệnh PowerShell trình chiếu Diff:**
```powershell
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading
git show 32aa1a84 --stat
git show 32aa1a84 -- src/Ecolink.Backend/Ecolink.Infrastructure/Data/ApplicationDbContext.cs src/Ecolink.Backend/Ecolink.Infrastructure/Data/Migrations/20261109154426_AddPendingModelChanges.cs
```
- **Điểm cần chỉ cho Hội đồng trên màn hình Diff:** Khai báo bảng `handover_requests`, chuỗi sequence `seq_handover_request_code` và `DbSet` được thêm (`+` xanh) trong `ApplicationDbContext.cs` & migration `20261109154426_AddPendingModelChanges.cs`.

---

### 2.2. `INC-2026-GEO-02` — API `/api/v1/recyclers/nearby` lỗi 500 (PostGIS & Seeding Hòa Lạc)
- **Tóm tắt lỗi (Round 1) → Cách vá (Round 2):** Truy vấn không gian PostGIS trong `GeoMatchingService` và dữ liệu seed Hòa Lạc gặp lỗi khi đối chiếu entity `RecyclerProfile` với DB. Commit `32aa1a84` sửa cấu hình tọa độ `LocationPoint` (`geometry(Point, 4326)`) và cập nhật `GeoMatchingService.cs` cùng bộ test khám phá lân cận.
- **Lệnh PowerShell trình chiếu Diff:**
```powershell
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading
git show 32aa1a84 -- src/Ecolink.Backend/Ecolink.Infrastructure/Services/GeoMatchingService.cs src/Ecolink.Backend/Ecolink.Infrastructure/Data/Configurations/RecyclerProfileConfiguration.cs
```
- **Điểm cần chỉ cho Hội đồng trên màn hình Diff:** Thay đổi cấu hình cột tọa độ PostGIS trong `RecyclerProfileConfiguration.cs` (`.HasColumnType("geometry(Point, 4326)").IsRequired(false)`) và logic truy vấn khoảng cách / fallback trong `GeoMatchingService.cs`.

---

### 2.3. `INC-2026-DB-03` — Lỗi SQL `42703: column r.Id does not exist` trên `recycler_profiles`
- **Tóm tắt lỗi (Round 1) → Cách vá (Round 2):** `RecyclerProfile` kế thừa `BaseEntity` (có property `Id`) nhưng thiết kế bảng `recycler_profiles` dùng **Shared Primary Key 1-1** là `user_id`. EF Core tự động sinh SQL `SELECT r.Id ...` gây ngoại lệ `PostgresException 42703`. Commit `32aa1a84` thêm `builder.Ignore(r => r.Id);` trong `RecyclerProfileConfiguration.cs`.
- **Lệnh PowerShell trình chiếu Diff:**
```powershell
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading
git show 32aa1a84 -- src/Ecolink.Backend/Ecolink.Infrastructure/Data/Configurations/RecyclerProfileConfiguration.cs
```
- **Điểm cần chỉ cho Hội đồng trên màn hình Diff:** Dòng xanh `+ builder.Ignore(r => r.Id);` được chèn ngay trong `Configure()` giúp EF Core không map property `Id` thừa kế từ `BaseEntity` vào câu lệnh SQL PostgreSQL.

---

### 2.4. `INC-2026-HND-04` — Xung đột sinh mã bàn giao `HND-yyyyMMdd-NNNN` & lỗi EF Core InMemory Unit Test
- **Tóm tắt lỗi (Round 1) → Cách vá (Round 2):** Sau merge `aeabd7b3`, hàm sinh mã gọi thẳng `SELECT nextval('seq_handover_request_code')` qua raw SQL khiến toàn bộ Unit Test dùng EF Core `InMemory` bị crash (`InvalidOperationException`), đồng thời mất định dạng 4 chữ số `D4`. Commit `8519e7b1` (kết hợp refactor ở `2c26e166`) kiểm tra `_db.Database.IsRelational()` để tách nhánh PostgreSQL `nextval` với fallback đếm InMemory và chuẩn hóa `:D4`.
- **Lệnh PowerShell trình chiếu Diff:**
```powershell
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading
git show 8519e7b1 -- src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverRequestService.cs src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs
```
- **Điểm cần chỉ cho Hội đồng trên màn hình Diff:** So sánh khối đỏ `-` (gọi trực tiếp `SqlQuery<long>("SELECT nextval(...)")`) với khối xanh `+` (`if (_db.Database.IsRelational()) { ... nextval ... } else { CountAsync(...) }` kèm `${n:D4}`).

---

### 2.5. `INC-2026-DI-05` — Thiếu đăng ký DI (`IFileStorageService`, `IRecyclerOnboardingService`) & lệch ApiTests
- **Tóm tắt lỗi (Round 1) → Cách vá (Round 2):** Pipeline runtime / `EcoLink.ApiTests` văng `InvalidOperationException: Unable to resolve service for type ...` khi gọi controller Onboarding/Storage vì chưa đăng ký trong `DependencyInjection.cs`. Commit `2cc9b221` bổ sung đủ 2 đăng ký Scoped DI và đồng bộ kỳ vọng property DTO / message code trong `EcoLink.ApiTests`.
- **Lệnh PowerShell trình chiếu Diff:**
```powershell
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading
git show 2cc9b221 --stat
git show 2cc9b221 -- src/Ecolink.Backend/Ecolink.Infrastructure/DependencyInjection.cs src/Ecolink.Backend/EcoLink.ApiTests/OnboardingTests.cs
```
- **Điểm cần chỉ cho Hội đồng trên màn hình Diff:** Các dòng xanh `+` thêm `AddScoped<IFileStorageService, CloudflareR2StorageService>()` và `AddScoped<IRecyclerOnboardingService, RecyclerOnboardingService>()` trong `DependencyInjection.cs`, kèm assertion trong `OnboardingTests.cs`.

---

### 2.6. `INC-2026-TRD-06` — Lỗi khóa chính `RecyclerProfile`, nuốt ngoại lệ im lặng & khóa chat inquiry trước đơn hàng
- **Tóm tắt lỗi (Round 1) → Cách vá (Round 2):** Tạo đơn bàn giao/chat tìm `RecyclerProfile` sai trường khóa, khối `catch` nuốt lỗi không báo, và luồng chat inquiry chưa cho người dân gửi tin hỏi giá trước khi có `HandoverRequest`. Commit `dc0d2cdb` (Backend) và `831632d` (Frontend) sửa `HandoverTradeSyncHelper` / `TradeAndGamificationServices` / `NegotiationChatService` + đồng bộ `Chat.tsx` và `chatApi.ts`.
- **Lệnh PowerShell trình chiếu Diff:**
```powershell
# Phần Backend:
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading
git show dc0d2cdb --stat
git show dc0d2cdb -- src/Ecolink.Backend/Ecolink.Infrastructure/Services/TradeAndGamificationServices.cs src/Ecolink.Backend/Ecolink.Infrastructure/Services/NegotiationChatService.cs src/Ecolink.Backend/Ecolink.WebApi/Hubs/NegotiationChatHub.cs

# Phần Frontend:
cd c:\Projects\EcoLink\ecolink-web
git show 831632d --stat
git show 831632d -- src/lib/chatApi.ts src/lib/recyclerWorkflowApi.ts
```
- **Điểm cần chỉ cho Hội đồng trên màn hình Diff:** Sửa truy vấn hồ sơ cơ sở theo `UserId`, đồng bộ trạng thái đơn giữa `HandoverRequest` & `TradeOrder` trong `NegotiationChatHub` / `NegotiationChatService`, và cập nhật nâng cấp inquiry draft trên `chatApi.ts`.

---

### 2.7. `INC-2026-GAM-07` — Xung đột Migration Blog/Gamification sau merge, lỗ hổng RBAC & dữ liệu Mock Profile
- **Tóm tắt lỗi (Round 1) → Cách vá (Round 2):** Sau merge phân hệ Blog & Gamification/Leaderboard Cycles, migration va chạm với schema hiện có, một số endpoint thiếu gắn role chặt chẽ và UI Portal còn dùng mảng mock tĩnh. Cặp commit `551a57e9` + `d79a169b` (Backend) và `2bdfa01` (Frontend) giải quyết triệt để cấu hình EF, migration `AddLeaderboardCyclesModule`, service/controller RBAC và nối API thật (`blogApi.ts`, `gamificationApi.ts`).
- **Lệnh PowerShell trình chiếu Diff:**
```powershell
# Phần Backend (Migration & Service/RBAC fixes):
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading
git show 551a57e9 --stat
git show 551a57e9 -- src/Ecolink.Backend/Ecolink.Infrastructure/Data/Configurations/GamificationConfigurations.cs src/Ecolink.Backend/Ecolink.Infrastructure/Services/LeaderboardService.cs
git show d79a169b -- src/Ecolink.Backend/Ecolink.Infrastructure/Services/BlogPostService.cs src/Ecolink.Backend/Ecolink.Infrastructure/Services/PointsService.cs src/Ecolink.Backend/Ecolink.Infrastructure/Services/VirtualTreeService.cs

# Phần Frontend (Nối API thật):
cd c:\Projects\EcoLink\ecolink-web
git show 2bdfa01 --stat
git show 2bdfa01 -- src/lib/blogApi.ts src/lib/gamificationApi.ts
```
- **Điểm cần chỉ cho Hội đồng trên màn hình Diff:** File cấu hình `GamificationConfigurations.cs`, logic chu kỳ xếp hạng `LeaderboardService.cs`, kiểm tra phân quyền & xử lý ngoại lệ trong `BlogPostService.cs` / `PointsService.cs` / `VirtualTreeService.cs`, và thay thế dữ liệu giả bằng API client trong `blogApi.ts` / `gamificationApi.ts`.

---

### 2.8. `INC-2026-RCP-08` — Lệch DTO Biên nhận (HTTP 404), upload ảnh chứng từ R2 & Offline Fallback im lặng
- **Tóm tắt lỗi (Round 1) → Cách vá (Round 2):** Tạo & xem biên nhận thu gom giữa Recycler và Citizen bị lệch tên trường DTO/route (gây `404`), thiếu upload chứng từ lên Cloudflare R2, và FE tự fallback sang mock khi API báo lỗi khiến tester tưởng đã lưu thành công. Commit `ec287f0e` (Backend) và `00e26e3` (Frontend) đồng bộ DTO, controller upload R2 và hiển thị lỗi thật trên UI biên nhận.
- **Lệnh PowerShell trình chiếu Diff:**
```powershell
# Phần Backend:
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading
git show ec287f0e -- src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverRequestService.cs src/Ecolink.Backend/Ecolink.WebApi/Controllers/RecyclerReceiptController.cs

# Phần Frontend:
cd c:\Projects\EcoLink\ecolink-web
git show 00e26e3 -- src/lib/recyclerWorkflowApi.ts src/pages/recycler/Receipt.tsx src/pages/user/Receipt.tsx
```
- **Điểm cần chỉ cho Hội đồng trên màn hình Diff:** Endpoint & DTO đồng bộ biên nhận trong `RecyclerReceiptController.cs` + `HandoverReceiptService.cs`, cùng logic bỏ offline fallback im lặng trong `recyclerWorkflowApi.ts`, `recycler/Receipt.tsx` và `user/Receipt.tsx`.

---

### 2.9. `INC-2026-ONB-09` — Luồng Đăng ký Cơ sở gọi sai Endpoint & hiển thị sai `MSG-AUTH-01` do Offline Fallback
- **Tóm tắt lỗi (Round 1) → Cách vá (Round 2):** Form nâng cấp cơ sở thu mua trên Frontend (`JoinRecycler.tsx`) gọi sai route/payload khi nộp hồ sơ và tự nhảy vào trạng thái demo thành công giả khi backend trả lỗi validation/auth (`MSG-AUTH-01`). Commit `00e26e3` trên Frontend sửa chuẩn API nộp hồ sơ `/api/v1/recycler-applications` và trả thông báo lỗi thực tế về UI.
- **Lệnh PowerShell trình chiếu Diff:**
```powershell
cd c:\Projects\EcoLink\ecolink-web
git show 00e26e3 -- src/pages/JoinRecycler.tsx src/lib/recyclerWorkflowApi.ts
```
- **Điểm cần chỉ cho Hội đồng trên màn hình Diff:** Sửa endpoint/FormData gửi hồ sơ & chứng từ trong `JoinRecycler.tsx` và `recyclerWorkflowApi.ts` (bỏ nhánh catch giả lập thành công, hiển thị `ApiError` chính xác cho người dùng).

---

### 2.10. `INC-2026-AUTH-10` — Vòng lặp vô hạn `GET /api/v1/auth/me`, Cookie Session & `RequireRole` re-render liên tục
- **Tóm tắt lỗi (Round 1) → Cách vá (Round 2):**
  1. `RequireRole.tsx` đặt toàn bộ object `session` vào dependency của `useEffect`, trong khi `updateSession()` ghi lại `localStorage` dù nội dung không đổi làm thay đổi tham chiếu object → kích hoạt vòng lặp `ensureActiveAuthSession()` → `GET /api/v1/auth/me` liên tục.
  2. Ngoài ra, Vite 8 dev websocket tự reload khi chạy qua tunnel/proxy và Backend cần đọc `User.Role` mới từ DB trước khi JWT hết hạn.
  - **Vá lỗi:** Commit `5f7903f` (FE) dùng chuỗi ổn định `sessionIdentity` trong `RequireRole.tsx` + thêm `normalizeSessionObject` & kiểm tra `if (prevJson === nextJson) return` trong `session.ts`; commit `a046f00` (FE) tắt Vite dev websocket auto-reload; commit `31b6241c` (BE) đồng bộ role trực tiếp từ DB.
- **Lệnh PowerShell trình chiếu Diff:**
```powershell
# Phần Frontend (Sửa loop RequireRole & session.ts + Vite config):
cd c:\Projects\EcoLink\ecolink-web
git show 5f7903f -- src/components/RequireRole.tsx src/lib/session.ts
git show a046f00 -- vite.config.ts

# Phần Backend (Đồng bộ User.Role từ DB):
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading
git show 31b6241c -- src/Ecolink.Backend/Ecolink.Infrastructure/Services/ServicePackageService.cs
```
- **Điểm cần chỉ cho Hội đồng trên màn hình Diff:**
  - `RequireRole.tsx`: `- }, [session])` đổi thành `+ }, [sessionIdentity])`.
  - `session.ts`: Thêm `+ if (prevJson === nextJson) return` chống bắn sự kiện cập nhật session thừa.
  - `vite.config.ts`: Vô hiệu hóa HMR websocket khi phục vụ proxy/tunnel.

---

### 2.11. `INC-2026-TST-11` — Lỗi MSBuild `MSB3027` / `MSB3021` khóa tệp DLL khi chạy Test song song WebApi
- **Tóm tắt sự cố (Round 1) → Giải pháp vận hành (Round 2):** Trên Windows, khi `Ecolink.WebApi.exe` đang chạy nền (port `localhost:5000`), thao tác *Run All Tests* toàn Solution ép MSBuild ghi đè vào `Ecolink.WebApi/bin/Debug/net10.0/*.dll` đang bị khóa, gây lỗi `MSB3027` / `MSB3021` dù code không có lỗi. Cách khắc phục là tách lệnh test chạy thẳng `Ecolink.UnitTests.csproj` và dùng `-p:OutDir=...` khi muốn verify build WebApi song song (tài liệu hóa ở commit `87b74b79` & `0de13a80`).
- **Lệnh PowerShell trình chiếu Diff & Chứng minh vận hành an toàn:**
```powershell
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading

# 1. Chiếu Git Diff tài liệu quy chuẩn hóa lệnh chạy Unit Test & Báo cáo sự cố 11:
git show 87b74b79 -- src/Ecolink.Backend/Ecolink.UnitTests/README_UNIT_TESTS.md
git show 0de13a80 -- "documents/log/log document/11_incident_report_unit_test_msbuild_msb3027_locked_webapi_binaries.md"

# 2. Chạy minh họa trực tiếp ngay khi WebApi vẫn đang bật Demo (Không đụng DLL đang khóa):
dotnet test src\Ecolink.Backend\Ecolink.UnitTests\Ecolink.UnitTests.csproj -v minimal
dotnet build src\Ecolink.Backend\Ecolink.WebApi\Ecolink.WebApi.csproj -p:OutDir=C:\temp\ecolink-webapi-verify\
```
- **Điểm cần chỉ cho Hội đồng trên màn hình:** `UnitTests` hoàn tất sạch và `WebApi` biên dịch ra `C:\temp\ecolink-webapi-verify\` với `0 Error(s)` ngay cả khi tiến trình WebApi phục vụ demo đang chạy nền.

---

### 2.12. `INC-2026-TST-12` — Flaky Unit Test `ServicePackagePaymentTests.UTC004` do đụng độ cờ tĩnh `_demoSeedChecked`
- **Tóm tắt lỗi (Round 1) → Cách vá (Round 2):** Khi chạy song song toàn bộ test suite, test `ServicePackagePaymentTests.UTC004` sử dụng cùng tọa độ khu vực Hòa Lạc kích hoạt/đụng độ cờ tĩnh dùng chung `_demoSeedChecked`, gây rớt ngẫu nhiên (flaky) tùy thứ tự chạy test. Commit `31b6241c` cô lập tọa độ kiểm thử trong `UTC004` sang khu vực Đà Nẵng (và được tài liệu hóa / rà soát thêm ở `0de13a80`).
- **Lệnh PowerShell trình chiếu Diff:**
```powershell
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading
git show 31b6241c -- src/Ecolink.Backend/Ecolink.UnitTests/Module12_ServicePackages/ServicePackagePaymentTests.cs
git show 0de13a80 -- "documents/log/log document/12_incident_report_flaky_unit_test_utc004_static_demo_seed_flag_collision.md"
```
- **Điểm cần chỉ cho Hội đồng trên màn hình Diff:** Xem cặp dòng đỏ `-` / xanh `+` trong `ServicePackagePaymentTests.cs` (`isolate UTC004 geo coordinates`) đổi tọa độ test sang vùng độc lập tránh đụng cờ seed Hòa Lạc.

---

### 2.13. `INC-2026-TST-13` — `AiCategoryNormalizerTests` lỗi 14 case do giữ bộ 6 lớp cũ thay vì 10 lớp YOLO26n
- **Tóm tắt lỗi (Round 1) → Cách vá (Round 2):** Hệ thống AI đã nâng cấp model `yolo26n_waste_detect.pt` lên **10 lớp phế liệu chuẩn**, nhưng file test `PureLogicTests.cs` vẫn assert theo bảng ánh xạ 6 lớp cũ nên failed hàng loạt khi chạy `dotnet test`. Commit `87b74b79` đồng bộ `AiCategoryNormalizerTests` lên chuẩn 10 lớp YOLO26n.
- **Lệnh PowerShell trình chiếu Diff:**
```powershell
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading
git show 87b74b79 -- src/Ecolink.Backend/Ecolink.UnitTests/PureLogic/PureLogicTests.cs
```
- **Điểm cần chỉ cho Hội đồng trên màn hình Diff:** Phần diff trong `PureLogicTests.cs` cho thấy sự cập nhật ánh xạ lớp mới (`cardboard`, `metal`, `paper`, `plastic`, `glass`, ...) khớp 100% với `AiCategoryNormalizer` của model YOLO26n.

---

### 2.14. `INC-2026-VAL-14` — FluentValidation ném `ArgumentNullException` (HTTP 500) khi Collection/Input `null` do thiếu `CascadeMode.Stop`
- **Tóm tắt lỗi (Round 1) → Cách vá (Round 2):** Khi client gửi payload có collection/field `null`, FluentValidation vẫn chạy tiếp rule phía sau (như `.Must(x => x.Count ...)` / `.ForEach(...)`) dù đã có `.NotNull()`, gây ngoại lệ `ArgumentNullException` (HTTP `500`) thay vì trả HTTP `400 Bad Request`. Commit `87b74b79` bổ sung `RuleLevelCascadeMode = CascadeMode.Stop;` & điều kiện guard `!= null` trên 5 Validators + test xác minh; commit `0de13a80` hoàn thiện gắn FluentValidation cho toàn bộ các Controller còn lại.
- **Lệnh PowerShell trình chiếu Diff:**
```powershell
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading
git show 87b74b79 --stat
git show 87b74b79 -- src/Ecolink.Backend/Ecolink.Application/Validators/AiCurationValidators.cs src/Ecolink.Backend/Ecolink.Application/Validators/GovernanceValidators.cs src/Ecolink.Backend/Ecolink.Application/Validators/NegotiationChatValidators.cs src/Ecolink.Backend/Ecolink.Application/Validators/RecyclerValidators.cs src/Ecolink.Backend/Ecolink.Application/Validators/WasteCatalogValidators.cs
```
- **Điểm cần chỉ cho Hội đồng trên màn hình Diff:** Dòng xanh `+ RuleLevelCascadeMode = CascadeMode.Stop;` ở đầu constructor của cả 5 class Validator cùng điều kiện kiểm tra null trong bộ test `AiBlogGamificationValidatorTests.cs` / `GovernanceChatValidatorTests.cs` / `RecyclerReviewPackageWasteValidatorTests.cs`.

---

## 3. Bảng Lệnh Tra cứu Siêu nhanh (Cheat Sheet Copy-Paste khi Bảo vệ)

Khi giảng viên gọi tên bất kỳ mã sự cố nào, chỉ cần copy đúng 1 khối lệnh tương ứng:

```powershell
# === REPOSITORY BACKEND (c:\Projects\EcoLink\intelligent-waste-sorting-trading) ===
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading

# INC-01: HandoverRequests table missing (42P01)
git show 32aa1a84 -- src/Ecolink.Backend/Ecolink.Infrastructure/Data/ApplicationDbContext.cs

# INC-02: Nearby Recyclers PostGIS 500 & Hoa Lac seed
git show 32aa1a84 -- src/Ecolink.Backend/Ecolink.Infrastructure/Services/GeoMatchingService.cs

# INC-03: SQL 42703 column r.Id does not exist
git show 32aa1a84 -- src/Ecolink.Backend/Ecolink.Infrastructure/Data/Configurations/RecyclerProfileConfiguration.cs

# INC-04: Handover sequence D4 & InMemory fallback
git show 8519e7b1 -- src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverRequestService.cs src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs

# INC-05: Missing DI registrations & ApiTests
git show 2cc9b221 -- src/Ecolink.Backend/Ecolink.Infrastructure/DependencyInjection.cs

# INC-06 (BE): TradeOrder PK, silent catch & inquiry chat lock
git show dc0d2cdb -- src/Ecolink.Backend/Ecolink.Infrastructure/Services/TradeAndGamificationServices.cs src/Ecolink.Backend/Ecolink.Infrastructure/Services/NegotiationChatService.cs

# INC-07 (BE): Blog & Gamification merge migration & RBAC
git show 551a57e9 -- src/Ecolink.Backend/Ecolink.Infrastructure/Data/Configurations/GamificationConfigurations.cs src/Ecolink.Backend/Ecolink.Infrastructure/Services/LeaderboardService.cs

# INC-08 (BE): Handover receipt DTO mismatch & R2 proof upload
git show ec287f0e -- src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs src/Ecolink.Backend/Ecolink.WebApi/Controllers/RecyclerReceiptController.cs

# INC-10 (BE): User.Role DB sync for Pro package
git show 31b6241c -- src/Ecolink.Backend/Ecolink.Infrastructure/Services/ServicePackageService.cs

# INC-11 (Env): MSBuild MSB3027 locked DLL proof (Safe while WebApi is running)
dotnet test src\Ecolink.Backend\Ecolink.UnitTests\Ecolink.UnitTests.csproj -v minimal

# INC-12: Flaky Unit Test UTC004 static seed collision
git show 31b6241c -- src/Ecolink.Backend/Ecolink.UnitTests/Module12_ServicePackages/ServicePackagePaymentTests.cs

# INC-13: AiCategoryNormalizerTests 6 vs 10 YOLO26 classes
git show 87b74b79 -- src/Ecolink.Backend/Ecolink.UnitTests/PureLogic/PureLogicTests.cs

# INC-14: FluentValidation CascadeMode.Stop & null collection guards
git show 87b74b79 -- src/Ecolink.Backend/Ecolink.Application/Validators/AiCurationValidators.cs src/Ecolink.Backend/Ecolink.Application/Validators/GovernanceValidators.cs


# === REPOSITORY FRONTEND (c:\Projects\EcoLink\ecolink-web) ===
cd c:\Projects\EcoLink\ecolink-web

# INC-06 (FE): Inquiry draft & chat sync
git show 831632d -- src/lib/chatApi.ts src/lib/recyclerWorkflowApi.ts

# INC-07 (FE): Connect Blog, Leaderboard & Gamification portals to real API
git show 2bdfa01 -- src/lib/blogApi.ts src/lib/gamificationApi.ts

# INC-08 (FE) & INC-09 (FE): Receipt DTO sync, JoinRecycler endpoint & remove silent offline fallback
git show 00e26e3 -- src/pages/JoinRecycler.tsx src/lib/recyclerWorkflowApi.ts src/pages/recycler/Receipt.tsx src/pages/user/Receipt.tsx

# INC-10 (FE): Fix /auth/me infinite loop in RequireRole + session.ts + Vite 8 HMR reload
git show 5f7903f -- src/components/RequireRole.tsx src/lib/session.ts
git show a046f00 -- vite.config.ts
```
