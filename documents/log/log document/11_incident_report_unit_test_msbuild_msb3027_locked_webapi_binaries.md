# BÁO CÁO SỰ CỐ LỖI MSBUILD `MSB3027` / `MSB3021` DO KHÓA TỆP DLL KHI CHẠY TEST EXPLORER SONG SONG VỚI WEBAPI (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-TST-11`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Quy trình Build & Thực thi Kiểm thử Đơn vị (`Ecolink.UnitTests`, `Ecolink.WebApi`, Visual Studio Test Explorer)  
**Các tệp ảnh hưởng chính:**
* **Solution / Projects:** [`src/Ecolink.Backend/Ecolink.UnitTests/Ecolink.UnitTests.csproj`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Ecolink.UnitTests.csproj), [`src/Ecolink.Backend/Ecolink.WebApi/Ecolink.WebApi.csproj`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Ecolink.WebApi.csproj)
* **Thư mục nhị phân bị khóa:** `src/Ecolink.Backend/Ecolink.WebApi/bin/Debug/net10.0/Ecolink.*.dll`, `apphost.exe`  
**Thời điểm ghi nhận:** 23/11/2026 (Quá trình kiểm tra chạy lại toàn bộ Unit Test trên máy phát triển sau khi đồng bộ code)  
**Mức độ nghiêm trọng:** Trung bình (Medium – Làm gián đoạn thao tác *Run All Tests* trên IDE khi server dev `Ecolink.WebApi` đang mở, gây hiểu nhầm rằng mã nguồn/unit test đang lỗi biên dịch)

---

## 1. MÔ TẢ HIỆN TƯỢNG (INCIDENT SYNOPSIS)

Trong quá trình chạy kiểm thử đơn vị (`Ecolink.UnitTests`):

1. Khi lập trình viên bấm **Run All Tests** trong Visual Studio Test Explorer (hoặc chạy `dotnet test` trên toàn bộ tệp solution `.sln`) trong lúc máy chủ phát triển `Ecolink.WebApi` đang chạy nền (lắng nghe trên `localhost:5000`), quá trình build bị dừng sớm với cặp mã lỗi MSBuild:
   * `MSB3027`: *"Could not copy '...\Ecolink.Domain.dll' to 'bin\Debug\net10.0\Ecolink.Domain.dll'. Exceeded retry count of 10. Failed. The file is locked by: 'Ecolink.WebApi (...)'"*
   * `MSB3021`: *"Unable to copy file '...\Ecolink.Application.dll' / '...\Ecolink.WebApi.dll' to 'bin\Debug\net10.0\...' Access to the path is denied."*
2. Toàn bộ các bài Unit Test không được kích hoạt chạy dù mã nguồn C# hoàn toàn hợp lệ và không hề có lỗi cú pháp hay lỗi logic nghiệp vụ.

---

## 2. CHUỖI TRUY VẾT SỰ CỐ (FAILURE TRACE)

```text
[1] Server phát triển `Ecolink.WebApi` đang chạy nền tại localhost:5000
      └─► Tiến trình Windows khóa (file-lock) các assembly trong WebApi/bin/Debug/net10.0/
            (Ecolink.WebApi.dll, Ecolink.Application.dll, Ecolink.Infrastructure.dll, Ecolink.Domain.dll)
      │
      ▼
[2] Lập trình viên kích hoạt "Run All Tests" trên toàn Solution (.sln)
      └─► MSBuild biên dịch lại toàn bộ các project trong solution, bao gồm cả `Ecolink.WebApi`
      │
      ▼
[3] MSBuild cố gắng ghi đè tệp DLL mới vào `Ecolink.WebApi/bin/Debug/net10.0/`
      └─► Hệ điều hành Windows từ chối ghi đè tệp đang bị lock -> thử lại 10 lần không thành công
      │
      ▼
[4] Build thất bại với `MSB3027` & `MSB3021` -> Test Runner hủy phiên chạy kiểm thử
```

---

## 3. PHÂN TÍCH CHI TIẾT NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

