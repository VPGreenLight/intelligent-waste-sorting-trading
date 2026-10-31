# HƯỚNG DẪN THIẾT LẬP VÀ KHỞI CHẠY MÔI TRƯỜNG BACKEND & AI SERVICE
**Dự án:** EcoLink – Nền tảng Phân loại Rác Thông minh & Giao dịch Phế liệu  
**Tài liệu:** Hướng dẫn Cài đặt & Vận hành Môi trường Phát triển (Developer Setup & Deployment Guide)  
**Mục tiêu áp dụng:** Thành viên nhóm phát triển Backend, AI Engineer, Tester và Đánh giá Hội đồng  

---

## 📌 MỤC LỤC
1. [Tổng quan Kiến trúc Hệ thống Backend](#1-tổng-quan-kiến-trúc-hệ-thống-backend)
2. [Yêu cầu Tiền quyết (Prerequisites)](#2-yêu-cầu-tiền-quyết-prerequisites)
3. [Phần 1: Thiết lập Hạ tầng CSDL & Bộ nhớ đệm với Docker](#3-phần-1-thiết-lập-hạ-tầng-csdl--bộ-nhớ-đệm-với-docker)
4. [Phần 2: Thiết lập & Khởi chạy Dịch vụ AI (Python FastAPI + YOLOv8)](#4-phần-2-thiết-lập--khởi-chạy-dịch-vụ-ai-python-fastapi--yolov8)
5. [Phần 3: Thiết lập & Khởi chạy Core Web API (ASP .NET Core)](#5-phần-3-thiết-lập--khởi-chạy-core-web-api-asp-net-core)
6. [Phần 4: Khởi chạy Trọn gói All-in-One bằng Docker Compose](#6-phần-4-khởi-chạy-trọn-gói-all-in-one-bằng-docker-compose)
7. [Phần 5: Bảng Cổng (Port Map), Tài khoản Demo & Kiểm tra Dịch vụ](#7-phần-5-bảng-cổng-port-map-tài-khoản-demo--kiểm-tra-dịch-vụ)
8. [Phần 6: Xử lý Sự cố Thường gặp (Troubleshooting)](#8-phần-6-xử-lý-sự-cố-thường-gặp-troubleshooting)

---

## 1. TỔNG QUAN KIẾN TRÚC HỆ THỐNG BACKEND

Hệ thống Backend của EcoLink được thiết kế theo mô hình **Microservices kết hợp Clean Architecture**:

```
                              ┌──────────────────────────────────┐
                              │     Client (Web / Mobile App)    │
                              └─────────────────┬────────────────┘
                                                │
                                                ▼ HTTP / RESTful API
┌────────────────────────────────────────────────────────────────────────────────────────────────┐
│  CORE BACKEND (ASP .NET Core Web API - Port 5000)                                              │
│  - Clean Architecture: WebApi -> Application -> Domain <- Infrastructure                       │
│  - Chịu trách nhiệm: Xác thực Auth, Giao dịch Thu mua, Gamification, Rule Engine               │
└───────────────┬───────────────────────────────┬───────────────────────────────┬────────────────┘
                │                               │                               │
                │ HTTP /predict (Multipart)     │ SQL + PostGIS (Port 5432)     │ Cache (Port 6379)
                ▼                               ▼                               ▼
┌───────────────────────────────┐ ┌───────────────────────────┐ ┌────────────────────────────────┐
│ AI VISION SERVICE             │ │ POSTGRESQL 16 + POSTGIS   │ │ REDIS 7                        │
│ (Python FastAPI - Port 8000)  │ │ (Docker Container)        │ │ (Docker Container)             │
│ - YOLOv8 Bounding Box & Class │ │ - Không gian địa lý GIS   │ │ - Leaderboard điểm xanh        │
│ - CNN Phân loại chi tiết      │ │ - Lưu trữ toàn bộ dữ liệu │ │ - Cache quy tắc phân loại tĩnh │
└───────────────────────────────┘ └───────────────────────────┘ └────────────────────────────────┘
```

---

## 2. YÊU CẦU TIỀN QUYẾT (PREREQUISITES)

Trước khi bắt đầu, máy tính phát triển (Dev Machine) cần cài đặt các công cụ sau:

| Công cụ | Phiên bản khuyến nghị | Mục đích | Link tải chính thức |
|:---|:---|:---|:---|
| **Git** | $\ge 2.40$ | Quản lý mã nguồn | [git-scm.com](https://git-scm.com/) |
| **Docker Desktop** | $\ge 4.25$ (WSL 2 backend) | Chạy CSDL PostgreSQL + PostGIS, Redis | [docker.com](https://www.docker.com/products/docker-desktop/) |
| **.NET SDK** | .NET 8.0 hoặc .NET 10.0 | Biên dịch & chạy Core Web API | [dotnet.microsoft.com](https://dotnet.microsoft.com/download) |
| **Python** | **Python 3.10 hoặc 3.11** *(Khuyến cáo: Tránh Python 3.13+ vì chưa hỗ trợ PyTorch)* | Chạy AI Microservice YOLOv8 | [python.org](https://www.python.org/downloads/) |
| **IDE / Editor** | Visual Studio 2022 / VS Code / JetBrains Rider | Môi trường lập trình | [code.visualstudio.com](https://code.visualstudio.com/) |

> **⚠️ Lưu ý cực kỳ quan trọng về Python:**  
> Dịch vụ AI sử dụng thư viện **PyTorch (`torch`)** và **Ultralytics (`ultralytics`)**. Hiện tại, các bản phân phối bánh xe (wheel) nhị phân của PyTorch hoạt động ổn định nhất trên **Python 3.10 hoặc 3.11**. Không dùng Python 3.14 của hệ điều hành để cài thư viện AI trực tiếp.

---

## 3. PHẦN 1: THIẾT LẬP HẠ TẦNG CSDL & BỘ NHỚ ĐỆM VỚI DOCKER

EcoLink yêu cầu **PostgreSQL 16** tích hợp sẵn tiện ích mở rộng không gian **PostGIS 3.4** (để hỗ trợ tìm kiếm điểm thu gom gần nhất theo tọa độ GPS bằng `ST_DWithin`) và **Redis 7**.

### Bước 1.1: Khởi động Docker Desktop
Hãy đảm bảo phần mềm **Docker Desktop** đang chạy (biểu tượng cá voi màu xanh lá cây hoặc trạng thái `Engine running`).

### Bước 1.2: Khởi chạy Container PostgreSQL (PostGIS) và Redis
Mở Terminal / PowerShell tại thư mục gốc dự án:
```powershell
# Di chuyển về thư mục dự án
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading

# Khởi chạy 2 dịch vụ hạ tầng nền tảng
docker compose up -d ecolink-db ecolink-redis
```

### Bước 1.3: Kiểm tra trạng thái Container
```powershell
docker ps
```
**Kết quả kỳ vọng:**
* Container `ecolink-db` (`postgis/postgis:16-3.4-alpine`) đang chạy ở trạng thái `Up`, cổng `0.0.0.0:5432->5432`.
* Container `ecolink-redis` (`redis:7-alpine`) đang chạy ở trạng thái `Up`, cổng `0.0.0.0:6379->6379`.

### Thông số kết nối CSDL mặc định (Local Development):
* **Host:** `localhost` (hoặc `127.0.0.1`)
* **Port:** `5432`
* **Database:** `ecolink_db`
* **Username:** `postgres`
* **Password:** `postgrespassword`
* **Connection String:**
  ```
  Host=localhost;Port=5432;Database=ecolink_db;Username=postgres;Password=postgrespassword;Include Error Detail=true
  ```

---

## 4. PHẦN 2: THIẾT LẬP & KHỞI CHẠY DỊCH VỤ AI (PYTHON FASTAPI + YOLOV8)

Mã nguồn dịch vụ AI nằm tại thư mục: `src/Ecolink.AiService`.

### Bước 2.1: Tạo môi trường ảo (Virtual Environment)
Luôn luôn sử dụng môi trường ảo độc lập (`venv`) để tránh xung đột thư viện:

```powershell
# 1. Di chuyển về thư mục gốc
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading

# 2. Khởi tạo môi trường ảo với Python 3.11 (hoặc Python 3.10)
python -m venv venv
```

### Bước 2.2: Kích hoạt môi trường ảo
* **Trên Windows PowerShell:**
  ```powershell
  .\venv\Scripts\Activate.ps1
  ```
  *(Nếu gặp lỗi `Execution_Policies`, chạy lệnh: `Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass` rồi chạy lại).*
* **Trên Windows Command Prompt (CMD):**
  ```cmd
  .\venv\Scripts\activate.bat
  ```
* **Trên Linux / macOS:**
  ```bash
  source venv/bin/activate
  ```

### Bước 2.3: Cài đặt các thư viện cần thiết
Dự án hỗ trợ chạy nhẹ nhàng trên CPU hoặc tăng tốc bằng GPU NVIDIA CUDA:

* **Tùy chọn A (Chạy bằng CPU - Mặc định cho mọi Laptop/Máy tính cá nhân):**
  ```powershell
  pip install --no-cache-dir -r requirements.txt --extra-index-url https://download.pytorch.org/whl/cpu
  ```
* **Tùy chọn B (Nếu máy có card đồ họa rời NVIDIA GPU):**
  ```powershell
  pip install --no-cache-dir -r requirements.txt --extra-index-url https://download.pytorch.org/whl/cu118
  ```

### Bước 2.4: Khởi động AI Vision Service
Bạn có thể khởi động theo 1 trong 2 cách:

* **Cách 1 (Nhanh nhất - Chạy bằng script tiện ích có sẵn):**
  ```powershell
  .\run_ai_service.bat
  ```
* **Cách 2 (Chạy lệnh qua Uvicorn trực tiếp):**
  ```powershell
  set PYTHONPATH=%cd%\src\Ecolink.AiService;%PYTHONPATH%
  python -m uvicorn app:app --app-dir src/Ecolink.AiService --host 127.0.0.1 --port 8000 --reload
  ```

### Bước 2.5: Xác minh dịch vụ AI hoạt động
* Mở trình duyệt truy cập Swagger UI của AI: **`http://127.0.0.1:8000/docs`**
* Kiểm tra Health Check: **`http://127.0.0.1:8000/health`**  
  Kết quả trả về:
  ```json
  {
    "status": "healthy",
    "service": "EcoLink Dedicated AI Vision Service",
    "version": "1.0.0",
    "default_model": "yolov8_detect",
    "available_models": ["yolov8_detect", "yolov8n", "mobilenet_v3", "efficientnet_b0"]
  }
  ```

---

## 5. PHẦN 3: THIẾT LẬP & KHỞI CHẠY CORE WEB API (ASP .NET CORE)

Mã nguồn Backend nằm tại thư mục: `src/Ecolink.Backend`.

### Bước 3.1: Kiểm tra cấu hình cấu trúc Solution
Solution gồm 4 dự án chuẩn Clean Architecture:
* `Ecolink.Domain`: Chứa các Thực thể cốt lõi (User, RecyclerProfile, TradeOrder,...) và Enums.
* `Ecolink.Application`: Chứa Interfaces, DTOs, FluentValidation Validators.
* `Ecolink.Infrastructure`: Cấu hình EF Core, NetTopologySuite PostGIS, Gmail SMTP MailKit, Google OAuth.
* `Ecolink.WebApi`: Tầng Presentation chứa Controllers, Middlewares, Program.cs.

### Bước 3.2: Cài đặt công cụ Entity Framework Core CLI (Nếu chưa có)
```powershell
dotnet tool install --global dotnet-ef
```
*(Nếu đã cài đặt từ trước, có thể cập nhật qua lệnh: `dotnet tool update --global dotnet-ef`).*

### Bước 3.3: Khôi phục Dependencies và Biên dịch dự án
```powershell
# Di chuyển vào thư mục Backend
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend

# Khôi phục các gói NuGet
dotnet restore

# Build kiểm tra cú pháp
dotnet build
```
*(Kết quả mong đợi: `Build succeeded - 0 Error(s)`).*

### Bước 3.4: Cập nhật CSDL (EF Core Migration)
Đảm bảo container `ecolink-db` đang chạy ở Phần 1, sau đó chạy lệnh update migration:
```powershell
# Chạy từ thư mục gốc dự án
cd c:\Projects\EcoLink\intelligent-waste-sorting-trading

dotnet ef database update --project src/Ecolink.Backend/Ecolink.Infrastructure --startup-project src/Ecolink.Backend/Ecolink.WebApi
```
Lệnh này sẽ tự động tạo toàn bộ các bảng trong CSDL `ecolink_db`:
* Bảng tài khoản: `users`, `recycler_profiles` (chứa cột `geom` PostGIS Point 4326), `staff_profiles`, `refresh_tokens`, `password_reset_tokens`.
* Bảng nghiệp vụ: `TradeOrders`, `WasteCategories`, `UserEcoProfiles`,...

### Bước 3.5: Cấu hình Khóa Bí Mật (appsettings.Development.json)
File cấu hình phát triển cục bộ đã được thiết lập sẵn sàng tại:  
`src/Ecolink.Backend/Ecolink.WebApi/appsettings.Development.json`
* **Google OAuth Client ID:** Đã cấu hình Client ID của dự án Google Cloud Platform.
* **Gmail SMTP (MailKit):** Đã cấu hình tài khoản `ecolinksupport@gmail.com` kèm App Password bảo mật.
* **JWT Signing Key:** Cấu hình thời hạn Access Token (30 phút), Refresh Token (7 ngày).

### Bước 3.6: Khởi chạy Web API Server
```powershell
dotnet run --project src/Ecolink.Backend/Ecolink.WebApi
```
**Màn hình Terminal sẽ hiển thị:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://[::]:5000
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://[::]:5002
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to shut down.
info: Microsoft.Hosting.Lifetime[0]
      Hosting environment: Development
```

### Bước 3.7: Xác minh Web API hoạt động
* Mở trình duyệt và truy cập Swagger UI: **`http://localhost:5000`** hoặc **`http://localhost:5000/swagger`**
* Kiểm tra Health Check: **`http://localhost:5000/health`**  
  Kết quả trả về:
  ```json
  {
    "service": "EcoLink Core Business API",
    "version": "1.0.0",
    "status": "Healthy",
    "time": "2026-10-24T05:00:00Z"
  }
  ```

### Bước 3.8: Khởi chạy Nhanh Tự động (One-Click All-in-One Dev Runner)
Để tiết kiệm thời gian mở thủ công từng cửa sổ console, dự án cung cấp sẵn script chạy song song cả 3 tiến trình (.NET Web API, AI Service, và ngrok Tunnel):

```cmd
# Nhấp đúp chuột hoặc chạy từ Terminal:
.\run_all_services.bat
```
Hoặc trên PowerShell:
```powershell
.\run_all_services.ps1
```
* **Tiến trình 1:** Khởi động AI Vision Service tại `http://localhost:8000` (FastAPI + YOLOv8).
* **Tiến trình 2:** Khởi động Core Web API tại `http://localhost:5000` (ASP .NET Core).
* **Tiến trình 3:** Mở ngrok Tunneling public cổng `http://localhost:5000` ra Internet (kèm Web Inspector tại `http://localhost:4040`).

Khi cần dừng toàn bộ các dịch vụ và giải phóng cổng, chạy:
```cmd
.\stop_all_services.bat
```

---

## 6. PHẦN 4: KHỞI CHẠY TRỌN GÓI ALL-IN-ONE BẰNG DOCKER COMPOSE

Nếu bạn muốn khởi chạy **toàn bộ 4 thành phần** (Postgres PostGIS + Redis + Python AI + .NET Web API) mà không cần cài đặt .NET SDK hay Python lên máy trạm:

### Bước 4.1: Chạy lệnh Docker Compose
Tại thư mục gốc dự án:
```powershell
# Build toàn bộ Dockerfile và khởi chạy nền
docker compose up --build -d
```

### Bước 4.2: Kiểm tra các Container đang chạy
```powershell
docker compose ps
```
**Danh sách 4 container hoạt động đồng bộ:**
1. `ecolink-db`: CSDL PostGIS (Cổng `5432`)
2. `ecolink-redis`: Bộ nhớ đệm (Cổng `6379`)
3. `ecolink-ai-service`: AI Microservice (Cổng `8000`)
4. `ecolink-backend-api`: Web API Gateway (Cổng `5000`)

### Bước 4.3: Theo dõi Log khi cần debug
```powershell
# Xem log của Core Backend
docker compose logs -f ecolink-backend-api

# Xem log của AI Service
docker compose logs -f ecolink-ai-service
```

### Bước 4.4: Dừng hệ thống
```powershell
docker compose down
```
*(Nếu muốn xóa sạch toàn bộ dữ liệu CSDL để làm mới từ đầu, thêm cờ `-v`: `docker compose down -v`).*

---

## 7. PHẦN 5: BẢNG CỔNG (PORT MAP), TÀI KHOẢN DEMO & KIỂM TRA DỊCH VỤ

### 7.1. Bảng Phân bổ Cổng Mạng (Network Ports)

| Dịch vụ | Giao thức | Cổng Host | Cổng Container | Địa chỉ kiểm tra |
|:---|:---:|:---:|:---:|:---|
| **Core Web API (.NET)** | HTTP / REST | `5000` | `5000` | `http://localhost:5000/swagger` |
| **Core Web API (HTTP/2)** | HTTP/2 | `5002` | `5002` | `http://localhost:5002` |
| **AI Vision Service (FastAPI)**| HTTP / REST | `8000` | `8000` | `http://127.0.0.1:8000/docs` |
| **PostgreSQL 16 + PostGIS** | TCP | `5432` | `5432` | Kết nối qua DBeaver / pgAdmin |
| **Redis Cache** | TCP | `6379` | `6379` | Kết nối qua RedisInsight |

---

### 7.2. Tài khoản Demo có sẵn (Tự động Seed vào CSDL)

Hệ thống đã tích hợp sẵn cơ chế **Auto-Seed** tại [`AuthService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AuthService.cs). Bạn có thể đăng nhập ngay mà không cần tạo mới:

| Loại vai trò (Role) | Tên đăng nhập (Username) | Mật khẩu (Password) | Email | Ghi chú quyền hạn |
|:---|:---|:---|:---|:---|
| **Người dân (Citizen)** | `citizen` | `citizen123` | `citizen@ecolink.vn` | Quét rác AI, tạo đơn thu gom, tích điểm xanh |
| **Cơ sở thu gom (Collector)**| `collector` | `collector123` | `collector@ecolink.vn` | Nhận đơn thu gom, xác nhận khối lượng phế liệu |
| **Quản trị viên (Admin)** | `admin` | `admin123` | `admin@ecolink.vn` | Quản trị quy tắc phân loại, duyệt hồ sơ cơ sở |

---

### 7.3. Hướng dẫn Test Nhanh Luồng Xác thực trên Swagger UI

1. Mở trình duyệt: `http://localhost:5000`
2. Mở endpoint `POST /api/v1/auth/login`, bấm **Try it out**, điền:
   ```json
   {
     "usernameOrEmail": "citizen",
     "password": "citizen123"
   }
   ```
3. Bấm **Execute**. Sao chép chuỗi mã `accessToken` trong JSON phản hồi.
4. Cuộn lên đầu trang Swagger, bấm nút **Authorize 🔓** (màu xanh lá ở góc trên bên phải).
5. Dán mã token theo định dạng: `Bearer <mã_token_vừa_copy>` rồi bấm **Authorize**.
6. Thử nghiệm gọi endpoint `GET /api/v1/auth/me` để lấy thông tin cá nhân và điểm sinh thái (Green Points).

---

## 8. PHẦN 6: XỬ LÝ SỰ CỐ THƯỜNG GẶP (TROUBLESHOOTING)

### 🔴 Lỗi 1: Xung đột cổng 5432 (`Bind for 0.0.0.0:5432 failed: port is already allocated`)
* **Nguyên nhân:** Máy tính của bạn đã có sẵn dịch vụ PostgreSQL chạy ngầm trên Windows.
* **Cách khắc phục:**
  1. Mở `services.msc` trên Windows $\rightarrow$ Tìm dịch vụ `postgresql-x64-xx` $\rightarrow$ Chuột phải chọn **Stop** và chuyển sang **Manual**.
  2. Hoặc đổi cổng ngoài trong `docker-compose.yml` thành `"5433:5432"`, đồng thời sửa lại chuỗi kết nối trong `appsettings.Development.json` thành `Port=5433`.

### 🔴 Lỗi 2: Lỗi cài đặt PyTorch trên Python 3.13 hoặc 3.14
* **Hiện tượng:** Chạy `pip install -r requirements.txt` báo lỗi `No matching distribution found for torch`.
* **Cách khắc phục:** Máy tính đang dùng Python phiên bản quá mới. Hãy cài đặt thêm **Python 3.11** từ trang chủ python.org, sau đó tạo lại môi trường ảo:
  ```powershell
  py -3.11 -m venv venv
  .\venv\Scripts\activate
  pip install -r requirements.txt --extra-index-url https://download.pytorch.org/whl/cpu
  ```

### 🔴 Lỗi 3: Không thể thực thi file script PowerShell (`Activate.ps1 cannot be loaded`)
* **Nguyên nhân:** Chính sách bảo mật ExecutionPolicy của Windows hạn chế chạy script PowerShell.
* **Cách khắc phục:**
  ```powershell
  Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
  ```
  Sau đó chạy lại lệnh kích hoạt môi trường ảo `.\venv\Scripts\Activate.ps1`.

### 🔴 Lỗi 4: Lỗi `Could not copy ... dll because it is being used by another process` khi chạy `dotnet run`
* **Nguyên nhân:** Tiến trình chạy WebApi cũ chưa được tắt hẳn nên đang khóa file dll trong thư mục `bin`.
* **Cách khắc phục:**
  Mở PowerShell và tắt tiến trình dotnet bị treo:
  ```powershell
  Stop-Process -Name "Ecolink.WebApi" -Force -ErrorAction SilentlyContinue
  dotnet build src/Ecolink.Backend/Ecolink.WebApi
  ```

### 🔴 Lỗi 5: Lỗi kết nối PostGIS (`npgsql: type geometry not found`)
* **Nguyên nhân:** Database PostgreSQL chưa kích hoạt extension PostGIS.
* **Cách khắc phục:**
  Truy cập vào container database và bật thủ công:
  ```powershell
  docker exec -it ecolink-db psql -U postgres -d ecolink_db -c "CREATE EXTENSION IF NOT EXISTS postgis;"
  ```
  Sau đó khởi động lại Web API.

---

## 9. CHECKLIST KIỂM TRA TRƯỚC KHI BÀN GIAO HOẶC DEMO HỘI ĐỒNG

- [ ] Docker container `ecolink-db` và `ecolink-redis` đang ở trạng thái `Up`.
- [ ] Dịch vụ AI Vision Service mở được Swagger tại `http://127.0.0.1:8000/docs`.
- [ ] Dịch vụ Core Web API mở được Swagger tại `http://localhost:5000`.
- [ ] Đăng nhập thử thành công bằng 3 tài khoản mẫu: `citizen`, `collector`, `admin`.
- [ ] Gọi thử API phân loại rác `POST /api/v1/waste-scans/classify` kèm 1 file ảnh rác và nhận được phản hồi phân loại + hướng dẫn tái chế hợp lệ.
