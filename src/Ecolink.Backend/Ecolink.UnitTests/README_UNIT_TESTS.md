# Bổ sung Unit Test (Validators + Logic thuần)

> ⚠️ Toàn bộ code này **chưa được build/chạy** (sandbox không có .NET SDK). Bạn build → chạy → gửi log lại giúp mình.

## 1. Thêm file
Copy 2 thư mục vào project `Ecolink.UnitTests` (giữ nguyên các Module hiện có):

```
Ecolink.UnitTests/Validators/   (5 file)
Ecolink.UnitTests/PureLogic/    (1 file)
```

Không cần thêm NuGet (FluentValidation đi theo project Application).

## 2. BẮT BUỘC: mở internal cho project test
`ServicePackageHelpers`, `HandoverTradeSyncHelper`, `GamTime`, `GamErrors`, `TabularReportExportHelper`,
`PointsService.ActionText` đều là `internal`. Thêm vào `Ecolink.Infrastructure.csproj`:

```xml
<ItemGroup>
  <InternalsVisibleTo Include="Ecolink.UnitTests" />
</ItemGroup>
```
(Không thêm thì `PureLogicTests.cs` sẽ lỗi CS0122.)

## 3. Nội dung
| File | Phủ |
|---|---|
| Validators/AuthValidatorTests | 8 validator Auth (đăng ký, recycler, login, google, quên/đặt lại/đổi MK, hồ sơ) |
| Validators/AiBlogGamificationValidatorTests | Feedback AI, YOLO box, thẩm định, gán nhãn, batch, dataset version, Blog, Gift, Redeem, EcoFact, GamificationConfig, Leaderboard |
| Validators/GovernanceChatValidatorTests | Khóa/vô hiệu hóa, staff (tạo/sửa/trạng thái/quyền), check-in/out, chat, proposal |
| Validators/RecyclerReviewPackageWasteValidatorTests | Onboarding, profile, giá (single/bulk), lịch, nearby, review, complaint, gói dịch vụ, đối soát, danh mục rác, sorting rule |
| PureLogic/PureLogicTests | AiCategoryNormalizer, ServicePackageHelpers, HandoverTradeSyncHelper, GamTime/GamErrors, TabularReportExportHelper (csv/xlsx), StaffPermissions, Haversine (reflection) |

Tổng: ~109 `[Fact]` + ~159 `[Theory]` (≈ 660 `InlineData`) → khoảng **770 case**, đều không cần DB, chạy mili-giây.
Đã phủ **30/30 validator** (trước đây mỗi validator chỉ có 1 test).

## 4. Chạy
```
dotnet test Ecolink.UnitTests --filter "Area=Validators|Area=PureLogic"
```

## 5. Những chỗ mình KHÔNG chắc – nếu fail hãy gửi log
1. `UpdatePermissions_Null_ReturnsValidationError_NotException` – mình nghi **bug thật** trong
   `UpdateStaffPermissionsValidator`: `.NotNull().Must(perms => perms.All(...))` với CascadeMode mặc định (Continue)
   vẫn chạy `Must` khi `Permissions == null` → `NullReferenceException` (JSON `"permissions": null` sẽ ra 500 thay vì 400).
   Sửa: thêm `.Cascade(CascadeMode.Stop)` trước `NotNull()` hoặc đổi thành `perms != null && perms.All(...)`.
   Tương tự có thể xảy ra ở `BatchAssignLabelsRequestValidator` (`ItemIds` null).
2. `FormatSlotText_Then_ParseSlotToUtc_RoundTrips` – kiểm tra chuỗi slot hệ thống sinh ra (`yyyy-MM-dd (HH:mm - HH:mm)`)
   phải parse ngược đúng giờ. `TryParseAppointmentSlotUtc` thử `DateTimeOffset.TryParse` trước; nếu .NET parse được chuỗi có ngoặc
   thì giờ sẽ sai → test sẽ lộ bug này (hoặc mình đoán sai hành vi parse).
3. `ValidateAppointmentWindowUtc` dùng `DateTime.UtcNow` nên test chỉ dùng các mốc cách xa biên (chưa thể test sát biên khi chưa có `TimeProvider`).
4. `GeoDistanceTests` gọi hàm `private static CalculateDistanceKm` bằng reflection → nếu đổi tên hàm test sẽ báo rõ.
   (Nên tách ra class `GeoMath` public/internal để khỏi dùng reflection.)

## 6. Bước tiếp theo đã đề xuất
- Đo coverage: thêm `coverlet.collector` vào csproj test rồi
  `dotnet test --collect:"XPlat Code Coverage"` + ReportGenerator.
- Project test Testcontainers/PostgreSQL cho các service rẽ nhánh `IsRelational()`.
- `TimeProvider` để test mốc 14/7/30 ngày, 48h (JOB-01).
