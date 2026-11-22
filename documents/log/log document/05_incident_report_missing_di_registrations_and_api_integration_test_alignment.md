# BÁO CÁO SỰ CỐ KỸ THUẬT RUNTIME & KIỂM THỬ TÍCH HỢP (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-DI-05`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Core Infrastructure DI (`Ecolink.Infrastructure`), Backend WebApi (`Ecolink.WebApi`), AI Microservice (`Ecolink.AiService`), API Integration Test Suite (`EcoLink.ApiTests`)  
**Tác vụ / Chức năng ảnh hưởng:**
* Module 03: Đăng ký & Xét duyệt Hồ sơ Cơ sở Tái chế KYB (`RecyclerApplicationsController` – UC U-17, S-04)
* Module 08: Phòng Đàm phán Thời gian thực & Đề xuất Giá/Lịch hẹn (`NegotiationChatController` – UC U-08, R-05)
* Module 02: Phân loại Rác thông minh bằng AI & Rule Engine (`WasteClassificationController` – UC U-03, U-04)
* Bộ kiểm thử tích hợp tự động đầu-cuối (`EcoLink.ApiTests` – 397 kịch bản)
**Tuyến API ảnh hưởng:**
* `POST /api/v1/recycler-applications`, `GET /api/v1/recycler-applications/me`, `GET /api/v1/staff/recycler-applications`, `PATCH /api/v1/staff/recycler-applications/{userId}/verify`
* `GET|POST /api/v1/trade/orders/{orderId}/messages`, `POST /api/v1/trade/orders/{orderId}/proposals`, `PATCH /api/v1/trade/orders/{orderId}/proposals/{messageId}/respond`, `PATCH /api/v1/trade/orders/{orderId}/messages/read`
* `POST /api/v1/waste/classify`, `POST /api/v1/waste/feedback`
**Thời điểm ghi nhận:** 11/11/2026 – Môi trường Live Integration Testing (`localhost:5000` + `localhost:8000` + PostgreSQL `ecolink_db`)  
**Mức độ nghiêm trọng:** Nghiêm trọng (Critical – Gây sập HTTP `500 Internal Server Error` toàn bộ API Module 03 & Module 08 khi gọi thực tế qua HTTP, dù `dotnet build` và Unit Test đều báo xanh)

---

## 1. MÔ TẢ HIỆN TƯỢNG (INCIDENT SYNOPSIS)

Sau khi cập nhật hoàn tất PostgreSQL Migration `20261109022316_HandoverCodeSequences` và xác nhận `168/168` Unit Tests đạt `Passed`, quá trình khởi chạy máy chủ thực tế `Ecolink.WebApi` (`http://localhost:5000`) kết nối PostgreSQL (`ecolink_db`) để chạy toàn bộ **397 kịch bản Integration Test** ([`EcoLink.ApiTests`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj)) đã phát hiện **3 nhóm lỗi Runtime & Tích hợp hệ thống**:

### 1.1. Hiện tượng 1: Sập `500 Internal Server Error` (`MSG-SYS-500`) tại toàn bộ tuyến API KYB Onboarding & Negotiation Chat
* Mọi request HTTP hợp lệ gửi đến `RecyclerApplicationsController` (12 kịch bản kiểm thử Module 03) và `NegotiationChatController` (15 kịch bản kiểm thử Module 08) đều lập tức trả về mã lỗi `500 Internal Server Error` với payload chuẩn của `GlobalExceptionMiddleware`:
  ```json
  {
    "success": false,
    "message": "Đã xảy ra sự cố trong quá trình xử lý hệ thống. Vui lòng thử lại sau.",
    "messageCode": "MSG-SYS-500",
    "errors": []
  }
  ```
* Điểm đặc biệt nguy hiểm của lỗi này là dự án biên dịch thành công (`0 Error(s)`) và toàn bộ 168 Unit Tests (vốn khởi tạo trực tiếp `new RecyclerOnboardingService(...)` / `new NegotiationChatService(...)`) đều vượt qua 100%, khiến lỗi bị che khuất hoàn toàn cho đến khi kích hoạt Controller qua giao thức HTTP thật.

