# Kế Hoạch Xây Dựng Kiến Trúc Dự Án EcoLink

Dự án **EcoLink (Intelligent Recyclable Waste Classification and Trade Matching System)** đã có các tài liệu khảo sát, phân tích 24 usecase, sơ đồ phân tầng hệ thống (`architecture.png`) và một prototype AI demo (FastAPI + YOLOv8n).

Mục tiêu là hiện thực hóa kiến trúc hệ thống chuẩn doanh nghiệp (Enterprise Clean Architecture) kết hợp Microservices/Modular Monolith với AI Service chuyên biệt, bám sát tài liệu và sơ đồ kiến trúc đã đề ra.

---

## 1. User Review Required

> [!IMPORTANT]
> **Công nghệ Backend chính**: Sơ đồ kiến trúc định hướng sử dụng **ASP.NET Core (Clean Architecture)** cho Core Business Service và **Python FastAPI** cho Dedicated AI Vision Service. Máy hiện tại đã có sẵn **.NET SDK 10.0** và **Docker 29.6**.
> Chúng ta sẽ thiết lập project template theo chuẩn Clean Architecture (.NET Core) kết nối sang AI Vision Service (FastAPI) qua REST / gRPC.

> [!NOTE]
> **Cấu trúc repository**: Hiện tại code AI demo đang nằm rải rác ở thư mục gốc (`app.py`, `models/`, `data/`, `static/`). Kế hoạch kiến trúc sẽ tổ chức lại theo cấu trúc thư mục chuẩn:
> - `src/Ecolink.Backend/`: Chứa Solution .NET Clean Architecture.
> - `src/Ecolink.AiService/`: Chứa FastAPI AI Service độc lập, tái sử dụng model `best.pt` đã train.
> - `docker-compose.yml`: Triển khai hạ tầng gồm PostgreSQL/PostGIS, Redis, Backend API, AI Service.

---

## 2. Open Questions

1. **Cơ sở dữ liệu chính**: Trong sơ đồ ghi nhận *PostgreSQL + PostGIS* hoặc *SQL Server*. Đề xuất ưu tiên **PostgreSQL + PostGIS** để xử lý không gian địa lý (Geo-matching điểm thu mua gần nhất theo tọa độ GPS [UC5]) hiệu quả và tối ưu chi phí container.
2. **Giao tiếp giữa Backend và AI Service**: Giai đoạn hiện tại ưu tiên giao tiếp qua **REST API (HTTP/2 - multipart/form-data upload)** để đơn giản hóa quá trình tích hợp và debug, có sẵn interface để nâng cấp lên **gRPC** khi cần tối ưu hiệu năng truyền binary.

---

## 3. Proposed Changes

### Component 1: Tài liệu Thiết Kế Kiến Trúc Chi Tiết
Nâng cấp và hoàn thiện file kiến trúc kỹ thuật đầy đủ để làm tài liệu chuẩn cho toàn bộ dự án.

#### [MODIFY] [architecture.md](file:///c:/Projects/intelligent-waste-sorting-trading/documents/architecture.md)
- Mô tả tổng quan kiến trúc hệ thống tổng thể theo sơ đồ `architecture.png`.
- Chi tiết 4 tầng kiến trúc:
  1. **Client Layer**: Mobile/Web App cho Người dân, Portal Cơ sở thu gom, Admin Dashboard.
  2. **API Gateway / Ingress**: Routing, Rate Limiting, CORS, Authentication Gateway.
  3. **Core Business Service (ASP.NET Core Clean Architecture)**:
     - **Domain Layer**: Khai báo các Entity cốt lõi (`User`, `Role`, `WasteCategory`, `SortingRule`, `CollectionPartner`, `CollectionMaterial`, `TradeOrder`, `TransactionReceipt`, `Voucher`, `RewardHistory`, `AIFeedback`).
     - **Application Layer**: Phân chia theo CQRS / Feature Handlers, Services (`IWasteRuleEngineService`, `IAiVisionClient`, `IGeoMatchingService`, `ITradeMatchingService`, `IGamificationService`).
     - **Infrastructure Layer**: EF Core DbContext, PostGIS Provider (NetTopologySuite), Redis Cache Service, MinIO/S3 Storage Client.
     - **WebApi Layer**: Controllers mapping 24 usecases (`WasteScanController`, `TradeController`, `GamificationController`, `AdminController`, `AuthController`).
  4. **Dedicated AI Vision Service (FastAPI)**:
     - YOLOv8 Nano Inference Engine.
     - Cơ chế Out-of-Distribution Checker (ngưỡng tự tin < 60% và lọc đối tượng không hợp lệ).
     - Endpoint nhận diện ảnh và chuẩn hóa kết quả Top-3 dự đoán.
  5. **Data & Storage Layer**: PostgreSQL + PostGIS, Redis, MinIO Object Storage, Background Workers.
