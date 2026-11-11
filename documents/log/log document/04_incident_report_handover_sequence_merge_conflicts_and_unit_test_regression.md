# BÁO CÁO SỰ CỐ KỸ THUẬT HỢP NHẤT NHÁNH & KIỂM THỬ (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-HND-04`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Core Infrastructure (`Ecolink.Infrastructure`), Unit Test Suite (`Ecolink.UnitTests`), Database PostgreSQL (`ecolink_db`)  
**Nhánh nguồn kiểm tra:** `origin/handover-completion-&-transaction-receipt-fix` (`aeabd7b3` -> Merge commit `72a67446` vào `develop`)  
**Tác vụ / Chức năng ảnh hưởng:**
* Module 06: Quản lý Yêu cầu Bàn giao Rác (`HandoverRequestService.GenerateCodeAsync` – UC U-06 / BR-HND-01)
* Module 07: Hoàn tất Giao dịch & Biên nhận Điện tử (`HandoverReceiptService.GenerateCodeAsync` – UC R-06, U-09 / BR-HND-04)
* Hệ thống Migration CSDL (`20261109022316_HandoverCodeSequences` vs `20261109154426_AddPendingModelChanges`)
**Tuyến API ảnh hưởng:**
* `POST /api/v1/handovers`
* `POST /api/v1/recycler/handovers/{id}/receipt`
**Thời điểm ghi nhận:** 11/11/2026 – Môi trường Post-Merge Verification (`develop`)  
**Mức độ nghiêm trọng:** Cao (High – Làm vỡ 31/168 kịch bản Unit Test, sai lệch đặc tả mã biên nhận `RCP-yyyy-NNNN`, lệch snapshot Migration và tiềm ẩn lỗi đụng độ khóa duy nhất `23505` trên PostgreSQL)

---

## 1. MÔ TẢ HIỆN TƯỢNG (INCIDENT SYNOPSIS)

Ngay sau khi kiểm tra và hợp nhất nhánh `origin/handover-completion-&-transaction-receipt-fix` (`aeabd7b3`) vào nhánh phát triển chung `develop` (`72a67446`) nhằm chuyển cơ chế cấp số thứ tự mã yêu cầu bàn giao (`HND-yyyyMMdd-NNNN`) và mã biên nhận (`RCP-yyyy-NNNN`) sang PostgreSQL Sequence nguyên tử (`nextval`), hệ thống phát sinh đồng thời **3 nhóm xung đột kỹ thuật và lỗi hồi quy**:

### 1.1. Hiện tượng 1: Sập hàng loạt 31/168 kịch bản Unit Test trên `EF Core InMemory`
* Khi thực thi bộ kiểm thử đơn vị [`Ecolink.UnitTests`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Ecolink.UnitTests.csproj), toàn bộ **31 test case** thuộc Module 06 ([`HandoverRequestUnitTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Module06_HandoverRequest/HandoverRequestUnitTests.cs)) và Module 07 ([`HandoverReceiptUnitTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Module07_HandoverReceipt/HandoverReceiptUnitTests.cs)) bị thất bại đồng loạt với ngoại lệ:
  > `System.InvalidOperationException : Relational-specific methods can only be used when the context is using a relational database provider.`