### 1.2. Hiện tượng 2: Lỗi từ chối kết nối `MSG-CLS-03` ở tuyến Phân loại ảnh AI (`POST /api/v1/waste/classify`)
* Khi chỉ khởi chạy đơn lẻ `Ecolink.WebApi` (`localhost:5000`) mà chưa bật dịch vụ Python FastAPI `Ecolink.AiService` (`localhost:8000`), 5 kịch bản phân loại ảnh hợp lệ trong [`WasteScanTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/EcoLink.ApiTests/WasteScanTests.cs) nhận phản hồi `400 Bad Request` kèm mã `MSG-CLS-03`:
  > *"Lỗi kết nối tới AI Service: No connection could be made because the target machine actively refused it. (localhost:8000)"*

### 1.3. Hiện tượng 3: Độ lệch hợp đồng trường JSON & mã lỗi nghiệp vụ trong các tệp Integration Test mới viết
* Sau khi khôi phục DI và bật `Ecolink.AiService`, một số kịch bản kiểm thử tích hợp mở rộng mới bổ sung vào [`DiscoveryTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/EcoLink.ApiTests/DiscoveryTests.cs), [`OnboardingTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/EcoLink.ApiTests/OnboardingTests.cs), [`NegotiationChatTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/EcoLink.ApiTests/NegotiationChatTests.cs) và [`WasteScanTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/EcoLink.ApiTests/WasteScanTests.cs) bị lệch tên thuộc tính DTO hoặc mã `messageCode` so với đặc tả thực tế đang vận hành trên Controller/Service.

---

## 2. TRÍCH ĐOẠN LOG NGUYÊN BẢN (STACK TRACE & RUNTIME LOGS)

* **Tệp log lưu trữ:** [`post_merge_handover_and_di_errors.txt`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/log/runtime/post_merge_handover_and_di_errors.txt)

### 2.1. Lỗi không thể khởi tạo `RecyclerApplicationsController` do thiếu `IRecyclerOnboardingService`
```log
fail: Ecolink.WebApi.Middlewares.GlobalExceptionMiddleware[0]
      [Unhandled Error] Tuyến /api/v1/recycler-applications xảy ra lỗi: Unable to resolve service for type 'Ecolink.Application.Interfaces.IRecyclerOnboardingService' while attempting to activate 'Ecolink.WebApi.Controllers.RecyclerApplicationsController'.
      System.InvalidOperationException: Unable to resolve service for type 'Ecolink.Application.Interfaces.IRecyclerOnboardingService' while attempting to activate 'Ecolink.WebApi.Controllers.RecyclerApplicationsController'.
         at Microsoft.Extensions.DependencyInjection.ActivatorUtilities.ThrowHelperUnableToResolveService(Type type, Type requiredBy)
         at Microsoft.AspNetCore.Mvc.Controllers.ControllerFactoryProvider.<>c__DisplayClass6_0.<CreateControllerFactory>g__CreateController|0(ControllerContext controllerContext)
         at Ecolink.WebApi.Middlewares.GlobalExceptionMiddleware.InvokeAsync(HttpContext context) in c:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend\Ecolink.WebApi\Middlewares\GlobalExceptionMiddleware.cs:line 31
```

### 2.2. Lỗi không thể khởi tạo `NegotiationChatController` do thiếu `IFileStorageService`
```log
fail: Ecolink.WebApi.Middlewares.GlobalExceptionMiddleware[0]
      [Unhandled Error] Tuyến /api/v1/trade/orders/d900bb74-8536-46bb-a195-87556dda07e1/proposals xảy ra lỗi: Unable to resolve service for type 'Ecolink.Application.Interfaces.IFileStorageService' while attempting to activate 'Ecolink.WebApi.Controllers.NegotiationChatController'.
      System.InvalidOperationException: Unable to resolve service for type 'Ecolink.Application.Interfaces.IFileStorageService' while attempting to activate 'Ecolink.WebApi.Controllers.NegotiationChatController'.
         at Microsoft.Extensions.DependencyInjection.ActivatorUtilities.ThrowHelperUnableToResolveService(Type type, Type requiredBy)
         at Microsoft.AspNetCore.Mvc.Controllers.ControllerFactoryProvider.<>c__DisplayClass6_0.<CreateControllerFactory>g__CreateController|0(ControllerContext controllerContext)
         at Ecolink.WebApi.Middlewares.GlobalExceptionMiddleware.InvokeAsync(HttpContext context) in c:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend\Ecolink.WebApi\Middlewares\GlobalExceptionMiddleware.cs:line 31
```

### 2.3. Lỗi kết nối tới AI Microservice (`localhost:8000`) khi chạy `WasteScanTests`
```json
{
  "success": false,
  "message": "Lỗi kết nối tới AI Service: No connection could be made because the target machine actively refused it. (localhost:8000)",
  "messageCode": "MSG-CLS-03",
  "errors": [],
  "timestamp": "2026-11-11T06:24:35Z"
}
```

---

## 3. PHÂN TÍCH NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

### 3.1. Thất thoát cấu hình Dependency Injection khi hợp nhất nhiều nhánh tính năng vào `develop`
1. **Kiểm tra Constructor của các Controller bị lỗi:**
   - [`RecyclerApplicationsController`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/RecyclerApplicationsController.cs) yêu cầu inject `IRecyclerOnboardingService` và `IFileStorageService`.
   - [`NegotiationChatController`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/NegotiationChatController.cs) yêu cầu inject `INegotiationChatService`, `IFileStorageService` và `IHubContext<NegotiationHub>`.
2. **Kiểm tra [`DependencyInjection.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/DependencyInjection.cs):**
   - Trong quá trình giải quyết xung đột merge giữa nhiều nhánh tính năng (`feature/recycler_onboarding_&_verification`, `feature/in_app_real_time_negotiation_chatbox`, `recycler-profile-pricing-and-availability`) vào `develop` trước đó, hai dòng đăng ký dịch vụ trong `AddInfrastructureServices` đã bị ghi đè mất:
     - Thiếu `services.AddHttpClient<IFileStorageService, CloudflareR2StorageService>();`
     - Thiếu `services.AddScoped<IRecyclerOnboardingService, RecyclerOnboardingService>();`
   - Do mặc định ASP.NET Core chỉ phân giải phụ thuộc của Controller tại thời điểm nhận HTTP request đầu tiên (Lazy Activation), trình biên dịch C# và `dotnet ef` đều không phát hiện ra sự thiếu hụt này.

