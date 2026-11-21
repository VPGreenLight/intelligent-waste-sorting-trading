# Kế Hoạch Triển Khai: Terms Management (`/terms`) & Đồng Bộ Danh Mục Rác (`Categories` $\rightarrow$ `Policy`)

> **Phạm vi:** Toàn diện cả **Backend (`.NET 10` — `intelligent-waste-sorting-trading`)** và **Frontend (`React 19 + TypeScript + Vite` — `ecolink-web`)**.  
> **Trạng thái:** Tài liệu thiết kế kỹ thuật & lộ trình triển khai chờ duyệt trước khi chỉnh sửa mã nguồn.

---

## 1. Tổng Quan & Bảng Tóm Tắt Hai Hạng Mục

| Hạng mục | Hiện trạng & Vấn đề trong Code | Giải pháp Clean Coding & Kiến trúc mục tiêu | File tác động chính |
| :---: | :--- | :--- | :--- |
| **1. Đồng bộ `Categories` $\rightarrow$ `Policy`** | [`CreateWasteCategoryAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/RecyclerManagementService.cs#L127-L172) chỉ chèn dòng vào `WasteCategories`, chưa tạo `SortingRule`. Trong khi [`GetAllRuleDetailsAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/WasteRuleEngineService.cs#L249-L290) lại query từ `_db.SortingRules`, khiến danh mục mới tạo ở `/admin/categories` hoàn toàn **không xuất hiện** bên `/admin/policy`. | Tự động khởi tạo `SortingRule` mặc định (`DRAFT`) ngay khi tạo danh mục mới; sửa `GetAllRuleDetailsAsync` truy vấn gốc từ `_db.WasteCategories` (left-join `SortingRule`); nâng cấp [`Policy.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Policy.tsx) hợp nhất cả danh mục chưa có rule, cho phép tìm kiếm/lọc, chỉnh `applicableRegion` và công bố 1 chạm. | **BE:** `RecyclerManagementService.cs`, `WasteRuleEngineService.cs`, `AiClassificationUnitTests.cs` <br> **FE:** `Policy.tsx`, `wasteScanApi.ts` |
| **2. Terms & Platform Policy Management (`/admin/policy` $\leftrightarrow$ `/terms`)** | Trang công khai [`Terms.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Terms.tsx#L4-L25) hardcode cứng 5 mục điều khoản; các mục Footer ở [`nav.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/nav.ts#L36-L40) chưa trỏ link tới trang đích; Admin chưa có nơi thêm/sửa/xoá/ẩn-hiện điều khoản & chính sách môi trường. | Xây dựng module quản trị `PlatformPolicySection` trên Backend (CRUD + Phân nhóm `TERMS`/`ENVIRONMENT`/`PRIVACY` + Sắp xếp thứ tự + Bật/Ẩn + `AuditLog`), API công khai `/api/v1/policies` & quản trị `/api/v1/admin/policies`; nâng cấp [`Policy.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Policy.tsx) thành 2 Tab quản trị, đọc động trên [`Terms.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Terms.tsx) và gắn link thực tế cho Footer. | **BE:** `PlatformPolicySection.cs` *(mới)*, `PlatformPolicyConfigurations.cs` *(mới)*, `PlatformPolicyDtos.cs` *(mới)*, `IPlatformPolicyService.cs` *(mới)*, `PlatformPolicyService.cs` *(mới)*, `PlatformPoliciesController.cs` *(mới)*, `ApplicationDbContext.cs`, `DependencyInjection.cs` <br> **FE:** `policyApi.ts` *(mới)*, `Policy.tsx`, `Terms.tsx`, `nav.ts` |

---

## 2. Thiết Kế Chi Tiết Hạng Mục 1: Đồng Bộ `Categories` $\rightarrow$ `Policy` (`/admin/categories` $\rightarrow$ `/admin/policy`)

### 2.1. Phân tích nguyên nhân gốc rễ trong Code

1. **Bước tạo danh mục ([`RecyclerManagementService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/RecyclerManagementService.cs#L127-L172)):**
   - Khi Admin bấm **"Thêm vào danh mục"** ở [`Categories.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Categories.tsx#L272), Backend gọi `CreateWasteCategoryAsync` và chỉ thêm `WasteCategory` vào `_db.WasteCategories`.
   - `SortingRule` của danh mục mới là `null` (`HasSortingRule = false`).
2. **Bước tải danh sách Policy ([`WasteRuleEngineService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/WasteRuleEngineService.cs#L249-L290)):**
   - Trang [`Policy.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Policy.tsx#L79) gọi `GET /api/v1/waste-scan/rules?detailed=true&includeDraft=true` ([`WasteScanController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/WasteScanController.cs#L180-L192)).
   - Phía dưới, `GetAllRuleDetailsAsync` truy vấn:
     ```csharp
     var query = _db.SortingRules
         .AsNoTracking()
         .Include(r => r.WasteCategory)
         .Where(r => r.WasteCategory != null);
     ```
   - Vì dùng bảng gốc `_db.SortingRules` (tương đương INNER JOIN), mọi `WasteCategory` chưa có `SortingRule` đều bị loại khỏi kết quả, dù Backend đã có sẵn API `PUT /api/v1/waste-scan/rules/{categoryId}` để upsert rule mới.

### 2.2. Giải pháp Phía Backend (`.NET 10`)

#### A. Tự động khởi tạo `SortingRule` nháp trong [`RecyclerManagementService.CreateWasteCategoryAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/RecyclerManagementService.cs#L127-L172)
- Khi tạo `WasteCategory` mới thành công, khởi tạo đồng thời 1 bản ghi `SortingRule` gắn `WasteCategoryId = entity.Id` ở trạng thái `"DRAFT"` để Admin chỉ cần sang `/admin/policy` chỉnh sửa và bấm công bố (không ảnh hưởng kết quả nhận diện AI hay trang `/guidance` cho tới khi Admin công bố `ACTIVE`):
  - `Title`: `$"Hướng dẫn phân loại {entity.Name}"`
  - `SortingInstruction`: lấy từ `entity.Description` nếu có, mặc định `$"Tách riêng {entity.Name} khỏi các nhóm rác khác để tránh lẫn tạp chất."`
  - `PreparationSteps`: mặc định chuẩn theo `entity.IsRecyclable` (làm sạch/để ráo nếu tái chế, hoặc buộc kín túi rác nếu rác thường).
  - `HandlingAction`: nếu `entity.IsRecyclable == true` $\rightarrow$ `$"Bỏ vào thùng rác màu {entity.StandardBinColor} hoặc đặt lịch thu gom trên EcoLink."`; ngược lại $\rightarrow$ `$"Bỏ vào thùng rác màu {entity.StandardBinColor} theo quy định thu gom rác sinh hoạt."`
  - `ApplicableRegion`: `"VN_NATIONAL"`, `Status`: `"DRAFT"`.
- Sau khi lưu DB thành công, kết quả `MapCategory(entity)` trả về `HasSortingRule = true`, `RuleStatus = "DRAFT"`.

#### B. Sửa truy vấn gốc của [`WasteRuleEngineService.GetAllRuleDetailsAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/WasteRuleEngineService.cs#L249-L290)
- Đảm bảo **cả các `WasteCategory` đã tạo từ trước khi cập nhật code** (chưa có `SortingRule`) vẫn hiện 100% trên `/admin/policy`:
  - Khi `includeDraft == true`: đổi truy vấn gốc sang `_db.WasteCategories.AsNoTracking().Include(c => c.SortingRule).OrderBy(c => c.Code)`.
    - Nếu `c.SortingRule != null`: gọi `MapDetail(c, c.SortingRule)`.
    - Nếu `c.SortingRule == null`: tạo `SortingRuleDetailDto` khung với `RuleId = Guid.Empty`, `WasteCategoryId = c.Id`, `CategoryCode = c.Code`, `CategoryName = c.Name`, `CategoryStatus = c.Status`, `StandardBinColor = c.StandardBinColor`, `Title = $"Hướng dẫn phân loại {c.Name}"`, `SortingInstruction = c.Description ?? ""`, `PreparationSteps = ...`, `HandlingAction = ...`, `ApplicableRegion = "VN_NATIONAL"`, `Status = "UNCONFIGURED"` (hoặc `"DRAFT"`).
  - Khi `includeDraft == false`: giữ nguyên chỉ trả các bản ghi có `c.SortingRule != null && c.SortingRule.Status == "ACTIVE" && c.Status == "Active"`.

### 2.3. Giải pháp Phía Frontend ([`src/pages/admin/Policy.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Policy.tsx) & [`src/lib/wasteScanApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/wasteScanApi.ts))

1. **Hợp nhất nguồn dữ liệu Categories + Rules:**
   - Trong `Policy.tsx`, tải song song `Promise.all([fetchDetailedSortingRules(true), fetchWasteCategories({ includeInactive: true, includeNonRecyclable: true })])`.
   - Hợp nhất theo `wasteCategoryId` (fallback theo `categoryCode`): nếu có danh mục trong `WasteCategories` chưa xuất hiện hoặc có `RuleId` là `Guid.Empty` (`00000000-0000-0000-0000-000000000000`), tự sinh hàng khung `CHƯA THIẾT LẬP` (`hasRule = false`) với dữ liệu gợi ý sẵn để Admin điền và lưu trực tiếp.
2. **Hỗ trợ Công bố 1 chạm (`Save + Publish` liền mạch):**
   - Khi Admin bấm **"Công bố & Đồng bộ AI"** trên thẻ chưa có `ruleId` hợp lệ (`!r.ruleId || r.ruleId === EMPTY_GUID`), hàm `handlePublishRule` tự động gọi tuần tự:
     1. `upsertSortingRule(r.wasteCategoryId, payload)` để lưu & nhận về `ruleId` thật.
     2. `publishSortingRule(saved.data.ruleId)` để kích hoạt `ACTIVE` và đồng bộ AI Service.
3. **Bổ sung công cụ quản lý cho Tab Quy tắc phân loại:**
   - **Thanh tìm kiếm & Lọc:** Tìm theo tên/mã danh mục và lọc theo trạng thái (`Tất cả` | `Đang áp dụng (ACTIVE)` | `Bản nháp (DRAFT)` | `Chưa thiết lập`).
   - **Chọn khu vực áp dụng (`applicableRegion`):** Dropdown cho từng thẻ (`VN_NATIONAL` — Toàn quốc, `VN_NORTH` — Miền Bắc, `VN_CENTRAL` — Miền Trung, `VN_SOUTH` — Miền Nam) thay cho nhãn tĩnh hiện tại.
   - **Hiển thị trạng thái danh mục gốc:** Nếu danh mục bên `/admin/categories` đang là `Inactive`, hiển thị badge cảnh báo nhỏ *"Danh mục đang tạm ngưng"* trên thẻ Policy để Admin nắm rõ lý do rule chưa hiển thị công khai.

---

## 3. Thiết Kế Chi Tiết Hạng Mục 2: Chức Năng Management Cho Điều Khoản & Chính Sách (`/terms`)

### 3.1. Phân tích hiện trạng & Yêu cầu nghiệp vụ

- Hiện tại [`src/pages/Terms.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Terms.tsx#L4-L25) hardcode cứng mảng `sections` gồm 5 mục (nguyên tắc phân loại 3 nhóm, quy chuẩn cân đo & biên nhận, tích lũy điểm xanh, trách nhiệm đối tác/EPR, bảo mật & quyền riêng tư).
- Trong [`src/data/nav.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/nav.ts#L33-L40), khối Footer **CỘNG ĐỒNG & PHÁP LÝ** có 4 mục chữ tĩnh (`"Hướng dẫn phân loại rác nguồn"`, `"Quy chuẩn vật liệu tái chế"`, `"Chính sách quyền riêng tư"`, `"Điều khoản dịch vụ môi trường"`) chưa điều hướng tới trang tương ứng, dù [`Footer.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/Footer.tsx#L67-L75) đã hỗ trợ sẵn object `{ label, to }`.

### 3.2. Thiết kế Phía Backend (`.NET 10`)

#### A. Entity, Cấu hình EF Core & Seed dữ liệu chuẩn
- Tạo Entity **`PlatformPolicySection`** (`Ecolink.Domain/Entities/PlatformPolicySection.cs`):
  - `Id` (`Guid`, PK)
  - `Code` (`string`, tối đa 40 ký tự, unique, VD: `TERM-01`, `PRIV-01`)
  - `Category` (`string`, tối đa 30 ký tự: `"TERMS"` | `"ENVIRONMENT"` | `"PRIVACY"`)
  - `Title` (`string`, tối đa 200 ký tự, bắt buộc)
  - `Content` (`string`, bắt buộc, lưu nội dung chi tiết điều khoản)
  - `DisplayOrder` (`int`, mặc định tăng dần theo thứ tự hiển thị)
  - `Status` (`string`, tối đa 20 ký tự: `"ACTIVE"` | `"DRAFT"` | `"INACTIVE"`)
  - `UpdatedByAdminId` (`Guid?`, FK nullable $\rightarrow$ `Users.Id`, `OnDelete(DeleteBehavior.SetNull)`)
  - `CreatedAt` (`DateTime`)
  - `UpdatedAt` (`DateTime`)
- Tạo `PlatformPolicySectionConfiguration` (`IEntityTypeConfiguration<PlatformPolicySection>`) ánh xạ bảng PostgreSQL `platform_policy_sections` (các cột `snake_case`, unique index `ux_platform_policy_sections_code`, index `ix_platform_policy_sections_status_order`), và khai báo `DbSet<PlatformPolicySection>` trong [`ApplicationDbContext.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Data/ApplicationDbContext.cs).
- **Khởi tạo bảng an toàn & Seed mặc định:** Trong `PlatformPolicyService.EnsureSchemaAndSeedAsync(CancellationToken ct)`:
  - Kiểm tra nếu DB đang dùng PostgreSQL (`_db.Database.IsNpgsql()`), chạy lệnh DDL idempotent `CREATE TABLE IF NOT EXISTS platform_policy_sections (...)` + index (tránh lỗi thiếu bảng nếu môi trường chưa chạy `dotnet ef database update`). Nếu chạy EF InMemory cho Unit Test thì bỏ qua DDL raw SQL.
  - Nếu bảng chưa có bản ghi nào, tự seed 5 mục điều khoản chuẩn tương ứng nội dung hiện hành ở `Terms.tsx` ở trạng thái `"ACTIVE"` để dữ liệu công khai và Admin quản trị đồng nhất ngay lập tức.

#### B. Application DTOs, Validators & Service (`IPlatformPolicyService`)
- DTOs (`Ecolink.Application/Dtos/PlatformPolicyDtos.cs`):
  - `PlatformPolicySectionDto`: `Id`, `Code`, `Category`, `Title`, `Content`, `DisplayOrder`, `Status`, `UpdatedByAdminId`, `CreatedAt`, `UpdatedAt`.
  - `SavePlatformPolicyRequest`: `Code?`, `Category`, `Title`, `Content`, `DisplayOrder`, `Status?`.
  - `SetPlatformPolicyStatusRequest`: `Status` (`ACTIVE` | `DRAFT` | `INACTIVE`).
- FluentValidation Validators: kiểm tra `Title` (tối thiểu 4, tối đa 200 ký tự), `Content` (tối thiểu 20, tối đa 5000 ký tự), `Category` thuộc tập `TERMS | ENVIRONMENT | PRIVACY`, `DisplayOrder >= 1`.
- Service (`PlatformPolicyService`):
  - `GetPublicPoliciesAsync(string? category, CancellationToken ct)`: trả danh sách mục `Status == "ACTIVE"` sắp theo `DisplayOrder` rồi `CreatedAt`.
  - `GetAdminPoliciesAsync(string? category, string? status, string? search, CancellationToken ct)`: trả toàn bộ danh sách phục vụ Admin/Staff, hỗ trợ lọc theo nhóm, trạng thái, từ khoá.
  - `CreatePolicyAsync(Guid adminId, SavePlatformPolicyRequest req, CancellationToken ct)`: tự sinh mã `TERM-XX` / `ENV-XX` / `PRIV-XX` nếu bỏ trống `Code`, kiểm tra trùng `Code` (ném `ConflictException`), lưu DB và ghi `AuditLog` (`CREATE_PLATFORM_POLICY`).
  - `UpdatePolicyAsync(Guid adminId, Guid id, SavePlatformPolicyRequest req, CancellationToken ct)`: kiểm tra ràng buộc *không cho chuyển mục `ACTIVE` duy nhất cuối cùng sang `DRAFT`/`INACTIVE`*, cập nhật DB, ghi `AuditLog` (`UPDATE_PLATFORM_POLICY`).
  - `SetPolicyStatusAsync(Guid adminId, Guid id, string status, CancellationToken ct)`: chuyển nhanh trạng thái, chặn nếu cố ẩn mục `ACTIVE` cuối cùng, ghi `AuditLog` (`UPDATE_PLATFORM_POLICY_STATUS`).
  - `DeletePolicyAsync(Guid adminId, Guid id, CancellationToken ct)`: kiểm tra không phải mục `ACTIVE` cuối cùng còn hiển thị công khai, xoá bản ghi và ghi `AuditLog` (`DELETE_PLATFORM_POLICY`).
- Đăng ký `services.AddScoped<IPlatformPolicyService, PlatformPolicyService>();` trong [`DependencyInjection.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/DependencyInjection.cs).

#### C. API Controller (`PlatformPoliciesController`)
| Method & Endpoint | Quyền truy cập | Chức năng |
| :--- | :--- | :--- |
| `GET /api/v1/policies` | `[AllowAnonymous]` | Lấy các điều khoản/chính sách `ACTIVE` hiển thị tại `/terms` (tuỳ chọn `?category=TERMS\|ENVIRONMENT\|PRIVACY`). |
| `GET /api/v1/admin/policies` | `[Authorize(Roles = "Admin,Staff")]` | Lấy danh sách đầy đủ cho trang quản trị kèm bộ lọc `category`, `status`, `search`. |
| `POST /api/v1/admin/policies` | `[Authorize(Roles = "Admin")]` | Thêm mới một điều khoản / chính sách nền tảng. |
| `PUT /api/v1/admin/policies/{id:guid}` | `[Authorize(Roles = "Admin")]` | Cập nhật tiêu đề, nội dung, phân nhóm, thứ tự, trạng thái. |
| `PATCH /api/v1/admin/policies/{id:guid}/status` | `[Authorize(Roles = "Admin")]` | Bật / Ẩn nhanh (`ACTIVE` / `DRAFT` / `INACTIVE`). |
| `DELETE /api/v1/admin/policies/{id:guid}` | `[Authorize(Roles = "Admin")]` | Xoá mục điều khoản (chặn xoá nếu là mục `ACTIVE` cuối cùng). |

---

### 3.3. Thiết kế Phía Frontend (`React + TypeScript`)

#### A. API Client [`src/lib/policyApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/policyApi.ts) *(mới)*
- Kiểu dữ liệu `PlatformPolicySectionDto` (`id`, `code`, `category`, `title`, `content`, `displayOrder`, `status`, `updatedAt`) và `POLICY_CATEGORY_LABELS` (`TERMS`: *Điều khoản sử dụng*, `ENVIRONMENT`: *Chính sách môi trường*, `PRIVACY`: *Chính sách quyền riêng tư*).
- Hàm gọi API dùng `apiRequest` (đã tích hợp `credentials: "include"` qua Cookie JWT):
  - `fetchPublicPolicies(category?, signal?)`
  - `fetchAdminPolicies(params?, signal?)`
  - `createPlatformPolicy(payload)`
  - `updatePlatformPolicy(id, payload)`
  - `setPlatformPolicyStatus(id, status)`
  - `deletePlatformPolicy(id)`
- Kèm `DEFAULT_PLATFORM_POLICIES` để làm fallback an toàn cho SSR smoke test (`npm run check` / `npm run check:routes`) khi chưa kết nối Backend.

#### B. Nâng cấp trang quản trị [`src/pages/admin/Policy.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Policy.tsx) (2 Tab Quản Trị)
- **Thanh Tab ở đầu trang (giữ nguyên URL `/admin/policy` — không phá cấu trúc sidebar hiện tại ở [`portal.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/portal.ts#L109)):**
  - **Tab 1: Chính sách phân loại rác (`Rule Engine & AI`)** — chứa phần quản lý rule từng danh mục rác đã đồng bộ từ `/admin/categories` (Mục 2 của kế hoạch).
  - **Tab 2: Điều khoản & Chính sách nền tảng (`/terms`)** — giao diện quản lý đầy đủ cho các mục điều khoản công khai:
    1. **Bộ lọc & Tìm kiếm:** Tìm theo tiêu đề/mã/nội dung, lọc theo nhóm (`Tất cả` | `Điều khoản sử dụng` | `Chính sách môi trường` | `Quyền riêng tư`) và lọc theo trạng thái (`Tất cả` | `ACTIVE` | `DRAFT` | `INACTIVE`).
    2. **Form Thêm / Sửa điều khoản** (dùng `Field` kèm `required` dấu `*` và `<FormRequiredLegend />` đúng quy chuẩn UX):
       - *Phân nhóm* (`TERMS` | `ENVIRONMENT` | `PRIVACY`) `*`
       - *Tiêu đề điều khoản* `*`
       - *Thứ tự hiển thị* (`number >= 1`) `*`
       - *Trạng thái* (`ACTIVE` — Công khai | `DRAFT` — Bản nháp | `INACTIVE` — Tạm ẩn) `*`
       - *Mã tham chiếu* (tùy chọn, tự sinh nếu trống)
       - *Nội dung chi tiết* (`textarea`) `*`
    3. **Danh sách các mục hiện có:** Hiển thị badge nhóm, mã, thứ tự (`#1, #2...`), trạng thái (`ACTIVE`/`DRAFT`/`INACTIVE`), mốc thời gian cập nhật, kèm các nút hành động nhanh: **Nâng/Hạ thứ tự ($\uparrow$/$\downarrow$)**, **Sửa** (đổ dữ liệu lên form), **Công khai / Tạm ẩn**, và **Xoá** (có hộp thoại xác nhận).
    4. **Liên kết xem trước:** Nút `"Xem trang /terms công khai →"` mở sang `/terms`.

#### C. Cập nhật trang công khai [`src/pages/Terms.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Terms.tsx) & Footer [`src/data/nav.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/nav.ts)
- **[`Terms.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Terms.tsx):**
  - Bỏ danh sách hardcode tĩnh làm dữ liệu chính; dùng `initApiLoading(true)` + `CardListSkeleton` khi đang tải `GET /api/v1/policies` (tránh nháy mock data trước khi nhận dữ liệu thật; chỉ fallback sang 5 mục chuẩn khi chạy SSR hoặc mất kết nối Backend).
  - Bổ sung thanh chip lọc theo nhóm (`Tất cả` | `Điều khoản dịch vụ` | `Chính sách môi trường` | `Quyền riêng tư`) đọc/đồng bộ qua query `?category=...`.
  - Hiển thị badge nhóm + ngày cập nhật gần nhất cho các điều khoản đang công khai.
- **[`nav.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/nav.ts#L33-L41):**
  - Chuyển các mục tĩnh ở cột **CỘNG ĐỒNG & PHÁP LÝ** thành liên kết thực tế:
    - `{ label: "Hướng dẫn phân loại rác nguồn", to: "/guidance" }`
    - `{ label: "Quy chuẩn vật liệu tái chế", to: "/waste" }`
    - `{ label: "Chính sách quyền riêng tư", to: "/terms?category=PRIVACY" }`
    - `{ label: "Điều khoản dịch vụ môi trường", to: "/terms" }`

---

## 4. Lộ Trình Triển Khai & Kế Hoạch Kiểm Thử

### Giai đoạn 1: Backend — Đồng bộ `Categories` $\rightarrow$ `Policy` & Module `PlatformPolicySection`
1. Cập nhật [`RecyclerManagementService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/RecyclerManagementService.cs) (`CreateWasteCategoryAsync` tự tạo `SortingRule` nháp) và [`WasteRuleEngineService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/WasteRuleEngineService.cs) (`GetAllRuleDetailsAsync` lấy gốc từ `WasteCategories`).
2. Thêm Entity `PlatformPolicySection`, Configuration, DTOs + Validators, `IPlatformPolicyService` / `PlatformPolicyService`, `PlatformPoliciesController`, và đăng ký vào `ApplicationDbContext` + `DependencyInjection.cs`.
3. Cập nhật / bổ sung Unit Tests trong [`AiClassificationUnitTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Module02_AiClassification/AiClassificationUnitTests.cs) và thêm unit test cho `PlatformPolicyService`:
   - Kiểm chứng tạo `WasteCategory` mới tự sinh `SortingRule` ở `DRAFT` và xuất hiện trong `GetAllRuleDetailsAsync(includeDraft: true)`.
   - Kiểm chứng CRUD, ràng buộc không xoá/ẩn mục `ACTIVE` cuối cùng, sắp xếp thứ tự và `AuditLog` của `PlatformPolicyService`.

### Giai đoạn 2: Frontend — Nâng cấp `/admin/policy`, `/terms` & Footer
1. Tạo [`src/lib/policyApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/policyApi.ts) và cập nhật [`src/lib/wasteScanApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/wasteScanApi.ts) (nếu cần helper đồng bộ rule khung).
2. Nâng cấp [`src/pages/admin/Policy.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Policy.tsx) với 2 tab (**Quy tắc phân loại rác** đồng bộ từ `Categories` + tìm kiếm/lọc/`applicableRegion` và **Điều khoản & Chính sách nền tảng** CRUD đầy đủ).
3. Cập nhật [`src/pages/Terms.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Terms.tsx) tải động từ `/api/v1/policies` và [`src/data/nav.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/nav.ts) gắn link Footer.

### Giai đoạn 3: Kiểm Thử Toàn Diện
1. **Backend:** Chạy `dotnet build` cho `Ecolink.WebApi.csproj` và `dotnet test` trên `Ecolink.UnitTests.csproj`.
2. **Frontend (`c:\Projects\EcoLink\ecolink-web`):**
   - `npm run typecheck`
   - `npm run check`
   - `npm run check:routes`
   - `npm run build`
3. **Kiểm tra luồng E2E thủ công:**
   - Tạo danh mục rác mới (VD: `e_waste`) tại `/admin/categories` $\rightarrow$ mở `/admin/policy` thấy ngay danh mục vừa tạo (kèm bộ lọc, chọn khu vực, công bố AI).
   - Ở Tab **Điều khoản & Chính sách nền tảng** trong `/admin/policy`: thêm mới 1 điều khoản, đổi thứ tự hiển thị, tạm ẩn/công khai và mở `/terms` xác nhận cập nhật đồng bộ từ Backend (không nháy dữ liệu tĩnh).
