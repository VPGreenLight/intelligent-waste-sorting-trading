# BÁO CÁO SỰ CỐ KỸ THUẬT RUNTIME (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-HND-01`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Backend WebApi (`Ecolink.WebApi`) & Core Infrastructure (`Ecolink.Infrastructure`)  
**Tác vụ phát sinh:** `JOB-03` (Handover Expiration Background Worker)  
**Thời điểm ghi nhận:** 09/11/2026 – Môi trường Runtime Development  
**Mức độ nghiêm trọng:** Trung bình (Medium – Gián đoạn tác vụ nền tự động, không gây sập máy chủ API)

---

## 1. MÔ TẢ HIỆN TƯỢNG (INCIDENT SYNOPSIS)

Trong quá trình khởi chạy máy chủ Backend WebApi, dịch vụ nền `HandoverExpirationJob` (tác vụ tự động quét định kỳ các yêu cầu bàn giao rác quá thời hạn phản hồi để chuyển trạng thái từ `Pending` sang `Expired`) bị văng lỗi ngoại lệ liên tục ở mỗi chu kỳ quét, sinh ra cảnh báo lỗi trong console:

```text
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (63ms)
      UPDATE handover_requests AS h
      SET status = @p,
          updated_at = @p2
      WHERE h.status = 'Pending' AND h.created_at < @cutoff
fail: Ecolink.WebApi.Services.HandoverExpirationJob[0]
      JOB-03 thất bại, sẽ thử lại ở lượt sau.
      Npgsql.PostgresException (0x80004005): 42P01: relation "handover_requests" does not exist
```

---

## 2. TRÍCH ĐOẠN LOG NGUYÊN BẢN (STACK TRACE)

* **Tệp log lưu trữ:** `documents/log/runtime/middleware bug.txt`