### 3.2. Phụ thuộc kiến trúc Đa dịch vụ (Polyglot Microservice Architecture)
* Tuyến `POST /api/v1/waste/classify` trong `WasteClassificationController` chuyển tiếp luồng byte ảnh qua HTTP tới dịch vụ AI độc lập `Ecolink.AiService` (`uvicorn app:app --host 127.0.0.1 --port 8000`, chạy mô hình ONNX MobileNetV3). Khi chạy Integration Test đầu-cuối mà không khởi động đồng thời tiến trình Python FastAPI này, `HttpClient` ném `HttpRequestException (Connection refused)` và được chuyển đổi thành `BusinessRuleException("MSG-CLS-03")`.

### 3.3. Khác biệt chi tiết Hợp đồng DTO & Quy tắc kiểm tra đầu vào trong Test Suite
* **Module 02 (`WasteScanTests`):** Kịch bản `Feedback_WithoutAuth_IsAllowedByDesign` gửi body thiếu trường bắt buộc `userReportedLabel`, dẫn đến phản hồi `400 MSG-GEN-01` thay vì xác nhận thành công `200 OK` cho người dùng ẩn danh.
* **Module 05 (`DiscoveryTests`):**
  - `RecyclerPartnerSummaryDto` trả khóa định danh là `recyclerId` thay vì `userId`.
  - Tuyến tương thích cũ `GET /api/v1/trade/partners/nearby` trả trực tiếp mảng JSON (`[...]`) thay vì bọc trong `ApiResponse<T>`.