1. **Cơ chế khóa tệp thực thi trên Windows:** Hệ điều hành Windows khóa độc quyền (write-lock) các tệp `.dll` và `.exe` đang được tiến trình .NET nạp vào bộ nhớ khi `Ecolink.WebApi` chạy ở chế độ dev (`dotnet run` hoặc Debug Profile).
2. **Phạm vi build mặc định của IDE/Solution:** Dù [`Ecolink.UnitTests.csproj`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Ecolink.UnitTests.csproj) chỉ tham chiếu 3 tầng cốt lõi (`Ecolink.Domain`, `Ecolink.Application`, `Ecolink.Infrastructure`) và **không tham chiếu** `Ecolink.WebApi`, thao tác *Build Solution* hoặc *Run All Tests* cấp Solution vẫn ép MSBuild biên dịch cả `Ecolink.WebApi` cùng thư mục đích mặc định `bin\Debug\net10.0\`.
3. **Đụng độ đường dẫn xuất (Output Directory Collision):** Khi `Ecolink.WebApi` vừa đang chạy vừa bị build đè vào đúng thư mục `bin\Debug\net10.0\`, MSBuild buộc phải ném `MSB3027` / `MSB3021`.

---

## 4. ĐÁNH GIÁ TÁC ĐỘNG HỆ THỐNG (SYSTEM IMPACT)

| Thành phần | Mức độ ảnh hưởng | Mô tả tác động thực tế |
| :--- | :---: | :--- |
| **Visual Studio Test Explorer** | Trung bình | Không thể thực thi Unit Test qua nút *Run All Tests* toàn solution khi API dev server đang bật. |
| **Luồng phát triển liên tục (Local Dev)** | Thấp – Trung bình | Lập trình viên phải tắt/mở lại server `Ecolink.WebApi` nhiều lần hoặc dễ nhầm lẫn giữa lỗi môi trường (locked DLL) với lỗi code. |
| **Code sản phẩm & Test logic** | Không ảnh hưởng | Không làm sai lệch logic mã nguồn; `Ecolink.UnitTests` không phụ thuộc `Ecolink.WebApi`. |

---

## 5. GIẢI PHÁP KHẮC PHỤC & QUY TRÌNH XỬ LÝ (REMEDIATION)

1. **Chạy kiểm thử theo từng Project độc lập thay vì Build toàn Solution khi WebApi đang chạy:**
   ```powershell
   dotnet test src\Ecolink.Backend\Ecolink.UnitTests\Ecolink.UnitTests.csproj -v minimal
   ```
   Lệnh này chỉ biên dịch `Domain`, `Application`, `Infrastructure`, `UnitTests` và xuất vào `Ecolink.UnitTests/bin/Debug/net10.0/`, hoàn toàn không chạm tới `Ecolink.WebApi/bin/`.
2. **Tách thư mục `OutDir` khi cần kiểm chứng build `Ecolink.WebApi` song song với tiến trình dev đang mở:**
   ```powershell
   dotnet build src\Ecolink.Backend\Ecolink.WebApi\Ecolink.WebApi.csproj -p:OutDir=C:\temp\ecolink-webapi-verify\
   ```
3. **Lưu ý khi chạy Integration Tests (`EcoLink.ApiTests`):** Nếu sử dụng `WebApplicationFactory` trong tiến trình test, có thể chạy trực tiếp project `EcoLink.ApiTests.csproj`; chỉ khi chạy E2E trên port `localhost:5000` đang mở mới giữ server chạy sẵn thay vì build lại vào cùng `bin/`.

---

## 6. BÀI HỌC VÀ BIỆN PHÁP PHÒNG NGỪA (LESSONS LEARNED & PREVENTIONS)

1. **Tách biệt lệnh chạy Unit Test (`dotnet test <Project>.csproj`):** Luôn nhắm đích trực tiếp tệp `.csproj` của `Ecolink.UnitTests` trong tài liệu hướng dẫn và pipeline nội bộ để tránh kéo theo `Ecolink.WebApi` không cần thiết.
2. **Kiểm tra nguyên nhân gốc trước khi can thiệp tiến trình:** Không tự ý tắt tiến trình `Ecolink.WebApi` đang phục vụ kiểm thử thủ công của lập trình viên; ưu tiên tách `-p:OutDir` khi chỉ cần xác minh biên dịch sạch.
3. **Kết quả xác minh sau áp dụng:** `dotnet test src\Ecolink.Backend\Ecolink.UnitTests\Ecolink.UnitTests.csproj` hoàn tất không xung đột DLL và `dotnet build ... -p:OutDir=...` đạt `0 Warning(s), 0 Error(s)`.
