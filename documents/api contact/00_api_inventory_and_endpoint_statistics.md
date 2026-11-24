# BẢNG TỔNG HỢP, DỰ BÁO QUY MÔ & THỐNG KÊ SỐ LƯỢNG API TOÀN HỆ THỐNG
## (FULL-SCOPE API INVENTORY, FORECAST & STATISTICAL DASHBOARD)

**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform (`SEP490_G141`)  
**Phiên bản tài liệu:** `v2.7.0` (Đồng bộ 100% mã nguồn thực tế: **`218` Canonical Endpoints** [`202` Core `.NET 10` + `16` Python `FastAPI`], **`252` Route Bindings** bao gồm **`34` backward-compatibility aliases**; bổ sung `UploadProof` ở `M-07`, `17` canonical actions Gamification & Virtual Tree Care ở `M-10`, `5` canonical actions Blog Workflow ở `M-14`, `6` APIs `PlatformPoliciesController` ở `M-15`, toàn bộ `16` APIs AI Service chuẩn **YOLO26 Two-Stage 10 danh mục rác**, và cập nhật đầy đủ phân quyền thực tế `[Authorize]` / `[AllowAnonymous]` / `[HasPermission]`)  
**Thời điểm cập nhật:** 23/11/2026  
**Công nghệ Backend:** ASP.NET Core 10 WebApi (Clean Architecture, Npgsql PostGIS, SignalR WebSocket, `37` Controller classes trong `35` tệp `.cs`)  
**Công nghệ AI Service:** Python 3.11 FastAPI (`YOLO26` Unified Two-Stage End-to-End: `yolo26_detect`, `yolo26s_cls`, `yolo26n_cls` + benchmark `mobilenet_v3`, `efficientnet_b0` — `10` danh mục rác chuẩn)  
**Tình trạng tài liệu:** **Tài liệu quy hoạch & đối chiếu tổng thể (Master API Inventory)** – Dùng làm nguồn dữ liệu chuẩn để đồng bộ bảng tính `Report_API List_SEP490_G141` trên Google Drive (`Dashboard` & `API Inventory`).

---

## 1. BẢNG TỔNG HỢP SỐ LƯỢNG API TOÀN DỰ ÁN (EXECUTIVE SUMMARY DASHBOARD)

Tính trên **toàn bộ phạm vi mã nguồn và đặc tả SRS (`Report-3_Software Requirement Specification_SEP490_G141`)**, hệ sinh thái **EcoLink** có tổng quy mô **`218` Canonical Endpoints** (đạt độ phủ triển khai **`100.0%`**, `0` Planned), tương ứng **`252` Route Bindings** khi tính kèm **`34` tuyến đường tương thích ngược (aliases)** tại `M-10` (`19` aliases) và `M-14` (`15` aliases):

* **Tổng quy mô toàn hệ thống (Full Project Scope – `218` Canonical Endpoints / `252` Route Bindings):**
  * **`202` Endpoints thuộc Core Backend (`Ecolink.WebApi` - .NET 10):** gồm **`199` Controller REST Actions** (trên `37` Controller classes trong `35` tệp `.cs`; tổng `233` `[Http*]` route bindings gồm `199` canonical + `34` aliases) + **`1` Minimal API** (`GET /health`) + **`2` SignalR WebSocket Hubs** (`/hubs/negotiation-chat`, `/hubs/notifications`) = `202` Canonical Endpoints (`236` tổng route bindings).
  * **`16` REST Endpoints thuộc Dedicated AI Vision Service (`src/Ecolink.AiService/app.py` - Python FastAPI):** gồm `GET /`, `GET /health` và `14` endpoints `/api/v1/*` phục vụ mô hình **YOLO26** (`yolo26_detect`, `yolo26s_cls`, `yolo26n_cls`, `mobilenet_v3`, `efficientnet_b0`), mẫu kiểm thử, Rule Engine, phản hồi sai, đóng gói tập dữ liệu `70/20/10` và nạp nóng trọng số.
* **Tiến độ triển khai thực tế trên nhánh hiện tại:**
  * **Đã triển khai (`218` Endpoints – đạt `100.0%`):** gồm `200` Core REST APIs + `2` SignalR WebSocket Hubs + `16` AI FastAPI APIs (`100%`).
  * **Dự kiến bổ sung (`0` Endpoints – `0.0%`):** toàn bộ 15 phân hệ nghiệp vụ từ `M-01` đến `M-15`, `SYS` và `AIS` đã hoàn tất 100%.

```text
+--------------------------------------------------------------------------------------------------------+
|                         TỔNG QUY MÔ API TOÀN HỆ THỐNG ECOLINK (FULL SRS SCOPE)                         |
|                                        218 CANONICAL ENDPOINTS                                         |
|                             (252 Route Bindings incl. 34 Compatibility Aliases)                        |
|                        [ Đã triển khai: 218 (100.0%)  |  Dự kiến bổ sung: 0 (0.0%) ]                   |
+---------------------------------------------------+----------------------------------------------------+
|       Core .NET 10 WebApi: 202 Endpoints          |       Dedicated AI Service: 16 Endpoints           |
| (199 Controller + 1 Minimal REST + 2 SignalR WS)  |               (FastAPI Python 3.11)              |
|     [ 236 Bindings incl. 34 Aliases | Planned: 0 ]|             [ 16 REST | Planned: 0 ]             |
+---------------------------------------------------+----------------------------------------------------+
```

### Bảng chỉ số KPI đầu trang cho Sheet `API Inventory` & `Dashboard` (Google Drive)

| Chỉ số KPI trên Google Sheet | Hiện trạng Code (`v2.7.0`) | Dự kiến mở rộng (Planned) | Tổng quy mô Toàn dự án (`v2.7.0`) |
|:---|:---:|:---:|:---:|
| **Tổng Endpoint (Canonical)** | 218 | 0 | **218** |
| **Core .NET 10** *(REST + WS)* | 202 | 0 | **202** |
| **AI FastAPI** | 16 | 0 | **16** |
| **WebSocket (SignalR Hub)** | 2 | 0 | **2** |

---

### 1.1. Thống kê số lượng API theo Module nghiệp vụ (Đã triển khai vs. Dự kiến)

| Mã Modul | Mã SRS | Tên Module / Phân hệ nghiệp vụ | Controller / Service phụ trách | Đã code | Dự kiến | REST | WS | Tổng API | Tỷ trọng | Tiến độ |
|:---:|:---:|:---|:---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **M-01** | `FE-01..02` | **Authentication & Account** | `AuthController` | 14 | 0 | 14 | 0 | **14** | 6.4% | 100.0% |
| **M-02** | `FE-04` | **Recycler Onboarding & Verification** | `RecyclerApplicationsController` | 6 | 0 | 6 | 0 | **6** | 2.8% | 100.0% |
| **M-03** | `FE-03` | **AI Waste Classification & Rule Engine** | `WasteScanController` (7), `WasteCategoryController` (4) | 11 | 0 | 11 | 0 | **11** | 5.0% | 100.0% |
| **M-04** | `FE-05` | **Recycler Profile, Pricing & Availability** | `RecyclerController` | 9 | 0 | 9 | 0 | **9** | 4.1% | 100.0% |
| **M-05** | `FE-06` | **Location-Based Recycler Discovery** | `RecyclersDiscoveryController` (2), `GeocodingController` (1), `TradeController` (1) | 4 | 0 | 4 | 0 | **4** | 1.8% | 100.0% |
| **M-06** | `FE-07` | **Handover Request Management** | `HandoverRequestController` (5), `RecyclerHandoverController` (5) | 10 | 0 | 10 | 0 | **10** | 4.6% | 100.0% |
| **M-07** | `FE-09` | **Handover Completion, Receipt & History** | `RecyclerReceiptController` (4), `RecyclerTransactionController` (1), `HandoverReceiptController` (3), `TransactionHistoryController` (1) | 9 | 0 | 9 | 0 | **9** | 4.1% | 100.0% |
| **M-08** | `FE-08` | **In-App Real-Time Negotiation Chatbox** | `NegotiationChatController` (7), `NegotiationChatHub` (1) | 8 | 0 | 7 | 1 | **8** | 3.7% | 100.0% |
| **M-09** | `FE-07/09` | **Direct Trade Orders & QR Confirmation** | `TradeController` | 5 | 0 | 5 | 0 | **5** | 2.3% | 100.0% |
| **M-10** | `FE-13` | **Gamification, Virtual Tree & Rewards** | `GamificationController` (3), `UserGamificationController` (8), `PointsAndRewardsController` (8), `StaffGamificationController` (17), `AdminGamificationController` (8) | 44 | 0 | 44 | 0 | **44** | 20.2% | 100.0% |
| **M-11** | `FE-10` | **Rating, Review & Complaint Resolution** | `ReviewsController` (5), `ComplaintsController` (3), `StaffComplaintsController` (4) | 12 | 0 | 12 | 0 | **12** | 5.5% | 100.0% |
| **M-12** | `FE-14` | **In-System Service Packages & VietQR** | `ServicePaymentController` (6), `ServicePackagesController` (6), `AdminReconciliationController` (3) | 15 | 0 | 15 | 0 | **15** | 6.9% | 100.0% |
| **M-13** | `FE-15` | **AI Misclassification & Dataset Curation** | `WasteScanController` (1), `StaffAiFeedbackController` (6), `StaffDatasetCurationController` (3) | 10 | 0 | 10 | 0 | **10** | 4.6% | 100.0% |
| **M-14** | `FE-16` | **Blog & Content Management** | `PublicBlogController` (2), `StaffBlogController` (8), `AdminBlogController` (5) | 15 | 0 | 15 | 0 | **15** | 6.9% | 100.0% |
| **M-15** | `FE-17..18` | **Account, Staff, Governance, Policies & Analytics** | `AdminAnalyticsController` (4), `StaffAccountsController` (4), `AdminStaffController` (7), `StaffAttendanceController` (4), `NotificationsController` (3), `NotificationHub` (1), `PlatformPoliciesController` (6) | 29 | 0 | 28 | 1 | **29** | 13.3% | 100.0% |
| **SYS** | `INFRA` | **System Infrastructure & Health** | Minimal API (`Program.cs`) | 1 | 0 | 1 | 0 | **1** | 0.5% | 100.0% |
| **AIS** | `FE-03/15` | **Dedicated AI Vision Service (Python)** | FastAPI (`src/Ecolink.AiService/app.py`) | 16 | 0 | 16 | 0 | **16** | 7.3% | 100.0% |
| **TỔNG** | **ALL SRS** | **TOÀN BỘ HỆ THỐNG ECOLINK** | **Core .NET 10 (202) + FastAPI Python (16)** | **218** | **0** | **216** | **2** | **218** | **100.0%** | **100.0%** |

---

### 1.2. Thống kê số lượng theo Phương thức (HTTP Methods & Protocols)

```text
  [GET]     ############################################ (96) - 44.0%
  [POST]    ################################## (74) - 33.9%
  [PUT]     ########## (22) - 10.1%
  [PATCH]   ######### (19) - 8.7%
  [DELETE]  ## (5) - 2.3%
  [WS]      # (2) - 0.9%
```

| Phương thức HTTP / Giao thức | Ý nghĩa sử dụng chính trong dự án | Đã triển khai | Dự kiến bổ sung | Tổng quy mô | Tỷ lệ (%) |
|:---|:---|:---:|:---:|:---:|:---:|
| **GET** | Truy vấn danh sách, tìm kiếm PostGIS, đọc hồ sơ/khiếu nại/chính sách, KPI Dashboard, Audit Log, xuất báo cáo CSV/XLSX/ZIP, FastAPI models/samples/rules/stats | 96 | 0 | **96** | 44.0% |
| **POST** | Đăng ký/đăng nhập, quét rác AI, tạo đơn thu gom, upload ảnh cân/bằng chứng/quà/blog, biên nhận, điểm danh, chăm sóc/thu hoạch cây, đổi quà, khiếu nại, VietQR, đóng gói Dataset | 74 | 0 | **74** | 33.9% |
| **PUT** | Cập nhật toàn phần hồ sơ, bảng giá, cấu hình Gamification, chu kỳ BXH, chính sách nền tảng, gán nhãn ảnh chuẩn, phân quyền RBAC Staff, cập nhật luật FastAPI | 22 | 0 | **22** | 10.1% |
| **PATCH** | Chuyển trạng thái nghiệp vụ tức thời: duyệt khiếu nại, duyệt blog, kích hoạt chu kỳ, khóa tài khoản, đối soát giao dịch, bật/tắt danh mục/quà/chính sách | 19 | 0 | **19** | 8.7% |
| **DELETE** | Xóa đánh giá của chính tác giả (`BR-RVW-03`), xóa thẻ Eco Fact nháp, xóa bài Blog nháp, xóa mục điều khoản nền tảng, vô hiệu hóa mềm tài khoản | 5 | 0 | **5** | 2.3% |
| **WebSocket (`WS`)** | Kết nối SignalR song công thời gian thực cho phòng Chat thương lượng giá và kênh Đẩy thông báo hệ thống | 2 | 0 | **2** | 0.9% |
| **Tổng cộng** | | **218** | **0** | **218** | **100.0%** |

---

### 1.3. Thống kê theo Cơ chế Phân quyền Truy cập (Role-Based Access Control)

#### A. Bảng tổng hợp theo 5 Nhóm RBAC chuẩn (Khớp 100% cột `Nhóm RBAC` trên Google Sheet)

| Nhóm RBAC chuẩn | Đối tượng được phép truy cập | Đã triển khai | Dự kiến bổ sung | Tổng API | Tỷ lệ (%) | Ví dụ nghiệp vụ tiêu biểu |
|:---|:---|:---:|:---:|:---:|:---:|:---|
| **Public / Guest** | Mọi người dùng (kể cả chưa đăng nhập, Webhook Gateway & Internal AI) | 41 | 0 | **41** | 18.8% | Đăng ký/đăng nhập, upload tài liệu KYB công khai, tìm vựa GPS, bảng xếp hạng, đọc Blog, chính sách `/terms`, xem Review vựa, Webhook VietQR, 16 endpoint FastAPI |
| **Citizen (User)** | Người dân đã xác thực tài khoản (`Role = User / Citizen`) | 22 | 0 | **22** | 10.1% | Nộp đơn mở vựa, lịch sử quét AI cá nhân, đặt lịch thu gom, xác nhận/khiếu nại biên nhận, nuôi/chăm sóc/thu hoạch Cây Ảo, mở Eco Fact, gửi/xóa đánh giá sao, lịch sử báo cáo AI sai |
| **Recycler (Collector)** | Đối tác cơ sở thu gom đã được xác minh (`Role = Recycler / Collector`) | 20 | 0 | **20** | 9.2% | Hồ sơ vựa, cấu hình bảng giá thu mua, lịch mở cửa, nhận/từ chối/hủy đơn, upload ảnh cân & phát hành biên nhận điện tử, chốt điều khoản chat |
| **Staff / Admin** | Nhân viên vận hành (`Staff`) và Quản trị viên hệ thống (`Admin`) | 90 | 0 | **90** | 41.3% | Duyệt vựa, xử lý khiếu nại, kho quà & Eco Fact (`17` APIs), gán nhãn & đóng gói Dataset AI, biên tập/duyệt/thu hồi Blog (`13` APIs), cấu hình Gamification & chu kỳ BXH (`8` APIs), đối soát VietQR, chính sách nền tảng, KPI & RBAC |
| **Authenticated (All)** | Nhiều vai trò đã đăng nhập (`Citizen / Recycler` hoặc mọi User hợp lệ `[Authorize]`) | 45 | 0 | **45** | 20.6% | Hồ sơ cá nhân, quét phân loại ảnh AI & gửi báo sai, chat thương lượng, điểm danh Streak, ví điểm & đổi quà, gửi khiếu nại, mua gói dịch vụ VietQR, nhận thông báo REST & WS |
| **Tổng cộng** | | **218** | **0** | **218** | **100.0%** | |

