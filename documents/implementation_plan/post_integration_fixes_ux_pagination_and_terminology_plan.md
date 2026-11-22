# KẾ HOẠCH GIẢI ĐÁP KỸ THUẬT & TRIỂN KHAI TỐI ƯU HỆ THỐNG (IMPLEMENTATION PLAN) - HẬU TÍCH HỢP 8 TRƯỜNG HỢP (`UX`, `GAMIFICATION`, `TERMINOLOGY`, `PAGINATION` & `IDE`)

**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform (`SEP490_G141`)  
**Phạm vi:** Liên phân hệ `GAM` (Gamification / EcoFact / Gifts / Leaderboard), `ONB` (Recycler Onboarding), `GOV` (Account Governance), `HND` (Handover & Receipts), `UI/UX Architecture` (`ecolink-web`)  
**Mục tiêu:** Giải đáp trực diện 8 câu hỏi kỹ thuật sau nghiệm thu giao diện thực tế, đồng thời lập kế hoạch sửa triệt để, chuẩn hóa thuật ngữ (`User/Recycler`), loại bỏ chuỗi kỹ thuật dư thừa, thiết kế kiến trúc **Tìm kiếm & Phân trang đồng bộ toàn hệ thống** theo nguyên tắc Clean Code / DRY / Single Source of Truth.  
**Tài liệu tham chiếu:**
- Quy chuẩn Khung Phản hồi API: [`00_api_overview_and_response_contract.md`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/api%20contact/00_api_overview_and_response_contract.md)
- Thiết kế API Gamification (`M-10`): [`13_module_gamification_and_rewards_api.md`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/api%20contact/13_module_gamification_and_rewards_api.md)
- Quy tắc Nghiệp vụ & Thông báo: [`business_rule_&_system_message.md`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/business_rule_%26_system_message.md)
- Nhật ký E2E Frontend-Backend: [`2026-11-19.md`](file:///c:/Projects/EcoLink/ecolink-web/docs/2026-11-19.md)

---

## TỔNG QUAN 8 TRƯỜNG HỢP & MA TRẬN HÀNH ĐỘNG

| STT | Trường hợp | Kết luận chẩn đoán nhanh | Trọng tâm xử lý (BE / DB / FE) | Mức ưu tiên |
| :-: | :--- | :--- | :--- | :-: |
| **1** | Ảnh quà tặng không hiển thị trên `/rewards` | Cột `gift_inventory.image_url` đã tồn tại nhưng 6 quà seed đang `NULL`; FE thiếu nút upload ảnh từ Staff | BE thêm API upload ảnh quà lên R2 + seed/cập nhật `image_url` từ 9 ảnh `/assets/rewards/`; FE thêm uploader & bộ chọn ảnh ở `staff/Gifts.tsx` | **P1** |
| **2** | Cây Ảo chưa lưu tiến trình/rate limit & cách vận hành EcoFact | Trang `/ecofacts` bị tách 2 tầng: cây hoạt họa trên chạy `localStorage` + cộng điểm giả; tầng dưới mới nối DB thật (`DailyTreeCareLimit = 3`); mã `EF-100000` bị trùng prefix GUID | Hợp nhất Cây Ảo trên-dưới về 1 nguồn sự thật trên PostgreSQL, bỏ `addSpendPoints()` cục bộ, sửa sinh mã `EF-` hết trùng, thêm UI mở khóa theo 6 cấp + modal đọc chi tiết | **P0** |
| **3** | Ai tham gia BXH, thi gì & có quà mỗi kỳ không? | Code hiện tại mới lọc `UserRole.User` (`BR-GAM-08` cũ) dù `Recycler` cũng được cộng điểm bàn giao/điểm danh; **CÓ** thưởng Top 1/2/3 (`500 / 300 / 150` điểm) nhưng FE đang ẩn | Mở rộng BE `LeaderboardService` áp dụng cho **cả Người dân (`User`) lẫn Người thu gom (`Recycler`)** (+ tính đúng `Area`/`TotalRecycledKg` từ `RecyclerProfile`, bổ sung `Role` & `RecentFinalizedCycles`); FE thêm bộ lọc vai trò, badge vai trò, Banner giải thưởng & Bảng vàng kỳ trước | **P1** |
| **4** | Nhiều tag chứng từ ở Staff nhưng khi đăng ký chỉ chọn ảnh/PDF | BE chỉ có 2 trường `LegalPermitUrl` và `BusinessPhotoUrl`, `JoinRecycler.tsx` gộp 1 input `multiple` (nhưng âm thầm bỏ file 3–5), còn Staff render trùng 2 tag text + 2 tag link | FE tách 2 ô upload rõ nghĩa (*Giấy phép pháp lý* & *Ảnh kho bãi*); Staff bỏ 2 tag chữ thừa, chỉ giữ 2 nút xem/preview tài liệu | **P1** |
| **5** | Loại bỏ nhãn `Nguồn: PostgreSQL + PostGIS` | Còn sót các badge/toast kỹ thuật nội bộ (`PostgreSQL`, `PostGIS`, `CSDL PostGIS`) trên giao diện người dùng & nhân viên | Dọn sạch toàn bộ nhãn CSDL kỹ thuật ở `staff/Applications.tsx`, `JoinRecycler.tsx` và các thông báo toast ở FE; cập nhật SSR test đi kèm | **P1** |
| **6** | Đổi thống nhất `User/Buyer` và `collector` $\rightarrow$ `recycler` | Còn sót nhãn `User/Buyer`, prefix mã `B-` (và trùng `B-9999`), dữ liệu offline `@thugom.vn`, 11 demo `collector*`, `AuthController`, `Program.cs` & test suites | Đổi toàn diện FE, BE seeder, migration SQL cập nhật demo users theo UUID, sửa collision `R-9999`, và đồng bộ test suites | **P0** |
| **7** | Thiết kế tìm kiếm & phân trang cho các thành phần thiếu | BE có 4 kiểu Paged DTO khác nhau và nhiều endpoint thiếu paging/search; FE mới chỉ có `admin/Reports.tsx` có nút chuyển trang | Chuẩn hóa BE (`BaseFilterQuery` + `ToPagedResultAsync`), tạo bộ hook + `<SearchFilterBar />` + `<PaginationBar />` dùng chung cho 15+ trang FE | **P0** |
| **8** | Thông báo lỗi ở Antigravity IDE là gì? | Cảnh báo bảo vệ ghi đè *File Save Conflict* của VS Code / Antigravity khi tab đang có sửa dở (dirty buffer) trong lúc disk vừa đổi (do AI / Git) | Hướng dẫn xử lý an toàn qua nút `Compare` / `Revert File` và quy trình tránh xung đột | **Đã giải đáp** |

---

## 1. TRƯỜNG HỢP 1: ẢNH QUÀ TẶNG KHÔNG HIỂN THỊ TRÊN `/rewards`

### 1.1. Giải đáp nguyên nhân
> **Trả lời:** **Đúng một phần.** Trong CSDL PostgreSQL, bảng `gift_inventory` (định nghĩa tại [`GamificationEntities.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/GamificationEntities.cs#L59-L80) và migration [`20261116150439_GamificationRewards.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Data/Migrations/20261116150439_GamificationRewards.cs#L85-L105)) **đã có sẵn cột `image_url`** (`VARCHAR`, cho phép `NULL`). Tuy nhiên, ảnh không hiện là do **3 nguyên nhân kết hợp**:
1. **Dữ liệu seed trong PostgreSQL đang để `image_url = NULL`:** Theo kết quả đối chiếu tại [`2026-11-19.md`](file:///c:/Projects/EcoLink/ecolink-web/docs/2026-11-19.md#L204), bảng `gift_inventory` hiện có `6` món quà đang mở nhưng cột `image_url` chưa được gán giá trị (và Backend chưa có seeder/migration gán ảnh mặc định cho 6 món này).
2. **Hành vi fallback của Frontend (`Rewards.tsx`):** Trong [`Rewards.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Rewards.tsx#L64-L80), component `<Thumb img={g.imageUrl} ... />` được lập trình để hiển thị biểu tượng vector `<RewardIcon />` (hình hộp quà hoặc ví voucher xám) khi `img` là `null` hoặc chuỗi rỗng — mặc dù thư mục `ecolink-web/public/assets/rewards/` **đã có sẵn 9 ảnh sản phẩm thật** (`bag.jpg`, `compactor.jpg`, `coupon10.jpg`, `glasses.jpg`, `grand-cookware.jpg`, `grand-scooter.jpg`, `grand-trip.jpg`, `kit.jpg`, `seedling.jpg`).
3. **Thiếu giao diện Upload ảnh ở trang Staff [`staff/Gifts.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/Gifts.tsx):** Staff khi tạo/sửa quà hiện chỉ thấy ô nhập text URL thô (không có nút tải ảnh từ máy tính lên Cloudflare R2 hay nút chọn nhanh từ thư viện ảnh có sẵn), và Backend [`StaffGamificationController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/StaffGamificationController.cs#L31-L66) cũng **chưa có endpoint upload ảnh quà**.

### 1.2. Phương án thiết kế & Tối ưu (Clean Code)

#### A. Backend (`.NET 10`)
1. **Endpoint Upload ảnh quà cho Staff (`STT` mới thuộc `M-10`):**
   - Bổ sung trên [`StaffGamificationController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/StaffGamificationController.cs):
     - `POST /api/v1/staff/gifts/upload-image` (`[HasPermission(StaffPermissions.ManageInventory)]`, `[Consumes("multipart/form-data")]`).
   - **Tái sử dụng (`DRY`):** Dùng lại [`IFileStorageService.UploadRecyclerDocumentAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Interfaces/IRecyclerOnboardingService.cs#L46-L56) với `folderPrefix: "gamification-gifts"` (hoặc trích xuất method chung `UploadPublicAssetAsync(Stream, fileName, contentType, folderPrefix, maxBytes, allowedExts, ct)` trong `IFileStorageService` để không mang tên `RecyclerDocument` khi dùng cho module khác).
   - **Validation đồng nhất với `GamErrors.CleanImageUrl`** ([`GamificationCommon.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GamificationCommon.cs#L29-L44)): chỉ chấp nhận `.jpg`, `.jpeg`, `.png`, `.webp`, dung lượng tối đa `5 MB`.
2. **Idempotent Seeder cho `gift_inventory.image_url`:**
   - Thêm hàm đảm bảo seed / backfill ảnh mặc định (chỉ điền khi `string.IsNullOrWhiteSpace(gift.ImageUrl)` để không ghi đè ảnh Staff tự upload):
     - `"Voucher ưu đãi phí dịch vụ xanh 10%"` $\rightarrow$ `/assets/rewards/coupon10.jpg`
     - `"Bộ kit ươm mầm cây xanh mini"` $\rightarrow$ `/assets/rewards/kit.jpg` (hoặc `/assets/rewards/seedling.jpg`)
     - `"Túi vải canvas tái chế EcoLink"` $\rightarrow$ `/assets/rewards/bag.jpg`
     - Các quà khác ánh xạ tương ứng với `compactor.jpg`, `glasses.jpg`, `grand-cookware.jpg`, `grand-scooter.jpg`, `grand-trip.jpg`, `seedling.jpg`.
   - Lưu ý: [`GamErrors.CleanImageUrl`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GamificationCommon.cs#L36-L38) **đã cho phép sẵn đường dẫn tương đối bắt đầu bằng `/assets/`** với đuôi `.jpg/.png/.webp`, nên cả URL Cloudflare R2 (`https://...`) lẫn ảnh nội bộ `/assets/rewards/...` đều hợp lệ 100%.

#### B. Frontend (`ecolink-web`)
1. **Nâng cấp [`staff/Gifts.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/Gifts.tsx) & [`gamificationApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/gamificationApi.ts):**
   - Thêm hàm `uploadStaffGiftImage(file: File)` gọi `POST /api/v1/staff/gifts/upload-image`.
   - Trong form Tạo / Sửa quà ở `staff/Gifts.tsx`, cung cấp:
     - **Nút "Tải ảnh từ máy (JPG/PNG/WEBP)"** tự động upload lên R2 và điền `imageUrl`.
     - **Bộ chọn nhanh ảnh mẫu hệ thống** (hiển thị thumbnail 9 ảnh từ `public/assets/rewards/`) + **ô URL tùy chỉnh** + **khung xem trước (Preview) tức thời**.
2. **An toàn hiển thị ở [`Rewards.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Rewards.tsx):**
   - Ở component `<Thumb />`, gắn thêm `onError` state fallback sang `<RewardIcon />` nếu link ảnh R2 hoặc đường dẫn bên ngoài bị gãy, tránh vỡ khung ảnh.

---

## 2. TRƯỜNG HỢP 2: TIẾN TRÌNH & RATE LIMIT TRỒNG CÂY + CƠ CHẾ ECOFACT

### 2.1. Giải đáp chi tiết: Hiện trạng 2 tầng tách rời & Cơ chế EcoFact

#### A. Vì sao phần trồng cây chưa lưu tiến trình và chưa có Rate Limit?
Kiểm tra trực tiếp [`EcoFactsPage.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/EcoFactsPage.tsx#L25-L808) cho thấy trang này đang **vận hành 2 hệ thống song song nhưng bị ngắt kết nối**:
1. **Khối giao diện phía trên (Cây Ảo 70 frame WebP + Lượng nước + Tiến trình EXP + nút `Tưới nước` / `Bón phân`):**
   - Toàn bộ state (`stage`, `exp`, `water`, `coins`, `harvestReady`) chỉ lưu trong React state và `localStorage("ecolink:virtual-tree-v1")` ([`EcoFactsPage.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/EcoFactsPage.tsx#L47)).
   - Khi bấm **`Tưới nước`** hoặc **`Bón phân`**, [`handleWater`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/EcoFactsPage.tsx#L330-L360) và `handleFertilize` chỉ cộng số liệu cục bộ và gọi `triggerCoinReward(amount)` $\rightarrow$ `addSpendPoints(amount)` trong `src/data/rewards.ts`.
   - **Hệ quả:** Không gọi Backend API, không lưu PostgreSQL, không bị giới hạn lượt/ngày (`DailyTreeCareLimit`), và `+3 xu` / `+6 xu` chỉ là điểm giả ở `localStorage` (khi đổi trình duyệt hoặc reload theo ví thật Backend sẽ biến mất hoặc lệch số liệu).
2. **Khối bên dưới (`"Cấp độ chính thức: Cấp 1 — Hạt mầm"` và nút `Nâng lên Cấp 2 (-100 điểm)`):**
   - Khối này mới là phần nối thật với PostgreSQL qua [`VirtualTreeService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/VirtualTreeService.cs#L1-L194) (`GET /api/v1/gamification/tree` và `POST /api/v1/gamification/tree/upgrade`).
   - Backend **ĐÃ có sẵn đầy đủ**: lưu `virtual_tree_progress` (`TreeLevel`, `GrowthPoints`, `DailyWaterCount`, `LastWaterDate`), reset lượt theo ngày ICT (UTC+7), giới hạn `DailyTreeCareLimit` (mặc định `3 lượt/ngày`), chỉ cho `Citizen/User` dùng (`BR-GAM-08`), và tăng cấp một chiều (`BR-GAM-10`).

#### B. Phần kiến thức EcoFact được lưu trữ, lên tiến trình và trình bày như thế nào?
1. **Lưu trữ (Database):**
   - Lưu trong bảng `eco_facts` ([`GamificationEntities.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/GamificationEntities.cs#L102-L115)): gồm `fact_id` (`Guid`), `title` (tối đa 180 ký tự), `summary` (tóm tắt 1–2 câu), `content` (bài viết chi tiết tối đa 4000 ký tự), `category_code` (phân loại rác/chủ đề), `source_reference` (nguồn trích dẫn khoa học), `unlock_tree_level` (`1..6`), `status` (`ACTIVE`/`INACTIVE`), `created_by_staff_id`.
   - Nhân viên có quyền `MANAGE_ECO_FACTS` tạo/sửa/ẩn/hiện EcoFact tại [`staff/EcoFacts.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/EcoFacts.tsx) (`S-09`).
2. **Cơ chế lên tiến trình & mở khóa:**
   - Trong [`VirtualTreeService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/VirtualTreeService.cs#L36-L110) và [`GamificationConfigService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GamificationConfigService.cs):
     - Cấp cây chạy từ **Cấp 1 (`Hạt mầm`)** đến **Cấp 6**, mỗi cấp cấu hình ngưỡng điểm đầu tư tại `tree_level_thresholds` (`RequiredPoints`, ví dụ Cấp 2 tốn `100` điểm).
     - Khi User dùng điểm tích được (từ giao rác / điểm danh) để chăm cây và lên **Cấp `L`**, tất cả các EcoFact đang `ACTIVE` có `unlock_tree_level <= L` sẽ tự động được **mở khóa** cho User đó.
   - Bổ sung lỗi quan sát từ ảnh chụp màn hình: Cả 2 thẻ EcoFact đều hiện trùng mã **`EF-100000`** vì [`EcoFactService.ToDto`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/EcoFactService.cs#L171) sinh mã bằng `"EF-" + f.Id.ToString("N")[..6]` trong khi các bản ghi seed đều có UUID bắt đầu bằng `10000000-...`.
3. **Cách trình bày hiện tại vs Chuẩn cần đạt:**
   - Hiện tại `EcoFactsPage.tsx` mới chỉ render thẻ tóm tắt (`code`, `unlockTreeLevel`, `title`, `summary`) ở thanh đáy — **chưa cho bấm xem bài phân tích `content` & `sourceReference`**, và **chưa cho xem lộ trình các EcoFact ở Mốc Cấp cao hơn (đang khóa)** để tạo động lực nuôi cây.

### 2.2. Phương án xử lý & Clean Architecture

#### A. Hợp nhất 2 tầng Cây Ảo về 1 Nguồn sự thật (PostgreSQL)
Có 2 hướng nghiệp vụ; đề xuất **Hướng thống nhất chuẩn** kết hợp được cả animation 70-frame đẹp mắt lẫn kỷ luật DB/Rate Limit:
1. **Mở rộng nhẹ `VirtualTreeProgress` & Endpoint chăm sóc ngày (`POST /api/v1/gamification/tree/care`):**
   - Bổ sung trên `VirtualTreeProgress`:
     - `HydrationPercent` (`int`, `0..100`, mặc định `80`), `CareExp` (`int`, mặc định `0`), `LastCareActionAt` (`DateTime?`).
     - Mỗi ngày (`LastWaterDate` theo ICT `GamTime.TodayIct()`), User được chăm sóc tối đa **`DailyTreeCareLimit` (mặc định `3` lượt/ngày)** — áp dụng chung hoặc tách `Water` (`+30%` nước, `+15` CareExp) và `Fertilize` (`+25` CareExp, chỉ dùng khi độ ẩm > 0).
     - Điểm thưởng nhỏ từ chăm sóc (nếu giữ cơ chế thưởng `+2`/`+3` điểm) PHẢI được Backend cộng thông qua `IPointsService.CreditAsync(..., PointActionType.TreeCareBonus, ..., idempotencyKey: $"TREE_CARE:{userId}:{todayIct}:{nextDailyCount}")` — bảo đảm chống spam F5/Postman và tuân thủ `DailyTreeCareLimit`.
   - **Tính cấp cây đồng bộ:** Hình ảnh 70-frame bên trái ánh xạ trực tiếp từ **`TreeLevel` (`1..6`) và `CareExp` thật trả về từ API** (không đọc từ `localStorage` khi đã đăng nhập). Khi đủ EXP hoặc người dùng bấm **`Nâng cấp cây`** (`POST /api/v1/gamification/tree/upgrade`, tốn `RequiredPoints`), `TreeLevel` tăng lên, frame cây tiến sang giai đoạn mới và **mở khóa toàn bộ EcoFact mốc cấp mới** kèm thông báo chúc mừng.
2. **Sửa lỗi trùng mã `EF-100000` trong `EcoFactService.ToDto`:**
   - Cách nhanh gọn không phá schema: đổi `FormatCode(Guid id)` dùng tổ hợp tránh prefix seed (`"EF-" + hex[..2] + hex[^4..]`, ví dụ `10000000-...-000000000001` thành `EF-100001`, `...000000000002` thành `EF-100002`), hoặc chuẩn hơn: thêm cột `code` (`VARCHAR(16)`, unique) vào `eco_facts` cho Staff tự định nghĩa hoặc auto-increment `EF-001`, `EF-002`.

#### B. Nâng cấp trình bày EcoFact (`EcoFactsPage.tsx`)
- Hiển thị đầy đủ **Tủ Kiến thức EcoFact 6 Mốc Cấp (`Lộ trình học tái chế`)**:
  - **Nhóm đã mở khóa (`Unlocked`):** Hiện đầy đủ tiêu đề, chủ đề (`Nhựa`, `3R`, `Kim loại`...), nút **"Đọc chi tiết"** mở Modal hiển thị `content` định dạng rõ ràng + `sourceReference`.
  - **Nhóm chưa mở khóa (`Locked`):** Hiện trạng thái mờ có icon khóa + badge `"Mở khóa khi Cây đạt Cấp X"` (chỉ trả `Title`/`UnlockTreeLevel`/`CategoryCode` ở API hoặc che `Content` để kích thích tương tác).
- Xóa bỏ hoàn toàn việc gọi `addSpendPoints()` cục bộ khi đã đăng nhập (`hasLiveBackendAuth()`); đồng bộ số dư điểm góc trên khung cây trực tiếp với `wallet.spendablePoints`.

---

## 3. TRƯỜNG HỢP 3: ĐỐI TƯỢNG, TIÊU CHÍ & GIẢI THƯỞNG BẢNG XẾP HẠNG (NGƯỜI DÂN & NGƯỜI THU GOM)

### 3.1. Giải đáp chi tiết (Đối chiếu Hiện trạng Code & Quy định Cập nhật)

| Câu hỏi | Quy định mục tiêu (Đã điều chỉnh cho cả `User` & `Recycler`) | Hiện trạng Code & Điểm cần điều chỉnh |
| :--- | :--- | :--- |
| **1. Áp dụng cho ai?** | **Áp dụng cho cả Người dân (`UserRole.User`) lẫn Người thu gom / Cơ sở thu mua (`UserRole.Recycler`) đang ở trạng thái `Active`.** Nhân viên (`Staff`) và Quản trị viên (`Admin`) vẫn không tham gia xếp hạng. | Hiện tại [`LeaderboardService.cs:348`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/LeaderboardService.cs#L348) mới lọc `u.Role == UserRole.User` theo [`BR-GAM-08`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/business_rule_%26_system_message.md#L137) cũ (và test [`UTC005`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Module10_GamificationAndBlog/GamificationAndBlogUnitTests.cs#L187-L238)). Cần mở rộng sang `u.Role == UserRole.User \|\| u.Role == UserRole.Recycler`. |
| **2. Cạnh tranh về gì?** | **Điểm Xếp Hạng (Ranking Points)** tích lũy trong chu kỳ hiện tại (`PeriodEarned`, gồm: điểm hoàn tất bàn giao `HandoverReward` — vốn **đã cộng cho cả User và Recycler** theo [`HandoverReceiptService.cs:347-351`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs#L347-L351) & [`PointsService.cs:169-172`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PointsService.cs#L169-L172), cộng điểm danh hằng ngày `DailyCheckin` mà `Recycler` cũng đã được phép dùng; **loại trừ** `LeaderboardBonus` kỳ trước). Nếu chu kỳ hiện tại chưa phát sinh điểm mới, fallback theo `LifetimeEarned`. **Đổi quà / tiêu điểm KHÔNG làm tụt hạng (`BR-GAM-07`)**. Đồng điểm xét lần lượt: `LifetimeEarned ↓` $\rightarrow$ `TotalRecycledKg ↓` $\rightarrow$ `FullName ↑`. | [`LeaderboardService.cs:371-414`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/LeaderboardService.cs#L371-L414) hiện chỉ quét `HandoverRequests` theo `h.UserId` và chưa join `RecyclerProfiles`, nên nếu bật `Recycler` ngay mà không sửa câu query thì `Recycler` sẽ bị `TotalRecycledKg = 0` và thiếu địa bàn `Area`. |
| **3. Có phần quà mỗi kỳ không?** | **CÓ.** Mỗi chu kỳ (`Weekly` / `Monthly` / `Quarterly`) có **Giải thưởng Điểm Xanh cho Top 3** do Admin cấu hình trong `leaderboard_cycles` (mặc định: **Top 1 `+500` điểm**, **Top 2 `+300` điểm**, **Top 3 `+150` điểm**). Khi chốt kỳ (`FinalizeCycleAsync`), Backend cộng điểm thưởng idempotent (`PointActionType.LeaderboardBonus`) vào ví của Top 1–3 (dù người thắng là `User` hay `Recycler`) và lưu `WinnersSnapshotJson`. | [`LeaderboardService.cs:199-269`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/LeaderboardService.cs#L199-L269), [`GamificationDtos.cs:296-341`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/GamificationDtos.cs#L296-L341) |

> [!NOTE]
> **Vì sao giao diện BXH hiện chưa hiện giải thưởng và lịch sử vinh danh?**  
> API `GET /api/v1/gamification/leaderboard` **đã trả về** `activeCycle.top1BonusPoints`, `top2BonusPoints`, `top3BonusPoints`, `startsAt`, `endsAt`, nhưng [`LeaderboardViewDto`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/GamificationDtos.cs#L336-L341) chưa kèm danh sách các kỳ đã chốt (`RecentFinalizedCycles`), và component [`src/components/Leaderboard.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/Leaderboard.tsx#L64-L80) hiện mới chỉ dùng `activeCycle.name` để in tiêu đề chứ chưa hiển thị giải thưởng Top 1/2/3 hay hạn chu kỳ.

### 3.2. Phương án xử lý & Tối ưu (Backend + Business Rule + Frontend)

#### A. Backend & Nghiệp vụ (`.NET 10` & Tài liệu `BR-GAM-08`)
1. **Cập nhật [`BR-GAM-08`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/business_rule_%26_system_message.md#L137) & [`13_module_gamification_and_rewards_api.md`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/api%20contact/13_module_gamification_and_rewards_api.md):**
   - Ghi nhận rõ: *Kiếm điểm, điểm danh hằng ngày, đổi quà và **Bảng xếp hạng đóng góp** áp dụng cho cả **User** và **Recycler**; chỉ Cây ảo & mở khóa Eco Facts giữ riêng cho vai trò User.*
2. **Mở rộng [`LeaderboardService.ComputeRankedEntriesAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/LeaderboardService.cs#L341-L499):**
   - Sửa bộ lọc người tham gia thành `u.Status == UserStatus.Active && (u.Role == UserRole.User || u.Role == UserRole.Recycler)`.
   - Hỗ trợ tham số lọc tùy chọn `string? role = null` (`all` | `user` | `recycler`) ở `GetLeaderboardViewAsync` & `GET /api/v1/gamification/leaderboard` để giao diện có thể xem **Chung cả cộng đồng** hoặc **Lọc riêng từng vai trò** (`Người dân` vs `Người thu gom`). Lưu ý `FinalizeCycleAsync` luôn xét trao giải trên bảng xếp hạng chính (`role = all`) hoặc cấu hình rõ theo chu kỳ.
   - **Tính đúng sản lượng tái chế & thông tin hiển thị cho `Recycler`:**
     - Truy vấn `RecyclerProfiles` cho danh sách `userIds` có role `Recycler` (lấy `FacilityName`, `BusinessAddress`).
     - Truy vấn `HandoverRequests` đã `Completed` theo điều kiện **`userIds.Contains(h.UserId) || userIds.Contains(h.RecyclerId)`**: cộng `receipt.TotalWeightKg` cho cả `h.UserId` (phía Người dân giao) lẫn `h.RecyclerId` (phía Cơ sở đã thu gom thành công), tránh đếm trùng nếu tự giao dịch.
     - Chuẩn hóa hiển thị cho `Recycler`: ưu tiên `FullName = profile?.FacilityName` (fallback `u.FullName`) và `Area = FormatArea(profile?.BusinessAddress)` (fallback `Cộng đồng Xanh EcoLink`).
3. **Bổ sung trường DTO trong [`TradeAndGamificationDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/TradeAndGamificationDtos.cs#L166-L179) & [`GamificationDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/GamificationDtos.cs#L296-L341):**
   - Thêm `public string Role { get; set; } = "User";` (`"User"` hoặc `"Recycler"`, thống nhất không dùng `"Collector"`) vào `LeaderboardEntryDto` và `LeaderboardWinnerDto`.
   - Thêm `public List<LeaderboardCycleDto> RecentFinalizedCycles { get; set; } = new();` vào `LeaderboardViewDto` (lấy tối đa `3` chu kỳ có `Status == Finalized` gần nhất) để Frontend render Bảng vàng kỳ trước chỉ trong 1 API call.
4. **Cập nhật Unit Tests & Tối ưu vận hành:**
   - Cập nhật [`UTC005_GetLeaderboardView_ShouldRankByPeriodEarnedWithoutRedeemPenalty`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Module10_GamificationAndBlog/GamificationAndBlogUnitTests.cs#L187-L238) để assert **cả `User` và `Recycler`** đều xuất hiện đúng thứ hạng, đúng `Role`, `Area`, `TotalRecycledKg`; bổ sung test `FinalizeCycleAsync` trao thưởng cho `Recycler` khi đứng Top 3 và test bộ lọc `role=user` / `role=recycler`.
   - Bổ sung cảnh báo trên `admin/Leaderboard.tsx` khi chu kỳ đang Active đã quá `EndsAt` để Admin kịp chốt thưởng kỳ.

#### B. Frontend ([`src/components/Leaderboard.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/Leaderboard.tsx) & [`src/pages/Leaderboard.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Leaderboard.tsx) & [`gamificationApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/gamificationApi.ts))
1. **Tab lọc vai trò & Badge phân biệt:**
   - Thêm nhóm pill lọc ở đầu BXH: **`Tất cả`** | **`Người dân`** | **`Người thu gom`**.
   - Gắn pill vai trò gọn cạnh tên thành viên (`Người dân` màu xanh lá nhạt / `Cơ sở thu mua` màu xanh dương nhạt) và hiển thị thêm `TotalRecycledKg` (`kg đã thu gom/tái chế`).
2. **Dải thông tin Chu kỳ & Cơ cấu giải thưởng:**
   - Ngay dưới header `TOP N ĐIỂM XANH · {cycle.name}`, hiển thị thời gian chu kỳ (`dd/MM/yyyy – dd/MM/yyyy` + đếm ngược `Còn X ngày`).
   - Gắn badge phần thưởng ngay dưới mỗi cột Top 1 / Top 2 / Top 3 trên bục podium: `🏆 Thưởng chốt kỳ: +500 điểm`, `🥈 +300 điểm`, `🥉 +150 điểm` (đọc động từ `activeCycle.top1BonusPoints / top2BonusPoints / top3BonusPoints`).
3. **Khối "Bảng vàng các kỳ trước" (`recentFinalizedCycles`):**
   - Hiển thị danh sách Top 3 người thắng giải của các kỳ đã chốt gần nhất kèm role badge và số điểm thưởng đã cộng vào ví.

---

## 4. TRƯỜNG HỢP 4: TAG GIẤY CHỨNG NHẬN ĐĂNG KÝ RECYCLER BỊ TRÙNG & MƠ HỒ

### 4.1. Giải đáp bản chất hiện trạng
Nhìn vào ảnh màn hình `/staff/applications`, ta thấy 1 hồ sơ có tới 4 tag (`Giấy phép kinh doanh / pháp lý`, `Ảnh mặt tiền kho bãi`, `Mở file giấy phép ↗`, `Xem ảnh kho bãi ↗`), nhưng bên [`JoinRecycler.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/JoinRecycler.tsx#L391-L416) người đăng ký chỉ thấy 1 vùng chọn file chung (`multiple`, tối đa 5 tệp). Nguyên nhân là sự **lệch pha giữa FE và BE**:
1. **Backend [`ApplyRecyclerUpgradeRequest`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/RecyclerOnboardingDtos.cs#L6-L18) chỉ thiết kế đúng 2 trường bắt buộc theo quy chuẩn KYB:**
   - `LegalPermitUrl`: Giấy phép kinh doanh / Chứng từ pháp lý.
   - `BusinessPhotoUrl`: Ảnh thực tế mặt tiền kho bãi / cân điện tử.
2. **Ở trang đăng ký ([`recyclerWorkflowApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/recyclerWorkflowApi.ts#L603-L634)):**
   - Dù form cho chọn tới `5` tệp (`slice(0, 5)`), hàm `submitRecyclerOnboarding` thực tế **chỉ upload `documents[0]` làm `legalPermitUrl` và `documents[1]` làm `businessPhotoUrl`**. Tệp thứ 3–5 bị bỏ qua hoàn toàn; ngược lại nếu người dùng chỉ chọn 1 tệp thì `businessPhotoUrl` lại tự lấy trùng URL của `legalPermitUrl`!
3. **Ở trang Staff thẩm định ([`recyclerWorkflowApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/recyclerWorkflowApi.ts#L934-L937) & [`staff/Applications.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/Applications.tsx#L189-L222)):**
   - `mapDtoToStaffView` tạo sẵn 2 chuỗi nhãn tĩnh trong mảng `docs[]` (`"Giấy phép kinh doanh / pháp lý"`, `"Ảnh mặt tiền kho bãi"`).
   - Sau đó `Applications.tsx` render **cả `a.docs` (2 pill xám không bấm được) LẪN 2 link xanh (`Mở file giấy phép ↗`, `Xem ảnh kho bãi ↗`)**, khiến cùng 2 file bị hiện thành **4 tag dư thừa**.

### 4.2. Phương án xử lý & Clean UX
1. **Tách rõ 2 ô upload đúng nghĩa tại [`JoinRecycler.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/JoinRecycler.tsx):**
   - **Mục 1 (Bắt buộc):** *Giấy phép kinh doanh / Chứng từ thu gom* (`accept="image/*,application/pdf"`, 1 tệp) $\rightarrow$ gán vào `legalPermitFile` $\rightarrow$ `legalPermitUrl`.
   - **Mục 2 (Bắt buộc):** *Ảnh thực tế kho bãi / bảng hiệu / cân thu mua* (`accept="image/*"`, 1 tệp) $\rightarrow$ gán vào `businessPhotoFile` $\rightarrow$ `businessPhotoUrl`.
   - Hiển thị ngay tên tệp + dung lượng + thumbnail preview nhỏ khi vừa chọn; loại bỏ tình trạng cho chọn 5 tệp rồi âm thầm bỏ tệp 3–5 hoặc gán nhầm ảnh kho bãi vào giấy phép.
   - *(Mở rộng tùy chọn nếu cần lưu >2 file sau này: chỉ làm khi bổ sung thêm cột `additional_document_urls (jsonb)` vào `recycler_profiles` & DTO; hiện tại 2 slot tách biệt đã đáp ứng đúng nghiệp vụ KYB).*
2. **Dọn sạch hiển thị trên [`staff/Applications.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/Applications.tsx) & `recyclerWorkflowApi.ts`:**
   - **Không render cả 2 loại tag trùng lặp:** Khi hồ sơ có URL thật (`legalPermitUrl` / `businessPhotoUrl`), **chỉ hiển thị 2 nút hành động rõ ràng** (hoặc thẻ preview ảnh/PDF trực tiếp kèm link mở tab mới):
     - `[📄 Giấy phép pháp lý ↗]` (khi bấm nếu là `.jpg/.png/.webp` có thể xem ngay trong Lightbox modal hoặc mở tab mới nếu là `.pdf`).
     - `[🏭 Ảnh thực tế kho bãi ↗]`.
   - Nhãn text tĩnh `a.docs` chỉ dùng làm fallback khi hồ sơ offline demo không có URL thực.

---

## 5. TRƯỜNG HỢP 5: LOẠI BỎ NHÃN KỸ THUẬT CSDL (`PostgreSQL` / `PostGIS`) KHỎI GIAO DIỆN

### 5.1. Danh sách kiểm đếm các vị trí phát hiện trong `ecolink-web/src`

| File & Dòng | Chuỗi kỹ thuật đang lộ ra UI | Hướng chuẩn hóa UX (Thân thiện với người dùng cuối) |
| :--- | :--- | :--- |
| [`staff/Applications.tsx:121`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/Applications.tsx#L121) | `"Nguồn: PostgreSQL + PostGIS"` / `"Nguồn: Hàng đợi trình duyệt"` | **Xóa hoàn toàn badge này** khỏi header (chỉ hiện banner cảnh báo nếu thực sự lỗi mạng/offline) |
| [`JoinRecycler.tsx:209, 482`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/JoinRecycler.tsx#L209) | `"Đã ghi nhận vào CSDL PostGIS (Trạng thái: Chờ thẩm định)"` | Đổi thành: `"Hồ sơ đã được tiếp nhận lên hệ thống (Trạng thái: Chờ thẩm định)"` |
| [`user/Transactions.tsx:67`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Transactions.tsx#L67) | `"Tổng tiền thu được từ các đơn hoàn tất (PostgreSQL)"` | Đổi thành: `"Tổng tiền thu được từ các đơn hoàn tất"` |
| [`user/Handover.tsx:200`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Handover.tsx#L200) | `... (đã lưu PostgreSQL)` | Đổi thành: `... (đã lưu trên hệ thống)` hoặc bỏ ngoặc thừa |
| [`user/Receipt.tsx:303`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Receipt.tsx) | `... hoàn tất giao dịch trên PostgreSQL.` | Đổi thành: `... và hoàn tất giao dịch.` |
| [`recycler/Catalog.tsx:123`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Catalog.tsx) | `... (đã cập nhật PostgreSQL).` | Đổi thành: `"Đã lưu và công bố bảng giá thành công lên hệ thống."` |
| [`recycler/Home.tsx:221, 505`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Home.tsx) | `... lên PostGIS.` / `... bản đồ PostGIS` | Đổi thành: `... lên bản đồ EcoLink.` / `... kết quả tìm kiếm bản đồ` |
| [`recycler/Receipt.tsx:296`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Receipt.tsx) | `... (đã lưu PostgreSQL và chuyển đơn sang AwaitingConfirmation)...` | Đổi thành: `"Đã phát hành biên nhận #{code} thành công. Đang chờ người dân xác nhận!"` |
| [`recycler/Requests.tsx:124-125, 153`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Requests.tsx#L124-L125) | `... (đã lưu PostgreSQL)` / `trên PostgreSQL` | Đổi thành: `"trên hệ thống"` hoặc bỏ hẳn khi online (chỉ hiện nhãn nếu đang dùng chế độ offline) |
| [`staff/AiReview.tsx:290`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/AiReview.tsx) & [`staff/Dataset.tsx:140`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/Dataset.tsx#L140) | `"Đã kết nối dữ liệu thực PostgreSQL"` / `"Dữ liệu thực PostgreSQL"` | Đổi thành: `"Dữ liệu đồng bộ hệ thống"` (hoặc bỏ nhãn khi hoạt động bình thường) |
| [`AuthForm.tsx:503`](file:///c:/Projects/EcoLink/ecolink-web/src/components/AuthForm.tsx) / `506` | Ghi chú / mô tả `"CSDL"` hoặc tên DB | Đổi sang `"trên hệ thống"` (giữ comment dev sạch hoặc ẩn khỏi UI người dùng) |

> [!TIP]
> **Nguyên tắc Clean UI:** Giao diện sản phẩm không bao giờ phơi bày tên công nghệ hạ tầng (`PostgreSQL`, `PostGIS`, tên enum nội bộ như `AwaitingConfirmation`) cho người dùng cuối hay nhân viên vận hành.

---

## 6. TRƯỜNG HỢP 6: ĐỔI THỐNG NHẤT `User/Buyer` VÀ `collector` $\rightarrow$ `recycler` TOÀN HỆ THỐNG & DATABASE

### 6.1. Phạm vi kiểm toán toàn diện (Frontend + Backend + Database + Tests)

| Tầng | Vị trí hiện hữu thuật ngữ cũ (`Buyer` / `B-` / `collector`) | Thay đổi chuẩn hóa sang `recycler` |
| :--- | :--- | :--- |
| **1. FE Navigation & UI Labels** | • [`src/data/portal.ts:81`](file:///c:/Projects/EcoLink/ecolink-web/src/data/portal.ts#L81): `"Tài khoản User/Buyer"`<br>• [`src/pages/staff/Accounts.tsx:61, 262`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/Accounts.tsx#L262): `"Danh sách User và Buyer"`<br>• [`src/check.tsx:132`](file:///c:/Projects/EcoLink/ecolink-web/src/check.tsx) & [`src/check-routes.tsx:53`](file:///c:/Projects/EcoLink/ecolink-web/src/check-routes.tsx): assert tiêu đề trên | Đổi thành `"Tài khoản User/Recycler"` và `"Danh sách User và Recycler"` (đồng bộ cả `check.tsx` & `check-routes.tsx` để `npm run check` & `npm run check:routes` không fail) |
| **2. FE Mã tài khoản & Fix trùng `B-9999`** | • [`src/lib/governanceApi.ts:446-450`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/governanceApi.ts#L446-L450): `prefix = ... ? "B" : "U"` và `${prefix}-${r.userId.slice(0, 4).toUpperCase()}` | Đổi prefix `"B"` $\rightarrow$ **`"R"`**. **Đồng thời sửa lỗi trùng `B-9999`** (xem ảnh Case 6: 3 vựa cùng hiện `B-9999` do UUID seed cùng bắt đầu `9999999x`): lấy `const hex = r.userId.replace(/-/g, "").toUpperCase(); const shortCode = hex.slice(-6)` hoặc `hex.slice(0, 2) + hex.slice(-4)` $\rightarrow$ tạo mã duy nhất như **`R-999991`**, **`R-999992`**, **`R-222222`** |
| **3. FE Demo Auth & Data mẫu** | • [`src/lib/authApi.ts:97-102, 125-130`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/authApi.ts#L97-L130): `collector` / `collector@ecolink.vn` / `collector123`, key fallback `collector:`<br>• [`src/lib/recyclerWorkflowApi.ts:297`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/recyclerWorkflowApi.ts): `{ usernameOrEmail: "collector", password: "collector123" }`<br>• [`src/data/ops.ts:208-210`](file:///c:/Projects/EcoLink/ecolink-web/src/data/ops.ts#L208-L210): `B-0207`, `B-0211`, `B-0202`, `@thugom.vn`<br>• [`src/pages/JoinRecycler.tsx:321`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/JoinRecycler.tsx#L321): placeholder `ten@thugom.vn` | • Đổi đăng nhập nhanh FE thành **`recycler`** / **`recycler@ecolink.vn`** / **`recycler123`**<br>• Xóa key alias thừa hoặc chỉ giữ chuẩn `recycler`<br>• Đổi mã mẫu `ops.ts` thành `R-0207`, `R-0211`, `R-0202` và email thành `...@recycler.ecolink.vn`<br>• Đổi placeholder `ten@recycler.vn` |
| **4. BE Demo Seeders & Swagger Hints** | • [`AuthService.cs:842-1025`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AuthService.cs#L842-L947): `EnsureDemoUsersAsync` (`collector`, `collector@ecolink.vn`, `collector123`)<br>• [`GeoMatchingService.cs:489-820`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GeoMatchingService.cs#L551-L785): `EnsureDemoCollectorPricesSeededAsync` và `11` tài khoản (`collector`, `collector_q1`, `collector_tb`, `collector_q7`, `collector_hn`, `collector_hl1`..`hl4`, `collector_hndd`, `collector_hnhbt`, mật khẩu `collector123`)<br>• [`AuthController.cs:318`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/AuthController.cs#L318) & [`Program.cs:124`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Program.cs#L124) | • Đổi tên method `EnsureDemoRecyclerPricesSeededAsync`<br>• Đổi toàn bộ 11 username/email sang **`recycler`**, **`recycler_q1`**, **`recycler_tb`**, **`recycler_q7`**, **`recycler_hn`**, **`recycler_hl1`..`hl4`**, **`recycler_hndd`**, **`recycler_hnhbt`**; email **`recycler*@ecolink.vn`**; mật khẩu **`recycler123`**<br>• Cập nhật Swagger / message hint thành `recycler / recycler123` |
| **5. BE Role Enum & Auth Attributes** | • [`DomainEnums.cs:7-8`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Enums/DomainEnums.cs#L7-L8): `Recycler = 2, Collector = 2`<br>• Các `[Authorize(Roles = "...")]` trong `NegotiationChatController`, `PointsAndRewardsController`, `RecyclerController`, `RecyclerHandoverController`, `RecyclerReceiptController`, `ServicePaymentController`, `UserGamificationController`<br>• `AccountGovernanceService.cs:55` | • Giữ `UserRole.Recycler = 2` làm định danh chuẩn duy nhất<br>• Đánh dấu `[Obsolete]` hoặc bỏ alias `Collector` trong C# sau khi dọn hết controller (`[Authorize(Roles = "Recycler")]`), trừ trường hợp DB hoặc client cũ còn gửi query param `"collector"` có thể map mềm về `UserRole.Recycler` ở tầng parse input để an toàn |
| **6. Database PostgreSQL Migration (Cập nhật data đang sống)** | Bảng `users` hiện đã seed sẵn các dòng `username = 'collector*'`, `email = 'collector*@ecolink.vn'` trên PostgreSQL cục bộ / staging | **Bắt buộc viết EF Core Migration `RenameDemoCollectorUsersToRecycler`:** chạy SQL `UPDATE users` theo danh sách UUID cố định (`22222222-...`, `44444444-...`..`77777777-...`, `99999991-...`..`99999996-...`) và thêm bước đồng bộ trong `EnsureDemoUsersAsync` / `EnsureDemoRecyclerPricesSeededAsync` (tìm theo `u.Id` thay vì chỉ tìm theo `Username == "recycler"` để nâng cấp an toàn các DB cũ mà **không** gặp lỗi trùng PK `user_id`!) |
| **7. Test Suites (`ApiTests` & `UnitTests`)** | • [`EcoLink.ApiTests/Support.cs:177, 182`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/EcoLink.ApiTests/Support.cs#L177)<br>• `GamificationTests.cs` (`GamHelper.Collector()`, `Redeem_LastItem_TwoBuyers_OnlyOneSucceeds`), `DiscoveryTests.cs`, `HandoverTests.cs`, `NegotiationChatTests.cs`, `OnboardingTests.cs`, `ReceiptTests.cs`, `RecyclerSupport.cs`, `RecyclerTests.cs`<br>• [`NegotiationChatUnitTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Module08_NegotiationChat/NegotiationChatUnitTests.cs) | Đổi `"collector"` / `"collector123"` thành `"recycler"` / `"recycler123"`, đổi helper `Collector()` $\rightarrow$ `Recycler()`, tên test `TwoBuyers` $\rightarrow$ `TwoRecyclers`, và `collector_hl1*` trong UnitTest sang `recycler_hl1*` |

> [!CAUTION]
> **Chi tiết kỹ thuật quan trọng khi cập nhật Demo Accounts trong PostgreSQL:**  
> Ở [`AuthService.cs:854-855`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AuthService.cs#L854-L855) và [`GeoMatchingService.cs:775-777`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GeoMatchingService.cs#L775-L777), hàm seeder kiểm tra `.AnyAsync(u => u.Id == demoId || u.Username == ...)`. Nếu bạn chỉ đổi chuỗi `"collector"` thành `"recycler"` trong C# mà **không** kèm migration/update cho bản ghi đã tồn tại theo `u.Id`, DB cũ sẽ giữ nguyên `username = 'collector'` khiến nút đăng nhập nhanh `recycler / recycler123` bị **401 Unauthorized**! Do đó **Migration SQL + logic cập nhật khi tìm thấy `existingUser.Id` trùng `demoId` mà `Username` vẫn là `collector*`** là bắt buộc để chạy trơn tru trên mọi môi trường DB.

---

## 7. TRƯỜNG HỢP 7: KIẾN TRÚC TÌM KIẾM & PHÂN TRANG TOÀN HỆ THỐNG

### 7.1. Đánh giá lỗ hổng hiện tại
1. **Backend đang phân mảnh 4 kiểu DTO phân trang khác nhau:**
   - `PagedResult<T>` (`PageNumber`, `PageSize`, `TotalRecords`, `TotalPages`) — dùng bởi Governance, Onboarding, AI Review, Complaints, Reviews, Payments.
   - `GamPagedResult<T>` (`Page`, `PageSize`, `TotalCount`) — dùng bởi `PointsService`, `GiftService`, `EcoFactService`.
   - `BlogPagedResult<T>` (`Page`, `PageSize`, `TotalCount`) — dùng bởi `BlogPostService`.
   - `HandoverPagedResult<T>` (`Page`, `PageSize`, `TotalCount`) — dùng bởi `HandoverRequestService`.
2. **Backend thiếu phân trang hoặc thiếu tìm kiếm (`search`) ở nhiều endpoint:**
   - Thiếu **Phân trang**: `GET /api/v1/gamification/gifts` (`List<GiftDto>`), `GET /api/v1/gamification/ecofacts` (User), `GET /api/v1/admin/gamification/cycles`, `GET /api/v1/gamification/leaderboard` (hiện chỉ lấy `top` cố định), `GET /api/v1/recycler/prices` (nên hỗ trợ optional paging hoặc giữ catalog lọc nhanh).
   - Có phân trang nhưng **Thiếu `search` / `keyword`**: `GET /api/v1/handovers`, `GET /api/v1/recycler/handovers` (chưa lọc theo mã đơn `BH-...`, địa chỉ, tên đối tác), `GET /api/v1/handovers/history`, `GET /api/v1/recycler/handovers/history`, `GET /api/v1/gamification/redemptions/me`, `GET /api/v1/gamification/transactions`.
3. **Frontend thiếu thanh phân trang & tìm kiếm đồng bộ:**
   - Trong toàn bộ 55+ page của `ecolink-web`, **chỉ có duy nhất [`admin/Reports.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Reports.tsx) có nút `Trang trước / Trang sau`**.
   - Các trang vận hành lớn như `staff/Accounts.tsx` (cố định `pageSize: 30`), `staff/Applications.tsx` (cố định `pageSize: 50`), `staff/Complaints.tsx` (`pageSize: 50`), `staff/AiReview.tsx` (`pageSize: 50`), `staff/Gifts.tsx`, `staff/EcoFacts.tsx`, `staff/BlogEditor.tsx`, `admin/BlogReview.tsx`, `admin/StaffAccounts.tsx`, `admin/Reconciliation.tsx`, `user/Handover.tsx`, `recycler/Requests.tsx`, `user/Transactions.tsx` đều **chưa có thanh chuyển trang**, dẫn tới khi dữ liệu vượt 20–50 dòng sẽ bị **ẩn mất bản ghi trang 2 trở đi** hoặc phải tải quá nhiều dữ liệu vào RAM trình duyệt.

### 7.2. Thiết kế chuẩn hóa Backend (Clean Code & DRY)

1. **Tạo Base Query DTO dùng chung (`Ecolink.Application/Common/Pagination/`):**
   ```csharp
   public class PagedFilterRequest
   {
       private const int MaxPageSize = 100;
       private int _pageNumber = 1;
       private int _pageSize = 20;

       public string? Search { get; set; }

       public int PageNumber
       {
           get => _pageNumber;
           set => _pageNumber = value < 1 ? 1 : value;
       }

       public int PageSize
       {
           get => _pageSize;
           set => _pageSize = value < 1 ? 20 : (value > MaxPageSize ? MaxPageSize : value);
       }
   }
   ```
   *(Hỗ trợ `[FromQuery]` alias `page` $\leftrightarrow$ `pageNumber` qua `Page` getter/setter hoặc model binding nhẹ để không gãy các query FE/ApiTest cũ).*

2. **Tạo Extension Method `IQueryable<T>.ToPagedResultAsync` (loại bỏ lặp `CountAsync` + `Skip` + `Take` ở hàng chục Service):**
   - Triển khai 1 helper duy nhất trong `Ecolink.Infrastructure/Extensions/QueryableExtensions.cs`:
     - Tính `totalRecords = await query.CountAsync(ct)`.
     - Tự động kẹp `pageNumber` nếu `totalRecords > 0` và `(pageNumber - 1) * pageSize >= totalRecords` (tránh trả trang rỗng sau khi xóa bản ghi cuối cùng của trang cuối).
     - Thực thi `Skip` + `Take` + `ToListAsync(ct)`.
   - Giữ `PagedResult<T>` làm kiểu chuẩn duy nhất cho mọi endpoint mới/chuẩn hóa; đối với `GamPagedResult<T>`, `BlogPagedResult<T>`, `HandoverPagedResult<T>`, **FE chuẩn hóa 1 hàm `normalizePagedResult` duy nhất** đọc được cả `totalRecords`/`totalCount` và `pageNumber`/`page` để tránh breaking change cho ApiTests hiện tại trong khi codebase vẫn sạch 100%.

3. **Bổ sung `search` & Phân trang cho các Endpoint BE còn thiếu:**
   - `HandoverRequestService` (`GetUserRequestsAsync`, `GetRecyclerRequestsAsync`): thêm `string? search` (tìm theo `HandoverCode`, `PickupAddress`, `RecyclerProfile.FacilityName`, `User.FullName`).
   - `HandoverReceiptService` (`GetUserHistoryAsync`, `GetRecyclerHistoryAsync`): thêm `string? search` (tìm theo `ReceiptCode`, `HandoverCode`, tên đối tác/người dân).
   - `GiftService.ListStoreAsync` (`GET /api/v1/gamification/gifts`): bổ sung tham số `int page = 1, int pageSize = 12` trả về `GamPagedResult<GiftDto>` (hoặc giữ tương thích qua query flag / cập nhật FE & `GamificationTests` đồng bộ).
   - `EcoFactService`: thêm endpoint user `GET /api/v1/gamification/ecofacts` hỗ trợ `treeLevel`, `unlockedOnly`, `search`, `page`, `pageSize`.
   - `LeaderboardService.GetLeaderboardAsync`: hỗ trợ `page`, `pageSize` (mặc định `page=1, pageSize=20`) bên cạnh `top` để xem hết danh sách xếp hạng dài.
   - `AdminStaffController` & `AdminGamificationController.GetCycles`: đảm bảo có `search`, `page`, `pageSize`.

### 7.3. Thiết kế Thư viện UI & Hooks dùng chung Frontend (`ecolink-web`)

Để áp dụng nhanh gọn cho **hơn 15 trang** mà **không trùng lặp code JSX hay logic phân trang**:

1. **Hooks dùng chung (`src/hooks/`):**
   - `src/hooks/useDebouncedValue.ts`: Debounce chuỗi tìm kiếm `300ms` (tránh gọi API liên tục khi người dùng gõ).
   - `src/hooks/useTableQuery.ts`: Quản lý state thống nhất `{ page, pageSize, search, filters }`, cung cấp:
     - `setSearch(q)` $\rightarrow$ tự động **reset `page = 1`**.
     - `setFilter(key, val)` $\rightarrow$ tự động **reset `page = 1`**.
     - `setPage(nextPage)` & `setPageSize(nextSize)`.
     - Hỗ trợ 2 chế độ: **Server-side Pagination** (khi kết nối Backend) và **Client-side Slicing** (dùng chung 1 helper `paginateArray(items, page, pageSize)` khi dữ liệu danh mục nhỏ hoặc offline fallback).

2. **Bộ 2 Component chuẩn UI (`src/components/list/`):**
   - **`<SearchFilterBar />`:**
     - Ô nhập tìm kiếm có icon `Search`, nút `X` xóa nhanh (Clear button), `placeholder` tùy biến theo ngữ cảnh.
     - Slot chọn bộ lọc nhanh (`tabs` / `<select>` trạng thái, vai trò, loại quà...) và hiển thị tổng kết quả (`"Tìm thấy 42 kết quả"`).
   - **`<PaginationBar />`:**
     - Thiết kế khớp design system EcoLink (`rounded-xl border-line bg-white`, nút `btn-outline` / `bg-brand text-white`).
     - Hiển thị: `Hiển thị 1–10 trên 84 mục`, nút `Trước`, danh sách số trang thông minh có dấu `...` (ví dụ `1 ... 4 [5] 6 ... 12`), nút `Sau`, và dropdown chọn kích thước trang (`10 / 20 / 50`).
     - Tự động ẩn hoặc disable hợp lý khi danh sách có `<= 1` trang tùy ngữ cảnh (hoặc luôn giữ thông tin tổng số mục + chọn pageSize).

3. **Helper chuẩn hóa dữ liệu API (`src/lib/pagination.ts`):**
   ```ts
   export interface StandardPaged<T> {
     items: T[]
     page: number
     pageSize: number
     totalCount: number
     totalPages: number
   }
   export function normalizePaged<T>(raw: any): StandardPaged<T> { ... }
   ```
   Xử lý thống nhất cả 4 format DTO từ Backend (`PagedResult`, `GamPagedResult`, `BlogPagedResult`, `HandoverPagedResult`).

### 7.4. Ma trận áp dụng Tìm kiếm & Phân trang theo từng Màn hình

| Nhóm | Trang Frontend | Nguồn API / Chế độ | Cần bổ sung |
| :-: | :--- | :--- | :--- |
| **Staff** | `staff/Accounts.tsx` | Server (`GET /api/v1/staff/accounts`) | Đã có search + filter vai trò $\rightarrow$ Thay `pageSize: 30` cứng bằng state `page/pageSize` + `<PaginationBar />` |
| **Staff** | `staff/Applications.tsx` | Server (`GET /api/v1/recycler-applications`) | Bổ sung `<SearchFilterBar />` (tìm theo tên vựa, SĐT, email, địa chỉ + tab trạng thái `Chờ duyệt / Đã duyệt / Từ chối`) + `<PaginationBar />` |
| **Staff** | `staff/Complaints.tsx` | Server (`GET /api/v1/staff/complaints`) | Bổ sung `<SearchFilterBar />` (mã `CMP-...`, tiêu đề, trạng thái) + `<PaginationBar />` thay `pageSize: 50` cứng |
| **Staff** | `staff/AiReview.tsx` | Server (`GET /api/v1/staff/ai-feedback` & `labeling-queue`) | Bổ sung `<SearchFilterBar />` (tìm theo loại rác, trạng thái duyệt) + `<PaginationBar />` |
| **Staff** | `staff/Gifts.tsx` | Server (`staff/gifts` & `staff/redemptions`) | Thêm `<PaginationBar />` cho cả Tab Kho quà lẫn Tab Đơn đổi quà + đồng bộ search |
| **Staff** | `staff/EcoFacts.tsx` | Server (`GET /api/v1/staff/ecofacts`) | Thêm `<SearchFilterBar />` (search từ khóa, lọc `unlockTreeLevel`, `status`) + `<PaginationBar />` |
| **Staff** | `staff/BlogEditor.tsx` | Server (`GET /api/v1/staff/blogs`) | Thêm `<SearchFilterBar />` + `<PaginationBar />` cho danh sách bài viết |
| **Admin** | `admin/BlogReview.tsx` | Server (`GET /api/v1/admin/blogs/queue`) | Thêm `<SearchFilterBar />` + `<PaginationBar />` |
| **Admin** | `admin/StaffAccounts.tsx` | Server (`GET /api/v1/admin/staff`) | Thêm `<SearchFilterBar />` (tên, email, mã NV, trạng thái) + `<PaginationBar />` |
| **Admin** | `admin/Reconciliation.tsx` | Server (`GET /api/v1/admin/reconciliation/ledger`) | Thêm `<SearchFilterBar />` (mã đơn `PAY-...`, tên vựa) + `<PaginationBar />` |
| **Admin** | `admin/Reports.tsx` | Server (`GET /api/v1/admin/analytics/audit-logs`) | Chuẩn hóa nút `Trước/Sau` hiện tại sang dùng chung `<PaginationBar />` + `<SearchFilterBar />` |
| **Admin** | `admin/Leaderboard.tsx` | Server (`GET /api/v1/admin/gamification/cycles`) | Thêm `<PaginationBar />` cho bảng lịch sử các chu kỳ BXH |
| **User** | `user/Handover.tsx` | Server (`GET /api/v1/handovers`) | Thêm `<SearchFilterBar />` (tìm mã `BH-...`, tên vựa, địa chỉ + lọc trạng thái) + `<PaginationBar />` |
| **User** | `user/Transactions.tsx` | Server (`GET /api/v1/handovers/history`) | Thêm `<SearchFilterBar />` (search + lọc ngày) + `<PaginationBar />` |
| **User** | `user/Gamification.tsx` | Server (`transactions` & `redemptions/me`) | Thêm lọc loại giao dịch + `<PaginationBar />` cho Lịch sử điểm và Lịch sử đổi quà |
| **Recycler** | `recycler/Requests.tsx` | Server (`GET /api/v1/recycler/handovers`) | Thêm `<SearchFilterBar />` (mã `BH-...`, người gửi, trạng thái) + `<PaginationBar />` |
| **Recycler** | `recycler/Catalog.tsx` | Server (`GET /api/v1/recycler/prices`) | Thêm ô lọc/tìm nhanh loại phế liệu ngay đầu bảng giá |
| **Public** | `Rewards.tsx`, `Blog.tsx`, `EcoFactsPage.tsx`, `Leaderboard.tsx`, `Partners.tsx` | Server / Hybrid | `Blog.tsx` & `Rewards.tsx`: gắn `<SearchFilterBar />` + `<PaginationBar />`; `EcoFactsPage.tsx`: search + lọc theo Cấp cây 1–6; `Leaderboard.tsx`: search tên + chuyển trang Top 100+; `Partners.tsx`: phân trang danh sách đối tác quy chuẩn |

---

## 8. TRƯỜNG HỢP 8: GIẢI ĐÁP THÔNG BÁO LỖI Ở ANTIGRAVITY IDE

### 8.1. Thông báo đang gặp là gì?
Từ ảnh chụp thông báo:
> `Failed to save 'Gamification.tsx': The content of the file is newer. Please compare your version with the file contents or overwrite the file with your changes.`  
> `Failed to save 'Home.tsx': The content of the file is newer...`

Đây **không phải lỗi cú pháp code hay lỗi build**, mà là **cảnh báo xung đột ghi tệp (File Save Conflict / Optimistic Concurrency Guard)** của nhân VS Code / Antigravity IDE.

### 8.2. Nguyên nhân gốc rễ
- Tab `Gamification.tsx` và `Home.tsx` đang được mở trong trình soạn thảo của bạn với **trạng thái chưa lưu (dirty buffer — có dấu chấm tròn ở tiêu đề tab)**.
- Trong lúc tab đó đang giữ bản nháp cũ trong RAM, **nội dung thực của chính file đó trên ổ đĩa (disk) vừa bị cập nhật bởi một tác nhân khác** (thường do **AI Agent vừa dùng công cụ chỉnh sửa file trực tiếp**, hoặc bạn vừa chạy lệnh `git pull` / `git checkout` / formatter).
- Khi bạn bấm `Ctrl + S` (hoặc Auto Save kích hoạt), IDE phát hiện timestamp/checksum trên ổ đĩa mới hơn bản bạn đang mở nên **chặn lưu** để bảo vệ bạn không vô tình ghi đè làm mất toàn bộ code mà AI hoặc Git vừa cập nhật.

### 8.3. Cách xử lý chuẩn & an toàn
1. **KHÔNG bấm `Overwrite` theo phản xạ** (bấm `Overwrite` sẽ đẩy bản cũ trong tab đè lên ổ đĩa, làm mất thay đổi mới nhất của AI/Git).
2. Bấm **`Compare`** để mở màn hình so sánh 2 cột (**Ổ đĩa** vs **Tab đang mở**):
   - **Nếu cột Tab đang mở không chứa sửa đổi quan trọng nào của bạn:** Đóng tab đó (chọn *Don't Save*) hoặc nhấn `Ctrl + Shift + P` $\rightarrow$ gõ **`File: Revert File`** để nạp lại đúng bản mới nhất từ ổ đĩa.
   - **Nếu cả hai bên đều có phần bạn muốn giữ:** Trong màn hình `Compare`, sao chép/merge đoạn bạn muốn từ cột phải sang cột trái, hoặc lưu tạm đoạn code của bạn, chạy `Revert File`, rồi dán lại vào file mới và lưu.
   - **Chỉ bấm `Overwrite`** khi bạn **chắc chắn 100%** thay đổi trong tab đang mở là bản mới hơn mà bạn muốn giữ đè lên file trên đĩa.
3. **Thói quen phòng tránh khi làm việc cùng AI Agent:**
   - Nhấn `Ctrl + S` lưu tất cả tab đang mở trước khi ra lệnh cho Agent sửa file.
   - Đóng bớt các tab không cần tự tay chỉnh sửa để IDE luôn tự động nạp lại file mới nhất từ disk mà không bật thông báo xung đột.

---

## 9. LỘ TRÌNH THỰC THI THEO GIAI ĐOẠN (CLEAN CODING & KHÔNG ĐỨT GÃY)

| Giai đoạn | Nội dung triển khai chi tiết | Điều kiện hoàn thành & Kiểm chứng |
| :-: | :--- | :--- |
| **Giai đoạn 1**<br>*(Thuật ngữ & DB Sync — Case 5, 6)* | 1. BE: Tạo migration SQL + nâng cấp `AuthService.EnsureDemoUsersAsync` & `GeoMatchingService.EnsureDemoRecyclerPricesSeededAsync` đổi 11 tài khoản `collector*` $\rightarrow$ `recycler*` (`recycler123`); cập nhật `AuthController.cs`, `Program.cs`.<br>2. FE: Đổi `"User/Buyer"` $\rightarrow$ `"User/Recycler"`, `"B-"` $\rightarrow$ `"R-"` + fix trùng `R-9999` (`governanceApi.ts`), đổi `ops.ts`, `authApi.ts`, `recyclerWorkflowApi.ts`, `JoinRecycler.tsx`, `check.tsx`, `check-routes.tsx`.<br>3. FE: Gỡ toàn bộ nhãn `PostgreSQL` / `PostGIS` / `CSDL PostGIS` khỏi UI & toast (Case 5).<br>4. BE Tests: Cập nhật `EcoLink.ApiTests` & `Ecolink.UnitTests`. | `dotnet ef database update` thành công; `dotnet test` (UnitTests & ApiTests) pass; `npm run check` & `npm run check:routes` pass; đăng nhập nhanh bằng `recycler / recycler123` thành công. |
| **Giai đoạn 2**<br>*(Quà tặng, BXH đa vai trò & KYB Onboarding — Case 1, 3, 4)* | 1. Case 1: Thêm `POST /api/v1/staff/gifts/upload-image` + backfill/seed `image_url` cho 6 quà mặc định; nâng cấp `staff/Gifts.tsx` chọn/upload ảnh + fallback `<Thumb />` ở `Rewards.tsx`.<br>2. Case 3: Mở rộng `LeaderboardService` & DTO xếp hạng **cả `User` và `Recycler`** (tính đúng `TotalRecycledKg`/`Area`/`Role`, thêm `RecentFinalizedCycles` & lọc `role`), cập nhật `BR-GAM-08` & unit test `UTC005`; FE bổ sung tab lọc `Tất cả / Người dân / Người thu gom`, role badge, Banner giải thưởng Top 1–2–3 (`500/300/150` điểm) và Bảng vàng kỳ trước.<br>3. Case 4: Tách 2 slot upload rõ ràng tại `JoinRecycler.tsx`; dọn bỏ 2 tag chữ lặp ở `staff/Applications.tsx` & thêm Lightbox xem ảnh/PDF. | Tạo/sửa quà upload ảnh R2 & 6 quà mặc định hiển thị ảnh đầy đủ ở `/rewards`; BXH hiển thị đúng cả Người dân lẫn Người thu gom, có giải thưởng & vinh danh kỳ trước, `UTC005` pass; hồ sơ Recycler ở `/staff/applications` gọn đúng 2 nút chứng từ. |
| **Giai đoạn 3**<br>*(Thống nhất Cây Ảo & EcoFact — Case 2)* | 1. BE: Thêm trạng thái chăm cây (`HydrationPercent`, `CareExp`, `POST /api/v1/gamification/tree/care`) gắn chặt `DailyTreeCareLimit` theo ngày ICT & thưởng điểm qua `IPointsService` có idempotency key; sửa sinh mã `EF-` hết trùng `EF-100000` trong `EcoFactService`.<br>2. FE: Gắn hoạt họa Cây Ảo 70-frame vào state thật từ PostgreSQL; xóa cộng điểm giả ở `localStorage`; bổ sung lộ trình EcoFact 6 cấp + Modal đọc bài viết `Content` & `SourceReference`. | Bấm `Tưới nước` / `Bón phân` lưu DB bền vững qua F5/đổi trình duyệt, khóa đúng giới hạn lượt/ngày (`DailyTreeCareLimit`), không còn mã `EF-100000` trùng nhau. |
| **Giai đoạn 4**<br>*(Chuẩn hóa Search & Phân trang — Case 7)* | 1. BE: Tạo `PagedFilterRequest` & `QueryableExtensions.ToPagedResultAsync`; bổ sung `search` và phân trang cho `HandoverRequestService`, `HandoverReceiptService`, `GiftService.ListStoreAsync`, `EcoFactService`, `LeaderboardService`, `AdminGamificationController`.<br>2. FE: Tạo `useDebouncedValue`, `useTableQuery`, `normalizePaged`, `<SearchFilterBar />`, `<PaginationBar />`; tích hợp tuần tự vào toàn bộ nhóm Staff, Admin, User, Recycler, Public theo Bảng 7.4. | Mọi danh sách > 10 dòng đều tìm kiếm debounce `300ms` (tự reset `page=1`) và chuyển trang mượt mà; test kiểm chứng edge case trang trống / đổi pageSize. |
| **Giai đoạn 5**<br>*(Kiểm thử hồi quy & Nghiệm thu tổng thể)* | 1. Chạy `dotnet build` & `dotnet test` toàn bộ BE.<br>2. Chạy `npm run typecheck`, `npm run check`, `npm run check:routes`, `npm run build` ở FE.<br>3. Chạy E2E walkthrough trực tiếp trên UI: Login `recycler`, kiểm tra `/staff/accounts` (`R-...`, không còn `B-9999`), `/rewards`, `/ecofacts`, `/leaderboard`, `/staff/applications`, và chuyển trang trên các bảng chính. | Zero lỗi biên dịch, zero failed unit/integration/SSR checks, 100% 8 trường hợp được giải quyết sạch sẽ. |
