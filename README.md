# EcoLink Platform – Nền Tảng Phân Loại & Thu Gom Rác Tái Chế Thông Minh
## (Intelligent Waste Sorting & Trading Platform)

[![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C# 13](https://img.shields.io/badge/C%23-13.0-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/en-us/dotnet/csharp/)
[![PostgreSQL 16](https://img.shields.io/badge/PostgreSQL-16%20%2B%20PostGIS-4169E1?logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![Redis](https://img.shields.io/badge/Redis-7.x%20Cache-DC382D?logo=redis&logoColor=white)](https://redis.io/)
[![Python 3.11](https://img.shields.io/badge/Python-3.11%20FastAPI-3776AB?logo=python&logoColor=white)](https://fastapi.tiangolo.com/)
[![YOLOv8](https://img.shields.io/badge/AI%20Vision-YOLOv8%20%2B%20MobileNetV3-00FFFF?logo=yolo&logoColor=black)](https://ultralytics.com/)
[![SignalR](https://img.shields.io/badge/Real--time-SignalR%20WebSocket-5C2D91?logo=socketdotio&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet/signalr)
[![Cloudflare R2](https://img.shields.io/badge/Storage-Cloudflare%20R2-F38020?logo=cloudflare&logoColor=white)](https://www.cloudflare.com/products/r2/)

---

## 📖 MỤC LỤC

1. [Tổng Quan Dự Án](#-tổng-quan-dự-án)
2. [Các Tính Năng Cốt Lõi Theo Phân Hệ](#-các-tính-năng-cốt-lõi-theo-phân-hệ)
3. [Kiến Trúc Kỹ Thuật (System Architecture)](#-kiến-trúc-kỹ-thuật-system-architecture)
4. [Cấu Trúc Thư Mục Dự Án](#-cấu-trúc-thư-mục-dự-án)
5. [Yêu Cầu Môi Trường & Cài Đặt Nhanh](#-yêu-cầu-môi-trường--cài-đặt-nhanh)
6. [Hướng Dẫn Khởi Chạy (Quick Start)](#-hướng-dẫn-khởi-chạy-quick-start)
7. [Quản Trị Cơ Sở Dữ Liệu & Migrations](#-quản-trị-cơ-sở-dữ-liệu--migrations)
8. [Tổng Quan API & Tài Liệu Kỹ Thuật](#-tổng-quan-api--tài-liệu-kỹ-thuật)
9. [Kiểm Thử Tự Động (Testing & Quality Assurance)](#-kiểm-thử-tự-động-testing--quality-assurance)

---

## 🌟 TỔNG QUAN DỰ ÁN

**EcoLink** là nền tảng số hóa chuỗi cung ứng rác thải tái chế tuần hoàn (Circular Economy), kết nối trực tiếp giữa **Người dân (`Citizen`)** có nhu cầu phân loại và bán phế liệu với các **Cơ sở thu gom / Vựa phế liệu (`Recycler`)**, dưới sự giám sát và vận hành của đội ngũ **Kiểm duyệt viên (`Staff / Admin`)**.

### Vấn đề giải quyết:
* **Người dân:** Không biết phân loại rác đúng quy chuẩn, thiếu thông tin về các điểm thu mua phế liệu lân cận, e ngại bị ép giá hoặc không sắp xếp được thời gian thu gom tận nơi.
* **Cơ sở thu gom:** Khó khăn trong việc tìm kiếm nguồn rác sạch đã phân loại tại nguồn, quản lý đơn hẹn thủ công, thiếu công cụ chốt giá và phát hành biên nhận minh bạch.
* **Môi trường & Xã hội:** Tỷ lệ rác tái chế bị chôn lấp cao; thiếu động lực kinh tế và thiếu ghi nhận đóng góp giảm phát thải carbon (CO2).

---

## 🎯 CÁC TÍNH NĂNG CỐT LÕI THEO PHÂN HỆ

Hệ thống được thiết kế hoàn chỉnh theo **9 Module nghiệp vụ cốt lõi**:

* **M-01 | Xác thực & Quản trị tài khoản (`AUTH`):** Đăng ký, đăng nhập tài khoản Citizen/Recycler, xác thực Google OAuth 2.0, bảo mật JWT kép (Access Token 30p + Refresh Token xoay vòng 7 ngày chống Replay Attack), chống dò mật khẩu Brute-force (khóa tài khoản sau 5 lần sai), khôi phục mật khẩu OTP qua Gmail SMTP.
* **M-02 | Thẩm định cơ sở thu gom (`ONB`):** Tiếp nhận hồ sơ đăng ký mở cơ sở, lưu trữ ảnh giấy phép kinh doanh / chứng từ môi trường trên Cloudflare R2 (S3-compatible), nhân viên Staff đối soát thông tin và phê duyệt/từ chối.
* **M-03 | Nhận diện rác thông minh bằng AI (`CLS` & `SRT`):** Thị giác máy tính nhận diện 6 nhóm rác TrashNet (Giấy, Nhựa, Kim loại, Thủy tinh, Bìa carton, Rác khác) bằng mô hình YOLOv8 Nano kết hợp MobileNetV3/EfficientNet; phát hiện ảnh ngoài phân phối (Out-of-Distribution - OOD threshold 60%); Rule Engine tự động đề xuất màu thùng rác và mẹo xử lý sạch trước khi bán.
* **M-04 | Hồ sơ vựa, Bảng giá & Lịch mở cửa (`PRF` & `PRC`):** Quản lý hồ sơ cơ sở, cấu hình bảng giá thu mua theo từng kg (`VND/kg`) và khối lượng tối thiểu (`min_quantity_kg`), lưu bảng giá hàng loạt (Bulk Save), bật/tắt trạng thái nhận đơn (`Online` / `Offline`) tức thời và cài đặt lịch mở cửa cố định theo tuần.
* **M-05 | Khám phá điểm thu gom theo GPS (`GEO` & `MTH`):** Tọa độ hóa địa chỉ qua PostGIS Point (SRID 4326), tìm kiếm các cơ sở đã xác minh (`Verified`) và đang mở cửa (`Online`) trong bán kính quét $R$ (km) từ vị trí người dùng; xem bảng giá niêm yết công khai và khoảng cách di chuyển thực tế.
* **M-06 | Đặt lịch hẹn & Quản lý yêu cầu bàn giao (`HND`):** Người dân tạo yêu cầu gom rác tận nơi (`HND-yyyyMMdd-NNNN`); cơ sở tiếp nhận, chấp nhận hoặc từ chối; ẩn số điện thoại liên hệ trước khi chốt lịch (`BR-HND-08`); cơ chế cảnh báo hủy muộn (`BR-HND-06`) và background worker tự động quét hủy đơn quá hạn (`JOB-03`).
* **M-07 | Biên nhận số & Lịch sử giao dịch (`RCP` & `TRX`):** Cơ sở phát hành biên nhận điện tử (`RCP-yyyy-NNNN`) kèm ảnh đồng hồ cân thực tế; cảnh báo khi khối lượng cân thực tế lệch $> 20\%$ so với ước tính ban đầu (`MSG-HND-04`); người dân xác nhận nhận tiền để khóa vĩnh viễn giao dịch; hỗ trợ khiếu nại (`Disputed`) và tra cứu báo cáo giao dịch có phân trang.
* **M-08 | Chat đàm phán thời gian thực (`NEG`):** Phòng chat SignalR WebSocket đính kèm theo mã đơn; gửi tin nhắn văn bản và ảnh chụp thực tế; gửi đề xuất/phản đề xuất đơn giá và khung giờ hẹn mới (`Superseded`); chấp nhận điều khoản để tự động đồng bộ sang hợp đồng bàn giao.
* **M-09 | Giao dịch, Gamification & Thanh toán (`TRD`, `GAM`, `PAY`):** Quét mã QR hoàn tất giao dịch tại điểm thu mua; tích lũy Điểm Xanh (Eco Points) và nuôi Cây Xảo (Virtual Eco-Tree); bảng xếp hạng cư dân sống xanh; tích hợp cổng thanh toán VietQR / PayOS để đối tác nạp phí dịch vụ nền tảng.

---

## 🏗️ KIẾN TRÚC KỸ THUẬT (SYSTEM ARCHITECTURE)

Dự án áp dụng mô hình kiến trúc **Clean Architecture (Onion Architecture)** phân tách nghiêm ngặt 4 tầng trách nhiệm:

```
                      ┌────────────────────────────────────────┐
                      │        Ecolink.WebApi (Presentation)   │
                      │  - Controllers (REST API, 76 Endpoints)│
                      │  - SignalR Negotiation Hub             │
                      │  - Middlewares (Global Exception, Auth)│
                      └──────────────────┬─────────────────────┘
                                         │ depends on
                      ┌──────────────────▼─────────────────────┐
                      │       Ecolink.Application (Use Cases)  │
                      │  - DTOs, Request / Response Contracts  │
                      │  - Service Interfaces, Business Logic  │
                      │  - FluentValidation Rules              │
                      └──────────┬───────────────────┬─────────┘
                                 │                   │
         depends on              │                   │ depends on
┌────────────────────────────────▼─────┐   ┌─────────▼────────────────────────┐
│     Ecolink.Infrastructure (Infra)   │   │       Ecolink.Domain (Core)      │
│  - EF Core 10, Npgsql PostGIS Mapping│   │  - Entities, Shared Auditable PK │
│  - Cloudflare R2 Storage Service     │   │  - Enums (OrderStatus, Role...) │
│  - Background Hosted Services (Jobs) │   │  - Zero External Dependencies!   │
│  - External HTTP Clients (PayOS, AI) │   └──────────────────────────────────┘
└──────────────────────────────────────┘
```

### Công nghệ sử dụng:

| Thành phần | Công nghệ / Thư viện | Vai trò trong hệ thống |
|:---|:---|:---|
| **Core Web API** | **ASP.NET Core 10 WebApi** (`net10.0`, C# 13) | Máy chủ xử lý toàn bộ logic nghiệp vụ, giao dịch và phân quyền RBAC |
| **Cơ sở dữ liệu** | **PostgreSQL 16 + PostGIS 3.4** | CSDL quan hệ chính lưu trữ dữ liệu nghiệp vụ, không gian địa lý (Spatial Point) |
| **ORM & Spatial** | **Entity Framework Core 10** (`Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite`) | Quản lý truy vấn dữ liệu, migrations, quan hệ 1-1/1-N và hàm không gian PostGIS |
| **Caching & Lock** | **Redis 7.x** | Lưu trữ phiên đăng nhập, bộ nhớ đệm bảng giá và chống race condition |
| **Thời gian thực** | **ASP.NET Core SignalR** | WebSocket giao tiếp thời gian thực cho phòng chat thương lượng đính kèm đơn |
| **AI Microservice** | **Python 3.11 + FastAPI + PyTorch / Ultralytics** | Dịch vụ thị giác máy tính nhận diện loại rác thải và kiểm tra độ tin cậy OOD |
| **Lưu trữ tệp tin** | **Cloudflare R2 Storage (AWS S3 SDK for .NET)** | Lưu trữ ảnh chứng từ cơ sở, ảnh chụp rác và ảnh đồng hồ cân thực tế |
| **Cổng thanh toán** | **VietQR / PayOS SDK** | Sinh mã QR động chuẩn Napas 247 và xử lý Webhook thanh toán phí dịch vụ |
| **Giao diện Web** | **React 18 / TypeScript / Vite / Tailwind CSS / Leaflet** | Web App người dùng & cơ sở thu gom tương tác bản đồ, quét camera rác AI |

---

## 📂 CẤU TRÚC THƯ MỤC DỰ ÁN

```text
c:\Projects\EcoLink\
├── ecolink-web/                              # Ứng dụng Giao diện Web (React / TypeScript / Vite)
│   ├── src/pages/                            # Màn hình: Map, Scanner, Requests, Receipts, Chat...
│   └── src/lib/                              # API Client, SignalR Client, Geolocation Helpers
│
└── intelligent-waste-sorting-trading/         # Kho mã nguồn chính của Hệ thống EcoLink
    ├── src/Ecolink.Backend/                  # Backend Solution (.NET 10 Clean Architecture)
    │   ├── Ecolink.slnx                      # Solution cấu hình mới của Visual Studio / .NET 10
    │   ├── Ecolink.Domain/                   # Thực thể nghiệp vụ, Enums (Entities, Auth, Trade...)
    │   ├── Ecolink.Application/              # DTOs, Service Interfaces, Validators FluentValidation
    │   ├── Ecolink.Infrastructure/           # EF Core DbContext, Configurations, PostGIS, Cloudflare R2
    │   │   └── Data/Migrations/              # 100% EF Core Migrations đã áp dụng vào CSDL
    │   ├── Ecolink.WebApi/                   # 15 API Controllers, SignalR Hub, Middlewares, Program.cs
    │   ├── Ecolink.UnitTests/                # 168 Unit Tests cho 8 Core Modules (xUnit, Moq, EF InMemory)
    │   └── EcoLink.ApiTests/                 # 236 API Integration Tests cho 8 Core Modules (415 kịch bản)
    │
    ├── models/                               # Trọng số mô hình AI đã train (YOLOv8n best.pt, MobileNet...)
    ├── data/                                 # Quy tắc rác thông minh (rules.json) và tập ảnh mẫu
    ├── app.py                                # Máy chủ AI Vision Microservice (Python FastAPI)
    │
    ├── documents/                            # Toàn bộ tài liệu quy chuẩn & đặc tả kỹ thuật
    │   ├── api document/                     # Đặc tả 9 module API & Bảng thống kê chi tiết 86 endpoints
    │   │   ├── 00_api_inventory_and_endpoint_statistics.md  # Bảng thống kê & danh mục 86 API toàn hệ thống
    │   │   ├── 00_api_overview_and_response_contract.md      # Quy chuẩn ApiResponse<T>, JWT & MSG-* Codes
    │   │   └── 01_module_authentication_api.md ...           # Tài liệu chi tiết từng module
    │   ├── test document/                    # Tài liệu đặc tả kiểm thử Unit Test (5.1) & Integration Test (5.2)
    │   ├── guideline/                        # Clean Architecture, DB snake_case, PostGIS, Git Workflow
    │   └── log/                              # Báo cáo sự cố kỹ thuật runtime (Incident Bug Reports)
    │       └── log document/                 # Tài liệu log lỗi chi tiết (INC-01, INC-02, INC-03)
    │
    ├── scripts/                              # Các kịch bản tự động hóa hỗ trợ phát triển
    │   └── scan_api_inventory.ps1            # Script PowerShell quét đối chiếu 86 API endpoints
    │
    ├── run_all_services.bat                  # Script khởi động 1-Click toàn bộ dịch vụ (AI, API, Web, DB)
    ├── run_ai_service.bat                    # Script chạy độc lập AI Vision Microservice
    └── stop_all_services.bat                 # Script dừng toàn bộ tiến trình hệ thống khi tắt máy
```

---

## ⚙️ YÊU CẦU MÔI TRƯỜNG & CÀI ĐẶT NHANH

### Yêu cầu phần mềm trên máy phát triển:
1. **.NET 10 SDK** (bản chính thức `10.0.100` trở lên): Kiểm tra bằng lệnh `dotnet --version`.
2. **Python 3.10 hoặc 3.11** (khuyến nghị 3.11.9): Kiểm tra bằng lệnh `python --version`.
3. **Node.js 18+ & npm / pnpm**: Kiểm tra bằng lệnh `node --version`.
4. **PostgreSQL 16 + PostGIS Extension** (Cài trực tiếp trên Windows hoặc qua Docker container `ecolink-db` tại cổng `5432`).
5. **Git**: Phục vụ quản lý phiên bản.

---

## 🚀 HƯỚNG DẪN KHỞI CHẠY (QUICK START)

### Cách 1: Khởi động 1-Click Toàn Bộ Dịch Vụ (Khuyến nghị)
Nhấp đúp chuột vào file:
👉 **`run_all_services.bat`**

Hệ thống sẽ tự động kiểm tra và khởi chạy tuần tự trong các cửa sổ riêng biệt:
1. **Docker Container CSDL:** Kiểm tra và khởi động PostgreSQL `ecolink-db` (Port `5432`).
2. **AI Vision Service:** Kích hoạt môi trường ảo Python và khởi chạy FastAPI tại `http://localhost:8000`.
3. **Core .NET Web API:** Biên dịch và khởi chạy Kestrel tại `http://localhost:5000` (Swagger UI: `http://localhost:5000/swagger`).
4. **Frontend Web App:** Khởi chạy máy chủ phát triển Vite tại `http://localhost:5173`.

---

### Cách 2: Khởi chạy từng thành phần độc lập qua Terminal

#### 1. Khởi chạy AI Vision Microservice (Python FastAPI)
```powershell
# Di chuyển vào thư mục repo
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading

# Kích hoạt venv (hoặc dùng run_ai_service.bat)
.\venv\Scripts\Activate.ps1

# Khởi chạy server FastAPI
uvicorn app:app --host 127.0.0.1 --port 8000 --reload
```
* Swagger Docs: `http://127.0.0.1:8000/docs`
* Health Check: `http://127.0.0.1:8000/`

#### 2. Khởi chạy Core Backend Web API (.NET 10)
```powershell
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend

# Biên dịch solution
dotnet build Ecolink.slnx

# Chạy WebApi với profile Development
dotnet run --project Ecolink.WebApi/Ecolink.WebApi.csproj --launch-profile "http"
```
* Swagger UI: `http://localhost:5000/swagger`
* Health Check: `http://localhost:5000/health`
* SignalR Hub: `ws://localhost:5000/hubs/negotiation-chat`

#### 3. Khởi chạy Frontend Web App (React / Vite)
```powershell
cd c:\Projects\EcoLink\ecolink-web

# Cài đặt dependencies (nếu chạy lần đầu)
npm install

# Khởi chạy giao diện
npm run dev
```
* Mở trình duyệt truy cập: `http://localhost:5173`

---

## 🗄️ QUẢN TRỊ CƠ SỞ DỮ LIỆU & MIGRATIONS

Hệ thống sử dụng Entity Framework Core với nhà cung cấp `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` kết hợp chuẩn đặt tên `snake_case` tuyệt đối.

### Chuỗi kết nối mặc định (`appsettings.Development.json`):
```json
"ConnectionStrings": {
  "PostgreSQL": "Host=localhost;Port=5432;Database=ecolink_db;Username=postgres;Password=postgrespassword;Include Error Detail=true"
}
```

### Lệnh cập nhật CSDL (Database Migration):
Mỗi khi có cập nhật mô hình dữ liệu hoặc pull code mới về, thực hiện lệnh sau trong PowerShell để áp dụng:

```powershell
$env:ASPNETCORE_ENVIRONMENT="Development"
dotnet ef database update `
    --project src/Ecolink.Backend/Ecolink.Infrastructure/Ecolink.Infrastructure.csproj `
    --startup-project src/Ecolink.Backend/Ecolink.WebApi/Ecolink.WebApi.csproj `
    --no-build
```

### Danh sách các Migrations đã áp dụng thành công (6/6):
1. `20261020130550_InitialAuthModule`: Khởi tạo bảng `users`, `user_eco_profiles`, `tokens`.
2. `20261030015551_RecyclerPriceListAndAvailability`: Cấu hình bảng giá `recycler_price_list`, lịch mở cửa.
3. `20261031134442_StandardizeSnakeCaseSchemaAndDesignColumns`: Chuẩn hóa 100% cột CSDL sang `snake_case`.
4. `20261103102102_HandoverRequests`: Tạo bảng yêu cầu thu gom `handover_requests` và chi tiết `handover_request_items`.
5. `20261107151710_HandoverReceipts`: Tạo bảng biên nhận số `handover_receipts`.
6. `20261109154426_AddPendingModelChanges`: Tạo bảng chat đàm phán `chat_messages` và bổ sung trường thương lượng đơn hàng.

---

## 📊 TỔNG QUAN API & TÀI LIỆU KỸ THUẬT

Toàn bộ hệ thống hiện cung cấp **`86 Endpoint`** đã sẵn sàng vận hành. Chi tiết thống kê và tài liệu đặc tả được lưu trữ tập trung tại thư mục [`documents/api document/`](documents/api%20document/):

| Phân hệ / Tài liệu | File Đặc Tả Chi Tiết | Số API | Chức năng chính |
|:---|:---|:---:|:---|
| **API Inventory & Statistics** | [`00_api_inventory_and_endpoint_statistics.md`](documents/api%20document/00_api_inventory_and_endpoint_statistics.md) | **86** | Bảng thống kê toàn diện, ma trận phân quyền RBAC và danh mục 100% endpoints |
| **Overview & Response Contract**| [`00_api_overview_and_response_contract.md`](documents/api%20document/00_api_overview_and_response_contract.md) | — | Cấu trúc chuẩn `ApiResponse<T>`, `PagedResult<T>`, JWT và mã thông báo `MSG-*` |
| **Module 01: Authentication** | [`01_module_authentication_api.md`](documents/api%20document/01_module_authentication_api.md) | 14 | Đăng ký, đăng nhập Brute-force protection, Google OAuth, Refresh Token, OTP Mail |
| **Module 02: Recycler Onboarding** | [`02_module_recycler_onboarding_api.md`](documents/api%20document/02_module_recycler_onboarding_api.md) | 6 | Nộp hồ sơ vựa, upload chứng từ Cloudflare R2, Staff thẩm định duyệt/từ chối |
| **Module 03: AI Classification** | [`03_module_ai_waste_classification_api.md`](documents/api%20document/03_module_ai_waste_classification_api.md) | 5 + 9 | Quét rác camera AI, OOD Detection, Rule Engine và danh mục phế liệu chuẩn |
| **Module 04: Profile & Pricing** | [`04_module_recycler_profile_pricing_availability_api.md`](documents/api%20document/04_module_recycler_profile_pricing_availability_api.md) | 9 | Bảng giá VND/kg, khối lượng tối thiểu, lưu hàng loạt, Online/Offline, lịch tuần |
| **Module 05: Discovery (GPS)** | [`05_module_location_based_recycler_discovery_api.md`](documents/api%20document/05_module_location_based_recycler_discovery_api.md) | 4 | Tìm kiếm cơ sở thu gom theo bán kính PostGIS, hồ sơ vựa công khai, geocoding |
| **Module 06: Handover Requests** | [`06_module_handover_request_management_api.md`](documents/api%20document/06_module_handover_request_management_api.md) | 10 | Tạo đơn hẹn gom rác, cơ sở duyệt/hủy, ẩn thông tin liên hệ, phạt hủy muộn |
| **Module 07: Receipts & History** | [`07_module_handover_completion_and_receipt_api.md`](documents/api%20document/07_module_handover_completion_and_receipt_api.md) | 8 | Biên nhận cân số, ảnh đồng hồ cân, cảnh báo lệch $> 20\%$, cộng điểm xanh, lịch sử |
| **Module 08: Negotiation Chat** | [`08_module_in_app_realtime_negotiation_chatbox_api.md`](documents/api%20document/08_module_in_app_realtime_negotiation_chatbox_api.md) | 7 + 1 | Chatbox SignalR theo đơn, gửi ảnh phế liệu, phản đề xuất giá/giờ, khóa điều khoản |
| **Module 09: Trading & Payment** | `TradeController`, `GamificationController`, `ServicePaymentController` | 12 | Đơn thương mại, xác thực QR, Cây ảo Gamification, Webhook thanh toán VietQR |

### Công cụ kiểm tra danh mục API tự động:
Developer có thể kiểm tra nhanh số lượng API hiện tại bất cứ lúc nào bằng lệnh:
```powershell
powershell -ExecutionPolicy Bypass -File scripts/scan_api_inventory.ps1
```

---

## 🧪 KIỂM THỬ TỰ ĐỘNG (TESTING & QUALITY ASSURANCE)

Dự án trang bị đầy đủ cả **Unit Tests** ([`src/Ecolink.Backend/Ecolink.UnitTests/`](src/Ecolink.Backend/Ecolink.UnitTests/)) và **API Integration Tests** ([`src/Ecolink.Backend/EcoLink.ApiTests/`](src/Ecolink.Backend/EcoLink.ApiTests/)):

```powershell
# 1. Chạy 168 Unit Tests độc lập trong bộ nhớ (8 Core Modules)
dotnet test "src/Ecolink.Backend/Ecolink.UnitTests/Ecolink.UnitTests.csproj" --logger "console;verbosity=normal"

# 2. Chạy 236 API Integration Tests (415 kịch bản tích hợp End-to-End)
dotnet test "src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj" --logger "console;verbosity=normal"
```

* **Phạm vi kiểm thử:**
  - Logic xác thực mật khẩu, kiểm tra Refresh Token và ngăn chặn Brute-force.
  - Quy tắc tính khoảng cách không gian địa lý PostGIS và lọc bán kính cơ sở.
  - Xử lý chuyển đổi trạng thái đơn thu gom và cơ chế cảnh báo hủy muộn (`is_late_cancellation`).
  - Kiểm tra tính toán chênh lệch khối lượng biên nhận và logic cộng điểm xanh hai chiều.
  - Kiểm tra logic phản đề xuất (`Superseded`) trong đàm phán thương lượng SignalR.

---

## 👥 QUY CHUẨN ĐÓNG GÓP & LIÊN HỆ

* **Quy chuẩn lập trình:** Tuân thủ hướng dẫn tại [`documents/guideline/clean_architecture_and_coding_guidelines.md`](documents/guideline/clean_architecture_and_coding_guidelines.md).
* **Quy chuẩn Git Commit:** Tuân thủ Conventional Commits tại [`documents/guideline/git_workflow_and_commit_guidelines.md`](documents/guideline/git_workflow_and_commit_guidelines.md).
* **Hỗ trợ & Báo cáo lỗi:** Đội ngũ phát triển EcoLink Platform – Email: `ecolinksupport@gmail.com`.

---
*Bản quyền © 2026 EcoLink Platform. Toàn quyền bảo lưu.*
