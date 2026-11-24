# BÁO CÁO SỰ CỐ LỖI `NullReferenceException` / `ArgumentNullException` TRONG 5 `FluentValidation` VALIDATORS KHI THIẾU `CascadeMode.Stop` (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-VAL-14`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Tầng kiểm tra hợp lệ đầu vào `Ecolink.Application.Validators` (Governance, AI Curation, Negotiation Chat, Recycler Schedule, Waste Catalog)  
**Các tệp ảnh hưởng chính:**
* **Validators đã vá:**
  * [`GovernanceValidators.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/GovernanceValidators.cs#L94-L98) (`UpdateStaffPermissionsValidator`)
  * [`AiCurationValidators.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/AiCurationValidators.cs#L97-L101) (`BatchAssignLabelsRequestValidator`)
  * [`NegotiationChatValidators.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/NegotiationChatValidators.cs#L15-L19) (`SendChatMessageRequestValidator`)
  * [`RecyclerValidators.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/RecyclerValidators.cs#L130-L133) (`UpdateScheduleRequestValidator`)
  * [`WasteCatalogValidators.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/WasteCatalogValidators.cs#L65-L69) (`ToggleWasteCategoryStatusValidator`)
* **Unit Tests kiểm chứng & khóa hồi quy:** [`GovernanceChatValidatorTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Validators/GovernanceChatValidatorTests.cs), [`AiBlogGamificationValidatorTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Validators/AiBlogGamificationValidatorTests.cs), [`RecyclerReviewPackageWasteValidatorTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Validators/RecyclerReviewPackageWasteValidatorTests.cs)  
**Thời điểm ghi nhận:** 23/11/2026 (Phát hiện sau khi hợp nhất nhánh `origin/add-unit-test` qua commit `27f3e9a9` và vá triệt để tại commit `87b74b79`)  
**Mức độ nghiêm trọng:** Cao (High – Lỗi mã nguồn thực tế trong Application layer; khi client gửi payload JSON có trường `null`, ASP.NET Core Validator ném `ArgumentNullException` / `NullReferenceException` chưa bắt làm API trả về `500 Internal Server Error` thay vì `400 Bad Request` kèm thông báo lỗi chuẩn)

---

## 1. MÔ TẢ HIỆN TƯỢNG (INCIDENT SYNOPSIS)

1. Ngay sau khi hợp nhất bộ kiểm thử Validator từ nhánh `origin/add-unit-test` (`27f3e9a9`), ca kiểm thử [`UpdatePermissions_Null_ReturnsValidationError_NotException`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Validators/GovernanceChatValidatorTests.cs) bị **`Failed`**:
   * Khi truyền `Permissions = null` vào `UpdateStaffPermissionsValidator`, thay vì trả về đối tượng `ValidationResult` có `IsValid = false` cùng thông báo *"Danh sách quyền không được null."*, validator lại **ném trực tiếp ngoại lệ `System.ArgumentNullException: Value cannot be null. (Parameter 'source')`** tại hàm `Enumerable.All(...)`.
2. Khi rà soát mở rộng toàn bộ các class kế thừa `AbstractValidator<T>` trong `Ecolink.Application/Validators/`, nhóm kỹ thuật phát hiện thêm **4 validator khác** mắc đúng mẫu lỗi y hệt khi các thuộc tính `ItemIds`, `MessageType`, `Days`, hoặc `Status` nhận giá trị `null`.

---

## 2. CHUỖI TRUY VẾT LỖI (VALIDATION PIPELINE TRACE)

```text
[1] Client gửi JSON thiếu/null thuộc tính (ví dụ: { "permissions": null } hoặc { "itemIds": null })
      │
      ▼
[2] FluentValidation mặc định cấu hình `RuleLevelCascadeMode = CascadeMode.Continue`
      │ -> Bước 1: Chạy `.NotNull()` (hoặc `.NotEmpty()`) -> Ghi nhận lỗi Validation Error #1
      │ -> Bước 2: KHÔNG DỪNG LẠI! Vẫn chạy tiếp `.Must(...)` nằm trên cùng RuleFor(...)!
      │
      ▼
[3] Biểu thức `.Must(...)` không kiểm tra null trước khi gọi phương thức đối tượng / LINQ:
      ├─► `perms => perms.All(...)`                  -> ArgumentNullException ("source")
      ├─► `ids => ids.Count is >= 1 and <= 200`      -> NullReferenceException
      ├─► `t => AllowedMessageTypes.Contains(t.Trim()...)` -> NullReferenceException
      ├─► `d => d.Count <= 7`                        -> NullReferenceException
      └─► `s => AllowedStatuses.Contains(s.Trim())`  -> NullReferenceException
      │
      ▼
[4] Ngoại lệ thoát ra khỏi lớp Validator -> Tầng GlobalExceptionMiddleware bắt như lỗi hệ thống
      └─► API trả về HTTP 500 Internal Server Error thay vì HTTP 400 Validation Error!
```

---

## 3. PHÂN TÍCH CHI TIẾT NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

1. **Hành vi mặc định `CascadeMode.Continue` của FluentValidation:** Theo thiết kế mặc định của thư viện `FluentValidation`, mọi rule thành phần nối tiếp nhau trong một chuỗi `RuleFor(x => x.Prop).NotNull().Must(...)` đều được thực thi bất chấp `.NotNull()` hay `.NotEmpty()` phía trước đã đánh rớt. Do đó, predicate bên trong `.Must(...)` **luôn có khả năng nhận tham số `null`**.
2. **Thiếu defensive null guard trong Lambda của `.Must(...)`:** Cả 5 validator sau đây đều truy cập trực tiếp `.All(...)`, `.Count`, hoặc `.Trim()` trên biến tham số chưa được bảo vệ `!= null`:
   * [`GovernanceValidators.cs#L94-L98`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/GovernanceValidators.cs#L94-L98): `RuleFor(x => x.Permissions).NotNull().Must(perms => perms.All(StaffPermissions.IsValid))`
   * [`AiCurationValidators.cs#L97-L101`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/AiCurationValidators.cs#L97-L101): `RuleFor(x => x.ItemIds).NotNull().Must(ids => ids.Count is >= 1 and <= 200)`
   * [`NegotiationChatValidators.cs#L15-L19`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/NegotiationChatValidators.cs#L15-L19): `RuleFor(x => x.MessageType).NotEmpty().Must(t => AllowedMessageTypes.Contains(t.Trim().ToLowerInvariant()))`
   * [`RecyclerValidators.cs#L130-L133`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/RecyclerValidators.cs#L130-L133): `RuleFor(x => x.Days).NotEmpty().Must(d => d.Count <= 7)`
   * [`WasteCatalogValidators.cs#L65-L69`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/WasteCatalogValidators.cs#L65-L69): `RuleFor(x => x.Status).NotEmpty().Must(s => AllowedStatuses.Contains(s.Trim()))`

---

## 4. ĐÁNH GIÁ TÁC ĐỘNG HỆ THỐNG (SYSTEM IMPACT)

| Thành phần | Mức độ ảnh hưởng | Mô tả tác động thực tế |
| :--- | :---: | :--- |
| **5 Endpoint APIs tương ứng** | Cao | Nếu client hoặc caller tích hợp gửi `null` (hoặc thiếu trường non-nullable mặc định thành `null`), các endpoint `PUT /admin/staff/{id}/permissions`, `POST /staff/ai-labeling/batch-assign`, `POST /trade/orders/{id}/messages`, `PUT /recycler/availability/schedule`, `PATCH /waste-categories/{id}/status` đều có thể ném lỗi 500 thay vì 400 Validation Error. |
| **Bảo mật & Trải nghiệm API** | Trung bình | Phản hồi lỗi `500` không trả về danh sách trường sai theo chuẩn `ApiResponse<object>` (`MSG-GEN-01`), làm sai lệch hợp đồng API và gây cảnh báo sai trên log server. |

---

## 5. GIẢI PHÁP KHẮC PHỤC (REMEDIATION IMPLEMENTED — COMMIT `87b74b79`)

Đã vá triệt để theo chuẩn **2 lớp bảo vệ** trên toàn bộ **5 Validator** (cả chặn cascade bằng `.Cascade(CascadeMode.Stop)` lẫn kiểm tra tường minh `!= null` / `!string.IsNullOrWhiteSpace(...)` bên trong `.Must(...)`):

1. **`UpdateStaffPermissionsValidator`** ([`GovernanceValidators.cs#L94-L98`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/GovernanceValidators.cs#L94-L98)):
   ```csharp
   RuleFor(x => x.Permissions)
       .Cascade(CascadeMode.Stop)
       .NotNull().WithMessage("Danh sách quyền không được null.")
       .Must(perms => perms != null && perms.All(StaffPermissions.IsValid))
       .WithMessage("Tồn tại mã quyền không nằm trong danh mục chuẩn RBAC của hệ thống (BR-ADM-02).");
   ```
2. **`BatchAssignLabelsRequestValidator`** ([`AiCurationValidators.cs#L97-L101`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/AiCurationValidators.cs#L97-L101)): Bổ sung `.Cascade(CascadeMode.Stop)` và `ids => ids != null && ids.Count is >= 1 and <= 200`.
3. **`SendChatMessageRequestValidator`** ([`NegotiationChatValidators.cs#L15-L19`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/NegotiationChatValidators.cs#L15-L19)): Bổ sung `.Cascade(CascadeMode.Stop)` và `t => !string.IsNullOrWhiteSpace(t) && AllowedMessageTypes.Contains(t.Trim().ToLowerInvariant())`.
4. **`UpdateScheduleRequestValidator`** ([`RecyclerValidators.cs#L130-L133`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/RecyclerValidators.cs#L130-L133)): Bổ sung `.Cascade(CascadeMode.Stop)` và `d => d != null && d.Count <= 7`.
5. **`ToggleWasteCategoryStatusValidator`** ([`WasteCatalogValidators.cs#L65-L69`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/WasteCatalogValidators.cs#L65-L69)): Bổ sung `.Cascade(CascadeMode.Stop)` và `s => !string.IsNullOrWhiteSpace(s) && AllowedStatuses.Contains(s.Trim())`.
6. **Bổ sung Unit Tests khóa hồi quy:** Thêm các ca kiểm thử đầu vào `null` trực tiếp vào [`AiBlogGamificationValidatorTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Validators/AiBlogGamificationValidatorTests.cs), [`GovernanceChatValidatorTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Validators/GovernanceChatValidatorTests.cs) và [`RecyclerReviewPackageWasteValidatorTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Validators/RecyclerReviewPackageWasteValidatorTests.cs).
7. **Kết quả xác minh:** Toàn bộ `1,118 / 1,118` Unit Tests (`Ecolink.UnitTests.csproj`) đạt **`100% Passed`** và `Ecolink.WebApi` biên dịch sạch (`0 Warning(s), 0 Error(s)`).

---

## 6. BÀI HỌC VÀ BIỆN PHÁP PHÒNG NGỪA (LESSONS LEARNED & PREVENTIONS)

1. **Quy tắc bắt buộc khi viết FluentValidation kết hợp `.Must(...)`:** Mọi `RuleFor(...)` có `.NotNull()` / `.NotEmpty()` đứng trước `.Must(...)` hoặc `.ForEach(...)` **bắt buộc phải đặt `.Cascade(CascadeMode.Stop)`** (hoặc dùng `.When(x => x.Prop != null)` / kiểm tra null bên trong `.Must(...)`).
2. **Viết Boundary Test bắt buộc cho giá trị `null` trên mọi Request DTO:** Bộ Validator Unit Test mới đã chứng minh hiệu quả phát hiện lỗi thật trước khi đưa ra API runtime; duy trì quy tắc kiểm thử input `null`, `""`, `"   "` cho mọi validator mới.