#### B. Phân rã chi tiết nội bộ nhóm Quản trị & Vận hành (`Staff / Admin` – `90 APIs`)

| Phân tầng quyền nội bộ | Số lượng API | Phạm vi Use Case phụ trách |
|:---|:---:|:---|
| **Staff & Admin (Vận hành chung)** | **46** | Duyệt hồ sơ mở vựa (`S-04`, `3` APIs), phân xử khiếu nại & kiểm duyệt đánh giá (`S-05`, `U-10`, `4` APIs `[HANDLE_COMPLAINT]`), quản lý Eco Fact & kho quà trả thưởng (`S-09`, `S-10`, `17` APIs `[MANAGE_INVENTORY]`), duyệt ảnh AI sai & đóng gói Dataset (`S-01..S-03`, `9` APIs `[REVIEW_AI]`/`[CURATE_DATASET]`), soạn thảo & thu hồi Blog (`S-08`, `8` APIs `[MANAGE_BLOG]`), tra cứu & khóa tài khoản vi phạm (`S-06`, `S-07`, `4` APIs `[MANAGE_ACCOUNTS]`), xem danh sách quản trị điều khoản nền tảng (`1` API `Roles = Admin,Staff`). |
| **Staff (Chấm công ca trực)** | **3** | Điểm danh vào ca, kết thúc ca trực và xem bảng công cá nhân của Nhân viên (`S-11`, `Roles = Staff,Admin`, tách biệt ví điểm theo `BR-GAM-08`/`BR-GAM-09`). |
| **Admin Only (Quản trị cấp cao)** | **41** | Cấu hình danh mục rác & Rule Engine (`A-05`, `A-06`, `5` APIs), cấu hình tham số Gamification, khôi phục mặc định & quản lý chu kỳ đua Top (`A-07`, `A-08`, `8` APIs), duyệt/từ chối xuất bản Blog (`A-09`, `5` APIs), cấu hình phí gói dịch vụ & đối soát dòng tiền VietQR (`A-10`, `A-11`, `7` APIs), quản trị chính sách nền tảng (`4` APIs), Dashboard KPI, xuất báo cáo, Audit Log, chấm công toàn đội ngũ, quản lý tài khoản & phân quyền RBAC cho Staff (`A-01..A-04`, `12` APIs). |

---

### 1.4. Ma trận Đối chiếu Độ phủ Use Case SRS sang Danh mục API

| Nhóm Phân hệ SRS | Danh sách Mã Use Case được bao phủ | Mã Modul | Dải STT Endpoint | Tổng số API |
|:---|:---|:---:|:---:|:---:|
| **Auth, Profile & Onboarding** | `G-02`, `SU-01`, `SU-02`, `SU-03`, `U-17`, `S-04` | `M-01`, `M-02` | `001` → `020` | **20** |
| **AI Waste Scan, Catalog & Rule Engine** | `U-01`, `U-02`, `U-03`, `U-04` *(gửi báo lỗi)*, `A-05`, `A-06` | `M-03`, `AIS` | `021` → `031`, `203` → `218` | **27** |
| **Recycler Operations & Geo Discovery** | `R-01`, `R-02`, `U-05` | `M-04`, `M-05` | `032` → `044` | **13** |
| **Handover, Negotiation Chat & Receipt** | `U-06`, `U-07`, `U-08`, `U-09`, `U-12`, `R-03`, `R-04`, `R-05`, `R-06` | `M-06` → `M-09` | `045` → `076` | **32** |
| **1. Gamification & Rewards (`FE-13`)** | `U-13`, `U-14`, `U-15`, `S-09`, `S-10`, `A-07`, `A-08` | `M-10` | `077` → `120` | **44** |
| **2. Rating, Review & Complaint (`FE-10`)** | `U-10`, `U-11`, `S-05` | `M-11` | `121` → `132` | **12** |
| **3. In-System Service Packages (`FE-14`)** | `U-16`, `R-07`, `A-10`, `A-11` | `M-12` | `133` → `147` | **15** |
| **4. AI Feedback & Dataset Curation (`FE-15`)** | `U-04` *(theo dõi)*, `S-01`, `S-02`, `S-03` | `M-13` *(+ `AIS`)* | `148` → `157` | **10** |
| **5. Blog & Content Management (`FE-16`)** | `G-01`, `S-08`, `A-09` | `M-14` | `158` → `172` | **15** |
| **6. Governance, Staff, Policies & Analytics (`FE-17`, `FE-18`)** | `A-01`, `A-02`, `A-03`, `A-04`, `S-06`, `S-07`, `S-11`, `SU-04` | `M-15`, `SYS` | `173` → `202` | **30** |

---

## 2. DANH MỤC CHI TIẾT 100% CÁC API ENDPOINTS TOÀN DỰ ÁN (`218` CANONICAL ENDPOINTS)

> **Hướng dẫn đồng bộ sang Google Sheet (`Report_API List_SEP490_G141`):**  
> Các bảng dưới đây chuẩn hóa đồng nhất 9 cột: `STT` | `Mã Modul` | `Phương thức` | `Tuyến đường Endpoint (Route)` | `Phân quyền (RBAC)` | `Action / Function` | `Mô tả chức năng & Use Case nghiệp vụ` | `Nhóm RBAC` | `Trạng thái` để sao chép trực tiếp vào tab `API Inventory` (dòng `5` → `222`). Các tuyến đường tương thích ngược (`34` aliases tại `M-10` và `M-14`) được chú thích trực tiếp ngay trong cột `Tuyến đường Endpoint (Route)`.

---