* **Module 03 (`OnboardingTests`):**
  - Request nộp hồ sơ KYB yêu cầu các trường `legalPermitUrl`, `businessPhotoUrl`, `operatingHours`; phản hồi phân trang của Staff dùng `totalRecords`; mã lỗi tọa độ dùng `MSG-MTH-03` / `MSG-MTH-04` và từ chối thiếu lý do trả `MSG-GEN-01`.
* **Module 08 (`NegotiationChatTests`):**
  - Gửi ảnh đính kèm nhận `attachmentUrl`; thông tin đề xuất nằm trong đối tượng con `proposal.proposalStatus`; danh sách tin nhắn nằm tại `messages.items`; trạng thái đơn khóa trả ở `orderStatus = "Confirmed"` và đánh dấu đã đọc trả `markedCount`.
  - Giới hạn độ dài tin nhắn là `2000` ký tự (không phải `1000`), trần đơn giá thu mua là `10.000.000 VNĐ/kg`, và các mã lỗi chuyên biệt gồm `MSG-NEG-SELF-RESPONSE`, `MSG-NEG-03`, `MSG-NEG-PROPOSAL-LOCKED`.

---

## 4. ĐÁNH GIÁ TÁC ĐỘNG HỆ THỐNG (SYSTEM IMPACT)

| Tiêu chí | Mức độ tác động | Chi tiết |
| :--- | :--- | :--- |
| **Luồng Đăng ký & Duyệt KYB (Module 03)** | Nghiêm trọng ( tê liệt 100% ) | Người dân không thể nộp hồ sơ nâng cấp lên Cơ sở Tái chế; Staff không thể tra cứu hay phê duyệt hồ sơ (`500 MSG-SYS-500`). |
| **Luồng Chat & Chốt giá Đàm phán (Module 08)** | Nghiêm trọng ( tê liệt 100% ) | Cả Citizen và Recycler đều không thể tải lịch sử chat, gửi tin nhắn hay gửi/phản hồi đề xuất giá trên giao diện web (`500 MSG-SYS-500`). |
| **Độ tin cậy của Bộ Kiểm thử Tự động** | Rất tích cực | Chính nhờ chạy bộ Integration Test thật qua HTTP (`EcoLink.ApiTests`) mới phát hiện kịp thời lỗi mất đăng ký DI mà Unit Test không thể bắt được. |

---

## 5. QUÁ TRÌNH KHẮC PHỤC VÀ TRIỂN KHAI (RESOLUTION & IMPLEMENTATION)