### 1.2. Hiện tượng 2: Xung đột định dạng mã biên nhận `ReceiptCode` (`D6` so với `D4`)
* Nhánh `origin/handover-completion-&-transaction-receipt-fix` tự ý đổi định dạng chuỗi số thứ tự của mã biên nhận từ 4 chữ số (`RCP-2026-0001`) sang 6 chữ số (`RCP-2026-000001`).
* Thay đổi này vi phạm trực tiếp biểu thức chính quy kiểm chứng hợp đồng API `^RCP-\d{4}-\d{4}$` trong cả Unit Test (`UTC004_CreateAsync_SequentialReceipts_IncrementsReceiptCode`) lẫn Integration Test ([`ReceiptTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/EcoLink.ApiTests/ReceiptTests.cs)).

### 1.3. Hiện tượng 3: Lệch thứ tự thời gian Migration & Nguy cơ đụng độ `Unique Constraint` với dữ liệu cũ
* Khi kiểm tra danh sách migration bằng `dotnet ef migrations list`, tệp migration mới [`20261109022316_HandoverCodeSequences.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Data/Migrations/20261109022316_HandoverCodeSequences.cs) có mốc thời gian (`02:23:16`) **sớm hơn** migration đã áp dụng trước đó trên `develop` là `20261109154426_AddPendingModelChanges` (`15:44:26`), nằm ở trạng thái `(Pending)` xen giữa lịch sử và khiến tệp [`20261109154426_AddPendingModelChanges.Designer.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Data/Migrations/20261109154426_AddPendingModelChanges.Designer.cs) bị thiếu khai báo `HasSequence`.
* Đồng thời, hai sequence `seq_handover_request_code` và `seq_handover_receipt_code` khi mới tạo trong PostgreSQL bắt đầu đếm lại từ `1` (`START WITH 1`), dẫn tới nguy cơ văng lỗi `23505: duplicate key value violates unique constraint` (`ux_handover_requests_request_code` / `ux_handover_receipts_receipt_code`) nếu bảng `handover_requests` hoặc `handover_receipts` trong `ecolink_db` đã tồn tại các bản ghi `0001`, `0002`,... được tạo trước thời điểm nâng cấp sequence.

---

## 2. TRÍCH ĐOẠN LOG NGUYÊN BẢN (STACK TRACE & TEST LOGS)

* **Tệp log lưu trữ:** [`post_merge_handover_and_di_errors.txt`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/log/runtime/post_merge_handover_and_di_errors.txt)

### 2.1. Ngoại lệ `InvalidOperationException` khi chạy `Ecolink.UnitTests`
```log
[xUnit.net 00:00:02.41]     Ecolink.UnitTests.Module06_HandoverRequest.HandoverRequestUnitTests.UTC001_CreateAsync_ValidSingleItem_ReturnsPendingAndHNDCode [FAIL]
  Error Message:
   System.InvalidOperationException : Relational-specific methods can only be used when the context is using a relational database provider.
  Stack Trace:
     at Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.GetRelationalConnection()
     at Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SqlQueryRaw[TResult](DatabaseFacade databaseFacade, String sql, Object[] parameters)
     at Microsoft.EntityFrameworkCore.RelationalDatabaseFacadeExtensions.SqlQuery[TResult](DatabaseFacade databaseFacade, FormattableString sql)
     at Ecolink.Infrastructure.Services.HandoverRequestService.GenerateCodeAsync(CancellationToken ct) in c:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend\Ecolink.Infrastructure\Services\HandoverRequestService.cs:line 431

Failed!  - Failed:    31, Passed:   137, Skipped:     0, Total:   168, Duration: 4 s - Ecolink.UnitTests.dll (net10.0)
```

### 2.2. Xung đột biểu thức chính quy mã biên nhận (`D6` vs `D4`)
```log
[xUnit.net 00:00:03.12]     Ecolink.UnitTests.Module07_HandoverReceipt.HandoverReceiptUnitTests.UTC004_CreateAsync_SequentialReceipts_IncrementsReceiptCode [FAIL]
  Error Message:
   Assert.Matches() Failure:
   Regex: ^RCP-\d{4}-\d{4}$
   Value: RCP-2026-000001
```

### 2.3. Trạng thái Migration xen kẽ trước khi cập nhật PostgreSQL
```text
info: Ecolink.Infrastructure.DependencyInjection[0]
      Using Local DB Connection String
20261020130550_InitialAuthModule
20261030015551_RecyclerPriceListAndAvailability
20261031134442_StandardizeSnakeCaseSchemaAndDesignColumns
20261103102102_HandoverRequests
20261107151710_HandoverReceipts
20261109022316_HandoverCodeSequences (Pending)
20261109154426_AddPendingModelChanges
```

---

## 3. PHÂN TÍCH NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

### 3.1. Thiếu nhánh bảo vệ Provider (`_db.Database.IsRelational()`)
* Trong nhánh `develop`, dự án đã chuẩn hóa việc dùng `_db.Database.IsRelational()` tại các hàm sử dụng tính năng đặc thù của CSDL quan hệ (như `ExecuteUpdateAsync` và `BeginTransactionAsync` trong [`HandoverRequestService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverRequestService.cs#L255-L276) và [`HandoverReceiptService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs#L277-L305)) để tương thích với `Microsoft.EntityFrameworkCore.InMemory` trong Unit Test.
* Tuy nhiên, nhánh `origin/handover-completion-&-transaction-receipt-fix` được tách ra trước thời điểm bổ sung bộ `Ecolink.UnitTests`, nên hàm `GenerateCodeAsync` gọi thẳng `_db.Database.SqlQuery<long>(...)`. Khi chạy trên `InMemoryDatabase`, `DatabaseFacade.GetRelationalConnection()` lập tức ném `InvalidOperationException`.

### 3.2. Thay đổi định dạng Padding không đồng bộ đặc tả (`{n:D6}`)
* Trong [`HandoverReceiptService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs), nhánh fix đổi chuỗi nội suy từ `$"RCP-{year}-{n:D4}"` thành `$"RCP-{year}-{n:D6}"`, trong khi `HandoverRequestService` vẫn giữ `D4` (`HND-yyyyMMdd-0001`) và toàn bộ tài liệu API / Test Suite quy định chuẩn 4 chữ số tối thiểu (`D4` tự động mở rộng lên 5–6 chữ số khi vượt `9999` mà không làm gãy các mã dưới `10000`).

### 3.3. Phân nhánh song song gây lệch chuỗi thời gian Migration & Sequence khởi tạo từ `1`
* Hai nhánh được tạo migration độc lập trong cùng ngày `09/11/2026`:
  - Nhánh fix tạo `20261109022316_HandoverCodeSequences` lúc `02:23 UTC`.
  - Nhánh `develop` tạo `20261109154426_AddPendingModelChanges` lúc `15:44 UTC`.
* Khi merge vào `develop`, `ApplicationDbContextModelSnapshot.cs` có chứa `HasSequence`, nhưng tệp snapshot trung gian `20261109154426_AddPendingModelChanges.Designer.cs` lại thiếu `HasSequence` của migration đứng trước nó theo thứ tự chữ cái.
* Ngoài ra, câu lệnh `CREATE SEQUENCE seq_handover_receipt_code START WITH 1` không tự biết số thứ tự lớn nhất đã lưu trong bảng `handover_receipts` / `handover_requests` trước đó, dẫn đến nguy cơ trùng mã ở những lần gọi `nextval` đầu tiên.

---

## 4. ĐÁNH GIÁ TÁC ĐỘNG HỆ THỐNG (SYSTEM IMPACT)

| Tiêu chí | Mức độ tác động | Chi tiết |
| :--- | :--- | :--- |
| **Kiểm thử tự động (CI / Unit Tests)** | Nghiêm trọng | 31/168 Unit Tests thất bại (tỷ lệ lỗi 18.45%), chặn hoàn toàn quy trình kiểm chứng hồi quy Module 06 & 07. |
| **Tương thích API & Integration Tests** | Cao | Sai định dạng `ReceiptCode` làm gãy kiểm chứng Regex của `ReceiptTests` và hiển thị không đồng nhất trên biên nhận giao diện. |
| **Tính toàn vẹn CSDL PostgreSQL** | Cao | Nếu không chạy `dotnet ef database update` và không có vòng lặp bỏ qua mã tồn tại, API tạo đơn/lập biên nhận trên PostgreSQL sẽ văng lỗi `42P01 (relation "seq_handover_..." does not exist)` hoặc `23505 (unique_violation)`. |

---

## 5. QUÁ TRÌNH KHẮC PHỤC VÀ TRIỂN KHAI (RESOLUTION & IMPLEMENTATION)

*(Đã thực hiện và ghi nhận qua các commit `8519e7b1` và `2c26e166` trên nhánh `develop`)*

### 5.1. Bọc `IsRelational()`, khôi phục chuẩn `D4` và bổ sung cơ chế tự động bỏ qua mã cũ
1. Cập nhật [`HandoverRequestService.GenerateCodeAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverRequestService.cs#L427-L447):
   ```csharp
   private async Task<string> GenerateCodeAsync(CancellationToken ct)
   {
       var prefix = $"HND-{(DateTime.UtcNow + LocalOffset):yyyyMMdd}-";
       if (_db.Database.IsRelational())
       {
           while (true)
           {
               var n = await _db.Database
                   .SqlQuery<long>($"SELECT nextval('seq_handover_request_code') AS \"Value\"")
                   .SingleAsync(ct);
               var candidate = $"{prefix}{n:D4}";
               if (!await _db.HandoverRequests.AnyAsync(h => h.RequestCode == candidate, ct))
                   return candidate;
           }
       }

       var count = await _db.HandoverRequests.CountAsync(h => h.RequestCode.StartsWith(prefix), ct);
       return $"{prefix}{count + 1:D4}";
   }
   ```
2. Cập nhật tương ứng tại [`HandoverReceiptService.GenerateCodeAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs#L666-L685) sử dụng `seq_handover_receipt_code`, định dạng `{n:D4}` và kiểm tra `AnyAsync` chống trùng lặp với dữ liệu cũ trước sequence.

### 5.2. Đồng bộ hóa Snapshot Migration & Cập nhật CSDL PostgreSQL (`ecolink_db`)
1. Đồng bộ khai báo `modelBuilder.HasSequence("seq_handover_receipt_code")` và `modelBuilder.HasSequence("seq_handover_request_code")` vào [`20261109154426_AddPendingModelChanges.Designer.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Data/Migrations/20261109154426_AddPendingModelChanges.Designer.cs) khớp hoàn toàn với [`ApplicationDbContextModelSnapshot.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Data/Migrations/ApplicationDbContextModelSnapshot.cs).
2. Thực thi cập nhật migration vào PostgreSQL local (`ecolink_db`):
   ```powershell
   dotnet ef database update `
       --project src/Ecolink.Backend/Ecolink.Infrastructure/Ecolink.Infrastructure.csproj `
       --startup-project src/Ecolink.Backend/Ecolink.WebApi/Ecolink.WebApi.csproj
   ```
   Hệ quản trị PostgreSQL thực thi thành công `CREATE SEQUENCE seq_handover_receipt_code START WITH 1 INCREMENT BY 1` và `CREATE SEQUENCE seq_handover_request_code START WITH 1 INCREMENT BY 1`.

---

## 6. KẾT QUẢ NGHIỆM THU VÀ XÁC THỰC (VERIFICATION & RESULTS)

1. **Trạng thái Migration trên `ecolink_db` (`dotnet ef migrations list`):**
   Toàn bộ **7/7 migrations** đều ở trạng thái đã áp dụng hoàn tất (không còn `(Pending)` và không phát sinh cảnh báo `PendingModelChangesWarning`):
   ```text
   20261020130550_InitialAuthModule
   20261030015551_RecyclerPriceListAndAvailability
   20261031134442_StandardizeSnakeCaseSchemaAndDesignColumns
   20261103102102_HandoverRequests
   20261107151710_HandoverReceipts
   20261109022316_HandoverCodeSequences
   20261109154426_AddPendingModelChanges
   ```
2. **Kết quả chạy lại `Ecolink.UnitTests`:**
   - **Passed: 168 / 168, Failed: 0, Skipped: 0** (thời gian chạy ~4–11s).
3. **Kết quả kiểm chứng thực tế trên PostgreSQL qua `EcoLink.ApiTests` (`HandoverTests` & `ReceiptTests`):**
   - Toàn bộ kịch bản tạo đơn đồng thời và lập biên nhận điện tử trên CSDL PostgreSQL thật hoạt động trơn tru, sinh mã đúng định dạng `HND-yyyyMMdd-NNNN` / `RCP-yyyy-NNNN` và không xảy ra bất kỳ lỗi trùng khóa nào.

---

## 7. KHUYẾN NGHỊ VÀ BÀI HỌC KINH NGHIỆM (LESSONS LEARNED & PREVENTIONS)

1. **Quy tắc bắt buộc khi dùng Raw SQL / Relational Extensions trong Infrastructure:**
   - Mọi lời gọi phương thức mở rộng đặc thù của hệ quản trị CSDL quan hệ (`SqlQuery`, `ExecuteSqlRaw`, `ExecuteUpdateAsync`, `ExecuteDeleteAsync`, `BeginTransactionAsync`) **bắt buộc** phải kiểm tra `if (_db.Database.IsRelational())` kèm nhánh xử lý tương đương trên LINQ chuẩn để bảo toàn khả năng chạy kiểm thử đơn vị với `EF Core InMemory`.
2. **An toàn khi bổ sung Sequence cho bảng đã có dữ liệu sẵn:**
   - Khi thêm `CREATE SEQUENCE` mới cho một bảng đã vận hành trên môi trường Development/Staging, cần kết hợp kiểm tra tồn tại (`AnyAsync`) hoặc đồng bộ giá trị `setval(...)` để tránh đụng độ với các bản ghi lịch sử.
3. **Kiểm tra chéo lịch sử Migration khi Merge nhánh song song:**
   - Sau mỗi thao tác `git merge` có chứa tệp trong `Data/Migrations/`, luôn chạy `dotnet ef migrations list` và `dotnet test` ngay lập tức trước khi đẩy code hoặc chạy máy chủ.