- Thiết kế sơ đồ quan hệ thực thể (ERD Logical Design) và sơ đồ luồng dữ liệu (Data Flow / Sequence Flows).

---

### Component 2: Khung Kiến Trúc Mã Nguồn (Scaffolding Solution)

Tổ chức lại thư mục dự án theo cấu trúc chuẩn trong `architecture.md`:

```
c:\Projects\intelligent-waste-sorting-trading\
├── src\
│   ├── Ecolink.Backend\
│   │   ├── Ecolink.sln
│   │   ├── Ecolink.Domain\
│   │   ├── Ecolink.Application\
│   │   ├── Ecolink.Infrastructure\
│   │   └── Ecolink.WebApi\
│   └── Ecolink.AiService\
│       ├── app.py
│       ├── models\best.pt
│       ├── requirements.txt
│       └── Dockerfile
├── docker-compose.yml
├── documents\
│   ├── architecture.md
│   ├── architecture.png
│   └── ...
└── README.md
```

#### [NEW] [src/Ecolink.Backend/Ecolink.sln](file:///c:/Projects/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.sln)
- Tạo Solution .NET và liên kết 4 project con:
  - `Ecolink.Domain` (Class Library): Không phụ thuộc bên ngoài, chứa Domain Entities, Enums, Domain Events.
  - `Ecolink.Application` (Class Library): Phụ thuộc Domain; chứa Application Interfaces, DTOs, Business Use Cases.
  - `Ecolink.Infrastructure` (Class Library): Phụ thuộc Application & Domain; cấu hình EF Core, DbContext, Redis, NetTopologySuite.
  - `Ecolink.WebApi` (Web API): Presentation Layer, Swagger, Middleware, Dependency Injection composition root.

#### [NEW] [src/Ecolink.AiService/](file:///c:/Projects/intelligent-waste-sorting-trading/src/Ecolink.AiService)
- Đóng gói AI Service riêng biệt vào `src/Ecolink.AiService`:
  - Di chuyển/sao chép model `models/best.pt`, rules data, và script phục vụ inference.
  - Định nghĩa API chuẩn REST `/api/v1/classify` trả về cấu trúc dữ liệu mà Backend mong đợi (Top-3 predictions, confidence, out-of-distribution flag).
  - Thêm `Dockerfile` cho AI Service.

#### [NEW] [docker-compose.yml](file:///c:/Projects/intelligent-waste-sorting-trading/docker-compose.yml)
- Cấu hình chạy cụm môi trường:
  - `db`: PostgreSQL 16 có cài sẵn PostGIS extension.
  - `redis`: Redis cache cho quy tắc phân loại và leaderboard.
  - `ai-service`: Container Python FastAPI AI Vision.
  - `backend-api`: Container ASP.NET Core Web API.

---

## 4. Verification Plan

### Automated Verification
1. **Kiểm tra biên dịch .NET Solution**:
   - Chạy lệnh:
     ```powershell
     dotnet build src/Ecolink.Backend/Ecolink.sln
     ```
   - Đảm bảo 4 project (`Domain`, `Application`, `Infrastructure`, `WebApi`) liên kết đúng và build thành công không có lỗi.
2. **Kiểm tra AI Service**:
   - Kiểm tra import và cú pháp của `src/Ecolink.AiService/app.py`.
3. **Kiểm tra Docker Compose syntax**:
   - Chạy lệnh kiểm tra cấu hình docker-compose:
     ```powershell
     docker compose config
     ```

### Manual Verification
- Review tài liệu [architecture.md](file:///c:/Projects/intelligent-waste-sorting-trading/documents/architecture.md) đảm bảo khớp 100% với các usecases trong `Báo cáo Khảo sát AI và Danh sách Usecase.md` và sơ đồ `architecture.png`.
