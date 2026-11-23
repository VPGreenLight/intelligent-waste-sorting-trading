# Bộ Unit Test Validators & Logic thuần (PureLogic)

## 1. Cấu trúc bổ sung
```text
Ecolink.UnitTests/Validators/   (5 file)
Ecolink.UnitTests/PureLogic/    (1 file)
```

- Không cần thêm NuGet (`FluentValidation` đi theo `Ecolink.Application`).
- `Ecolink.Infrastructure.csproj` mở `<InternalsVisibleTo Include="Ecolink.UnitTests" />` cho các helper `internal` (`ServicePackageHelpers`, `HandoverTradeSyncHelper`, `GamTime`, `GamErrors`, `TabularReportExportHelper`, `PointsService.ActionText`).

## 2. Phạm vi bao phủ
| File | Phủ |
| :--- | :--- |
| `Validators/AuthValidatorTests.cs` | 8 validator Auth (đăng ký, recycler, login, google, quên/đặt lại/đổi MK, hồ sơ) |
| `Validators/AiBlogGamificationValidatorTests.cs` | Feedback AI, YOLO box, thẩm định, gán nhãn, batch, dataset version, Blog, Gift, Redeem, EcoFact, GamificationConfig, Leaderboard |
| `Validators/GovernanceChatValidatorTests.cs` | Khóa/vô hiệu hóa, staff (tạo/sửa/trạng thái/quyền), check-in/out, chat, proposal |
| `Validators/RecyclerReviewPackageWasteValidatorTests.cs` | Onboarding, profile, giá (single/bulk), lịch, nearby, review, complaint, gói dịch vụ, đối soát, danh mục rác, sorting rule |
| `PureLogic/PureLogicTests.cs` | `AiCategoryNormalizer` (hợp đồng 10 danh mục YOLO26), `ServicePackageHelpers`, `HandoverTradeSyncHelper`, `GamTime`/`GamErrors`, `TabularReportExportHelper` (csv/xlsx), `StaffPermissions`, Haversine |

## 3. Lệnh chạy
```powershell
dotnet test src\Ecolink.Backend\Ecolink.UnitTests\Ecolink.UnitTests.csproj --filter "Area=Validators|Area=PureLogic"
```

## 4. Các điểm đã xác thực & vá sau khi merge `add-unit-test`
1. Đồng bộ `AiCategoryNormalizerTests` theo hợp đồng **10 danh mục YOLO26** (`battery`, `biological`, `cardboard`, `clothes`, `glass`, `metal`, `paper`, `plastic`, `shoes`, `trash`).
2. Bổ sung `.Cascade(CascadeMode.Stop)` và null-check cho `.Must(...)` trong `UpdateStaffPermissionsValidator`, `BatchAssignLabelsRequestValidator`, `SendChatMessageRequestValidator`, `UpdateScheduleRequestValidator` và `ToggleWasteCategoryStatusValidator` để đảm bảo input `null` trả về lỗi validation `400` thay vì ngoại lệ `500`.