### 5.1. Khôi phục đầy đủ đăng ký Dependency Injection tại `Ecolink.Infrastructure`
Cập nhật tệp [`DependencyInjection.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/DependencyInjection.cs#L65-L70):
```csharp
// Đăng ký HttpClient cho Cloudflare R2 Object Storage (KYB Permits, Facility Photos, Chat Attachments)
services.AddHttpClient<IFileStorageService, CloudflareR2StorageService>();

// Đăng ký Recycler Onboarding & Verification Service (Module 3: U-17 & S-04)
services.AddScoped<IRecyclerOnboardingService, RecyclerOnboardingService>();
```

### 5.2. Khởi chạy đồng bộ tổ hợp dịch vụ khi thực thi Integration Tests
Đảm bảo cả 3 thành phần hạ tầng cùng hoạt động trước khi kích hoạt `EcoLink.ApiTests`:
1. **PostgreSQL 16 + PostGIS (`localhost:5432` / `ecolink_db`)** đã áp dụng đủ 7/7 migrations.
2. **AI Vision Service (`localhost:8000`):**
   ```powershell
   python -m uvicorn app:app --host 127.0.0.1 --port 8000
   ```
3. **Backend WebApi (`localhost:5000`):**
   ```powershell
   dotnet run --project src/Ecolink.Backend/Ecolink.WebApi/Ecolink.WebApi.csproj --urls http://localhost:5000
   ```

### 5.3. Chuẩn hóa Hợp đồng Kiểm thử Tích hợp (`EcoLink.ApiTests`)
Cập nhật đồng bộ cấu trúc payload request/response và mã thông báo chuẩn trong 4 tệp kiểm thử:
* [`WasteScanTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/EcoLink.ApiTests/WasteScanTests.cs): Bổ sung `userReportedLabel = "plastic"` cho kịch bản gửi phản hồi ẩn danh để xác thực chuẩn HTTP `200 OK`.
* [`DiscoveryTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/EcoLink.ApiTests/DiscoveryTests.cs): Đồng bộ trường `recyclerId` và cấu trúc mảng JSON của tuyến `/api/v1/trade/partners/nearby`.
* [`OnboardingTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/EcoLink.ApiTests/OnboardingTests.cs): Chuẩn hóa DTO nộp hồ sơ KYB, trường phân trang `totalRecords` và các mã lỗi `MSG-MTH-03`, `MSG-MTH-04`, `MSG-GEN-01`.
* [`NegotiationChatTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/EcoLink.ApiTests/NegotiationChatTests.cs): Chuẩn hóa cấu trúc `proposal.proposalStatus`, `messages.items`, `orderStatus`, `markedCount`, biên độ dài `2000` ký tự, trần giá `10.000.000 VNĐ` và các mã lỗi `MSG-NEG-*`.

---

## 6. KẾT QUẢ NGHIỆM THU VÀ XÁC THỰC (VERIFICATION & RESULTS)

Thực thi lại toàn bộ 2 bộ kiểm thử tự động của dự án trên nhánh `develop`:

1. **Bộ Kiểm thử Đơn vị (`Ecolink.UnitTests`):**
   ```text
   Passed!  - Failed:     0, Passed:   168, Skipped:     0, Total:   168, Duration: 11 s - Ecolink.UnitTests.dll (net10.0)
   ```
2. **Bộ Kiểm thử Tích hợp API Đầu-Cuối (`EcoLink.ApiTests`):**
   ```text
   Passed!  - Failed:     0, Passed:   397, Skipped:     0, Total:   397, Duration: 41 s - EcoLink.ApiTests.dll (net10.0)
   ```
* **Tổng số kịch bản kiểm thử toàn dự án đạt chuẩn:** **565 / 565 kịch bản Passed (Tỷ lệ thành công 100%, 0 Failed)**.

---

## 7. KHUYẾN NGHỊ VÀ BÀI HỌC KINH NGHIỆM (LESSONS LEARNED & PREVENTIONS)

1. **Rà soát tệp `DependencyInjection.cs` sau mỗi đợt Merge nhánh tính năng:**
   - `DependencyInjection.cs` là điểm hội tụ của mọi module nên rất dễ bị ghi đè thiếu dòng khi giải quyết conflict thủ công. Cần đối chiếu danh sách mọi interface trong `Ecolink.Application/Interfaces` với đăng ký trong `AddInfrastructureServices`.
2. **Cân nhắc bật kiểm tra DI cho Controller ngay lúc khởi động (`AddControllersAsServices` + `ValidateOnBuild`):**
   - Trong môi trường `Development`, có thể kết hợp `.AddControllers().AddControllersAsServices()` cùng `ValidateScopes = true` / `ValidateOnBuild = true` để ứng dụng báo lỗi thiếu DI ngay giây đầu tiên khởi động thay vì đợi đến khi người dùng gọi trúng endpoint.
3. **Duy trì chạy song song cả Unit Test lẫn Live API Integration Test trước khi Release:**
   - Unit Test bảo vệ logic nghiệp vụ cô lập tốc độ cao, trong khi `EcoLink.ApiTests` bảo vệ toàn vẹn chuỗi lắp ghép thực tế (Routing -> Auth Middleware -> DI Container -> Controller -> EF Core Npgsql -> PostgreSQL + AI Service).

