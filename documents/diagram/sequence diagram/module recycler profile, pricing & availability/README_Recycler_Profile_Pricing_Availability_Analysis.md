# PHÂN TÍCH CLASS DIAGRAM & SEQUENCE DIAGRAM – MODULE: RECYCLER PROFILE, PRICING & AVAILABILITY (`R-01` & `R-02`)

Tài liệu này tổng hợp kiến trúc **Class Diagram** (1 bản Ultra-Compact chuẩn Clean Architecture) và **Sequence Diagram** (3 sơ đồ tách nhỏ theo từng kịch bản nghiệp vụ) của phân hệ **Quản lý Hồ sơ Cơ sở Thu gom, Bảng giá Phế liệu & Trạng thái Hoạt động (`Use Case R-01` & `Use Case R-02`)**.

---

## 1. SƠ ĐỒ LỚP TỔNG HỢP RÚT GỌN (ULTRA-COMPACT CLASS DIAGRAM)

* **File sơ đồ:** [`module_recycler_profile_pricing_availability_clean_architecture_class_diagram_ultra_compact.mmd`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/class%20diagram/module%20recycler%20profile,%20pricing%20&%20availability/module_recycler_profile_pricing_availability_clean_architecture_class_diagram_ultra_compact.mmd)
* **Cấu trúc 4 tầng Clean Architecture:**
  1. **Presentation Layer (`Ecolink.WebApi.Controllers`):**
     * [`RecyclerController`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/RecyclerController.cs) (`/api/v1/recycler`): Cung cấp 9 endpoints tự phục vụ (*Self-Service*) dành riêng cho vai trò `Recycler` / `Collector` (`GetProfile`, `UpdateProfile`, `GetPrices`, `AddPrice`, `UpdatePrice`, `BulkSavePrices`, `GetAvailability`, `UpdateAvailability`, `UpdateSchedule`).
     * [`WasteCategoryController`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/WasteCategoryController.cs) (`/api/v1/waste-categories`): Cung cấp endpoint `GetAll` lấy danh sách 5 loại phế liệu tái chế chuẩn có thể thu mua.
  2. **Application Layer (`Ecolink.Application.Interfaces` & `Dtos`):**
     * [`IRecyclerManagementService`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Interfaces/IRecyclerManagementService.cs) (Inbound Port) và `ICurrentUserService` (Outbound Port trích xuất `UserId` từ JWT).
  3. **Infrastructure Layer (`Ecolink.Infrastructure.Services` & `Data`):**
     * [`RecyclerManagementService`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/RecyclerManagementService.cs): Thực thi toàn bộ quy tắc nghiệp vụ `BR-MTH-01`, `BR-MTH-02`, `BR-MTH-03`, `BR-MTH-06`, `BR-MTH-07`, `EF-03`, `JOB-04` và tương tác với PostgreSQL + PostGIS thông qua `ApplicationDbContext`.
  4. **Domain Layer (`Ecolink.Domain.Entities`):**
     * `User` (`1 -- 1`) `RecyclerProfile` (`1 -- 0..*`) [`RecyclerPrice`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/RecyclerPrice.cs) (`0..* -- 1`) `WasteCategory`.

---

## 2. DANH SÁCH SEQUENCE DIAGRAMS THEO CHỨC NĂNG (3 SƠ ĐỒ TÁCH NHỎ)

| STT | Tên file sơ đồ (`.mmd`) | Chức năng / Use Case | Quy tắc nghiệp vụ (Business Rules) được mô tả |
| :-: | :--- | :--- | :--- |
| **1** | [`01_r01_manage_recycler_profile_and_add_update_price.mmd`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20recycler%20profile,%20pricing%20&%20availability/01_r01_manage_recycler_profile_and_add_update_price.mmd) | **`R-01` (Phần 1): Xem hồ sơ, Thêm mới loại phế liệu & Cập nhật/Tạm ngưng dòng giá đơn lẻ** | - `BR-MTH-01`: Chỉ cơ sở đã `Verified` mới được cấu hình bảng giá.<br>- `BR-MTH-03`: Giá thu mua nguyên dương (`VND/kg`), không cho phép thêm rác không tái chế (`trash`) hoặc thêm trùng loại phế liệu.<br>- `BR-MTH-07` & `EF-03`: Tạm ngưng mua bằng `Status = Inactive` (không xóa cứng) và chặn tắt dòng `Active` cuối cùng khi cơ sở đang `Online`. |
| **2** | [`02_r01_bulk_save_price_sheet_and_stale_price_check.mmd`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20recycler%20profile,%20pricing%20&%20availability/02_r01_bulk_save_price_sheet_and_stale_price_check.mmd) | **`R-01` (Phần 2): Tra cứu bảng giá (cảnh báo giá cũ `> 30 ngày`) & Lưu hàng loạt (*Save price sheet*)** | - `JOB-04`: Tự động gắn cờ `IsOutdated = true` nếu dòng giá quá 30 ngày chưa cập nhật.<br>- `SCR-RCY-01`: Lưu đồng loạt nhiều loại phế liệu (Upsert), kiểm tra chống trùng lặp `WasteCategoryId` và làm mới `UpdatedAt = DateTime.UtcNow`. |
| **3** | [`03_r02_toggle_online_offline_availability_and_weekly_schedule.mmd`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20recycler%20profile,%20pricing%20&%20availability/03_r02_toggle_online_offline_availability_and_weekly_schedule.mmd) | **`R-02`: Bật/Tắt trạng thái `Online`/`Offline` trên bản đồ & Cập nhật lịch hoạt động tuần** | - `BR-MTH-03`: Bật `Online` bắt buộc hồ sơ đã `Verified`, có tọa độ GPS hợp lệ và `activePriceCount >= 1` (`MSG-MTH-06`).<br>- `BR-MTH-06`: Chuyển `Offline` kèm `BusyReason` chỉ ẩn khỏi tìm kiếm bản đồ mới, giữ nguyên đơn hẹn đang xử lý.<br>- Tự động gộp các ngày liên tiếp có cùng khung giờ thành chuỗi tóm tắt `OperatingHours`. |

