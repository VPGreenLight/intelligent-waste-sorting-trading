# QUY CHUẨN THIẾT KẾ CƠ SỞ DỮ LIỆU VÀ TRƯỜNG KIỂM TOÁN (DATABASE DESIGN & AUDIT GUIDELINES)
**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Hệ quản trị CSDL:** PostgreSQL 16 tích hợp tiện ích mở rộng PostGIS 3.4  
**Công nghệ truy xuất:** Entity Framework Core 10 (Npgsql.EntityFrameworkCore.PostgreSQL + NetTopologySuite)  
**Tài liệu đối chiếu:** [dbml.io](file:///c:/Projects/Demo%20Waste%20Classification/documents/dbml.io)  

---

## 1. QUY ƯỚC ĐẶT TÊN TRONG CSDL (NAMING CONVENTIONS)

PostgreSQL phân biệt chữ hoa/thường rất phức tạp nếu dùng dấu ngoặc kép. Do đó, **bắt buộc 100% định danh trong CSDL phải dùng chữ thường và dấu gạch dưới (`snake_case`)**:

| Đối tượng CSDL | Quy tắc đặt tên | Ví dụ chuẩn | Ví dụ sai |
| :--- | :--- | :--- | :--- |
| **Bảng (Table)** | `snake_case` số nhiều | `users`, `trade_orders`, `recycler_profiles`, `waste_categories` | `User`, `TradeOrder`, `trade_order` |
| **Cột (Column)** | `snake_case` | `user_id`, `actual_weight_kg`, `operating_hours`, `created_at` | `UserId`, `ActualWeightKg`, `Actual_Weight` |
| **Khóa chính (PK)** | `pk_[table_name]` | `pk_trade_orders` (cột thường là `id` hoặc `trade_order_id`) | `PK_TradeOrder`, `trade_order_pk` |
| **Khóa ngoại (FK)** | `fk_[table]_[ref_table]` | `fk_trade_orders_users` (cột: `user_id` tham chiếu `users.id`) | `FK_User`, `fk_user_id` |
| **Chỉ mục thường (IDX)**| `idx_[table]_[columns]` | `idx_users_email`, `idx_trade_orders_status_created_at` | `Index_Email`, `idx_Email` |
| **Chỉ mục độc nhất (UQ)**| `uq_[table]_[columns]` | `uq_users_phone_number`, `uq_staff_profiles_staff_code` | `UniquePhone`, `UQ_users_phone` |
| **Chỉ mục không gian (GIST)**| `gix_[table]_[column]`| `gix_recycler_profiles_location` (Dùng cho tọa độ PostGIS) | `idx_location` |

---

## 2. BỘ TRƯỜNG KIỂM TOÁN BẮT BUỘC (MANDATORY AUDIT FIELDS) VÀ XÓA MỀM

Để phục vụ quản lý lịch sử, chống chối bỏ trách nhiệm và đối soát dữ liệu (đặc biệt quan trọng trong giao dịch phế liệu và thanh toán), **mọi bảng nghiệp vụ chính** đều phải kế thừa bộ trường kiểm toán:

### 2.1. Cấu trúc bảng kế thừa `BaseAuditableEntity`
```sql
-- Ví dụ cấu trúc chuẩn cho bảng trade_orders
CREATE TABLE trade_orders (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id),
    partner_id UUID REFERENCES users(id),
    status VARCHAR(50) NOT NULL DEFAULT 'Pending',
    actual_weight_kg NUMERIC(8, 2),
    total_amount_cash NUMERIC(12, 0),
    
    -- ===== BỘ TRƯỜNG KIỂM TOÁN (AUDIT FIELDS) =====
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by VARCHAR(100),
    updated_at TIMESTAMPTZ,
    updated_by VARCHAR(100),
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMPTZ
);
```

### 2.2. Ý nghĩa các trường kiểm toán:
1. `created_at` (`TIMESTAMPTZ`): Thời điểm bản ghi được tạo theo chuẩn UTC. Bắt buộc `NOT NULL`, mặc định `NOW()`.
2. `created_by` (`VARCHAR`): Định danh hoặc ID người dùng thực hiện tạo bản ghi.
3. `updated_at` (`TIMESTAMPTZ`): Thời điểm cập nhật dữ liệu gần nhất.
4. `updated_by` (`VARCHAR`): Người thực hiện cập nhật gần nhất.
5. `is_deleted` (`BOOLEAN`): Cờ xóa mềm. Mặc định là `FALSE`. Khi xóa bản ghi, chỉ chuyển cờ thành `TRUE` và ghi nhận `deleted_at`, **tuyệt đối không chạy lệnh `DELETE` vật lý khỏi ổ đĩa**.

### 2.3. Tự động hóa qua EF Core Interceptor (Developer không cần gán tay)
Tạo một `SaveChangesInterceptor` trong tầng `Infrastructure` để tự động điền `created_at`, `updated_at` mỗi khi gọi `SaveChangesAsync()`:

```csharp
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;

    public AuditableEntityInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, 
        InterceptionResult<int> result, 
        CancellationToken cancellationToken = default)
    {
        var context = eventData.Context;
        if (context == null) return base.SavingChangesAsync(eventData, result, cancellationToken);

        var now = DateTime.UtcNow;
        var currentUserId = _currentUserService.UserId?.ToString() ?? "System";

        foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = currentUserId;
                entry.Entity.IsDeleted = false;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = currentUserId;
            }
            else if (entry.State == EntityState.Deleted)
            {
                // Tự động chuyển đổi xóa cứng thành xóa mềm
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = now;
                entry.Entity.UpdatedBy = currentUserId;
            }
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
```

---

## 3. QUY CHUẨN XỬ LÝ DỮ LIỆU ĐỊA LÝ KHÔNG GIAN (POSTGIS GEOMETRY / GEOGRAPHY)

Một tính năng trọng tâm của EcoLink là tìm kiếm cơ sở thu gom xung quanh người dùng trong bán kính $X$ km (`BR-MTH-04`).

### 3.1. Tuyệt đối không tính khoảng cách bằng C# trong RAM
* ❌ **Sai lầm phổ biến:** Lưu 2 cột `latitude` và `longitude` kiểu số thực `decimal`, sau đó C# query toàn bộ 10,000 cơ sở thu gom về RAM rồi dùng công thức Haversine để tính khoảng cách $\rightarrow$ Làm sập server khi dữ liệu lớn.
*  **Chuẩn PostGIS:** Lưu trữ cột không gian dạng `geography(Point, 4326)` và để CSDL PostgreSQL tính toán bằng phần cứng chuyên dụng.

### 3.2. Cấu hình bảng `recycler_profiles` với PostGIS
```sql
-- Kích hoạt tiện ích mở rộng PostGIS
CREATE EXTENSION IF NOT EXISTS postgis;

-- Thêm cột location chuẩn WGS 84 (SRID 4326)
ALTER TABLE recycler_profiles 
ADD COLUMN location GEOGRAPHY(Point, 4326);

-- Đánh chỉ mục không gian GIST (Bắt buộc để query cực nhanh)
CREATE INDEX gix_recycler_profiles_location 
ON recycler_profiles USING GIST (location);
```

### 3.3. Câu truy vấn tìm trạm thu gom trong bán kính $R$ km (EF Core C#)
Sử dụng thư viện `NetTopologySuite`:
```csharp
var userPoint = new Point(userLongitude, userLatitude) { SRID = 4326 };
double radiusInMeters = radiusKm * 1000;

var nearbyRecyclers = await _dbContext.RecyclerProfiles
    .Where(r => !r.IsDeleted && r.VerificationStatus == VerificationStatus.Verified)
    // ST_DWithin tận dụng chỉ mục GIST tính toán trong vài mili-giây
    .Where(r => r.Location.IsWithinDistance(userPoint, radiusInMeters))
    .OrderBy(r => r.Location.Distance(userPoint))
    .Select(r => new RecyclerMapDto
    {
        Id = r.Id,
        FacilityName = r.FacilityName,
        Address = r.Address,
        DistanceMeters = r.Location.Distance(userPoint)
    })
    .ToListAsync(cancellationToken);
```

---

## 4. QUY CHUẨN KIỂU DỮ LIỆU (DATA TYPES STANDARD)

1. **Khóa chính (Primary Key):**
   * Sử dụng kiểu `UUID` (`Guid` trong C#) cho toàn bộ các bảng giao dịch: `trade_orders`, `users`, `transaction_receipts`. Giúp bảo mật thông tin, không làm lộ số lượng giao dịch và tránh bị tấn công quét tuần tự ID (`/api/v1/orders/1`, `/api/v1/orders/2`).
   * Sử dụng `SERIAL` / `INT` tự tăng cho các bảng danh mục tĩnh ít thay đổi: `waste_categories`, `system_configs`.
2. **Tiền tệ (Currency):**
   * Dùng kiểu `NUMERIC(12, 0)` hoặc `BIGINT` cho tiền Việt Nam Đồng (VND). Tuyệt đối không dùng `FLOAT` hoặc `DOUBLE` vì sẽ bị sai số dấu phẩy động khi tính toán.
3. **Khối lượng rác (Weight):**
   * Dùng `NUMERIC(8, 2)` (Tối đa 999,999.99 kg, lẻ 2 số thập phân).
4. **Trạng thái (Status / Enums):**
   * Lưu dưới dạng `VARCHAR(50)` trong bảng. Không lưu số nguyên `INT` (0, 1, 2) vì khi xem trực tiếp trong CSDL (pgAdmin, DBeaver) sẽ rất khó hiểu, và khi thêm giá trị mới vào giữa Enum trong C# sẽ làm đảo lộn toàn bộ dữ liệu cũ.

---

## 5. QUY CHUẨN CHỐNG TRANH CHẤP DỮ LIỆU ĐỒNG THỜI (CONCURRENCY CONTROL)

Trong EcoLink, có 2 trường hợp rất dễ xảy ra Race Condition (Xung đột đồng thời):
1. **Hai cơ sở thu gom cùng bấm tiếp nhận 1 yêu cầu bàn giao rác:** Chỉ 1 bên được nhận.
2. **Nhiều người dùng cùng đổi một số lượng Voucher có hạn:** Số lượng voucher còn lại không được phép âm.

### 5.1. Cơ chế Khóa Lạc quan (Optimistic Concurrency Control)
PostgreSQL có sẵn cột hệ thống `xmin` (Row Version) tự động thay đổi mỗi khi dòng dữ liệu được cập nhật. Cấu hình trong EF Core:
```csharp
builder.Entity<TradeOrder>()
    .UseXminAsConcurrencyToken();
```
Khi 2 request cùng sửa trạng thái của đơn hàng tại cùng 1 tích tắc, người thứ hai sẽ nhận ngoại lệ `DbUpdateConcurrencyException`. Hệ thống sẽ bắt lỗi này và thông báo ngay: *"Đơn hàng vừa được xử lý bởi người khác. Vui lòng làm mới lại trang."*

---

## 6. QUY TRÌNH MIGRATION CHUẨN (EF CORE MIGRATIONS WORKFLOW)

1. **Quy tắc đặt tên Migration:** Phải thể hiện rõ hành động và thực thể thay đổi:  
   *  `AddTradeOrderTable`
   *  `AddPostGisLocationToRecycler`
   *  `AddAuditFieldsToWasteCategories`
   * ❌ `UpdateDB`, `Migration1`, `FixBug`.
2. **Kiểm tra file SQL trước khi Apply:**  
   Luôn chạy lệnh sinh script để kiểm tra trước:  
   `dotnet ef migrations script --output update.sql`
3. **Môi trường Production / Docker:**  
   Tuyệt đối **không gọi `context.Database.EnsureCreated()`** vì hàm này sẽ bỏ qua toàn bộ lịch sử Migration và không thể cập nhật CSDL sau này. Luôn sử dụng `context.Database.Migrate()`.