### PHÂN HỆ 01 (`M-01`): XÁC THỰC & QUẢN TRỊ TÀI KHOẢN (AUTHENTICATION & ACCOUNT)
* **Tài liệu đặc tả chi tiết:** [`01_module_authentication_api.md`](./01_module_authentication_api.md)
* **Controller:** `AuthController` | **Base Route:** `/api/v1/auth` | **Quy mô:** `14` APIs (14 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **001** | `M-01` | `POST` | `/api/v1/auth/register` | Public (Guest) | `Register` | Đăng ký tài khoản mới cho Người dân (`Citizen`), khởi tạo đồng thời hồ sơ `UserEcoProfile` và cấp HttpOnly Cookie + JWT (`G-02`). | Public / Guest | Đã triển khai |
| **002** | `M-01` | `POST` | `/api/v1/auth/register-recycler` | Public (Guest) | `RegisterRecycler` | Đăng ký tài khoản cho Cơ sở thu gom, tạo đồng thời hồ sơ cơ sở ở trạng thái chờ duyệt `Pending` (`U-17`). | Public / Guest | Đã triển khai |
| **003** | `M-01` | `POST` | `/api/v1/auth/login` | Public (Guest) | `Login` | Đăng nhập bằng Username/Password (cấp Bearer JWT + HttpOnly cookies `accessToken`/`refreshToken`), chống Brute-force khóa tạm thời sau 5 lần sai liên tiếp (`SU-01`, `BR-AUTH-04`). | Public / Guest | Đã triển khai |
| **004** | `M-01` | `GET` | `/api/v1/auth/google-config` | Public (Guest) | `GetGooglePublicConfig` | Trả về `ClientId` Google OAuth 2.0 để Frontend khởi tạo luồng đăng nhập nhanh Google (`SU-01`). | Public / Guest | Đã triển khai |
| **005** | `M-01` | `POST` | `/api/v1/auth/google` | Public (Guest) | `GoogleLogin` | Đăng nhập hoặc liên kết tài khoản nhanh bằng Google ID Token (`SU-01`, `BR-AUTH-05`). | Public / Guest | Đã triển khai |
| **006** | `M-01` | `POST` | `/api/v1/auth/refresh-token` | Public (Cookie/Body) | `RefreshToken` | Cấp mới Access Token (30 phút) bằng Refresh Token (7 ngày qua Cookie `refreshToken` hoặc Body), xoay vòng chống Replay Attack (`BR-AUTH-06`). | Public / Guest | Đã triển khai |
| **007** | `M-01` | `POST` | `/api/v1/auth/forgot-password` | Public (Guest) | `ForgotPassword` | Yêu cầu khôi phục mật khẩu, sinh mã OTP/Token gửi tới Gmail qua SMTP (`SU-03`, `BR-AUTH-09`). | Public / Guest | Đã triển khai |
| **008** | `M-01` | `POST` | `/api/v1/auth/reset-password` | Public (Guest) | `ResetPassword` | Xác thực mã Token hợp lệ và cập nhật mật khẩu mới cho tài khoản (`SU-03`, `BR-AUTH-09`). | Public / Guest | Đã triển khai |
| **009** | `M-01` | `PUT` | `/api/v1/auth/profile` | Authenticated | `UpdateProfile` | Cập nhật họ tên, số điện thoại, địa chỉ mặc định và ảnh đại diện của người dùng hiện tại (`SU-02`). | Authenticated (All) | Đã triển khai |
| **010** | `M-01` | `POST` | `/api/v1/auth/change-password` | Authenticated | `ChangePassword` | Đổi mật khẩu tài khoản yêu cầu xác nhận mật khẩu hiện tại và thu hồi phiên thiết bị khác (`SU-02`). | Authenticated (All) | Đã triển khai |
| **011** | `M-01` | `POST` | `/api/v1/auth/logout` | Authenticated (Cookie/Body) | `Logout` | Thu hồi Refresh Token hiện tại, xóa các cookie phiên (`accessToken`, `refreshToken`, `ecolink_session_active`) và kết thúc phiên an toàn (`SU-01`). | Authenticated (All) | Đã triển khai |
| **012** | `M-01` | `GET` | `/api/v1/auth/me` | Authenticated (Bearer/Cookie) | `GetCurrentUser` | Lấy thông tin định danh, vai trò RBAC và trạng thái hồ sơ của tài khoản đang đăng nhập qua Bearer Header, Query hoặc HttpOnly Cookie `accessToken` (`SU-02`). | Authenticated (All) | Đã triển khai |
| **013** | `M-01` | `GET` | `/api/v1/auth/demo-users` | Public (Dev/Test) | `GetDemoUsers` | Danh sách tài khoản mẫu (`citizen`, `recycler`, `staff`, `admin`) phục vụ môi trường kiểm thử nhanh. | Public / Guest | Đã triển khai |
| **014** | `M-01` | `POST` | `/api/v1/auth/vietqr-token` | Authenticated (Basic) | `GenerateVietQrToken` | Sinh chữ ký token bảo mật (yêu cầu `Authorization: Basic`) phục vụ tích hợp cổng thanh toán VietQR / PayOS (`FE-14`). | Authenticated (All) | Đã triển khai |

---

### PHÂN HỆ 02 (`M-02`): TIẾP NHẬN & XÉT DUYỆT CƠ SỞ THU GOM (RECYCLER ONBOARDING & VERIFICATION)
* **Tài liệu đặc tả chi tiết:** [`02_module_recycler_onboarding_api.md`](./02_module_recycler_onboarding_api.md)
* **Controller:** `RecyclerApplicationsController` | **Base Route:** `/api/v1/recycler-applications` | **Quy mô:** `6` APIs (6 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **015** | `M-02` | `POST` | `/api/v1/recycler-applications/upload-document` | Public (`AllowAnonymous`) | `UploadDocument` | Tải giấy phép kinh doanh / chứng từ môi trường / ảnh mặt tiền cơ sở lên Cloudflare R2 (hỗ trợ cả form đăng ký `/join-recycler` và người dân nâng cấp) (`U-17`). | Public / Guest | Đã triển khai |
| **016** | `M-02` | `POST` | `/api/v1/recycler-applications` | Authenticated (Citizen) | `ApplyForUpgrade` | Nộp hồ sơ đăng ký mở cơ sở thu mua: tên vựa, địa chỉ, tọa độ GPS (PostGIS Point), danh sách chứng từ (`U-17`). | Citizen (User) | Đã triển khai |
| **017** | `M-02` | `GET` | `/api/v1/recycler-applications/me` | Authenticated | `GetMyApplication` | Người nộp đơn theo dõi tiến độ thẩm định hồ sơ (`Pending`, `Verified`, `Rejected`) và ghi chú phản hồi (`U-17`). | Authenticated (All) | Đã triển khai |
| **018** | `M-02` | `GET` | `/api/v1/recycler-applications` | Staff / Admin | `GetApplications` | Nhân viên vận hành lấy danh sách đơn đăng ký mở cơ sở cần thẩm định có lọc trạng thái và phân trang (`S-04`). | Staff / Admin | Đã triển khai |
| **019** | `M-02` | `GET` | `/api/v1/recycler-applications/{userId:guid}` | Staff / Admin | `GetApplicationDetail` | Xem chi tiết hồ sơ đăng ký của một cơ sở kèm ảnh chụp giấy phép và vị trí bản đồ (`S-04`). | Staff / Admin | Đã triển khai |
| **020** | `M-02` | `PUT` | `/api/v1/recycler-applications/{userId:guid}/verification` | Staff / Admin | `VerifyApplication` | Phê duyệt (`Verified` – nâng cấp vai trò thành `Recycler`) hoặc Từ chối (`Rejected` kèm lý do bắt buộc) (`S-04`). | Staff / Admin | Đã triển khai |

---

### PHÂN HỆ 03 (`M-03`): PHÂN LOẠI RÁC AI, DANH MỤC CHUẨN & LUẬT HƯỚNG DẪN (AI WASTE CLASSIFICATION & RULE ENGINE)
* **Tài liệu đặc tả chi tiết:** [`03_module_ai_waste_classification_api.md`](./03_module_ai_waste_classification_api.md)
* **Controllers:** `WasteScanController` (`7` APIs), `WasteCategoryController` (`4` APIs) | **Quy mô:** `11` APIs (11 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **021** | `M-03` | `POST` | `/api/v1/waste-scan/classify` | Authenticated | `ClassifyImage` | Gửi ảnh rác lên Backend gọi **YOLO26** (`yolo26_detect`, `yolo26s_cls`, `yolo26n_cls`, `mobilenet_v3`, `efficientnet_b0`), kiểm tra OOD `< 60%`, lưu vết `SHA-256` & trả về nhãn (10 nhóm) kèm hướng dẫn xử lý (`U-01`, `U-02`, `U-03`). | Authenticated (All) | Đã triển khai |
| **022** | `M-03` | `GET` | `/api/v1/waste-scan/models` | Public (`AllowAnonymous`) | `GetAvailableModels` | Lấy danh sách các mô hình thị giác máy tính đang sẵn sàng (`yolo26_detect`, `yolo26s_cls`, `yolo26n_cls`, `efficientnet_b0`, `mobilenet_v3`) và 10 lớp rác hỗ trợ (`U-01`). | Public / Guest | Đã triển khai |
| **023** | `M-03` | `GET` | `/api/v1/waste-scan/rules` | Public (`AllowAnonymous`) | `GetAllRules` | Tra cứu toàn bộ tập luật phân loại rác của Rule Engine (`ApiResponse<T>`; Admin có thể truyền `detailed=true&includeDraft=true`) (`U-03`, `A-06`). | Public / Guest | Đã triển khai |
| **024** | `M-03` | `POST` | `/api/v1/waste-scan/feedback` | Authenticated | `SubmitFeedback` | Người dùng gửi báo cáo phản hồi và gợi ý nhãn đúng khi AI nhận diện sai ảnh rác (`U-04`, `BR-CLS-05`). | Authenticated (All) | Đã triển khai |
| **025** | `M-03` | `GET` | `/api/v1/waste-categories` | Public (`AllowAnonymous`) | `GetAll` | Lấy danh mục loại phế liệu chuẩn (`Active` & `IsRecyclable=true` mặc định; truyền `includeNonRecyclable=true` hoặc Admin xem `includeInactive=true`) (`U-03`, `A-05`). | Public / Guest | Đã triển khai |
| **026** | `M-03` | `GET` | `/api/v1/waste-scan/history` | Authenticated | `GetMyScanHistory` | Người dân xem lại lịch sử các lần chụp/quét ảnh rác AI của cá nhân kèm hướng dẫn đã nhận (`U-01`, `U-03`, `BR-CLS-08`). | Citizen (User) | Đã triển khai |
| **027** | `M-03` | `POST` | `/api/v1/waste-categories` | Admin | `CreateCategory` | Quản trị viên thêm mới loại phế liệu chuẩn vào danh mục gốc hệ thống (`MasterWasteCategory`) (`A-05`, `BR-SRT-02`). | Staff / Admin | Đã triển khai |
| **028** | `M-03` | `PUT` | `/api/v1/waste-categories/{categoryId:guid}` | Admin | `UpdateCategory` | Quản trị viên cập nhật tên, mô tả, biểu tượng, cờ tái chế hoặc màu thùng chuẩn của loại phế liệu (`A-05`, `BR-SRT-02`). | Staff / Admin | Đã triển khai |
| **029** | `M-03` | `PATCH` | `/api/v1/waste-categories/{categoryId:guid}/status` | Admin | `ToggleCategoryStatus` | Vô hiệu hóa mềm (`Inactive`) hoặc mở lại (`Active`) danh mục phế liệu mà vẫn bảo toàn biên nhận lịch sử (`A-05`, `BR-SRT-04`). | Staff / Admin | Đã triển khai |
| **030** | `M-03` | `PUT` | `/api/v1/waste-scan/rules/{categoryId:guid}` | Admin | `UpsertSortingRule` | Cấu hình nội dung chính sách hướng dẫn phân loại, sơ chế sạch và cảnh báo an toàn theo từng loại rác (`A-06`, `BR-SRT-01`). | Staff / Admin | Đã triển khai |
| **031** | `M-03` | `PATCH` | `/api/v1/waste-scan/rules/{ruleId:guid}/publish` | Admin | `PublishSortingRule` | Phát hành phiên bản luật phân loại mới (`ACTIVE`), làm mới bộ nhớ đệm Rule Engine & đồng bộ sang AI Service (`A-06`, `BR-SRT-03`). | Staff / Admin | Đã triển khai |

---

### PHÂN HỆ 04 (`M-04`): HỒ SƠ CƠ SỞ, BẢNG GIÁ & LỊCH HOẠT ĐỘNG (RECYCLER PROFILE, PRICING & AVAILABILITY)
* **Tài liệu đặc tả chi tiết:** [`04_module_recycler_profile_pricing_availability_api.md`](./04_module_recycler_profile_pricing_availability_api.md)
* **Controller:** `RecyclerController` | **Base Route:** `/api/v1/recycler` | **Quy mô:** `9` APIs (9 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **032** | `M-04` | `GET` | `/api/v1/recycler/profile` | Recycler | `GetProfile` | Cơ sở thu gom xem đầy đủ hồ sơ vận hành của vựa: tên, địa chỉ, tọa độ GPS, giờ mở cửa, hotline, ảnh (`R-01`). | Recycler (Collector) | Đã triển khai |
| **033** | `M-04` | `PUT` | `/api/v1/recycler/profile` | Recycler | `UpdateProfile` | Cập nhật thông tin vận hành của cơ sở: tên vựa, số điện thoại liên hệ, tọa độ GPS, ảnh biển hiệu (`R-01`). | Recycler (Collector) | Đã triển khai |
| **034** | `M-04` | `GET` | `/api/v1/recycler/prices` | Recycler | `GetPrices` | Xem toàn bộ bảng giá thu mua phế liệu hiện đang cấu hình của cơ sở (`pricePerKg`, `minQuantityKg`) (`R-01`). | Recycler (Collector) | Đã triển khai |
| **035** | `M-04` | `POST` | `/api/v1/recycler/prices` | Recycler | `AddPrice` | Thêm đơn giá thu mua mới cho một danh mục phế liệu tái chế chuẩn (chặn nhóm không thương mại như `battery`, `biological`, `trash`, `clothes`, `shoes`) (`R-01`). | Recycler (Collector) | Đã triển khai |
| **036** | `M-04` | `PUT` | `/api/v1/recycler/prices/{priceId:guid}` | Recycler | `UpdatePrice` | Chỉnh sửa đơn giá thu mua (VNĐ/kg), khối lượng tối thiểu hoặc tạm dừng thu mua mặt hàng (`R-01`). | Recycler (Collector) | Đã triển khai |
| **037** | `M-04` | `PUT` | `/api/v1/recycler/prices` | Recycler | `BulkSavePrices` | Lưu đồng loạt toàn bộ bảng giá thu mua nhiều loại phế liệu thương mại trong một giao dịch nguyên tử (`R-01`). | Recycler (Collector) | Đã triển khai |
| **038** | `M-04` | `GET` | `/api/v1/recycler/availability` | Recycler | `GetAvailability` | Xem trạng thái nhận đơn tức thời (`Online` / `Offline`), lịch tuần và ghi chú lý do tạm nghỉ (`R-02`). | Recycler (Collector) | Đã triển khai |
| **039** | `M-04` | `PATCH` | `/api/v1/recycler/availability` | Recycler | `UpdateAvailability` | Bật/tắt trạng thái nhận đơn tức thời trên bản đồ tìm kiếm (`Online` ↔ `Offline`) (`R-02`). | Recycler (Collector) | Đã triển khai |
| **040** | `M-04` | `PUT` | `/api/v1/recycler/availability/schedule` | Recycler | `UpdateSchedule` | Thiết lập lịch mở cửa định kỳ từ Thứ 2 đến Chủ Nhật cho cơ sở thu gom (`R-02`). | Recycler (Collector) | Đã triển khai |

---

### PHÂN HỆ 05 (`M-05`): KHÁM PHÁ & TÌM KIẾM ĐIỂM THU GOM THEO TỌA ĐỘ (LOCATION-BASED RECYCLER DISCOVERY)
* **Tài liệu đặc tả chi tiết:** [`05_module_location_based_recycler_discovery_api.md`](./05_module_location_based_recycler_discovery_api.md)
* **Controllers:** `RecyclersDiscoveryController` (`2`), `GeocodingController` (`1`), `TradeController` (`1`) | **Quy mô:** `4` APIs (4 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **041** | `M-05` | `GET` | `/api/v1/recyclers/nearby` | Public (Guest) | `SearchNearby` | Tìm kiếm các cơ sở thu gom quanh tọa độ GPS trong bán kính R (km) bằng truy vấn không gian PostGIS, lọc loại rác (`U-05`). | Public / Guest | Đã triển khai |
| **042** | `M-05` | `GET` | `/api/v1/recyclers/{recyclerId:guid}` | Public (Guest) | `GetRecyclerDetail` | Xem trang hồ sơ công khai của cơ sở: khoảng cách GPS, bảng giá niêm yết, huy hiệu uy tín, giờ mở cửa (`U-05`). | Public / Guest | Đã triển khai |
| **043** | `M-05` | `GET` | `/api/v1/geocoding/search` | Public (Guest) | `SearchPlaces` | Gợi ý địa chỉ tự động và chuyển đổi tên đường/địa danh sang tọa độ Latitude/Longitude (`U-05`, `U-06`). | Public / Guest | Đã triển khai |
| **044** | `M-05` | `GET` | `/api/v1/trade/partners/nearby` | Public (Guest) | `GetNearbyPartners` | Tìm nhanh danh sách đối tác thu mua xung quanh phục vụ luồng khởi tạo đơn giao dịch trực tiếp (`U-05`). | Public / Guest | Đã triển khai |

---

### PHÂN HỆ 06 (`M-06`): QUẢN LÝ YÊU CẦU BÀN GIAO PHẾ LIỆU (HANDOVER REQUEST MANAGEMENT)
* **Tài liệu đặc tả chi tiết:** [`06_module_handover_request_management_api.md`](./06_module_handover_request_management_api.md)
* **Controllers:** `HandoverRequestController` (`5`), `RecyclerHandoverController` (`5`) | **Quy mô:** `10` APIs (10 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **045** | `M-06` | `POST` | `/api/v1/handover-requests` | Citizen (User) | `Create` | Người dân tạo yêu cầu hẹn bàn giao phế liệu (`HND-yyyyMMdd-NNNN`): chọn vựa, địa chỉ, loại rác ước tính, khung giờ (`[+2h .. +30d]`) (`U-06`). | Citizen (User) | Đã triển khai |
| **046** | `M-06` | `GET` | `/api/v1/handover-requests` | Citizen (User) | `GetMine` | Người dân tra cứu danh sách các đơn yêu cầu bàn giao của mình theo trạng thái (`Pending`, `Accepted`, `Completed`...) (`U-06`). | Citizen (User) | Đã triển khai |
| **047** | `M-06` | `GET` | `/api/v1/handover-requests/{id:guid}` | Citizen (User) | `GetById` | Người dân xem chi tiết tiến độ đơn hẹn thu gom, mặt hàng ước tính và khung giờ đã chốt (`U-06`). | Citizen (User) | Đã triển khai |
| **048** | `M-06` | `PUT` | `/api/v1/handover-requests/{id:guid}` | Citizen (User) | `Update` | Người dân cập nhật ghi chú hoặc điều chỉnh khung giờ đề xuất khi đơn đang ở trạng thái chờ nhận (`U-06`). | Citizen (User) | Đã triển khai |
| **049** | `M-06` | `POST` | `/api/v1/handover-requests/{id:guid}/cancel` | Citizen (User) | `Cancel` | Người dân hủy yêu cầu hẹn bàn giao kèm lý do (kiểm tra quy tắc cảnh báo hủy sát giờ hẹn `<= 4h`) (`U-07`). | Citizen (User) | Đã triển khai |
| **050** | `M-06` | `GET` | `/api/v1/recycler/handover-requests` | Recycler | `GetIncoming` | Cơ sở thu gom xem danh sách các yêu cầu bàn giao gửi đến cơ sở của mình theo trạng thái và từ khóa (`R-03`). | Recycler (Collector) | Đã triển khai |
| **051** | `M-06` | `GET` | `/api/v1/recycler/handover-requests/{id:guid}` | Recycler | `GetById` | Cơ sở xem chi tiết đơn bàn giao: ảnh chụp phế liệu, khối lượng ước tính và địa chỉ (chỉ lộ SĐT/địa chỉ sau `Accepted`, không lộ GPS) (`R-03`). | Recycler (Collector) | Đã triển khai |
| **052** | `M-06` | `POST` | `/api/v1/recycler/handover-requests/{id:guid}/accept` | Recycler | `Accept` | Cơ sở chấp nhận đơn bàn giao, chốt lịch hẹn chính thức và mở quyền xem thông tin liên lạc giữa hai bên (`R-03`). | Recycler (Collector) | Đã triển khai |
| **053** | `M-06` | `POST` | `/api/v1/recycler/handover-requests/{id:guid}/reject` | Recycler | `Reject` | Cơ sở từ chối yêu cầu bàn giao đang chờ (do quá tải, ngoài phạm vi hoặc không thu mua mặt hàng) (`R-03`). | Recycler (Collector) | Đã triển khai |
| **054** | `M-06` | `POST` | `/api/v1/recycler/handover-requests/{id:guid}/cancel` | Recycler | `Cancel` | Cơ sở hủy đơn đã nhận trong trường hợp bất khả kháng kèm lý do bắt buộc (`R-04`). | Recycler (Collector) | Đã triển khai |

---

### PHÂN HỆ 07 (`M-07`): BIÊN NHẬN SỐ & LỊCH SỬ GIAO DỊCH (HANDOVER COMPLETION, RECEIPT & HISTORY)
* **Tài liệu đặc tả chi tiết:** [`07_module_handover_completion_and_receipt_api.md`](./07_module_handover_completion_and_receipt_api.md)
* **Controllers:** `RecyclerReceiptController` (`4`), `RecyclerTransactionController` (`1`), `HandoverReceiptController` (`3`), `TransactionHistoryController` (`1`) | **Quy mô:** `9` APIs (9 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **055** | `M-07` | `POST` | `/api/v1/recycler/handover-requests/{requestId:guid}/receipt/upload-proof` | Recycler | `UploadProof` | Cơ sở tải ảnh bằng chứng cân phế liệu (`.jpg/.png/.webp`, tối đa 10 MB) lên Cloudflare R2 (`receipt-proofs/{requestId}`) trước khi lập biên nhận (`R-06`, `BR-HND-04`). | Recycler (Collector) | Đã triển khai |
| **056** | `M-07` | `POST` | `/api/v1/recycler/handover-requests/{requestId:guid}/receipt/preview` | Recycler | `Preview` | Cơ sở xem trước biên nhận: tính tổng tiền theo đơn giá đã chốt và cảnh báo nếu khối lượng cân lệch > 20% (`R-06`). | Recycler (Collector) | Đã triển khai |
| **057** | `M-07` | `POST` | `/api/v1/recycler/handover-requests/{requestId:guid}/receipt` | Recycler | `Create` | Cơ sở phát hành biên nhận điện tử (`RCP-yyyy-NNNN`), lưu khối lượng cân thực tế kèm 1–5 ảnh mặt đồng hồ cân (`R-06`). | Recycler (Collector) | Đã triển khai |
| **058** | `M-07` | `GET` | `/api/v1/recycler/handover-requests/{requestId:guid}/receipt` | Recycler | `Get` | Cơ sở xem lại chi tiết biên nhận điện tử đã phát hành cho đơn thu gom (`R-06`). | Recycler (Collector) | Đã triển khai |
| **059** | `M-07` | `GET` | `/api/v1/recycler/transactions` | Recycler | `GetHistory` | Cơ sở tra cứu lịch sử toàn bộ giao dịch thu mua đã hoàn tất, tổng sản lượng và tổng chi phí (`R-06`, `U-12`). | Recycler (Collector) | Đã triển khai |
| **060** | `M-07` | `GET` | `/api/v1/handover-requests/{requestId:guid}/receipt` | Citizen (User) | `Get` | Người dân kiểm tra biên nhận điện tử do vựa gửi: khối lượng thực tế, đơn giá, thành tiền và ảnh bằng chứng cân (`U-09`). | Citizen (User) | Đã triển khai |
| **061** | `M-07` | `POST` | `/api/v1/handover-requests/{requestId:guid}/receipt/confirm` | Citizen (User) | `Confirm` | Người dân xác nhận đã nhận đủ tiền, chốt trạng thái `Completed` nguyên tử, đồng bộ `TradeOrder` và cộng điểm Mầm Xanh (`U-09`). | Citizen (User) | Đã triển khai |
| **062** | `M-07` | `POST` | `/api/v1/handover-requests/{requestId:guid}/receipt/dispute` | Citizen (User) | `Dispute` | Người dân báo sai lệch biên nhận kèm bằng chứng, chuyển đơn sang trạng thái tranh chấp `Disputed` chờ Staff phân xử (`U-09`, `U-11`). | Citizen (User) | Đã triển khai |
| **063** | `M-07` | `GET` | `/api/v1/transactions` | Citizen (User) | `GetHistory` | Người dân xem lịch sử bán phế liệu cá nhân: tổng tiền thu được, khối lượng tái chế và điểm thưởng tích lũy (`U-12`). | Citizen (User) | Đã triển khai |

---

### PHÂN HỆ 08 (`M-08`): CHAT THƯƠNG LƯỢNG THỜI GIAN THỰC (IN-APP REAL-TIME NEGOTIATION CHATBOX)
* **Tài liệu đặc tả chi tiết:** [`08_module_in_app_realtime_negotiation_chatbox_api.md`](./08_module_in_app_realtime_negotiation_chatbox_api.md)
* **Controller:** `NegotiationChatController` (`7` REST) | **Hub:** `NegotiationChatHub` (`/hubs/negotiation-chat`, `1` WS) | **Quy mô:** `8` APIs (8 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **064** | `M-08` | `GET` | `/api/v1/trade/orders/{orderId:guid}/messages` | Citizen / Recycler / Staff / Admin | `GetChatThread` | Lấy luồng hội thoại của đơn: trạng thái thương lượng, cờ `isInquiryOnly`, đề xuất hiện hành và danh sách tin nhắn phân trang (`U-08`, `R-05`, `BR-DSP-06`). | Authenticated (All) | Đã triển khai |
| **065** | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/messages` | Citizen / Recycler | `SendMessage` | Gửi tin nhắn văn bản (`Text`) hoặc hình ảnh (`Image`) vào phòng thương lượng của đơn bàn giao (`U-08`, `R-05`). | Authenticated (All) | Đã triển khai |
| **066** | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/messages/attachments` | Citizen / Recycler | `UploadChatAttachment` | Tải ảnh chụp thực tế phế liệu lên Cloudflare R2 để đính kèm trực tiếp vào phòng chat (`U-08`, `R-05`). | Authenticated (All) | Đã triển khai |
| **067** | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/proposals` | Citizen / Recycler | `SendProposal` | Gửi thẻ đề xuất / phản đề xuất (`Proposal`): đơn giá thương lượng (VNĐ/kg), khối lượng ước tính và khung giờ hẹn (`[today .. +30d]`) (`U-08`, `R-05`). | Authenticated (All) | Đã triển khai |
| **068** | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/proposals/{messageId:guid}/respond` | Citizen / Recycler | `RespondToProposal` | Đối phương chấp nhận (`Accepted` – cập nhật điều khoản đơn & đồng bộ `HandoverRequest`) hoặc từ chối (`Rejected`) thẻ đề xuất giá (`U-08`, `R-05`). | Authenticated (All) | Đã triển khai |
| **069** | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/accept-terms` | Recycler | `AcceptOrderInitialTerms` | Cơ sở thu gom chốt chấp nhận ngay điều khoản ban đầu của Người dân mà không cần phản đề xuất (`R-05`). | Recycler (Collector) | Đã triển khai |
| **070** | `M-08` | `PUT` | `/api/v1/trade/orders/{orderId:guid}/messages/read` | Citizen / Recycler | `MarkMessagesAsRead` | Đánh dấu toàn bộ tin nhắn chưa đọc của đối phương trong phòng thương lượng là đã xem (`U-08`, `R-05`). | Authenticated (All) | Đã triển khai |
| **071** | `M-08` | `WS` | `/hubs/negotiation-chat` | Authenticated (`[Authorize]`) | `NegotiationChatHub` | Kênh WebSocket SignalR song công: `JoinOrderChatRoom`, `LeaveOrderChatRoom`, `SendTypingIndicator`, nhận `ReceiveChatMessage` / `ProposalCreated` / `ProposalResponded` / `OrderChatUpdated` (`U-08`, `R-05`). | Authenticated (All) | Đã triển khai |

---

### PHÂN HỆ 09 (`M-09`): ĐƠN GIAO DỊCH TRỰC TIẾP & XÁC NHẬN QR (DIRECT TRADE ORDERS & QR CONFIRMATION)
* **Tài liệu đặc tả chi tiết:** [`08_module_in_app_realtime_negotiation_chatbox_api.md`](./08_module_in_app_realtime_negotiation_chatbox_api.md)
* **Controller:** `TradeController` | **Base Route:** `/api/v1/trade` | **Quy mô:** `5` APIs (5 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **072** | `M-09` | `GET` | `/api/v1/trade/orders` | Authenticated | `GetOrders` | Tra cứu danh sách đơn giao dịch & luồng chat hỏi giá (`isInquiryOnly`), trả kèm `lastMessagePreview`, `lastMessageSenderId`, `lastMessageSenderRole`, `lastMessageAt`, `unreadCount` (`U-06`, `R-03`). | Authenticated (All) | Đã triển khai |
| **073** | `M-09` | `POST` | `/api/v1/trade/orders` | Citizen (User) | `CreateOrder` | Khởi tạo đơn giao dịch hoặc tạo/tái sử dụng đơn nháp hỏi giá (`isInquiryDraft=true` → `isInquiryOnly=true`), đồng bộ `HandoverRequest` & kiểm tra hạn 30 ngày (`U-06`, `U-08`). | Citizen (User) | Đã triển khai |
| **074** | `M-09` | `PATCH` | `/api/v1/trade/orders/{orderId:guid}/status` | Authenticated | `UpdateOrderStatus` | Cập nhật trạng thái luồng xử lý đơn giao dịch (`Accepted`, `Rejected`, `Cancelled`, `Scheduled`, `Completed`) và phát tín hiệu `OrderChatUpdated`. | Authenticated (All) | Đã triển khai |
| **075** | `M-09` | `PATCH` | `/api/v1/trade/orders/{orderId:guid}/time-slot` | Authenticated | `UpdateOrderTimeSlot` | Thay đổi khung thời gian hẹn bàn giao (`[today .. +30d]`) giữa Người dân và Cơ sở thu gom (`U-06`, `R-03`). | Authenticated (All) | Đã triển khai |
| **076** | `M-09` | `POST` | `/api/v1/trade/receipts/confirm-qr` | Authenticated | `ConfirmReceipt` | Quét mã QR bảo mật trên biên nhận điện tử để xác nhận hoàn tất giao dịch trực tiếp tại điểm cân (`U-09`, `R-06`). | Authenticated (All) | Đã triển khai |

---

### PHÂN HỆ 10 (`M-10` / `FE-13`): GAMIFICATION, CÂY ẢO & ĐỔI QUÀ THƯỞNG (GAMIFICATION & REWARDS MODULE)
* **Tài liệu đặc tả chi tiết:** [`13_module_gamification_and_rewards_api.md`](./13_module_gamification_and_rewards_api.md)
* **Use Cases bao phủ:** `U-13` (Daily Check-in), `U-14` (Redeem Points for Gifts), `U-15` (Virtual Tree Care & Upgrade), `S-09` (Manage Eco Fact Content), `S-10` (Manage Gift Inventory), `A-07` (Manage Gamification Config), `A-08` (Configure Leaderboard Cycles)
* **Controllers:** `GamificationController` (`3`), `UserGamificationController` (`8`), `PointsAndRewardsController` (`8`), `StaffGamificationController` (`17`), `AdminGamificationController` (`8`)
* **Quy mô:** `44` Canonical APIs (`63` Route Bindings gồm `19` aliases tương thích ngược) — `44` Đã triển khai, `0` Dự kiến

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **077** | `M-10` | `GET` | `/api/v1/gamification/profile/me` | Authenticated | `GetMyProfile` | Xem hồ sơ Sống Xanh cá nhân của tài khoản đang đăng nhập từ dữ liệu thật (`U-13`, `U-15`). | Authenticated (All) | Đã triển khai |
| **078** | `M-10` | `GET` | `/api/v1/gamification/profile/{userId:guid}` | Authenticated | `GetProfile` | Xem hồ sơ Sống Xanh của một người dùng theo `userId` (`user_point_wallets`, `virtual_tree_progress`, CO2 giảm thải) (`U-13`, `U-15`). | Authenticated (All) | Đã triển khai |
| **079** | `M-10` | `GET` | `/api/v1/gamification/leaderboard` | Public (`AllowAnonymous`) | `GetLeaderboard` | Xem Bảng xếp hạng đóng góp tái chế dẫn đầu theo chu kỳ Tuần/Tháng/Quý đang bật hoặc chỉ định (`A-08`, `BR-GAM-07`). | Public / Guest | Đã triển khai |
| **080** | `M-10` | `GET` | `/api/v1/gamification/check-in/status` *(Alias: `GET /api/v1/check-in`)* | Citizen / Recycler | `CheckinStatus` | Kiểm tra trạng thái điểm danh trong ngày (`ICT UTC+7`), lịch 7 ngày, chuỗi Streak và cảnh báo reset (`U-13`, `BR-GAM-02`). | Authenticated (All) | Đã triển khai |
| **081** | `M-10` | `POST` | `/api/v1/gamification/check-in` *(Alias: `POST /api/v1/check-in`)* | Citizen / Recycler | `CheckIn` | Thực hiện điểm danh hàng ngày để duy trì chuỗi Streak và nhận điểm Mầm Xanh (kiểm tra trần điểm phi giao dịch `BR-GAM-02`) (`U-13`). | Authenticated (All) | Đã triển khai |
| **082** | `M-10` | `GET` | `/api/v1/gamification/tree/me` *(Alias: `GET /api/v1/virtual-tree`)* | Citizen (User) | `Tree` | Xem chi tiết Cây Ảo cá nhân: cấp độ, độ ẩm (`hydrationPercent`), `isWilted`, `careExp`, túi phân bón (`fertilizerPacks`) và chi phí nâng cấp (`U-15`). | Citizen (User) | Đã triển khai |
| **083** | `M-10` | `POST` | `/api/v1/gamification/tree/care` *(Alias: `POST /api/v1/virtual-tree/care`)* | Citizen (User) | `Care` | Chăm sóc cây hằng ngày (`water` hồi sinh héo / `+35%` ẩm hoặc `fertilize` tại độ ẩm `>= 30%`, `x1.4 EXP` trong vùng `60–90%`, dùng túi phân hữu cơ) (`U-15`, `BR-GAM-06`). | Citizen (User) | Đã triển khai |
| **084** | `M-10` | `POST` | `/api/v1/gamification/tree/harvest` *(Alias: `POST /api/v1/virtual-tree/harvest`)* | Citizen (User) | `Harvest` | Thu hoạch quả chín khi `careExp` đầy (chỉ khi cây không héo), cộng Điểm Xanh vào ví dưới trần `BR-GAM-02` (`U-15`, `BR-GAM-06`). | Citizen (User) | Đã triển khai |
| **085** | `M-10` | `POST` | `/api/v1/gamification/tree/upgrade` *(Alias: `POST /api/v1/virtual-tree/water`)* | Citizen (User) | `Water` | Tiêu Điểm Xanh nâng cấp Cây Ảo lên cấp kế tiếp một chiều và tự động mở khóa thẻ kiến thức Eco Fact (`U-15`, `BR-GAM-06`, `BR-GAM-08`). | Citizen (User) | Đã triển khai |
| **086** | `M-10` | `GET` | `/api/v1/gamification/eco-facts/me` *(Alias: `GET /api/v1/eco-facts`)* | Citizen (User) | `Facts` | Xem bộ sưu tập thẻ kiến thức môi trường (`Eco Facts`) đã mở khóa theo cấp Cây Ảo (hỗ trợ `includeLocked=true`) (`U-15`). | Citizen (User) | Đã triển khai |
| **087** | `M-10` | `GET` | `/api/v1/gamification/eco-facts/{id:guid}` *(Alias: `GET /api/v1/eco-facts/{id:guid}`)* | Citizen (User) | `Fact` | Xem chi tiết một thẻ kiến thức `Eco Fact` đã mở khóa theo `id` (`U-15`, `BR-GAM-08`). | Citizen (User) | Đã triển khai |
| **088** | `M-10` | `GET` | `/api/v1/gamification/points/wallet` *(Alias: `GET /api/v1/points/wallet`)* | Citizen / Recycler | `Wallet` | Xem số dư ví điểm Mầm Xanh hiện tại, tổng điểm đã kiếm và tổng điểm đã chi tiêu (`U-14`). | Authenticated (All) | Đã triển khai |
| **089** | `M-10` | `GET` | `/api/v1/gamification/points/ledger` *(Alias: `GET /api/v1/points/transactions`)* | Citizen / Recycler | `Transactions` | Tra cứu sổ cái lịch sử biến động điểm Mầm Xanh (`point_transactions`), lọc theo ngày và `actionType` (`U-13`, `U-14`, `U-15`). | Authenticated (All) | Đã triển khai |
| **090** | `M-10` | `GET` | `/api/v1/gamification/gifts` *(Alias: `GET /api/v1/gifts`)* | Citizen / Recycler | `Gifts` | Duyệt kho quà tặng đang mở đổi thưởng (`Physical` / `Voucher`), sắp xếp và tìm kiếm (`U-14`). | Authenticated (All) | Đã triển khai |
| **091** | `M-10` | `GET` | `/api/v1/gamification/gifts/{id:guid}` *(Alias: `GET /api/v1/gifts/{id:guid}`)* | Citizen / Recycler | `Gift` | Xem thông tin chi tiết một vật phẩm quà tặng trong cửa hàng đổi thưởng (`U-14`). | Authenticated (All) | Đã triển khai |
| **092** | `M-10` | `POST` | `/api/v1/gifts/{id:guid}/redeem` | Citizen / Recycler | `Redeem` | Đổi quà trực tiếp qua `giftId` trên route (trừ tồn kho + trừ ví điểm nguyên tử trong `1` transaction `BR-GAM-03/04`) (`U-14`). | Authenticated (All) | Đã triển khai |
| **093** | `M-10` | `POST` | `/api/v1/gamification/redemptions` | Citizen / Recycler | `RedeemFromBody` | Đổi điểm Mầm Xanh lấy E-Voucher hoặc quà hiện vật nhận `giftId` từ request body (`U-14`, `BR-GAM-03/04`). | Authenticated (All) | Đã triển khai |
| **094** | `M-10` | `GET` | `/api/v1/gamification/redemptions/me` *(Alias: `GET /api/v1/gift-redemptions`)* | Citizen / Recycler | `MyRedemptions` | Xem lịch sử đơn đổi quà cá nhân, tra cứu mã E-Voucher đã cấp (`ECO-...`) hoặc tiến độ giao nhận (`U-14`). | Authenticated (All) | Đã triển khai |
| **095** | `M-10` | `GET` | `/api/v1/gamification/redemptions/{id:guid}` *(Alias: `GET /api/v1/gift-redemptions/{id:guid}`)* | Citizen / Recycler | `MyRedemption` | Xem chi tiết một đơn đổi quà cá nhân theo `id` (`U-14`). | Authenticated (All) | Đã triển khai |
| **096** | `M-10` | `POST` | `/api/v1/staff/gifts/upload-image` | Staff [`MANAGE_INVENTORY`] / Admin | `UploadGiftImage` | Nhân viên tải ảnh minh họa quà tặng (`.jpg/.png/.webp`, tối đa 5 MB) lên Cloudflare R2 (`gamification-gifts`) (`S-10`). | Staff / Admin | Đã triển khai |
| **097** | `M-10` | `GET` | `/api/v1/staff/gifts` | Staff [`MANAGE_INVENTORY`] / Admin | `Gifts` | Nhân viên quản lý danh sách toàn bộ kho quà tặng, lọc theo `type`/`status`/`search` (`S-10`). | Staff / Admin | Đã triển khai |
| **098** | `M-10` | `GET` | `/api/v1/staff/gifts/{id:guid}` | Staff [`MANAGE_INVENTORY`] / Admin | `Gift` | Nhân viên xem chi tiết thông tin và tồn kho một vật phẩm quà tặng trong kho (`S-10`). | Staff / Admin | Đã triển khai |
| **099** | `M-10` | `POST` | `/api/v1/staff/gifts` | Staff [`MANAGE_INVENTORY`] / Admin | `CreateGift` | Nhân viên thêm mới vật phẩm đổi thưởng (`Physical` / `Voucher`), cấu hình điểm quy đổi và tồn kho ban đầu (`S-10`). | Staff / Admin | Đã triển khai |
| **100** | `M-10` | `PUT` | `/api/v1/staff/gifts/{id:guid}` | Staff [`MANAGE_INVENTORY`] / Admin | `UpdateGift` | Nhân viên cập nhật thông tin mô tả quà tặng, ảnh minh họa hoặc điều chỉnh mức điểm quy đổi (`S-10`). | Staff / Admin | Đã triển khai |
| **101** | `M-10` | `POST` | `/api/v1/staff/gifts/{id:guid}/restock` | Staff [`MANAGE_INVENTORY`] / Admin | `Restock` | Nhân viên nhập bổ sung số lượng tồn kho cho vật phẩm quà tặng (`quantity > 0`) (`S-10`). | Staff / Admin | Đã triển khai |
| **102** | `M-10` | `POST` | `/api/v1/staff/gifts/{id:guid}/vouchers/import` | Staff [`MANAGE_INVENTORY`] / Admin | `ImportVouchers` | Nhập hàng loạt (khử trùng lặp danh sách mã `codes` hoặc bổ sung `quantity`) vào kho E-Voucher (`S-10`). | Staff / Admin | Đã triển khai |
| **103** | `M-10` | `PATCH` | `/api/v1/staff/gifts/{id:guid}/status` *(Alias: `POST /api/v1/staff/gifts/{id:guid}/status`)* | Staff [`MANAGE_INVENTORY`] / Admin | `GiftStatus` | Bật mở đổi thưởng (`ACTIVE`) hoặc tạm ẩn vật phẩm quà tặng (`INACTIVE`) khi hết hàng / hết hạn (`S-10`). | Staff / Admin | Đã triển khai |
| **104** | `M-10` | `GET` | `/api/v1/staff/gifts/{id:guid}/redemptions` | Staff [`MANAGE_INVENTORY`] / Admin | `GiftRedemptions` | Nhân viên xem lịch sử đơn đổi của một quà tặng cụ thể theo `id` (`S-10`). | Staff / Admin | Đã triển khai |
| **105** | `M-10` | `GET` | `/api/v1/staff/redemptions` | Staff [`MANAGE_INVENTORY`] / Admin | `AllRedemptions` | Nhân viên tra cứu danh sách toàn bộ đơn đổi quà của người dùng chờ đóng gói bàn giao (`S-10`). | Staff / Admin | Đã triển khai |
| **106** | `M-10` | `PATCH` | `/api/v1/staff/redemptions/{id:guid}/fulfill` *(Aliases: `POST /api/v1/staff/redemptions/{id:guid}/fulfill`, `POST /api/v1/staff/gift-redemptions/{id:guid}/fulfill`)* | Staff [`MANAGE_INVENTORY`] / Admin | `Fulfill` | Nhân viên xác nhận hoàn tất giao quà (`Fulfill` -> `Delivered`) hoặc hủy đơn (`Cancel` -> `Cancelled`) kèm hoàn trả điểm + tồn kho (`S-10`). | Staff / Admin | Đã triển khai |
| **107** | `M-10` | `GET` | `/api/v1/staff/eco-facts` | Staff [`MANAGE_INVENTORY`] / Admin | `Facts` | Nhân viên tra cứu danh sách toàn bộ thẻ kiến thức môi trường gắn với các mốc cấp độ Cây Ảo (`S-09`). | Staff / Admin | Đã triển khai |
| **108** | `M-10` | `GET` | `/api/v1/staff/eco-facts/{id:guid}` | Staff [`MANAGE_INVENTORY`] / Admin | `Fact` | Nhân viên xem chi tiết nội dung một thẻ `Eco Fact` theo `id` (`S-09`). | Staff / Admin | Đã triển khai |
| **109** | `M-10` | `POST` | `/api/v1/staff/eco-facts` | Staff [`MANAGE_INVENTORY`] / Admin | `CreateFact` | Nhân viên biên soạn thẻ Eco Fact mới, gán mốc cấp độ Cây Ảo mở khóa và đính kèm hình minh họa (`S-09`). | Staff / Admin | Đã triển khai |
| **110** | `M-10` | `PUT` | `/api/v1/staff/eco-facts/{id:guid}` | Staff [`MANAGE_INVENTORY`] / Admin | `UpdateFact` | Nhân viên chỉnh sửa nội dung thông điệp xanh, điều chỉnh mốc cấp độ cây hoặc nguồn trích dẫn của Eco Fact (`S-09`). | Staff / Admin | Đã triển khai |
| **111** | `M-10` | `PATCH` | `/api/v1/staff/eco-facts/{id:guid}/status` *(Alias: `POST /api/v1/staff/eco-facts/{id:guid}/status`)* | Staff [`MANAGE_INVENTORY`] / Admin | `FactStatus` | Bật/tắt trạng thái hiển thị `ACTIVE` / `INACTIVE` cho thẻ `Eco Fact` (`S-09`). | Staff / Admin | Đã triển khai |
| **112** | `M-10` | `DELETE` | `/api/v1/staff/eco-facts/{id:guid}` | Staff [`MANAGE_INVENTORY`] / Admin | `DeleteFact` | Xóa thẻ Eco Fact nháp hoặc tự động vô hiệu hóa mềm (`INACTIVE`) nếu đã có người dùng đạt cấp mở khóa (`S-09`, `BR-GAM-06`). | Staff / Admin | Đã triển khai |
| **113** | `M-10` | `GET` | `/api/v1/admin/gamification/config` | Admin | `Get` | Quản trị viên xem cấu hình quy tắc Gamification: hệ số điểm/kg rác, trần điểm ngày, hệ số Streak, ngưỡng cấp Cây Ảo (`A-07`). | Staff / Admin | Đã triển khai |
| **114** | `M-10` | `PUT` | `/api/v1/admin/gamification/config` | Admin | `Update` | Quản trị viên cập nhật tỷ lệ quy đổi điểm, giới hạn điểm/ngày và bảng ngưỡng điểm nâng cấp Cây Ảo ghi `AuditLog` (`A-07`, `BR-ADM-05`). | Staff / Admin | Đã triển khai |
| **115** | `M-10` | `POST` | `/api/v1/admin/gamification/config/reset` | Admin | `Reset` | Quản trị viên khôi phục toàn bộ tham số cấu hình Gamification về giá trị mặc định chuẩn hệ thống (`A-07`, `BR-ADM-05`). | Staff / Admin | Đã triển khai |
| **116** | `M-10` | `GET` | `/api/v1/admin/gamification/leaderboard-cycles` | Admin | `GetCycles` | Quản trị viên xem danh sách các chu kỳ thi đua Bảng xếp hạng (Tuần/Tháng/Quý) và cơ cấu giải thưởng Top 1/2/3 (`A-08`). | Staff / Admin | Đã triển khai |
| **117** | `M-10` | `POST` | `/api/v1/admin/gamification/leaderboard-cycles` | Admin | `CreateCycle` | Quản trị viên khởi tạo chu kỳ Bảng xếp hạng mới, định nghĩa khung thời gian và mức thưởng điểm cho Top 3 (`A-08`, `BR-GAM-07`). | Staff / Admin | Đã triển khai |
| **118** | `M-10` | `PUT` | `/api/v1/admin/gamification/leaderboard-cycles/{cycleId:guid}` | Admin | `UpdateCycle` | Quản trị viên cập nhật tên, thời gian, cơ cấu thưởng của một chu kỳ Bảng xếp hạng chưa chốt (`A-08`, `BR-ADM-05`). | Staff / Admin | Đã triển khai |
| **119** | `M-10` | `PATCH` | `/api/v1/admin/gamification/leaderboard-cycles/{cycleId:guid}/activate` *(Alias: `POST /api/v1/admin/gamification/leaderboard-cycles/{cycleId:guid}/activate`)* | Admin | `ActivateCycle` | Chuyển chu kỳ Bảng xếp hạng đang kích hoạt (chỉ duy nhất 1 chu kỳ bật tại một thời điểm) (`A-08`, `BR-GAM-07`). | Staff / Admin | Đã triển khai |
| **120** | `M-10` | `POST` | `/api/v1/admin/gamification/leaderboard-cycles/{cycleId:guid}/finalize` | Admin | `FinalizeCycle` | Chốt bảng tổng sắp cuối kỳ thi đua (`Finalized`) và tự động cộng điểm thưởng vinh danh cho Top 3 kèm ghi `AuditLog` (`A-08`, `BR-GAM-07`). | Staff / Admin | Đã triển khai |

---

### PHÂN HỆ 11 (`M-11` / `FE-10`): ĐÁNH GIÁ UY TÍN & GIẢI QUYẾT KHIẾU NẠI (RATING, REVIEW & COMPLAINT RESOLUTION)
* **Tài liệu đặc tả chi tiết:** [`12_module_rating_review_complaint_resolution_api.md`](./12_module_rating_review_complaint_resolution_api.md)
* **Use Cases bao phủ:** `U-10` (Rate Recycler), `U-11` (Submit Complaint), `S-05` (Handle Complaints)
* **Controllers:** `ReviewsController` (`5`), `ComplaintsController` (`3`), `StaffComplaintsController` (`4`)
* **Quy mô:** `12` APIs (12 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **121** | `M-11` | `POST` | `/api/v1/handover-requests/{requestId:guid}/reviews` | Citizen (User) | `CreateReview` | Người dân gửi đánh giá 1–5 sao và nhận xét cho cơ sở thu gom sau khi hoàn tất bàn giao (1 đánh giá duy nhất/đơn trong 14 ngày theo `BR-RVW-01`) (`U-10`). | Citizen (User) | Đã triển khai |
| **122** | `M-11` | `GET` | `/api/v1/recyclers/{recyclerId:guid}/reviews` | Public (`AllowAnonymous`) | `GetRecyclerReviews` | Xem danh sách nhận xét công khai, thống kê phân bố 1–5 sao và điểm uy tín trung bình của cơ sở thu gom (`U-10`). | Public / Guest | Đã triển khai |
| **123** | `M-11` | `GET` | `/api/v1/reviews/me` | Authenticated (Citizen) | `GetMyReviews` | Người dân tra cứu danh sách các đánh giá chất lượng đã gửi sau các lần giao dịch bán phế liệu (`U-10`). | Citizen (User) | Đã triển khai |
| **124** | `M-11` | `DELETE` | `/api/v1/reviews/{reviewId:guid}` | Authenticated (Author) | `DeleteMyReview` | Tác giả tự xóa vĩnh viễn bài đánh giá của mình và tính lại điểm sao trung bình của vựa (không cho phép sửa sao theo `BR-RVW-02`, `BR-RVW-03`) (`U-10`). | Citizen (User) | Đã triển khai |
| **125** | `M-11` | `POST` | `/api/v1/reviews/{reviewId:guid}/report` | Authenticated | `ReportReview` | Báo cáo bài đánh giá chứa nội dung xúc phạm, bôi nhọ hoặc spam để Nhân viên vận hành kiểm duyệt (`U-10`, `BR-RVW-03`). | Authenticated (All) | Đã triển khai |
| **126** | `M-11` | `PATCH` | `/api/v1/staff/reviews/{reviewId:guid}/moderate` | Staff [`HANDLE_COMPLAINT`] / Admin | `ModerateReview` | Nhân viên `[HANDLE_COMPLAINT]` / Admin ẩn hoặc khôi phục đánh giá vi phạm quy tắc cộng đồng kèm lý do & ghi `AuditLog` (`U-10`, `BR-RVW-03`). | Staff / Admin | Đã triển khai |
| **127** | `M-11` | `POST` | `/api/v1/complaints/upload-evidence` | Authenticated | `UploadEvidence` | Tải ảnh chụp mặt cân, ảnh hiện trường hoặc biên lai thanh toán lên Cloudflare R2 (`complaint-evidences`) làm bằng chứng khiếu nại (`U-11`, `BR-DSP-02`). | Authenticated (All) | Đã triển khai |
| **128** | `M-11` | `POST` | `/api/v1/complaints` | Authenticated | `SubmitComplaint` | Người dân hoặc Cơ sở gửi phiếu khiếu nại 2 chiều chính thức (sai lệch cân, thanh toán, bỏ hẹn) trong thời hạn 7 ngày (`BR-DSP-01`) (`U-11`). | Authenticated (All) | Đã triển khai |
| **129** | `M-11` | `GET` | `/api/v1/complaints/me` | Authenticated | `GetMyComplaints` | Tra cứu danh sách các phiếu khiếu nại đã gửi hoặc bị khiếu nại (cô lập cứng 2 bên) cùng kết quả phân xử của Staff (`U-11`, `BR-DSP-06`). | Authenticated (All) | Đã triển khai |
| **130** | `M-11` | `GET` | `/api/v1/staff/complaints` | Staff [`HANDLE_COMPLAINT`] / Admin | `GetComplaintQueue` | Nhân viên `[HANDLE_COMPLAINT]` xem hàng đợi phiếu khiếu nại (`Open`, `Investigating`, `Resolved`, `Dismissed`) kèm đánh giá bị báo cáo (`S-05`). | Staff / Admin | Đã triển khai |
| **131** | `M-11` | `GET` | `/api/v1/staff/complaints/{complaintId:guid}` | Staff [`HANDLE_COMPLAINT`] / Admin | `GetComplaintDossier` | Nhân viên mở Hồ sơ Trọng tài 360°: đối chiếu ảnh bằng chứng hai bên, biên nhận cân điện tử và toàn bộ lịch sử chat bất biến (`S-05`, `BR-DSP-06`). | Staff / Admin | Đã triển khai |
| **132** | `M-11` | `PATCH` | `/api/v1/staff/complaints/{complaintId:guid}/resolve` | Staff [`HANDLE_COMPLAINT`] / Admin | `ResolveComplaint` | Nhân viên ghi nhận phán quyết trọng tài chính thức, thu hồi điểm `GreenPoints` (`>= 0`, `BR-GAM-01/03`) và cảnh cáo `AccountModerationLog` (`S-05`, `BR-DSP-04`). | Staff / Admin | Đã triển khai |

---

### PHÂN HỆ 12 (`M-12` / `FE-14`): GÓI DỊCH VỤ NỘI BỘ & TÍCH HỢP VIETQR (IN-SYSTEM SERVICE PACKAGES & VIETQR)
* **Tài liệu đặc tả chi tiết:** [`10_module_in_system_service_packages_vietqr_api.md`](./10_module_in_system_service_packages_vietqr_api.md)
* **Use Cases bao phủ:** `U-16` (Citizen Purchase In-System Services via VietQR), `R-07` (Recycler Purchase In-System Services via VietQR), `A-10` (Configure In-System Service Fees), `A-11` (Manage Financial Reconciliation)
* **Controllers:** `ServicePaymentController` (`6`), `ServicePackagesController` (`6`), `AdminReconciliationController` (`3`)
* **Quy mô:** `15` APIs (15 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **133** | `M-12` | `POST` | `/api/v1/service-payments/create` | Citizen / Recycler / Admin | `CreateServicePayment` | Khởi tạo đơn mua gói dịch vụ nội bộ (`PREMIUM` 59k / `PRO` 1tr) và sinh mã VietQR động (kiểm tra 4 tiêu chí PRO, vai trò DB & chặn mua trùng/hạ bậc) (`BR-PAY-01`, `BR-PAY-03`). | Authenticated (All) | Đã triển khai |
| **134** | `M-12` | `GET` | `/api/v1/service-payments/orders/{orderCode}` | Authenticated | `GetOrderStatus` | Tra cứu trạng thái thanh toán thời gian thực của đơn mua gói dịch vụ (`Pending`, `Paid`, `Expired`, `Cancelled`, `Refunded`) (`U-16`, `R-07`). | Authenticated (All) | Đã triển khai |
| **135** | `M-12` | `GET` | `/api/v1/service-payments/orders` | Authenticated | `GetAllOrders` | Xem danh sách hóa đơn thanh toán gói dịch vụ nội bộ của chính tài khoản đăng nhập (`U-16`, `R-07`). | Authenticated (All) | Đã triển khai |
| **136** | `M-12` | `POST` | `/api/v1/service-payments/orders/{orderCode}/cancel` | Citizen / Recycler / Admin | `CancelPendingOrder` | Hủy mã đơn thanh toán VietQR đang chờ chuyển khoản (`Pending`) khi người dùng đổi ý chọn gói dịch vụ khác (`U-16`, `R-07`). | Authenticated (All) | Đã triển khai |
| **137** | `M-12` | `POST` | `/api/v1/service-payments/webhook` | Public (`AllowAnonymous`) | `ReceivePaymentWebhook` | Webhook tự động nhận thông báo biến động số dư từ cổng VietQR / PayOS, kiểm tra Idempotency, phân loại 5 bất thường và kích hoạt gói (`BR-PAY-02`, `BR-PAY-06`). | Public / Guest | Đã triển khai |
| **138** | `M-12` | `POST` | `/api/v1/service-payments/simulate-transfer/{orderCode}` | Public (`AllowAnonymous`, Dev/Test) | `SimulateTransfer` | Giả lập sự kiện khách hàng quét mã VietQR chuyển khoản (`Exact`, `Underpaid`, `Overpaid`, `LateTransfer`, `Unmatched`) phục vụ kiểm thử. | Public / Guest | Đã triển khai |
| **139** | `M-12` | `GET` | `/api/v1/service-packages` | Authenticated | `GetAvailablePackages` | Lấy bảng giá gói dịch vụ khả dụng theo vai trò DB (`effectiveRole`, ẩn `PRO` với `Citizen`), kèm `proEligibility` 4 tiêu chí & `isIncludedInHigherPlan` (`U-16`, `R-07`). | Authenticated (All) | Đã triển khai |
| **140** | `M-12` | `GET` | `/api/v1/service-packages/subscriptions/me` | Authenticated | `GetMySubscriptions` | Xem bậc gói hiện tại, các đặc quyền đang hoạt động, thời hạn còn lại và lịch sử gia hạn cộng dồn (`U-16`, `R-07`, `BR-PAY-04`). | Authenticated (All) | Đã triển khai |
| **141** | `M-12` | `GET` | `/api/v1/admin/service-packages` | Admin | `GetAllServicePackages` | Quản trị viên tra cứu toàn bộ danh mục gói dịch vụ thương mại, biểu phí, thời hạn và số lượng thuê bao đang đăng ký (`A-10`). | Staff / Admin | Đã triển khai |
| **142** | `M-12` | `POST` | `/api/v1/admin/service-packages` | Admin | `CreateServicePackage` | Quản trị viên tạo gói dịch vụ mới: tên gói, vai trò áp dụng, mức phí (`>= 2000` VNĐ), số ngày hiệu lực và danh sách đặc quyền (`A-10`, `BR-PAY-05`). | Staff / Admin | Đã triển khai |
| **143** | `M-12` | `PUT` | `/api/v1/admin/service-packages/{packageId:guid}` | Admin | `UpdateServicePackage` | Quản trị viên cập nhật đơn giá và chu kỳ gói dịch vụ cho các lượt đăng ký mới (giữ nguyên gói đang hiệu lực theo `BR-PAY-05`) (`A-10`). | Staff / Admin | Đã triển khai |
| **144** | `M-12` | `PATCH` | `/api/v1/admin/service-packages/{packageId:guid}/status` | Admin | `TogglePackageStatus` | Mở bán công khai hoặc ngừng cung cấp gói dịch vụ trên giao diện nâng cấp tài khoản (`A-10`). | Staff / Admin | Đã triển khai |
| **145** | `M-12` | `GET` | `/api/v1/admin/reconciliations/ledger` | Admin | `GetPaymentLedger` | Quản trị viên theo dõi sổ cái dòng tiền VietQR vào hệ thống, lọc các giao dịch lệch tiền hoặc sai nội dung chuyển khoản (`Anomaly`) (`A-11`, `BR-PAY-06`). | Staff / Admin | Đã triển khai |
| **146** | `M-12` | `PATCH` | `/api/v1/admin/reconciliations/orders/{orderCode}/resolve` | Admin | `ResolvePaymentAnomaly` | Đối soát thủ công giao dịch VietQR bất thường: xác nhận khớp lệnh bù hoặc ghi nhận hoàn tiền kèm mã giao dịch ngân hàng (`A-11`, `BR-PAY-06`). | Staff / Admin | Đã triển khai |
| **147** | `M-12` | `GET` | `/api/v1/admin/reconciliations/export` | Admin | `ExportReconciliationReport` | Xuất bảng kê đối soát doanh thu dịch vụ VietQR theo kỳ kế toán (CSV/XLSX) ẩn danh PII (`A-11`, `BR-ADM-06`). | Staff / Admin | Đã triển khai |

---

### PHÂN HỆ 13 (`M-13` / `FE-15`): PHẢN HỒI LỖI AI & QUẢN TRỊ BỘ DỮ LIỆU HUẤN LUYỆN (AI MISCLASSIFICATION FEEDBACK & DATASET CURATION)
* **Tài liệu đặc tả chi tiết:** [`11_module_ai_misclassification_feedback_dataset_curation_api.md`](./11_module_ai_misclassification_feedback_dataset_curation_api.md)
* **Use Cases bao phủ:** `U-04` (Report Incorrect AI Classification – phối hợp cùng endpoint `STT 021, 024` ở `M-03`), `S-01` (Review Misclassified Images), `S-02` (Label Image Data), `S-03` (Update Verified Dataset)
* **Controllers:** `WasteScanController` (`1`), `StaffAiFeedbackController` (`6`), `StaffDatasetCurationController` (`3`)
* **Quy mô:** `10` APIs tại Core .NET (10 Đã triển khai, 0 Dự kiến; phối hợp cùng `2` API nội bộ `STT 216–217` tại `AIS`)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **148** | `M-13` | `GET` | `/api/v1/waste-scan/feedback/me` | Authenticated | `GetMyFeedbackReports` | Người dân xem lại lịch sử các báo cáo nhận diện sai AI đã đóng góp và kết quả xác minh của đội ngũ vận hành (`U-04`, `BR-CLS-08`). | Citizen (User) | Đã triển khai |
| **149** | `M-13` | `GET` | `/api/v1/staff/ai-feedback` | Staff [`REVIEW_AI`] / Admin | `GetFeedbackQueue` | Nhân viên xem hàng đợi ảnh rác bị báo nhận diện nhầm (lọc theo nhãn dự đoán sai, nhãn người dùng đề xuất, độ tin cậy, tìm kiếm) (`S-01`). | Staff / Admin | Đã triển khai |
| **150** | `M-13` | `GET` | `/api/v1/staff/ai-feedback/{reportId:guid}` | Staff [`REVIEW_AI`] / Admin | `GetFeedbackDetail` | Xem chi tiết mẫu ảnh nhận diện lỗi: ảnh gốc trên R2, phân phối xác suất & bounding boxes lúc quét, nhãn đề xuất và người báo cáo (`S-01`). | Staff / Admin | Đã triển khai |
| **151** | `M-13` | `PATCH` | `/api/v1/staff/ai-feedback/{reportId:guid}/review` | Staff [`REVIEW_AI`] / Admin | `ReviewFeedback` | Nhân viên thẩm định ảnh báo lỗi (`ApproveAndLabel` 1 chạm, `ApproveToQueue` hoặc `Reject` ảnh mờ kèm lý do bắt buộc) (`S-01`, `BR-CLS-06`). | Staff / Admin | Đã triển khai |
| **152** | `M-13` | `GET` | `/api/v1/staff/ai-labeling/queue` | Staff [`REVIEW_AI`] / Admin | `GetLabelingQueue` | Lấy danh sách ảnh thô (`LowConfidenceAuto` < 60%) và ảnh báo lỗi đã duyệt đang chờ Nhân viên gán nhãn thực tế chuẩn (10 lớp) (`S-02`). | Staff / Admin | Đã triển khai |
| **153** | `M-13` | `PUT` | `/api/v1/staff/ai-labeling/{itemId:guid}` | Staff [`REVIEW_AI`] / Admin | `AssignGroundTruthLabel` | Nhân viên gán nhãn danh mục phế liệu chuẩn hóa (10 nhãn YOLO) và tọa độ hộp bao YOLO chuẩn hóa `[0, 1]` (`S-02`, `BR-CLS-06`). | Staff / Admin | Đã triển khai |
| **154** | `M-13` | `POST` | `/api/v1/staff/ai-labeling/batch-assign` | Staff [`REVIEW_AI`] / Admin | `BatchAssignLabels` | Gán nhãn chuẩn hàng loạt nguyên tử cho tập ảnh đồng nhất trong 1 giao dịch CSDL (`S-02`). | Staff / Admin | Đã triển khai |
| **155** | `M-13` | `GET` | `/api/v1/staff/ai-datasets` | Staff [`CURATE_DATASET`] / Admin | `GetDatasetDashboard` | Xem thống kê kho dữ liệu sạch theo từng lớp, chỉ số cân bằng (`ImbalanceRatio`), độ chính xác thực nghiệm và lịch sử phiên bản (`S-03`). | Staff / Admin | Đã triển khai |
| **156** | `M-13` | `POST` | `/api/v1/staff/ai-datasets/versions` | Staff [`CURATE_DATASET`] / Admin | `CreateDatasetVersion` | Đóng gói phiên bản Dataset mới theo thuật toán **Sticky Stratified Split `70/20/10`** chống rò rỉ theo `SHA-256` (`BR-CLS-07`) (`S-03`). | Staff / Admin | Đã triển khai |
| **157** | `M-13` | `GET` | `/api/v1/staff/ai-datasets/versions/{versionId:guid}/export` | Staff [`CURATE_DATASET`] / Admin | `ExportDatasetVersion` | Tải xuống gói dữ liệu chuẩn hóa `.zip` (`yolo`: `data.yaml` + `labels/*.txt` + `manifest.json`; hoặc `imagefolder`) kèm `MSG-ADM-02` (`S-03`). | Staff / Admin | Đã triển khai |

---

### PHÂN HỆ 14 (`M-14` / `FE-16`): QUẢN TRỊ BÀI VIẾT BLOG & NỘI DUNG TRUYỀN THÔNG (BLOG & CONTENT MANAGEMENT)
* **Tài liệu đặc tả chi tiết:** [`14_module_blog_and_content_management_api.md`](./14_module_blog_and_content_management_api.md)
* **Use Cases bao phủ:** `G-01` (Browse Public Eco Blogs), `S-08` (Create Blog Post), `A-09` (Approve / Reject Blog Post)
* **Controllers:** `PublicBlogController` (`2`), `StaffBlogController` (`8`), `AdminBlogController` (`5`)
* **Quy mô:** `15` Canonical APIs (`30` Route Bindings gồm `15` aliases `/api/v1/.../blog-posts*`) — `15` Đã triển khai, `0` Dự kiến

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **158** | `M-14` | `GET` | `/api/v1/blogs` *(Alias: `GET /api/v1/blog-posts`)* | Public (`AllowAnonymous`) | `List` | Người dân và khách vãng lai xem danh sách bài viết chuyên đề kinh tế tuần hoàn & phân loại rác đã xuất bản, lọc theo từ khóa/tag (`G-01`). | Public / Guest | Đã triển khai |
| **159** | `M-14` | `GET` | `/api/v1/blogs/{slug}` *(Alias: `GET /api/v1/blog-posts/{slug}`)* | Public (`AllowAnonymous`) | `GetBySlug` | Xem nội dung chi tiết bài viết Blog đã xuất bản theo đường dẫn thân thiện (`slug`), ẩn thông tin kiểm duyệt nội bộ (`G-01`). | Public / Guest | Đã triển khai |
| **160** | `M-14` | `POST` | `/api/v1/staff/blogs/upload-media` *(Alias: `POST /api/v1/staff/blog-posts/upload-media`)* | Staff [`MANAGE_BLOG`] / Admin | `UploadMedia` | Nhân viên tải ảnh bìa (`coverImageUrl`) và ảnh minh họa nội dung bài viết (`<= 5MB`, `.jpg/.png/.webp`) lên Cloudflare R2 (`S-08`). | Staff / Admin | Đã triển khai |
| **161** | `M-14` | `GET` | `/api/v1/staff/blogs` *(Alias: `GET /api/v1/staff/blog-posts`)* | Staff [`MANAGE_BLOG`] / Admin | `List` | Nhân viên quản lý danh sách bài viết của chính mình theo trạng thái biên tập (`Draft`, `Pending_Approval`, `Approved`/`Published`) (`S-08`). | Staff / Admin | Đã triển khai |
| **162** | `M-14` | `GET` | `/api/v1/staff/blogs/{id:guid}` *(Alias: `GET /api/v1/staff/blog-posts/{id:guid}`)* | Staff [`MANAGE_BLOG`] / Admin | `GetById` | Nhân viên xem chi tiết toàn bộ nội dung, ảnh bìa, thẻ tag và lý do trả về (nếu có) của bài viết do mình soạn (`S-08`). | Staff / Admin | Đã triển khai |
| **163** | `M-14` | `POST` | `/api/v1/staff/blogs` *(Alias: `POST /api/v1/staff/blog-posts`)* | Staff [`MANAGE_BLOG`] / Admin | `Create` | Nhân viên soạn thảo bài viết mới bằng trình biên tập Rich-Text/Markdown, lưu nháp (`Draft`) hoặc nộp duyệt ngay (`Pending_Approval`) (`S-08`). | Staff / Admin | Đã triển khai |
| **164** | `M-14` | `PUT` | `/api/v1/staff/blogs/{id:guid}` *(Alias: `PUT /api/v1/staff/blog-posts/{id:guid}`)* | Staff [`MANAGE_BLOG`] / Admin | `Update` | Nhân viên chỉnh sửa nội dung, ảnh bìa hoặc thẻ tag bài viết đang ở trạng thái bản nháp (`Draft` / bài bị trả về) (`S-08`). | Staff / Admin | Đã triển khai |
| **165** | `M-14` | `POST` | `/api/v1/staff/blogs/{id:guid}/submit` *(Alias: `POST /api/v1/staff/blog-posts/{id:guid}/submit`)* | Staff [`MANAGE_BLOG`] / Admin | `Submit` | Nhân viên gửi bài viết hoàn chỉnh lên hàng đợi biên tập (`Draft -> Pending_Approval`, `MSG-ADM-06`) kèm thông báo `Admin` (`S-08`, `BR-ADM-08`). | Staff / Admin | Đã triển khai |
| **166** | `M-14` | `POST` | `/api/v1/staff/blogs/{id:guid}/recall` *(Alias: `POST /api/v1/staff/blog-posts/{id:guid}/recall`)* | Staff [`MANAGE_BLOG`] / Admin | `Recall` | Nhân viên thu hồi bài viết đang chờ duyệt (`Pending_Approval -> Draft`) để chỉnh sửa lại trước khi Admin xử lý (`S-08`, `A-09`). | Staff / Admin | Đã triển khai |
| **167** | `M-14` | `DELETE` | `/api/v1/staff/blogs/{id:guid}` *(Alias: `DELETE /api/v1/staff/blog-posts/{id:guid}`)* | Staff [`MANAGE_BLOG`] / Admin | `Delete` | Nhân viên xóa bài viết bản nháp (`Draft`) chưa gửi duyệt hoặc chưa xuất bản khỏi hệ thống (`S-08`). | Staff / Admin | Đã triển khai |
| **168** | `M-14` | `GET` | `/api/v1/admin/blogs/pending` *(Alias: `GET /api/v1/admin/blog-posts/queue`)* | Admin | `Queue` | Quản trị viên xem hàng đợi các bài viết Blog do Nhân viên gửi lên đang chờ xét duyệt xuất bản (`A-09`). | Staff / Admin | Đã triển khai |
| **169** | `M-14` | `GET` | `/api/v1/admin/blogs/{id:guid}` *(Alias: `GET /api/v1/admin/blog-posts/{id:guid}`)* | Admin | `GetById` | Quản trị viên xem đầy đủ nội dung, ảnh bìa, thẻ tag của bài viết đang chờ kiểm duyệt (`A-09`). | Staff / Admin | Đã triển khai |
| **170** | `M-14` | `PATCH` | `/api/v1/admin/blogs/{id:guid}/review` *(Alias: `PATCH /api/v1/admin/blog-posts/{id:guid}/review`)* | Admin | `Review` | Quản trị viên phê duyệt hoặc từ chối bài viết trên một endpoint thống nhất (`action = Approve` hoặc `Reject`) kèm ghi `AuditLog` (`A-09`). | Staff / Admin | Đã triển khai |
| **171** | `M-14` | `POST` | `/api/v1/admin/blogs/{id:guid}/approve` *(Alias: `POST /api/v1/admin/blog-posts/{id:guid}/approve`)* | Admin | `Approve` | Quản trị viên phê duyệt đăng công khai (`Pending_Approval -> Approved`, `MSG-ADM-05`), thông báo tác giả & ghi `AuditLog` (`A-09`). | Staff / Admin | Đã triển khai |
| **172** | `M-14` | `POST` | `/api/v1/admin/blogs/{id:guid}/reject` *(Alias: `POST /api/v1/admin/blog-posts/{id:guid}/reject`)* | Admin | `Reject` | Quản trị viên từ chối bài viết (`Pending_Approval -> Draft`) kèm góp ý bắt buộc, thông báo Staff & ghi `AuditLog` (`A-09`). | Staff / Admin | Đã triển khai |

---

### PHÂN HỆ 15 (`M-15` / `FE-17`, `FE-18`): QUẢN TRỊ TÀI KHOẢN, NHÂN SỰ, CHÍNH SÁCH, THÔNG BÁO & PHÂN TÍCH HỆ THỐNG (GOVERNANCE, STAFF, POLICIES, NOTIFICATIONS & ANALYTICS)
* **Tài liệu đặc tả chi tiết:** [`09_module_account_staff_system_governance_analytics_api.md`](./09_module_account_staff_system_governance_analytics_api.md)
* **Use Cases bao phủ:** `A-01` (View Dashboard & Analytics), `A-02` (Export Data Reports & Audit Log), `S-06` (View User & Recycler Accounts), `S-07` (Lock / Delete Accounts), `A-03` (Manage Staff Accounts), `A-04` (Manage Staff Permissions), `S-11` (Staff Daily Check-in), `SU-04` (System In-App Notifications), Quản trị Điều khoản & Chính sách (`/terms`)
* **Controllers & Hubs:** `AdminAnalyticsController` (`4`), `StaffAccountsController` (`4`), `AdminStaffController` (`7`), `StaffAttendanceController` (`4`), `NotificationsController` (`3`), `NotificationHub` (`1` WS), `PlatformPoliciesController` (`6`)
* **Quy mô:** `29` APIs = `28` REST + `1` WS Hub (29 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **173** | `M-15` | `GET` | `/api/v1/admin/analytics/kpi-summary` | Admin | `GetExecutiveKpiSummary` | Hiển thị bảng chỉ số KPI điều hành: tổng sản lượng phế liệu tái chế (kg/tấn), độ chính xác mô hình AI, người dùng tích cực và doanh thu VietQR (`A-01`, `BR-ADM-07`). | Staff / Admin | Đã triển khai |
| **174** | `M-15` | `GET` | `/api/v1/admin/analytics/charts` | Admin | `GetAnalyticsCharts` | Truy vấn dữ liệu biểu đồ thống kê theo kỳ: tỷ trọng từng nhóm phế liệu, xu hướng giao dịch khu vực và tỷ lệ lỗi nhận diện AI (`A-01`, `BR-ADM-07`). | Staff / Admin | Đã triển khai |
| **175** | `M-15` | `GET` | `/api/v1/admin/reports/export` | Admin | `ExportSystemReport` | Trích xuất báo cáo tổng hợp giao dịch, sản lượng tái chế và vận hành ra tệp CSV/XLSX (ẩn dữ liệu nhạy cảm theo `BR-ADM-06`) (`A-02`). | Staff / Admin | Đã triển khai |
| **176** | `M-15` | `GET` | `/api/v1/admin/audit-logs` | Admin | `GetSystemAuditLogs` | Tra cứu nhật ký kiểm toán hệ thống bất biến (`Audit Trail` chỉ đọc theo `BR-ADM-05`): lịch sử đổi cấu hình, phân quyền, khóa tài khoản, đối soát (`A-02`). | Staff / Admin | Đã triển khai |
| **177** | `M-15` | `GET` | `/api/v1/staff/accounts` | Staff [`MANAGE_ACCOUNTS`] / Admin | `GetAccounts` | Nhân viên xem danh sách tài khoản Người dân và Cơ sở thu gom (lọc vai trò, trạng thái hoạt động, số lần cảnh cáo vi phạm) (`S-06`). | Staff / Admin | Đã triển khai |
| **178** | `M-15` | `GET` | `/api/v1/staff/accounts/{userId:guid}` | Staff [`MANAGE_ACCOUNTS`] / Admin | `GetAccountDetail` | Xem chi tiết hồ sơ tài khoản, thống kê hoạt động giao dịch, lịch sử khiếu nại và nhật ký bị cảnh cáo/khóa trước đó (`S-06`). | Staff / Admin | Đã triển khai |
| **179** | `M-15` | `PATCH` | `/api/v1/staff/accounts/{userId:guid}/lock` | Staff [`MANAGE_ACCOUNTS`] / Admin | `LockOrUnlockAccount` | Tạm khóa (`Suspended`) hoặc mở khóa tài khoản vi phạm chính sách kèm lý do bắt buộc và thu hồi ngay phiên đăng nhập hiện hành (`S-07`, `BR-ADM-03`). | Staff / Admin | Đã triển khai |
| **180** | `M-15` | `DELETE` | `/api/v1/staff/accounts/{userId:guid}` | Staff [`MANAGE_ACCOUNTS`] / Admin | `DeactivateAccount` | Vô hiệu hóa mềm (`Soft-Deactivate`) tài khoản vi phạm nghiêm trọng kèm biên bản lý do, giữ nguyên vẹn lịch sử biên nhận cũ (`S-07`, `BR-ADM-04`). | Staff / Admin | Đã triển khai |
| **181** | `M-15` | `GET` | `/api/v1/admin/staff` | Admin | `GetStaffAccounts` | Quản trị viên tra cứu danh sách tài khoản Nhân viên vận hành (`Staff`), trạng thái công tác và danh sách quyền hiện có (`A-03`). | Staff / Admin | Đã triển khai |
| **182** | `M-15` | `POST` | `/api/v1/admin/staff` | Admin | `CreateStaffAccount` | Quản trị viên tạo mới tài khoản Nhân viên vận hành nội bộ (chỉ Admin được phép tạo theo quy tắc bảo mật `BR-ADM-01`) (`A-03`). | Staff / Admin | Đã triển khai |
| **183** | `M-15` | `PUT` | `/api/v1/admin/staff/{staffId:guid}` | Admin | `UpdateStaffAccount` | Quản trị viên cập nhật thông tin nhân sự, bộ phận chuyên trách hoặc cấp lại mật khẩu tạm cho Nhân viên (`A-03`). | Staff / Admin | Đã triển khai |
| **184** | `M-15` | `PATCH` | `/api/v1/admin/staff/{staffId:guid}/status` | Admin | `UpdateStaffStatus` | Đình chỉ hoặc ngừng kích hoạt tài khoản Nhân viên chuyển công tác/nghỉ việc và hủy toàn bộ phiên truy cập (`A-03`, `BR-ADM-03`). | Staff / Admin | Đã triển khai |
| **185** | `M-15` | `GET` | `/api/v1/admin/permissions/catalog` | Admin | `GetPermissionCatalog` | Lấy danh mục chuẩn toàn bộ mã quyền RBAC nghiệp vụ (`VERIFY_RECYCLER`, `HANDLE_COMPLAINT`, `CURATE_DATASET`, `MANAGE_INVENTORY`, `MANAGE_BLOG`...) (`A-04`). | Staff / Admin | Đã triển khai |
| **186** | `M-15` | `GET` | `/api/v1/admin/staff/{staffId:guid}/permissions` | Admin | `GetStaffPermissions` | Xem chi tiết tập quyền RBAC hiện đang được gán cho một tài khoản Nhân viên cụ thể (`A-04`). | Staff / Admin | Đã triển khai |
| **187** | `M-15` | `PUT` | `/api/v1/admin/staff/{staffId:guid}/permissions` | Admin | `UpdateStaffPermissions` | Cấu hình cấp mới hoặc thu hồi các mã quyền RBAC cho Nhân viên và áp dụng kiểm soát truy cập tức thời (`A-04`, `BR-ADM-02`). | Staff / Admin | Đã triển khai |
| **188** | `M-15` | `POST` | `/api/v1/staff/attendance/check-in` | Staff / Admin | `CheckInShift` | Nhân viên chấm công bắt đầu ca làm việc hàng ngày để ghi nhận chuyên cần (không cộng ví điểm công dân theo `BR-GAM-08`/`BR-GAM-09`) (`S-11`). | Staff / Admin | Đã triển khai |
| **189** | `M-15` | `POST` | `/api/v1/staff/attendance/check-out` | Staff / Admin | `CheckOutShift` | Nhân viên chấm công kết thúc ca trực vận hành và lưu ghi chú bàn giao ca (`S-11`). | Staff / Admin | Đã triển khai |
| **190** | `M-15` | `GET` | `/api/v1/staff/attendance/me` | Staff / Admin | `GetMyAttendanceLogs` | Nhân viên tra cứu bảng chấm công cá nhân và lịch sử các ca trực trong tháng (`S-11`). | Staff / Admin | Đã triển khai |
| **191** | `M-15` | `GET` | `/api/v1/admin/staff/attendance` | Admin | `GetAllStaffAttendance` | Quản trị viên theo dõi báo cáo chuyên cần và nhật ký ca trực hàng ngày của toàn bộ đội ngũ Nhân viên (`S-11`, `A-03`). | Staff / Admin | Đã triển khai |
| **192** | `M-15` | `GET` | `/api/v1/notifications` | Authenticated | `GetMyNotifications` | Lấy danh sách thông báo hệ thống của người dùng (đơn thu gom, biên nhận, phán quyết khiếu nại, duyệt hồ sơ, thanh toán VietQR) (`SU-04`). | Authenticated (All) | Đã triển khai |
| **193** | `M-15` | `PATCH` | `/api/v1/notifications/{notificationId:guid}/read` | Authenticated | `MarkNotificationRead` | Đánh dấu một thông báo cụ thể là đã đọc (`isRead = true`) (`SU-04`). | Authenticated (All) | Đã triển khai |
| **194** | `M-15` | `PUT` | `/api/v1/notifications/read-all` | Authenticated | `MarkAllNotificationsRead` | Đánh dấu toàn bộ thông báo chưa đọc của tài khoản hiện tại là đã xem (`SU-04`). | Authenticated (All) | Đã triển khai |
| **195** | `M-15` | `WS` | `/hubs/notifications` | Authenticated (`[Authorize]`) | `NotificationHub` | Kênh WebSocket SignalR đẩy thông báo thời gian thực (`ReceiveNotification`, `UnreadCountUpdated`) ngay khi phát sinh sự kiện nghiệp vụ (`SU-04`, `EI-07`). | Authenticated (All) | Đã triển khai |
| **196** | `M-15` | `GET` | `/api/v1/policies` | Public (`AllowAnonymous`) | `GetPublicPolicies` | Lấy danh sách điều khoản dịch vụ, chính sách môi trường & quyền riêng tư đang `ACTIVE` hiển thị công khai trên `/terms` (`MSG-POL-LIST-OK`). | Public / Guest | Đã triển khai |
| **197** | `M-15` | `GET` | `/api/v1/admin/policies` | Admin / Staff | `GetAdminPolicies` | Admin/Staff xem toàn bộ các mục điều khoản và chính sách (bao gồm `ACTIVE`, `DRAFT`, `INACTIVE`), lọc theo nhóm/trạng thái/từ khóa (`MSG-POL-LIST-OK`). | Staff / Admin | Đã triển khai |
| **198** | `M-15` | `POST` | `/api/v1/admin/policies` | Admin | `CreatePolicy` | Admin tạo mới mục điều khoản/chính sách nền tảng (`TERMS` / `ENVIRONMENT` / `PRIVACY`), tự sinh mã nếu trống & ghi `AuditLog` (`MSG-POL-CREATED`). | Staff / Admin | Đã triển khai |
| **199** | `M-15` | `PUT` | `/api/v1/admin/policies/{id:guid}` | Admin | `UpdatePolicy` | Admin cập nhật nội dung, nhóm, thứ tự hiển thị hoặc trạng thái mục điều khoản (`BR-POL-03`, `MSG-POL-UPDATED`). | Staff / Admin | Đã triển khai |
| **200** | `M-15` | `PATCH` | `/api/v1/admin/policies/{id:guid}/status` | Admin | `SetPolicyStatus` | Admin bật/tắt nhanh trạng thái `ACTIVE` / `DRAFT` / `INACTIVE` (bảo vệ tối thiểu 1 mục `ACTIVE`) (`BR-POL-03`, `MSG-POL-STATUS-UPDATED`). | Staff / Admin | Đã triển khai |
| **201** | `M-15` | `DELETE` | `/api/v1/admin/policies/{id:guid}` | Admin | `DeletePolicy` | Admin xóa mục điều khoản khỏi hệ thống (chặn xóa mục `ACTIVE` duy nhất còn lại) và ghi `AuditLog` (`MSG-POL-DELETED`). | Staff / Admin | Đã triển khai |

---

### HẠ TẦNG HỆ THỐNG & KIỂM TRA SỨC KHỎE (`SYS`: SYSTEM INFRASTRUCTURE & HEALTH)
* **Host:** `Ecolink.WebApi` | **Entry:** `Program.cs` | **Quy mô:** `1` API (1 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **202** | `SYS` | `GET` | `/health` | Public (All) | `Minimal API` | Kiểm tra trạng thái sống của máy chủ WebApi (.NET 10), phiên bản dịch vụ và thời gian UTC. | Public / Guest | Đã triển khai |

---

### DỊCH VỤ THỊ GIÁC MÁY TÍNH CHUYÊN BIỆT (`AIS`: DEDICATED AI VISION SERVICE – FASTAPI PYTHON)
* **Dịch vụ:** Dedicated AI Vision Microservice (`http://127.0.0.1:8000`) | **Tệp mã nguồn:** [`app.py`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.AiService/app.py)
* **Quy mô:** `16` REST APIs (`16` Đã triển khai, `0` Dự kiến — hỗ trợ đầy đủ **YOLO26 Two-Stage End-to-End** với 5 mô hình `yolo26_detect`, `yolo26s_cls`, `yolo26n_cls`, `efficientnet_b0`, `mobilenet_v3` & 10 nhóm rác chuẩn)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **203** | `AIS` | `GET` | `/` | Public (Internal) | `root` | Chuyển hướng về trang tài liệu OpenAPI Swagger (`/docs`) của Dedicated AI Microservice (`SYS`). | Public / Guest | Đã triển khai |
| **204** | `AIS` | `GET` | `/health` | Public (Internal) | `health_check` | Kiểm tra tình trạng sức khỏe dịch vụ AI, phần cứng CUDA/CPU, danh sách model sẵn sàng và ngưỡng OOD (`SYS`). | Public / Guest | Đã triển khai |
| **205** | `AIS` | `GET` | `/api/v1/models` | Public (Internal) | `list_models` | Danh sách 5 mô hình AI (`yolo26_detect`, `yolo26s_cls`, `yolo26n_cls`, `efficientnet_b0`, `mobilenet_v3`) và 10 lớp rác chuẩn EcoLink (`U-01`). | Public / Guest | Đã triển khai |
| **206** | `AIS` | `GET` | `/api/v1/models/{model_id}` | Public (Internal) | `get_model_details` | Lấy thông số kỹ thuật, cấu trúc mạng, file trọng số và chỉ số benchmark của mô hình cụ thể (hỗ trợ alias `yolov8_detect`, `yolov8n`) (`U-01`). | Public / Guest | Đã triển khai |
| **207** | `AIS` | `POST` | `/api/v1/classify` | Public (Internal) | `classify_waste_image` | Suy luận phân loại đa vật thể Two-Stage (`yolo26_detect` + `yolo26s_cls`/`yolo26n_cls`), kiểm tra ngưỡng OOD `0.60`, trả 10 xác suất & `sorting_rule` (`U-01`, `U-02`). | Public / Guest | Đã triển khai |
| **208** | `AIS` | `POST` | `/api/v1/detect` | Public (Internal) | `detect_waste_objects` | Định vị từng món rác bằng Object Detection (`yolo26_detect`), trả về tọa độ `bbox`, đếm số lượng theo nhóm và ảnh Base64 đã vẽ khung (`U-01`). | Public / Guest | Đã triển khai |
| **209** | `AIS` | `POST` | `/api/v1/classify/batch` | Public (Internal) | `classify_waste_batch` | Phân loại rác theo lô nhiều ảnh (`Batch Classification`, mặc định `yolo26n_cls`) để kiểm thử hiệu năng và độ chính xác (`U-01`). | Public / Guest | Đã triển khai |
| **210** | `AIS` | `GET` | `/api/v1/samples` | Public (Internal) | `get_sample_images_list` | Liệt kê danh sách ảnh mẫu trong tập validation theo từng danh mục rác kèm đường dẫn tải về phục vụ kiểm thử (`U-01`). | Public / Guest | Đã triển khai |
| **211** | `AIS` | `GET` | `/api/v1/samples/{category}/{filename}` | Public (Internal) | `download_sample_image` | Xem hoặc tải về file ảnh mẫu cụ thể theo `category` và `filename` để dùng trong Postman/Swagger (`U-01`). | Public / Guest | Đã triển khai |
| **212** | `AIS` | `GET` | `/api/v1/rules` | Public (Internal) | `get_all_rules` | Lấy toàn bộ cơ sở tri thức quy tắc phân loại rác tĩnh (`rules.json`) và `confidence_threshold` từ AI Service (`U-03`, `A-06`). | Public / Guest | Đã triển khai |
| **213** | `AIS` | `PUT` | `/api/v1/rules` | Public (Internal) | `update_rules` | Đồng bộ hoặc cập nhật ngưỡng tin cậy OOD và quy tắc hướng dẫn phân loại (Upsert theo danh mục) từ Core Backend sang AI Service (`A-06`). | Public / Guest | Đã triển khai |
| **214** | `AIS` | `POST` | `/api/v1/feedback` | Public (Internal) | `submit_misclassification_feedback` | Tiếp nhận mẫu ảnh và nhãn gợi ý khi mô hình nhận diện nhầm để lưu vào `feedback.json` chờ kiểm duyệt (`U-04`). | Public / Guest | Đã triển khai |
| **215** | `AIS` | `GET` | `/api/v1/feedback` | Public (Internal) | `get_all_feedback_logs` | Truy xuất danh sách các phản hồi nhận diện sai đang lưu trên AI Service (`S-01`). | Public / Guest | Đã triển khai |
| **216** | `AIS` | `POST` | `/api/v1/datasets/curate` | Public (Internal) | `curate_dataset_version` | Tiếp nhận phiên bản tập dữ liệu chuẩn hóa `Train/Val/Test` từ Core Backend, kiểm định chống rò rỉ `SHA-256` (`BR-CLS-07`) và ghi `manifest.json` (`S-03`). | Public / Guest | Đã triển khai |
| **217** | `AIS` | `POST` | `/api/v1/models/reload` | Public (Internal) | `reload_ai_models` | Nạp nóng (`Hot-reload`) trọng số mô hình AI phiên bản mới trong `models/` sau khi huấn luyện lại mà không gián đoạn dịch vụ (`S-03`). | Public / Guest | Đã triển khai |
| **218** | `AIS` | `GET` | `/api/v1/stats` | Public (Internal) | `get_inference_stats` | Thống kê tổng lượt quét, tỷ lệ OOD (`ood_rate_percent`), phân bổ danh mục rác, độ trễ suy luận trung bình và 25 lượt quét gần nhất (`A-01`). | Public / Guest | Đã triển khai |

---

## 3. HƯỚNG DẪN ĐỒNG BỘ GOOGLE SHEET (`Report_API List_SEP490_G141`) & DUY TRÌ TÀI LIỆU

### 3.1. Công thức tự động hóa cho Google Sheet trên Drive
Khi cập nhật tệp **`Report_API List_SEP490_G141`** gồm 2 sheet `Dashboard` và `API Inventory` (dữ liệu trong `API Inventory` bắt đầu từ dòng `5` đến dòng `222`, với các cột: `A = STT`, `B = Mã Modul`, `C = Phân hệ / Module`, `D = Phương thức`, `E = Route`, `F = Phân quyền`, `G = Action`, `H = Mô tả`, `I = Nhóm RBAC`, `J = Trạng thái`):

* **Ô Tổng Endpoint (`E2`):** `=COUNTA('API Inventory'!A5:A222)` → Kết quả: **`218`**
* **Ô Core .NET 10 (`F2`):** `=COUNTIF('API Inventory'!B5:B222, "<>AIS")` → Kết quả: **`202`**
* **Ô AI FastAPI (`G2`):** `=COUNTIF('API Inventory'!B5:B222, "AIS")` → Kết quả: **`16`**
* **Ô WebSocket (`H2`):** `=COUNTIF('API Inventory'!D5:D222, "WS")` → Kết quả: **`2`**
* **Đếm số API Đã triển khai theo từng Module (trên Sheet `Dashboard`):**
  `=COUNTIFS('API Inventory'!$B$5:$B$222, A5, 'API Inventory'!$J$5:$J$222, "Đã triển khai")`
* **Đếm số API Dự kiến theo từng Module (trên Sheet `Dashboard`):**
  `=COUNTIFS('API Inventory'!$B$5:$B$222, A5, 'API Inventory'!$J$5:$J$222, "Dự kiến")`

### 3.2. Quy trình 4 bước kiểm tra & duy trì tài liệu khi cập nhật mã nguồn
1. **Bước 1 – Triển khai Controller & XML Swagger Doc:** Giữ nguyên đường dẫn Route chuẩn (hoặc bổ sung alias tương thích ngược), gắn đầy đủ `[Authorize]` / `[AllowAnonymous]` / `[HasPermission(...)]` và `[ProducesResponseType]`.
2. **Bước 2 – Chạy Script quét đối chiếu tự động:**
   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts/scan_api_inventory.ps1
   ```
   Kiểm tra đầu ra khớp đúng số lượng **Canonical Controller Actions**, **Route Bindings (incl. Aliases)**, **Minimal API**, **SignalR Hubs**, và **Python FastAPI** endpoints.
3. **Bước 3 – Đồng bộ `00_api_inventory_and_endpoint_statistics.md`:** Cập nhật bảng chi tiết Mục 2 và tính lại toàn bộ bảng thống kê Mục `1.1`, `1.2`, `1.3`, `1.4` cũng như công thức Google Sheet Mục `3.1`.
4. **Bước 4 – Cập nhật đặc tả Payload chi tiết:** Đồng bộ đầy đủ tài liệu hợp đồng JSON Request/Response cho các module tương ứng trong thư mục `documents/api contact/`.
