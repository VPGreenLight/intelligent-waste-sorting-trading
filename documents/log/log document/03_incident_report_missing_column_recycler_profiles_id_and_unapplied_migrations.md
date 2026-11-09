# BÁO CÁO SỰ CỐ KỸ THUẬT RUNTIME (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-DB-03`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Core Infrastructure (`Ecolink.Infrastructure`), Backend WebApi (`Ecolink.WebApi`), Database PostgreSQL (`ecolink_db`)  
**Tác vụ / Chức năng ảnh hưởng:**
* Module 05: Tìm kiếm Cơ sở Thu gom theo Vị trí Địa lý (UC U-05 / BR-MTH-01, BR-MTH-04, BR-MTH-05, BR-MTH-07)
* Module 06 & 07: Tác vụ nền quét đơn bàn giao quá hạn `HandoverExpirationJob` (`JOB-03`)
**Tuyến API & Job ảnh hưởng:**
* `GET /api/v1/recyclers/nearby`
* `GET /api/v1/recyclers/{id}`
* `HandoverExpirationJob.ExecuteAsync` (`JOB-03`)
**Thời điểm ghi nhận:** 09/11/2026 – Môi trường Runtime Development  
**Mức độ nghiêm trọng:** Cao (High – Gián đoạn chức năng hiển thị trạm thu gom trên bản đồ và gây lỗi ngoại lệ chu kỳ trong Background Worker)

---

## 1. MÔ TẢ HIỆN TƯỢNG (INCIDENT SYNOPSIS)

Trong quá trình khởi chạy và tích hợp hệ thống giữa Frontend (`ecolink-web`) và Backend WebApi (`Ecolink.WebApi`), hệ thống phát sinh đồng thời **hai lỗi cơ sở dữ liệu nghiêm trọng** từ Entity Framework Core kết nối PostgreSQL (`Npgsql`):

### 1.1. Hiện tượng 1: Bản đồ không hiển thị cơ sở thu gom và báo lỗi hệ thống 500
* **Màn hình giao diện (`/map`):**
  - Người dùng chọn vị trí tìm kiếm tại khu vực **Hòa Lạc** (Lat: `21.0124`, Lng: `105.5253`), hệ thống xuất hiện thông báo lỗi màu đỏ:
    > *"Đã xảy ra sự cố trong quá trình xử lý hệ thống. Vui lòng thử lại sau."*
  - Danh sách cơ sở trống rỗng và hiển thị trạng thái `0 mở cửa` mặc dù đã cấu hình bán kính quét từ 10 km đến 25 km.
* **Đối chiếu dữ liệu trong PostgreSQL (`ecolink_db`):**
  - Bảng `recycler_profiles` đã có sẵn đầy đủ **11 bản ghi**, bao gồm 4 cơ sở thu mua thực tế tại khu vực Hòa Lạc (`Trạm Thu Gom Tái Chế Xanh Hòa Lạc`, `Cơ Sở Phế Liệu Minh Tâm — Ngã Tư Hòa Lạc`, `Vựa Tái Chế Bình Yên — Hòa Lạc`, `Điểm Thu Mua Phế Liệu Phú Cát — Hòa Lạc`).
  - Toàn bộ 4 cơ sở đều thỏa mãn trạng thái `verification_status = 'Verified'` và `active_status = 'Online'`, tài khoản `users` tương ứng đều ở trạng thái `status = 'Active'`.
* **Nguyên nhân tầng API:**
  - API `GET /api/v1/recyclers/nearby` bị ném ngoại lệ `Npgsql.PostgresException (0x80004005): 42703: column r.Id does not exist`, khiến API sập và trả về mã HTTP `500 Internal Server Error`.

### 1.2. Hiện tượng 2: Tác vụ nền `HandoverExpirationJob` thất bại liên tục
* Tiến trình Background Hosted Service `HandoverExpirationJob` (quét định kỳ 5 phút/lần để tự động hủy các đơn yêu cầu thu gom đã quá hạn phản hồi) văng ngoại lệ liên tục:
  > `Npgsql.PostgresException (0x80004005): 42P01: relation "handover_requests" does not exist`
* Ngoại lệ này khiến tác vụ nền báo lỗi `JOB-03 thất bại, sẽ thử lại ở lượt sau` ở mỗi chu kỳ chạy.

---

## 2. TRÍCH ĐOẠN LOG NGUYÊN BẢN (STACK TRACE & RUNTIME LOGS)

* **Tệp log lưu trữ:** `documents/log/runtime/bug.txt`