```log
fail: Microsoft.EntityFrameworkCore.Database.Command[20102]
      Failed executing DbCommand (63ms) [Parameters=[@p='?', @p2='?' (DbType = DateTime), @cutoff='?' (DbType = DateTime)], CommandType='Text', CommandTimeout='30']
      UPDATE handover_requests AS h
      SET status = @p,
          updated_at = @p2
      WHERE h.status = 'Pending' AND h.created_at < @cutoff
fail: Ecolink.WebApi.Services.HandoverExpirationJob[0]
      JOB-03 thất bại, sẽ thử lại ở lượt sau.
      Npgsql.PostgresException (0x80004005): 42P01: relation "handover_requests" does not exist

      POSITION: 8
         at Npgsql.Internal.NpgsqlConnector.ReadMessageLong(Boolean async, DataRowLoadingMode dataRowLoadingMode, Boolean readingNotifications, Boolean isReadingPrependedMessage)
         at System.Runtime.CompilerServices.PoolingAsyncValueTaskMethodBuilder`1.StateMachineBox`1.System.Threading.Tasks.Sources.IValueTaskSource<TResult>.GetResult(Int16 token)
         at Npgsql.NpgsqlDataReader.NextResult(Boolean async, Boolean isConsuming, CancellationToken cancellationToken)
         at Npgsql.NpgsqlCommand.ExecuteNonQuery(Boolean async, CancellationToken cancellationToken)
         at Microsoft.EntityFrameworkCore.Storage.RelationalCommand.ExecuteNonQueryAsync(RelationalCommandParameterObject parameterObject, CancellationToken cancellationToken)
         at Npgsql.EntityFrameworkCore.PostgreSQL.Storage.Internal.NpgsqlExecutionStrategy.ExecuteAsync[TState,TResult](TState state, Func`4 operation, Func`4 verifySucceeded, CancellationToken cancellationToken)
         at Ecolink.Infrastructure.Services.HandoverRequestService.ExpirePendingAsync(TimeSpan timeout, CancellationToken ct) 
            in C:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend\Ecolink.Infrastructure\Services\HandoverRequestService.cs:line 254
         at Ecolink.WebApi.Services.HandoverExpirationJob.ExecuteAsync(CancellationToken stoppingToken) 
            in C:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend\Ecolink.WebApi\Services\HandoverExpirationJob.cs:line 34
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

### 3.1. Phân tích mã lỗi CSDL
* **Mã lỗi `SqlState: 42P01` (PostgreSQL Error Code):**  
  Đây là mã lỗi chuẩn của PostgreSQL định danh cho lỗi `UNDEFINED_TABLE` (Quan hệ / Bảng không tồn tại). Trình phân tích cú pháp của PostgreSQL (`parserOpenTable`, file `parse_relation.c:1449`) không tìm thấy bảng `handover_requests` trong schema `public`.

### 3.2. Cơ chế thực thi trong mã nguồn
1. Thực thể `HandoverRequest` đã được định nghĩa trong Domain và ánh xạ tên bảng tại `HandoverRequestConfiguration.cs`:
   ```csharp
   builder.ToTable("handover_requests");
   ```
2. Trong `HandoverRequestService.cs` (dòng 254), tác vụ định kỳ `JOB-03` sử dụng tính năng cập nhật nguyên tử không qua nạp bộ nhớ `ExecuteUpdateAsync`:
   ```csharp
   var count = await _db.HandoverRequests
       .Where(h => h.Status == HandoverStatus.Pending && h.CreatedAt < cutoff)
       .ExecuteUpdateAsync(s => s
           .SetProperty(h => h.Status, HandoverStatus.Expired)
           .SetProperty(h => h.UpdatedAt, now), ct);
   ```
3. Lệnh này được Entity Framework Core chuyển trực tiếp thành câu lệnh SQL:
   ```sql
   UPDATE handover_requests AS h
   SET status = @p, updated_at = @p2
   WHERE h.status = 'Pending' AND h.created_at < @cutoff
   ```
4. **Nguyên nhân chính:**  
   CSDL PostgreSQL đang kết nối chưa được áp dụng tệp Migration `20261103102102_HandoverRequests.cs` (chưa chạy `dotnet ef database update`), dẫn tới bảng vật lý `handover_requests` chưa hiện diện trong cơ sở dữ liệu.

---

## 4. ĐÁNH GIÁ TÁC ĐỘNG HỆ THỐNG (SYSTEM IMPACT)

* **Tính ổn định (Process Resilience):**  
  Tác vụ nền `HandoverExpirationJob` được bọc bởi khối `try...catch` ghi log cảnh báo và tiếp tục lặp lại ở chu kỳ tiếp theo (`JOB-03 thất bại, sẽ thử lại ở lượt sau`), do đó **không gây sập tiến trình máy chủ WebApi** (`Unhandler Exception Crash`). Toàn bộ các API phân loại ảnh AI, xác thực người dùng và tra cứu bản đồ vẫn phục vụ bình thường.
* **Nghiệp vụ bàn giao (Business Logic):**  
  Các yêu cầu bàn giao rác quá 2 tiếng không được tự động chuyển sang trạng thái `Expired`, gây ứ đọng dữ liệu chờ xử lý nếu chưa được cập nhật schema.

---

## 5. GIẢI PHÁP ĐÃ THỰC HIỆN VÀ HƯỚNG DẪN XỬ LÝ (RESOLUTION)

### 5.1. Tạo kịch bản DDL SQL độc lập (Đã hoàn thành)
Để hỗ trợ triển khai ngay cả khi dịch vụ đang chạy, hệ thống đã trích xuất toàn bộ kịch bản DDL chuẩn từ Entity Framework Core ra tệp:
* 👉 **Đường dẫn script:** `documents/log/runtime/migration_script.sql`
* Kịch bản đã bao gồm các lệnh tạo bảng hoàn chỉnh:
  - `CREATE TABLE handover_requests (...)` (Dòng 650)
  - `CREATE TABLE handover_request_items (...)` (Dòng 676)
  - `CREATE TABLE handover_receipts (...)` (Dòng 713)

### 5.2. Các bước cập nhật CSDL bằng CLI
Do tiến trình WebApi đang chạy sẽ khóa tệp thư viện `Ecolink.Infrastructure.dll`, các bước cập nhật đúng quy trình như sau:

1. **Bước 1:** Tạm dừng tiến trình máy chủ WebApi đang chạy (nhấn `Ctrl + C` tại cửa sổ Terminal đang chạy `dotnet run`).
2. **Bước 2:** Chạy lệnh cập nhật database:
   ```powershell
   cd c:\Projects\EcoLink\intelligent-waste-sorting-trading
   dotnet ef database update --project src/Ecolink.Backend/Ecolink.Infrastructure --startup-project src/Ecolink.Backend/Ecolink.WebApi
   ```
3. **Bước 3:** Khởi động lại máy chủ WebApi:
   ```powershell
   dotnet run --project src/Ecolink.Backend/Ecolink.WebApi
   ```
   *Tác vụ `HandoverExpirationJob` sẽ kết nối thành công và log lỗi sẽ hoàn toàn biến mất.*

### 5.3. Cách thay thế: Chạy trực tiếp Script SQL
Nếu không muốn tắt server, mở **pgAdmin** hoặc **DBeaver**, kết nối tới database `ecolink_db` và chạy các câu lệnh DDL trong tệp `documents/log/runtime/migration_script.sql`.

---

## 6. BÀI HỌC VÀ BIỆN PHÁP PHÒNG NGỪA (LESSONS LEARNED & PREVENTIONS)

1. **Cấu hình Auto-Migration trong môi trường Dev:**  
   Bổ sung phương thức kiểm tra `context.Database.MigrateAsync()` trong `Program.cs` trước khi các `HostedService` khởi chạy, đảm bảo cấu trúc bảng luôn đồng bộ trước khi Worker đầu tiên kích hoạt.
2. **Health Check Readiness:**  
   Thêm kiểm tra tính sẵn sàng của các bảng cốt lõi vào endpoint `/health` để thông báo rõ ràng cho đội ngũ phát triển khi CSDL chưa sẵn sàng.