### 2.1. Lỗi truy vấn hồ sơ cơ sở thu gom (`column r.Id does not exist`)
```log
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (3ms) [Parameters=[@targetUserId='?' (DbType = Guid)], CommandType='Text', CommandTimeout='30']
      SELECT r.user_id, r.active_status, r.business_address, r.business_photo_url, r.busy_reason, r.contact_phone, r.created_at, r.created_by, r.deleted_at, r.description, r.facility_name, r."Id", r.is_deleted, r.latitude, r.legal_permit_url, r.geom, r.longitude, r.operating_hours, r.rejection_reason, r.representative_name, r.updated_at, r.updated_by, r.verification_status, r.weekly_schedule
      FROM recycler_profiles AS r
      WHERE r.user_id = @targetUserId
      LIMIT 1
fail: Microsoft.EntityFrameworkCore.Query[10100]
      An exception occurred while iterating over the results of a query for context type 'Ecolink.Infrastructure.Data.ApplicationDbContext'.
      Npgsql.PostgresException (0x80004005): 42703: column r.Id does not exist

      POSITION: 184
         at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
         at Microsoft.EntityFrameworkCore.Query.Internal.SingleQueryingEnumerable`1.AsyncEnumerator.MoveNextAsync()
         at Ecolink.Infrastructure.Services.GeoMatchingService.EnsureDemoCollectorPricesSeededAsync(CancellationToken ct) in c:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend\Ecolink.Infrastructure\Services\GeoMatchingService.cs:line 755
        Exception data:
          Severity: ERROR
          SqlState: 42703
          MessageText: column r.Id does not exist
          Position: 184
          File: parse_relation.c
          Line: 3722
          Routine: errorMissingColumn
```

Tiếp tục phát sinh tại câu lệnh tìm kiếm cơ sở xung quanh tọa độ (`GeoMatchingService.FindNearestPartnersAsync`):
```log
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (4ms) [Parameters=[@minLat='?' (DbType = Decimal), @maxLat='?' (DbType = Decimal), @minLng='?' (DbType = Decimal), @maxLng='?' (DbType = Decimal)], CommandType='Text', CommandTimeout='30']
      SELECT r.user_id, ..., r."Id", ..., u0.id, ...
      FROM recycler_profiles AS r
      INNER JOIN users AS u0 ON r.user_id = u0.id
      ...
      WHERE NOT (r.is_deleted) AND r.verification_status = 'Verified' AND r.active_status = 'Online'
fail: Microsoft.EntityFrameworkCore.Query[10100]
      Npgsql.PostgresException (0x80004005): 42703: column r.Id does not exist
         at Ecolink.Infrastructure.Services.GeoMatchingService.FindNearestPartnersAsync(...) in c:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend\Ecolink.Infrastructure\Services\GeoMatchingService.cs:line 225
```

### 2.2. Lỗi bảng không tồn tại trong tác vụ nền (`relation "handover_requests" does not exist`)
```log
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (4ms) [Parameters=[@p='?', @p2='?' (DbType = DateTime), @cutoff='?' (DbType = DateTime)], CommandType='Text', CommandTimeout='30']
      UPDATE handover_requests AS h
      SET status = @p,
          updated_at = @p2
      WHERE h.status = 'Pending' AND h.created_at < @cutoff
fail: Ecolink.WebApi.Services.HandoverExpirationJob[0]
      JOB-03 thất bại, sẽ thử lại ở lượt sau.
      Npgsql.PostgresException (0x80004005): 42P01: relation "handover_requests" does not exist

      POSITION: 8
         at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(...)
         at Ecolink.Infrastructure.Services.HandoverRequestService.ExpirePendingAsync(TimeSpan timeout, CancellationToken ct) in c:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend\Ecolink.Infrastructure\Services\HandoverRequestService.cs:line 254
         at Ecolink.WebApi.Services.HandoverExpirationJob.ExecuteAsync(CancellationToken stoppingToken) in c:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend\Ecolink.WebApi\Services\HandoverExpirationJob.cs:line 34
        Exception data:
          Severity: ERROR
          SqlState: 42P01
          MessageText: relation "handover_requests" does not exist
          Position: 8
          File: parse_relation.c
          Line: 1449
          Routine: parserOpenTable
```

---

## 3. PHÂN TÍCH NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

### 3.1. Sự cố 1: Lệch ánh xạ ORM Kế thừa vs Lược đồ CSDL (`RecyclerProfileConfiguration`)
1. **Mô hình kế thừa Domain:**
   - Thực thể `RecyclerProfile` kế thừa từ `BaseAuditableEntity`, và `BaseAuditableEntity` lại kế thừa từ `BaseEntity`:
     ```csharp
     public abstract class BaseEntity
     {
         public Guid Id { get; set; } = Guid.NewGuid();
     }
     ```
   - Do đó, trong lớp C# `RecyclerProfile` luôn có thuộc tính `public Guid Id`.
2. **Thiết kế CSDL quan hệ 1-1 (Shared Primary Key):**
   - Theo tài liệu thiết kế CSDL [`database_design_and_audit_guidelines.md`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/guideline/database_design_and_audit_guidelines.md), bảng `recycler_profiles` có quan hệ 1-1 với bảng `users`, sử dụng trực tiếp cột `user_id` làm khóa chính (`pk_recycler_profiles`). Bảng trong database **không có cột nào tên `id` hay `Id`**.
3. **Thiếu sót trong cấu hình Fluent API:**
   - Khi so sánh với thực thể tương đồng là `StaffProfile` tại [`StaffProfileConfiguration.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Data/Configurations/StaffProfileConfiguration.cs#L14-L16):
     ```csharp
     builder.ToTable("staff_profiles");
     builder.HasKey(s => s.UserId).HasName("pk_staff_profiles");

     // Bỏ cột Id thừa kế từ BaseEntity vì khóa chính là UserId (Shared PK 1-1 với users)
     builder.Ignore(s => s.Id);
     ```
   - Trong khi đó, tại [`RecyclerProfileConfiguration.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Data/Configurations/RecyclerProfileConfiguration.cs#L11-L13):
     ```csharp
     builder.ToTable("recycler_profiles");
     builder.HasKey(r => r.UserId).HasName("pk_recycler_profiles");
     // THIẾU HOÀN TOÀN: builder.Ignore(r => r.Id);
     ```
   - **Hậu quả:** Vì không có chỉ thị `builder.Ignore(r => r.Id)`, EF Core coi thuộc tính `Id` là một cột bình thường và tự động đưa vào danh sách chiếu của mọi câu lệnh SQL `SELECT r."Id"`. Khi gửi sang PostgreSQL, engine phân tích cú pháp PostgreSQL ném mã lỗi `42703 (errorMissingColumn)`.

### 3.2. Sự cố 2: Tồn đọng Migrations chưa áp dụng vào Database (`Unapplied Migrations Backlog`)
1. **Tình trạng lược đồ thực tế của CSDL `ecolink_db`:**
   - Cơ sở dữ liệu local trên máy phát triển mới chỉ được áp dụng đến migration `20261031134442_StandardizeSnakeCaseSchemaAndDesignColumns` (chỉ gồm 17 bảng cơ bản).
2. **Các Migration bị tồn đọng chưa được thực thi (`database update`):**
   - `20261103102102_HandoverRequests.cs`: Chứa lệnh `CREATE TABLE handover_requests` và `CREATE TABLE handover_request_items`.
   - `20261107151710_HandoverReceipts.cs`: Chứa lệnh `CREATE TABLE handover_receipts`.
   - `20261109154426_AddPendingModelChanges.cs`: Chứa lệnh `CREATE TABLE chat_messages` cùng các cột mở rộng đàm phán trong `trade_orders`.
3. **Hậu quả:** Khi dịch vụ `HandoverExpirationJob` khởi động và chạy định kỳ, mã thực thi gọi câu lệnh `UPDATE handover_requests...`. Vì bảng này chưa từng được tạo trong PostgreSQL, hệ quản trị CSDL trả về mã lỗi `42P01 (relation "handover_requests" does not exist)`.

---

## 4. QUÁ TRÌNH KHẮC PHỤC VÀ TRIỂN KHAI (RESOLUTION & IMPLEMENTATION)

### 4.1. Bổ sung cấu hình bỏ qua thuộc tính `Id` trong ORM
Chỉnh sửa tệp [`RecyclerProfileConfiguration.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Data/Configurations/RecyclerProfileConfiguration.cs#L11-L16):

```csharp
builder.ToTable("recycler_profiles");
builder.HasKey(r => r.UserId).HasName("pk_recycler_profiles");

// Bỏ cột Id thừa kế từ BaseEntity vì khóa chính là UserId (Shared PK 1-1 với users)
builder.Ignore(r => r.Id);
```
*Tác dụng:* Đảm bảo Entity Framework Core không bao giờ sinh ra cột `r."Id"` trong bất kỳ câu lệnh SQL nào tương tác với bảng `recycler_profiles`.

### 4.2. Dừng tiến trình chạy Backend và Biên dịch lại Solution
* Dừng tiến trình `Ecolink.WebApi` để giải phóng khóa tệp DLL (`Ecolink.Infrastructure.dll`).
* Thực hiện biên dịch kiểm tra toàn bộ solution:
  ```powershell
  dotnet build src/Ecolink.Backend/Ecolink.slnx -m:1 /p:UseSharedCompilation=false
  ```
  *Kết quả:* Build succeeded với **0 Warning(s)** và **0 Error(s)**.

### 4.3. Đồng bộ hóa toàn bộ Migrations vào PostgreSQL
Thực thi lệnh cập nhật cơ sở dữ liệu trên môi trường Development:
```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet ef database update `
    --project src/Ecolink.Backend/Ecolink.Infrastructure/Ecolink.Infrastructure.csproj `
    --startup-project src/Ecolink.Backend/Ecolink.WebApi/Ecolink.WebApi.csproj `
    --no-build
```

**Nhật ký thực thi CSDL ghi nhận thành công:**
1. Áp dụng migration `20261103102102_HandoverRequests`:
   - Tạo thành công bảng `handover_requests`.
   - Tạo thành công bảng `handover_request_items`.
   - Bổ sung các chỉ mục khóa ngoại liên kết tới `users` và `recycler_profiles`.
2. Áp dụng migration `20261107151710_HandoverReceipts`:
   - Tạo thành công bảng biên nhận `handover_receipts`.
3. Áp dụng migration `20261109154426_AddPendingModelChanges`:
   - Tạo thành công bảng đàm phán trực tiếp `chat_messages`.
   - Bổ sung các trường thương lượng (`agreed_price_per_kg`, `category_code`, `confirmed_time_slot`, `proposed_time_slot`, `recycler_user_id`) vào bảng `trade_orders`.
   - Ghi nhận trạng thái hoàn tất vào bảng `__EFMigrationsHistory`.

---

## 5. KẾT QUẢ NGHIỆM THU VÀ XÁC THỰC (VERIFICATION)

### 5.1. Kiểm tra trạng thái lược đồ qua `dotnet ef migrations list`
Toàn bộ 6/6 migration đã được ghi nhận trạng thái **đã áp dụng (Applied)** trong CSDL:
```text
20261020130550_InitialAuthModule                          (Applied)
20261030015551_RecyclerPriceListAndAvailability          (Applied)
20261031134442_StandardizeSnakeCaseSchemaAndDesignColumns (Applied)
20261103102102_HandoverRequests                           (Applied)
20261107151710_HandoverReceipts                           (Applied)
20261109154426_AddPendingModelChanges                     (Applied)
```
Không còn migration nào ở trạng thái chờ (Pending).

### 5.2. Kiểm tra tính ổn định của hệ thống
1. **Truy vấn điểm thu gom bản đồ (`/api/v1/recyclers/nearby`):**
   - Câu lệnh SQL không còn mệnh đề `r."Id"`.
   - Các cơ sở thu mua tại khu vực Hòa Lạc được truy vấn trực tiếp từ bảng `recycler_profiles` trong CSDL và phản hồi đầy đủ về Frontend mà không bị văng lỗi 500 hay kích hoạt fallback.
2. **Tác vụ nền `HandoverExpirationJob`:**
   - Câu lệnh `UPDATE handover_requests AS h...` thực thi bình thường trên bảng `handover_requests` vừa được tạo, không còn gặp lỗi ngoại lệ `42P01`.

---

## 6. KHUYẾN NGHỊ VÀ BÀI HỌC KINH NGHIỆM (LESSONS LEARNED & PREVENTIONS)

1. **Chuẩn hóa cấu hình Entity có Shared Primary Key:**
   - Bất cứ Entity nào kế thừa `BaseEntity` (hoặc `BaseAuditableEntity`) nhưng lại sử dụng một thuộc tính khác làm khóa chính (như `UserId` trong các bảng hồ sơ quan hệ 1-1 `staff_profiles`, `recycler_profiles`), **bắt buộc** phải có dòng `builder.Ignore(e => e.Id);` trong file cấu hình Fluent API tương ứng.
2. **Quy trình đồng bộ Migration khi kéo mã nguồn (Git Pull):**
   - Trong quá trình phát triển nhóm, khi kéo nhánh mới hoặc merge có chứa các tệp trong thư mục `Data/Migrations/`, nhà phát triển bắt buộc phải thực hiện lệnh `dotnet ef database update` trước khi khởi chạy Backend.
3. **Cấu hình chuỗi kết nối trong môi trường phát triển:**
   - Khi chạy các công cụ CLI như `dotnet ef`, cần đảm bảo biến môi trường `ASPNETCORE_ENVIRONMENT=Development` để công cụ nạp đúng chuỗi kết nối `ecolink_db` từ `appsettings.Development.json` thay vì chuỗi placeholder ở `appsettings.json`.

