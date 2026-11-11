# BẢNG TỔNG HỢP, DỰ BÁO QUY MÔ & THỐNG KÊ SỐ LƯỢNG API TOÀN HỆ THỐNG
## (FULL-SCOPE API INVENTORY, FORECAST & STATISTICAL DASHBOARD)

**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform (`SEP490_G141`)  
**Phiên bản tài liệu:** `v2.0.0` (Cập nhật quy mô toàn dự án theo SRS `Report-3` – Bao gồm API đã triển khai & API dự kiến cho 6 nhóm phân hệ mở rộng)  
**Thời điểm cập nhật:** 11/11/2026  
**Công nghệ Backend:** ASP.NET Core 10 WebApi (Clean Architecture, Npgsql PostGIS, SignalR WebSocket)  
**Công nghệ AI Service:** Python 3.11 FastAPI (YOLOv8 + MobileNetV3 + EfficientNet)  
**Tình trạng tài liệu:** **Tài liệu quy hoạch & đối chiếu tổng thể (Master API Inventory)** – Dùng làm nguồn dữ liệu chuẩn để đồng bộ bảng tính `Report_API List_SEP490_G141` trên Google Drive (`Dashboard` & `API Inventory`).

---

## 1. BẢNG TỔNG HỢP SỐ LƯỢNG API TOÀN DỰ ÁN (EXECUTIVE SUMMARY DASHBOARD)

Tính trên **toàn bộ phạm vi nghiệp vụ SRS (`Report-3_Software Requirement Specification_SEP490_G141`)**, hệ sinh thái **EcoLink** có tổng quy mô **`184 Endpoint`**, được chia thành **2 nhóm trạng thái thực thi** rõ ràng:

* **Tổng quy mô toàn hệ thống (Full Project Scope – `184 Endpoints`):**
  * **173 Endpoints thuộc Core Backend (`Ecolink.WebApi` - .NET 10):** gồm **171 REST APIs** và **2 SignalR WebSocket Hubs** (`/hubs/negotiation-chat`, `/hubs/notifications`).
  * **11 REST Endpoints thuộc Dedicated AI Vision Service (`FastAPI` - Python 3.11):** phục vụ nhận diện đa mô hình, Rule Engine, đồng bộ tập dữ liệu huấn luyện và nạp nóng mô hình.
* **Tiến độ triển khai thực tế trên nhánh `develop` hiện tại:**
  * **Đã triển khai (`86 Endpoints` – đạt `46.7%`):** gồm `76` Core REST APIs + `1` SignalR Hub + `9` AI FastAPI APIs.
  * **Dự kiến bổ sung (`98 Endpoints` – chiếm `53.3%`):** gồm `95` Core REST APIs + `1` SignalR Hub + `2` AI FastAPI APIs trải đều trên các phân hệ `FE-03 (Admin Rules)`, `FE-10`, `FE-13`, `FE-14`, `FE-15`, `FE-16`, `FE-17`, `FE-18` và `SU-04`.

```text
+--------------------------------------------------------------------------------------------------+
|                      TỔNG QUY MÔ API TOÀN HỆ THỐNG ECOLINK (FULL SRS SCOPE)                      |
|                                          184 ENDPOINTS                                           |
|                     [ Đã triển khai: 86 (46.7%)  |  Dự kiến bổ sung: 98 (53.3%) ]                |
+---------------------------------------------------+----------------------------------------------+
|        Core .NET 10 WebApi: 173 Endpoints         |     Dedicated AI Service: 11 Endpoints       |
|     (171 REST APIs + 2 SignalR WebSocket Hubs)    |            (FastAPI Python 3.11)             |
|       [ Đã có: 77  |  Dự kiến bổ sung: 96 ]       |        [ Đã có: 9  |  Dự kiến thêm: 2 ]      |
+---------------------------------------------------+----------------------------------------------+
```

### Bảng chỉ số KPI đầu trang cho Sheet `API Inventory` & `Dashboard` (Google Drive)

| Chỉ số KPI trên Google Sheet | Hiện trạng Code (`v1.2.0`) | Dự kiến mở rộng (Planned) | Tổng quy mô Toàn dự án (`v2.0.0`) |
|:---|:---:|:---:|:---:|
| **Tổng Endpoint** | 86 | +98 | **184** |
| **Core .NET 10** *(REST + WS)* | 77 | +96 | **173** |
| **AI FastAPI** | 9 | +2 | **11** |
| **WebSocket (SignalR Hub)** | 1 | +1 | **2** |

---

### 1.1. Thống kê số lượng API theo Module nghiệp vụ (Đã triển khai vs. Dự kiến)

| Mã Modul | Mã SRS | Tên Module / Phân hệ nghiệp vụ | Controller / Service phụ trách (Hiện tại & Dự kiến) | Đã code | Dự kiến | REST | WS | Tổng API | Tỷ trọng | Tiến độ |
|:---:|:---:|:---|:---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **M-01** | `FE-01..02` | **Authentication & Account** | `AuthController` | 14 | 0 | 14 | 0 | **14** | 7.6% | 100.0% |
| **M-02** | `FE-04` | **Recycler Onboarding & Verification** | `RecyclerApplicationsController` | 6 | 0 | 6 | 0 | **6** | 3.3% | 100.0% |
| **M-03** | `FE-03` | **AI Waste Classification & Rule Engine** | `WasteScanController`, `WasteCategoryController` | 5 | 6 | 11 | 0 | **11** | 6.0% | 45.5% |
| **M-04** | `FE-05` | **Recycler Profile, Pricing & Availability** | `RecyclerController` | 9 | 0 | 9 | 0 | **9** | 4.9% | 100.0% |
| **M-05** | `FE-06` | **Location-Based Recycler Discovery** | `RecyclersDiscoveryController`, `GeocodingController`, `TradeController` | 4 | 0 | 4 | 0 | **4** | 2.2% | 100.0% |
| **M-06** | `FE-07` | **Handover Request Management** | `HandoverRequestController`, `RecyclerHandoverController` | 10 | 0 | 10 | 0 | **10** | 5.4% | 100.0% |
| **M-07** | `FE-09` | **Handover Completion, Receipt & History** | `RecyclerReceiptController`, `HandoverReceiptController`, `TransactionControllers` | 8 | 0 | 8 | 0 | **8** | 4.3% | 100.0% |
| **M-08** | `FE-08` | **In-App Real-Time Negotiation Chatbox** | `NegotiationChatController`, `NegotiationChatHub` | 8 | 0 | 7 | 1 | **8** | 4.3% | 100.0% |
| **M-09** | `FE-07/09` | **Direct Trade Orders & QR Confirmation** | `TradeController` | 5 | 0 | 5 | 0 | **5** | 2.7% | 100.0% |
| **M-10** | `FE-13` | **Gamification, Virtual Tree & Rewards** | `GamificationController`, `StaffGamificationController` *(dự kiến)*, `AdminGamificationController` *(dự kiến)* | 2 | 25 | 27 | 0 | **27** | 14.7% | 7.4% |
| **M-11** | `FE-10` | **Rating, Review & Complaint Resolution** | `ReviewsController` *(dự kiến)*, `ComplaintsController` *(dự kiến)*, `StaffComplaintsController` *(dự kiến)* | 0 | 12 | 12 | 0 | **12** | 6.5% | 0.0% |
| **M-12** | `FE-14` | **In-System Service Packages & VietQR** | `ServicePaymentController`, `ServicePackagesController` *(dự kiến)*, `AdminReconciliationController` *(dự kiến)* | 5 | 10 | 15 | 0 | **15** | 8.2% | 33.3% |
| **M-13** | `FE-15` | **AI Misclassification & Dataset Curation** | `StaffAiFeedbackController` *(dự kiến)*, `StaffDatasetCurationController` *(dự kiến)* | 0 | 10 | 10 | 0 | **10** | 5.4% | 0.0% |
| **M-14** | `FE-16` | **Blog & Content Management** | `BlogsController` *(dự kiến)*, `StaffBlogsController` *(dự kiến)*, `AdminBlogsController` *(dự kiến)* | 0 | 10 | 10 | 0 | **10** | 5.4% | 0.0% |
| **M-15** | `FE-17..18` | **Account, Staff, Governance & Analytics** | `AdminAnalyticsController`, `StaffAccountsController`, `AdminStaffController`, `NotificationsController`, `NotificationHub` *(dự kiến)* | 0 | 23 | 22 | 1 | **23** | 12.5% | 0.0% |
| **SYS** | `INFRA` | **System Infrastructure & Health** | Minimal API (`Program.cs`) | 1 | 0 | 1 | 0 | **1** | 0.5% | 100.0% |
| **AIS** | `FE-03/15` | **Dedicated AI Vision Service (Python)** | FastAPI (`app.py`) | 9 | 2 | 11 | 0 | **11** | 6.0% | 81.8% |
| **TỔNG** | **ALL SRS** | **TOÀN BỘ HỆ THỐNG ECOLINK** | **Core .NET 10 (173) + FastAPI Python (11)** | **86** | **98** | **182** | **2** | **184** | **100.0%** | **46.7%** |

---

### 1.2. Thống kê số lượng theo Phương thức (HTTP Methods & Protocols)

```text
  [GET]     ######################################### (81) - 44.0%
  [POST]    ############################### (62) - 33.7%
  [PUT]     ########## (19) - 10.3%
  [PATCH]   ######## (16) - 8.7%
  [DELETE]  ## (4) - 2.2%
  [WS]      # (2) - 1.1%
```

| Phương thức HTTP / Giao thức | Ý nghĩa sử dụng chính trong dự án | Đã triển khai | Dự kiến bổ sung | Tổng quy mô | Tỷ lệ (%) |
|:---|:---|:---:|:---:|:---:|:---:|
| **GET** | Truy vấn danh sách, tìm kiếm PostGIS, đọc hồ sơ/khiếu nại, KPI Dashboard, Audit Log, xuất báo cáo CSV/XLSX | 37 | 44 | **81** | 44.0% |
| **POST** | Đăng ký/đăng nhập, quét rác AI, tạo đơn thu gom, biên nhận, điểm danh, đổi quà, khiếu nại, VietQR, đóng gói Dataset | 37 | 25 | **62** | 33.7% |
| **PUT** | Cập nhật toàn phần hồ sơ, cấu hình bảng giá, cấu hình Gamification, gán nhãn ảnh chuẩn, phân quyền RBAC Staff | 8 | 11 | **19** | 10.3% |
| **PATCH** | Chuyển trạng thái nghiệp vụ tức thời: duyệt khiếu nại, duyệt blog, khóa tài khoản, đối soát giao dịch, ẩn/mở danh mục | 3 | 13 | **16** | 8.7% |
| **DELETE** | Xóa đánh giá của chính tác giả (`BR-RVW-03`), xóa thẻ Eco Fact nháp, xóa bài Blog nháp, vô hiệu hóa mềm tài khoản | 0 | 4 | **4** | 2.2% |
| **WebSocket (`WS`)** | Kết nối SignalR song công thời gian thực cho phòng Chat thương lượng giá và kênh Đẩy thông báo hệ thống | 1 | 1 | **2** | 1.1% |
| **Tổng cộng** | | **86** | **98** | **184** | **100.0%** |

---

### 1.3. Thống kê theo Cơ chế Phân quyền Truy cập (Role-Based Access Control)

#### A. Bảng tổng hợp theo 5 Nhóm RBAC chuẩn (Khớp 100% cột `Nhóm RBAC` trên Google Sheet)

| Nhóm RBAC chuẩn | Đối tượng được phép truy cập | Đã triển khai | Dự kiến bổ sung | Tổng API | Tỷ lệ (%) | Ví dụ nghiệp vụ tiêu biểu |
|:---|:---|:---:|:---:|:---:|:---:|:---|
| **Public / Guest** | Mọi người dùng (kể cả chưa đăng nhập, Webhook Gateway & Internal AI) | 30 | 5 | **35** | 19.0% | Đăng ký/đăng nhập, quét rác AI, tìm vựa GPS, bảng xếp hạng, đọc Blog, xem Review vựa, Webhook VietQR |
| **Citizen (User)** | Người dân đã xác thực tài khoản (`Role = Citizen`) | 11 | 8 | **19** | 10.3% | Nộp đơn mở vựa, đặt lịch thu gom, xác nhận biên nhận, nuôi Cây Ảo, mở Eco Fact, gửi/xóa đánh giá sao |
| **Recycler (Collector)** | Đối tác cơ sở thu gom đã được xác minh (`Role = Recycler`) | 19 | 0 | **19** | 10.3% | Hồ sơ vựa, cấu hình bảng giá thu mua, lịch mở cửa, nhận/từ chối đơn, phát hành biên nhận cân điện tử |
| **Staff / Admin** | Nhân viên vận hành (`Staff`) và Quản trị viên hệ thống (`Admin`) | 3 | 68 | **71** | 38.6% | Duyệt vựa, xử lý khiếu nại, kho quà & Eco Fact, gán nhãn Dataset AI, biên tập/duyệt Blog, đối soát VietQR, KPI & RBAC |
| **Authenticated (All)** | Nhiều vai trò đã đăng nhập (`Citizen / Recycler` hoặc mọi User hợp lệ) | 23 | 17 | **40** | 21.7% | Hồ sơ cá nhân, chat thương lượng, điểm danh Streak, đổi quà, gửi khiếu nại, mua gói dịch vụ VietQR, nhận thông báo |
| **Tổng cộng** | | **86** | **98** | **184** | **100.0%** | |

#### B. Phân rã chi tiết nội bộ nhóm Quản trị & Vận hành (`Staff / Admin` – `71 APIs`)

| Phân tầng quyền nội bộ | Số lượng API | Phạm vi Use Case phụ trách |
|:---|:---:|:---|
| **Staff & Admin (Vận hành chung)** | **37** | Duyệt hồ sơ mở vựa (`S-04`), phân xử khiếu nại & kiểm duyệt đánh giá (`S-05`, `U-10`), quản lý Eco Fact (`S-09`), quản lý kho quà & trả thưởng (`S-10`), duyệt ảnh AI sai & đóng gói Dataset (`S-01..S-03`), soạn thảo Blog (`S-08`), tra cứu & khóa tài khoản vi phạm (`S-06`, `S-07`). |
| **Staff (Chấm công ca trực)** | **3** | Điểm danh vào ca, kết thúc ca trực và xem bảng công cá nhân của Nhân viên (`S-11`, tách biệt ví điểm theo `BR-GAM-09`). |
| **Admin Only (Quản trị cấp cao)** | **31** | Cấu hình danh mục rác & Rule Engine (`A-05`, `A-06`), cấu hình tham số Gamification & chu kỳ đua Top (`A-07`, `A-08`), duyệt xuất bản Blog (`A-09`), cấu hình phí gói dịch vụ & đối soát dòng tiền VietQR (`A-10`, `A-11`), Dashboard KPI, xuất báo cáo, xem Audit Log, quản lý tài khoản & phân quyền RBAC cho Staff (`A-01..A-04`). |

---

### 1.4. Ma trận Đối chiếu Độ phủ Use Case SRS sang Danh mục API

| Nhóm Phân hệ SRS | Danh sách Mã Use Case được bao phủ | Mã Modul | Dải STT Endpoint | Tổng số API |
|:---|:---|:---:|:---:|:---:|
| **Auth, Profile & Onboarding** | `G-02`, `SU-01`, `SU-02`, `SU-03`, `U-17`, `S-04` | `M-01`, `M-02` | `001` → `020` | **20** |
| **AI Waste Scan, Catalog & Rule Engine** | `U-01`, `U-02`, `U-03`, `U-04` *(gửi báo lỗi)*, `A-05`, `A-06` | `M-03`, `AIS` | `021` → `031`, `174` → `184` | **22** |
| **Recycler Operations & Geo Discovery** | `R-01`, `R-02`, `U-05` | `M-04`, `M-05` | `032` → `044` | **13** |
| **Handover, Negotiation Chat & Receipt** | `U-06`, `U-07`, `U-08`, `U-09`, `U-12`, `R-03`, `R-04`, `R-05`, `R-06` | `M-06` → `M-09` | `045` → `075` | **31** |
| **1. Gamification & Rewards (`FE-13`)** | `U-13`, `U-14`, `U-15`, `S-09`, `S-10`, `A-07`, `A-08` | `M-10` | `076` → `102` | **27** |
| **2. Rating, Review & Complaint (`FE-10`)** | `U-10`, `U-11`, `S-05` | `M-11` | `103` → `114` | **12** |
| **3. In-System Service Packages (`FE-14`)** | `U-16`, `R-07`, `A-10`, `A-11` | `M-12` | `115` → `129` | **15** |
| **4. AI Feedback & Dataset Curation (`FE-15`)** | `U-04` *(theo dõi)*, `S-01`, `S-02`, `S-03` | `M-13` *(+ `AIS`)* | `130` → `139` | **10** |
| **5. Blog & Content Management (`FE-16`)** | `G-01`, `S-08`, `A-09` | `M-14` | `140` → `149` | **10** |
| **6. Governance, Staff, RBAC & Analytics (`FE-17`, `FE-18`)** | `A-01`, `A-02`, `A-03`, `A-04`, `S-06`, `S-07`, `S-11`, `SU-04` | `M-15`, `SYS` | `150` → `173` | **24** |

---

## 2. DANH MỤC CHI TIẾT 100% CÁC API ENDPOINTS TOÀN DỰ ÁN (`184 ENDPOINTS`)

> **Hướng dẫn đồng bộ sang Google Sheet (`Report_API List_SEP490_G141`):**  
> Các bảng dưới đây được chuẩn hóa đồng nhất các cột: `STT` | `Mã Modul` | `Phương thức` | `Tuyến đường Endpoint (Route)` | `Phân quyền (RBAC)` | `Action / Function` | `Mô tả chức năng & Use Case nghiệp vụ` | `Nhóm RBAC` | `Trạng thái` để sao chép trực tiếp vào tab `API Inventory`.

---

### PHÂN HỆ 01 (`M-01`): XÁC THỰC & QUẢN TRỊ TÀI KHOẢN (AUTHENTICATION & ACCOUNT)
* **Tài liệu đặc tả chi tiết:** [`01_module_authentication_api.md`](./01_module_authentication_api.md)
* **Controller:** `AuthController` | **Base Route:** `/api/v1/auth` | **Quy mô:** `14` APIs (14 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **001** | `M-01` | `POST` | `/api/v1/auth/register` | Public (Guest) | `Register` | Đăng ký tài khoản mới cho Người dân (`Citizen`), khởi tạo đồng thời hồ sơ `UserEcoProfile` (`G-02`). | Public / Guest | Đã triển khai |
| **002** | `M-01` | `POST` | `/api/v1/auth/register-recycler` | Public (Guest) | `RegisterRecycler` | Đăng ký tài khoản cho Cơ sở thu gom, tạo đồng thời hồ sơ cơ sở ở trạng thái chờ duyệt `Pending` (`U-17`). | Public / Guest | Đã triển khai |
| **003** | `M-01` | `POST` | `/api/v1/auth/login` | Public (Guest) | `Login` | Đăng nhập bằng Username/Password, chống Brute-force khóa tạm thời sau 5 lần sai liên tiếp (`SU-01`, `BR-AUTH-04`). | Public / Guest | Đã triển khai |
| **004** | `M-01` | `GET` | `/api/v1/auth/google-config` | Public (Guest) | `GetGoogleConfig` | Trả về `ClientId` Google OAuth 2.0 để Frontend khởi tạo luồng đăng nhập nhanh Google (`SU-01`). | Public / Guest | Đã triển khai |
| **005** | `M-01` | `POST` | `/api/v1/auth/google` | Public (Guest) | `GoogleLogin` | Đăng nhập hoặc liên kết tài khoản nhanh bằng Google ID Token (`SU-01`, `BR-AUTH-05`). | Public / Guest | Đã triển khai |
| **006** | `M-01` | `POST` | `/api/v1/auth/refresh-token` | Public (Guest) | `RefreshToken` | Cấp mới Access Token (30 phút) bằng Refresh Token (7 ngày), xoay vòng chống Replay Attack (`BR-AUTH-06`). | Public / Guest | Đã triển khai |
| **007** | `M-01` | `POST` | `/api/v1/auth/forgot-password` | Public (Guest) | `ForgotPassword` | Yêu cầu khôi phục mật khẩu, sinh mã OTP 6 chữ số gửi tới Gmail qua SMTP (`SU-03`, `BR-AUTH-09`). | Public / Guest | Đã triển khai |
| **008** | `M-01` | `POST` | `/api/v1/auth/reset-password` | Public (Guest) | `ResetPassword` | Xác thực mã OTP hợp lệ và cập nhật mật khẩu mới cho tài khoản (`SU-03`, `BR-AUTH-09`). | Public / Guest | Đã triển khai |
| **009** | `M-01` | `PUT` | `/api/v1/auth/profile` | Authenticated | `UpdateProfile` | Cập nhật họ tên, số điện thoại, địa chỉ mặc định và ảnh đại diện của người dùng hiện tại (`SU-02`). | Authenticated (All) | Đã triển khai |
| **010** | `M-01` | `POST` | `/api/v1/auth/change-password` | Authenticated | `ChangePassword` | Đổi mật khẩu tài khoản yêu cầu xác nhận mật khẩu hiện tại (`SU-02`). | Authenticated (All) | Đã triển khai |
| **011** | `M-01` | `POST` | `/api/v1/auth/logout` | Authenticated | `Logout` | Thu hồi Refresh Token hiện tại và kết thúc phiên đăng nhập an toàn (`SU-01`). | Authenticated (All) | Đã triển khai |
| **012** | `M-01` | `GET` | `/api/v1/auth/me` | Authenticated | `GetCurrentUser` | Lấy thông tin định danh, vai trò RBAC và trạng thái hồ sơ của tài khoản đang đăng nhập (`SU-02`). | Authenticated (All) | Đã triển khai |
| **013** | `M-01` | `GET` | `/api/v1/auth/demo-users` | Public (Dev/Test) | `GetDemoUsers` | Danh sách tài khoản mẫu (Citizen, Recycler, Staff, Admin) phục vụ môi trường kiểm thử nhanh. | Public / Guest | Đã triển khai |
| **014** | `M-01` | `POST` | `/api/v1/auth/vietqr-token` | Authenticated | `GenerateVietQrToken` | Sinh chữ ký token bảo mật phục vụ tích hợp cổng thanh toán VietQR / PayOS (`FE-14`). | Authenticated (All) | Đã triển khai |

---

### PHÂN HỆ 02 (`M-02`): TIẾP NHẬN & XÉT DUYỆT CƠ SỞ THU GOM (RECYCLER ONBOARDING & VERIFICATION)
* **Tài liệu đặc tả chi tiết:** [`02_module_recycler_onboarding_api.md`](./02_module_recycler_onboarding_api.md)
* **Controller:** `RecyclerApplicationsController` | **Base Route:** `/api/v1/recycler-applications` | **Quy mô:** `6` APIs (6 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **015** | `M-02` | `POST` | `/api/v1/recycler-applications/upload-document` | Authenticated | `UploadDocument` | Tải giấy phép kinh doanh / chứng từ môi trường / ảnh mặt tiền cơ sở lên Cloudflare R2 (`U-17`). | Authenticated (All) | Đã triển khai |
| **016** | `M-02` | `POST` | `/api/v1/recycler-applications` | Citizen (User) | `SubmitApplication` | Nộp hồ sơ đăng ký mở cơ sở thu mua: tên vựa, địa chỉ, tọa độ GPS (PostGIS Point), danh sách chứng từ (`U-17`). | Citizen (User) | Đã triển khai |
| **017** | `M-02` | `GET` | `/api/v1/recycler-applications/me` | Authenticated | `GetMyApplication` | Người nộp đơn theo dõi tiến độ thẩm định hồ sơ (`Pending`, `Verified`, `Rejected`) và ghi chú phản hồi (`U-17`). | Authenticated (All) | Đã triển khai |
| **018** | `M-02` | `GET` | `/api/v1/recycler-applications` | Staff / Admin | `GetApplications` | Nhân viên vận hành lấy danh sách đơn đăng ký mở cơ sở cần thẩm định có lọc trạng thái và phân trang (`S-04`). | Staff / Admin | Đã triển khai |
| **019** | `M-02` | `GET` | `/api/v1/recycler-applications/{userId:guid}` | Staff / Admin | `GetApplicationDetail` | Xem chi tiết hồ sơ đăng ký của một cơ sở kèm ảnh chụp giấy phép và vị trí bản đồ (`S-04`). | Staff / Admin | Đã triển khai |
| **020** | `M-02` | `PUT` | `/api/v1/recycler-applications/{userId:guid}/verification` | Staff / Admin | `ReviewApplication` | Phê duyệt (`Verified` – nâng cấp vai trò thành `Recycler`) hoặc Từ chối (`Rejected` kèm lý do bắt buộc) (`S-04`). | Staff / Admin | Đã triển khai |

---

### PHÂN HỆ 03 (`M-03`): PHÂN LOẠI RÁC AI, DANH MỤC CHUẨN & LUẬT HƯỚNG DẪN (AI WASTE CLASSIFICATION & RULE ENGINE)
* **Tài liệu đặc tả chi tiết:** [`03_module_ai_waste_classification_api.md`](./03_module_ai_waste_classification_api.md)
* **Controllers:** `WasteScanController`, `WasteCategoryController` | **Quy mô:** `11` APIs (5 Đã triển khai, 6 Dự kiến cho `U-01 History`, `A-05`, `A-06`)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **021** | `M-03` | `POST` | `/api/v1/waste-scan/classify` | Public / Citizen | `ClassifyImage` | Gửi ảnh rác lên Backend để gọi AI nhận diện, kiểm tra ngưỡng OOD, trả về nhãn rác và hướng dẫn xử lý (`U-01`, `U-02`, `U-03`). | Public / Guest | Đã triển khai |
| **022** | `M-03` | `GET` | `/api/v1/waste-scan/models` | Public (All) | `GetAvailableModels` | Lấy danh sách các mô hình thị giác máy tính đang sẵn sàng (`yolov8`, `mobilenet_v3`, `efficientnet`) (`U-01`). | Public / Guest | Đã triển khai |
| **023** | `M-03` | `GET` | `/api/v1/waste-scan/rules` | Public (All) | `GetAllRules` | Tra cứu toàn bộ tập luật phân loại rác của Rule Engine: màu thùng chuẩn, yêu cầu làm sạch, mẹo tái chế (`U-03`). | Public / Guest | Đã triển khai |
| **024** | `M-03` | `POST` | `/api/v1/waste-scan/feedback` | Authenticated | `SubmitFeedback` | Người dùng gửi báo cáo phản hồi và gợi ý nhãn đúng khi AI nhận diện sai ảnh rác (`U-04`, `BR-CLS-05`). | Authenticated (All) | Đã triển khai |
| **025** | `M-03` | `GET` | `/api/v1/waste-categories` | Public (All) | `GetAll` | Lấy danh mục loại phế liệu chuẩn hệ thống: mã danh mục, tên hiển thị, màu thùng, icon, cờ tái chế (`U-03`, `A-05`). | Public / Guest | Đã triển khai |
| **026** | `M-03` | `GET` | `/api/v1/waste-scan/history` | Citizen (User) | `GetMyScanHistory` | Người dân xem lại lịch sử các lần chụp/quét ảnh rác AI của cá nhân kèm hướng dẫn đã nhận (`U-01`, `U-03`). | Citizen (User) | Dự kiến |
| **027** | `M-03` | `POST` | `/api/v1/waste-categories` | Admin | `CreateCategory` | Quản trị viên thêm mới loại phế liệu chuẩn vào danh mục gốc hệ thống (`MasterWasteCategory`) (`A-05`, `BR-SRT-02`). | Staff / Admin | Dự kiến |
| **028** | `M-03` | `PUT` | `/api/v1/waste-categories/{categoryId:guid}` | Admin | `UpdateCategory` | Quản trị viên cập nhật tên, mô tả, biểu tượng, đơn vị tính hoặc màu thùng chuẩn của loại phế liệu (`A-05`, `BR-SRT-02`). | Staff / Admin | Dự kiến |
| **029** | `M-03` | `PATCH` | `/api/v1/waste-categories/{categoryId:guid}/status` | Admin | `ToggleCategoryStatus` | Vô hiệu hóa mềm (`Deactivate`) hoặc mở lại danh mục phế liệu mà vẫn bảo toàn biên nhận lịch sử (`A-05`, `BR-SRT-04`). | Staff / Admin | Dự kiến |
| **030** | `M-03` | `PUT` | `/api/v1/waste-scan/rules/{categoryId:guid}` | Admin | `UpsertSortingRule` | Cấu hình nội dung chính sách hướng dẫn phân loại, sơ chế sạch và cảnh báo an toàn theo từng loại rác (`A-06`, `BR-SRT-01`). | Staff / Admin | Dự kiến |
| **031** | `M-03` | `PATCH` | `/api/v1/waste-scan/rules/{ruleId:guid}/publish` | Admin | `PublishSortingRule` | Phát hành phiên bản luật phân loại mới và tự động đồng bộ bộ nhớ đệm Rule Engine sang AI Service (`A-06`, `BR-SRT-03`). | Staff / Admin | Dự kiến |

---

### PHÂN HỆ 04 (`M-04`): HỒ SƠ CƠ SỞ, BẢNG GIÁ & LỊCH HOẠT ĐỘNG (RECYCLER PROFILE, PRICING & AVAILABILITY)
* **Tài liệu đặc tả chi tiết:** [`04_module_recycler_profile_pricing_availability_api.md`](./04_module_recycler_profile_pricing_availability_api.md)
* **Controller:** `RecyclerController` | **Base Route:** `/api/v1/recycler` | **Quy mô:** `9` APIs (9 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **032** | `M-04` | `GET` | `/api/v1/recycler/profile` | Recycler | `GetProfile` | Cơ sở thu gom xem đầy đủ hồ sơ vận hành của vựa: tên, địa chỉ, tọa độ GPS, giờ mở cửa, hotline, ảnh (`R-01`). | Recycler (Collector) | Đã triển khai |
| **033** | `M-04` | `PUT` | `/api/v1/recycler/profile` | Recycler | `UpdateProfile` | Cập nhật thông tin vận hành của cơ sở: tên vựa, số điện thoại liên hệ, bán kính phục vụ, ảnh biển hiệu (`R-01`). | Recycler (Collector) | Đã triển khai |
| **034** | `M-04` | `GET` | `/api/v1/recycler/prices` | Recycler | `GetPrices` | Xem toàn bộ bảng giá thu mua phế liệu hiện đang cấu hình của cơ sở (`price_per_kg`, `min_kg`) (`R-01`). | Recycler (Collector) | Đã triển khai |
| **035** | `M-04` | `POST` | `/api/v1/recycler/prices` | Recycler | `AddPrice` | Thêm đơn giá thu mua mới cho một danh mục phế liệu chuẩn vào bảng giá niêm yết của vựa (`R-01`). | Recycler (Collector) | Đã triển khai |
| **036** | `M-04` | `PUT` | `/api/v1/recycler/prices/{priceId:guid}` | Recycler | `UpdatePrice` | Chỉnh sửa đơn giá thu mua (VNĐ/kg), khối lượng tối thiểu hoặc tạm dừng thu mua mặt hàng (`R-01`). | Recycler (Collector) | Đã triển khai |
| **037** | `M-04` | `PUT` | `/api/v1/recycler/prices` | Recycler | `BulkSavePrices` | Lưu đồng loạt toàn bộ bảng giá thu mua nhiều loại phế liệu trong một giao dịch nguyên tử (`R-01`). | Recycler (Collector) | Đã triển khai |
| **038** | `M-04` | `GET` | `/api/v1/recycler/availability` | Recycler | `GetAvailability` | Xem trạng thái nhận đơn tức thời (`Online` / `Offline` / `Busy`) và ghi chú lý do tạm nghỉ (`R-02`). | Recycler (Collector) | Đã triển khai |
| **039** | `M-04` | `PATCH` | `/api/v1/recycler/availability` | Recycler | `UpdateAvailability` | Bật/tắt trạng thái nhận đơn tức thời trên bản đồ tìm kiếm (`Online` ↔ `Offline`) (`R-02`). | Recycler (Collector) | Đã triển khai |
| **040** | `M-04` | `PUT` | `/api/v1/recycler/availability/schedule` | Recycler | `UpdateWeeklySchedule` | Thiết lập lịch mở cửa định kỳ từ Thứ 2 đến Chủ Nhật cho cơ sở thu gom (`R-02`). | Recycler (Collector) | Đã triển khai |

---

### PHÂN HỆ 05 (`M-05`): KHÁM PHÁ & TÌM KIẾM ĐIỂM THU GOM THEO TỌA ĐỘ (LOCATION-BASED RECYCLER DISCOVERY)
* **Tài liệu đặc tả chi tiết:** [`05_module_location_based_recycler_discovery_api.md`](./05_module_location_based_recycler_discovery_api.md)
* **Controllers:** `RecyclersDiscoveryController`, `GeocodingController`, `TradeController` | **Quy mô:** `4` APIs (4 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **041** | `M-05` | `GET` | `/api/v1/recyclers/nearby` | Public / Citizen | `FindNearest` | Tìm kiếm các cơ sở thu gom quanh tọa độ GPS trong bán kính R (km) bằng truy vấn không gian PostGIS, lọc loại rác (`U-05`). | Public / Guest | Đã triển khai |
| **042** | `M-05` | `GET` | `/api/v1/recyclers/{recyclerId:guid}` | Public / Citizen | `GetPublicDetail` | Xem trang hồ sơ công khai của cơ sở: khoảng cách GPS, bảng giá niêm yết, huy hiệu uy tín, giờ mở cửa (`U-05`). | Public / Guest | Đã triển khai |
| **043** | `M-05` | `GET` | `/api/v1/geocoding/search` | Public (All) | `Search` | Gợi ý địa chỉ tự động và chuyển đổi tên đường/địa danh sang tọa độ Latitude/Longitude (`U-05`, `U-06`). | Public / Guest | Đã triển khai |
| **044** | `M-05` | `GET` | `/api/v1/trade/partners/nearby` | Public / Citizen | `GetNearbyPartners` | Tìm nhanh danh sách đối tác thu mua xung quanh phục vụ luồng khởi tạo đơn giao dịch trực tiếp (`U-05`). | Public / Guest | Đã triển khai |

---

### PHÂN HỆ 06 (`M-06`): QUẢN LÝ YÊU CẦU BÀN GIAO PHẾ LIỆU (HANDOVER REQUEST MANAGEMENT)
* **Tài liệu đặc tả chi tiết:** [`06_module_handover_request_management_api.md`](./06_module_handover_request_management_api.md)
* **Controllers:** `HandoverRequestController`, `RecyclerHandoverController` | **Quy mô:** `10` APIs (10 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **045** | `M-06` | `POST` | `/api/v1/handover-requests` | Citizen (User) | `Create` | Người dân tạo yêu cầu hẹn bàn giao phế liệu (`HND-yyyyMMdd-NNNNNN`): chọn vựa, địa chỉ, loại rác ước tính, khung giờ (`U-06`). | Citizen (User) | Đã triển khai |
| **046** | `M-06` | `GET` | `/api/v1/handover-requests` | Citizen (User) | `GetMine` | Người dân tra cứu danh sách các đơn yêu cầu bàn giao của mình theo trạng thái (`Pending`, `Accepted`, `Completed`...) (`U-06`). | Citizen (User) | Đã triển khai |
| **047** | `M-06` | `GET` | `/api/v1/handover-requests/{id:guid}` | Citizen (User) | `GetById` | Người dân xem chi tiết tiến độ đơn hẹn thu gom, mặt hàng ước tính và khung giờ đã chốt (`U-06`). | Citizen (User) | Đã triển khai |
| **048** | `M-06` | `PUT` | `/api/v1/handover-requests/{id:guid}` | Citizen (User) | `Update` | Người dân cập nhật ghi chú hoặc điều chỉnh khung giờ đề xuất khi đơn đang ở trạng thái chờ nhận (`U-06`). | Citizen (User) | Đã triển khai |
| **049** | `M-06` | `POST` | `/api/v1/handover-requests/{id:guid}/cancel` | Citizen (User) | `Cancel` | Người dân hủy yêu cầu hẹn bàn giao kèm lý do (kiểm tra quy tắc cảnh báo hủy sát giờ hẹn) (`U-07`). | Citizen (User) | Đã triển khai |
| **050** | `M-06` | `GET` | `/api/v1/recycler/handover-requests` | Recycler | `GetIncoming` | Cơ sở thu gom xem danh sách các yêu cầu bàn giao gửi đến cơ sở của mình theo trạng thái và ngày hẹn (`R-03`). | Recycler (Collector) | Đã triển khai |
| **051** | `M-06` | `GET` | `/api/v1/recycler/handover-requests/{id:guid}` | Recycler | `GetById` | Cơ sở xem chi tiết đơn bàn giao: ảnh chụp phế liệu, khối lượng ước tính, tọa độ và địa chỉ thu gom (`R-03`). | Recycler (Collector) | Đã triển khai |
| **052** | `M-06` | `POST` | `/api/v1/recycler/handover-requests/{id:guid}/accept` | Recycler | `Accept` | Cơ sở chấp nhận đơn bàn giao, chốt lịch hẹn chính thức và mở quyền liên lạc giữa hai bên (`R-03`). | Recycler (Collector) | Đã triển khai |
| **053** | `M-06` | `POST` | `/api/v1/recycler/handover-requests/{id:guid}/reject` | Recycler | `Reject` | Cơ sở từ chối yêu cầu bàn giao đang chờ (do quá tải, ngoài phạm vi hoặc không thu mua mặt hàng) (`R-03`). | Recycler (Collector) | Đã triển khai |
| **054** | `M-06` | `POST` | `/api/v1/recycler/handover-requests/{id:guid}/cancel` | Recycler | `Cancel` | Cơ sở hủy đơn đã nhận trong trường hợp bất khả kháng kèm lý do bắt buộc (`R-04`). | Recycler (Collector) | Đã triển khai |

---

### PHÂN HỆ 07 (`M-07`): BIÊN NHẬN SỐ & LỊCH SỬ GIAO DỊCH (HANDOVER COMPLETION, RECEIPT & HISTORY)
* **Tài liệu đặc tả chi tiết:** [`07_module_handover_completion_and_receipt_api.md`](./07_module_handover_completion_and_receipt_api.md)
* **Controllers:** `RecyclerReceiptController`, `HandoverReceiptController`, `TransactionControllers` | **Quy mô:** `8` APIs (8 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **055** | `M-07` | `POST` | `/api/v1/recycler/handover-requests/{requestId:guid}/receipt/preview` | Recycler | `Preview` | Cơ sở xem trước biên nhận: tính tổng tiền theo đơn giá đã chốt và cảnh báo nếu khối lượng cân lệch > 20% (`R-06`). | Recycler (Collector) | Đã triển khai |
| **056** | `M-07` | `POST` | `/api/v1/recycler/handover-requests/{requestId:guid}/receipt` | Recycler | `Create` | Cơ sở phát hành biên nhận điện tử (`RCP-yyyy-NNNNNN`), lưu khối lượng cân thực tế kèm ảnh mặt đồng hồ cân (`R-06`). | Recycler (Collector) | Đã triển khai |
| **057** | `M-07` | `GET` | `/api/v1/recycler/handover-requests/{requestId:guid}/receipt` | Recycler | `Get` | Cơ sở xem lại chi tiết biên nhận điện tử đã phát hành cho đơn thu gom (`R-06`). | Recycler (Collector) | Đã triển khai |
| **058** | `M-07` | `GET` | `/api/v1/recycler/transactions` | Recycler | `GetHistory` | Cơ sở tra cứu lịch sử toàn bộ giao dịch thu mua đã hoàn tất, tổng sản lượng và tổng chi phí (`R-06`, `U-12`). | Recycler (Collector) | Đã triển khai |
| **059** | `M-07` | `GET` | `/api/v1/handover-requests/{requestId:guid}/receipt` | Citizen (User) | `Get` | Người dân kiểm tra biên nhận điện tử do vựa gửi: khối lượng thực tế, đơn giá, thành tiền và ảnh bằng chứng cân (`U-09`). | Citizen (User) | Đã triển khai |
| **060** | `M-07` | `POST` | `/api/v1/handover-requests/{requestId:guid}/receipt/confirm` | Citizen (User) | `Confirm` | Người dân xác nhận đã nhận đủ tiền, chốt trạng thái `Completed` nguyên tử và cộng điểm Mầm Xanh (`U-09`). | Citizen (User) | Đã triển khai |
| **061** | `M-07` | `POST` | `/api/v1/handover-requests/{requestId:guid}/receipt/dispute` | Citizen (User) | `Dispute` | Người dân báo sai lệch biên nhận, chuyển đơn sang trạng thái tranh chấp `Disputed` chờ Staff phân xử (`U-09`, `U-11`). | Citizen (User) | Đã triển khai |
| **062** | `M-07` | `GET` | `/api/v1/transactions` | Citizen (User) | `GetHistory` | Người dân xem lịch sử bán phế liệu cá nhân: tổng tiền thu được, khối lượng tái chế và điểm thưởng tích lũy (`U-12`). | Citizen (User) | Đã triển khai |

---

### PHÂN HỆ 08 (`M-08`): CHAT THƯƠNG LƯỢNG THỜI GIAN THỰC (IN-APP REAL-TIME NEGOTIATION CHATBOX)
* **Tài liệu đặc tả chi tiết:** [`08_module_in_app_realtime_negotiation_chatbox_api.md`](./08_module_in_app_realtime_negotiation_chatbox_api.md)
* **Controller:** `NegotiationChatController` | **Hub:** `NegotiationChatHub` (`/hubs/negotiation-chat`) | **Quy mô:** `8` APIs (8 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **063** | `M-08` | `GET` | `/api/v1/trade/orders/{orderId:guid}/messages` | Citizen / Recycler | `GetChatThread` | Lấy luồng hội thoại của đơn: trạng thái thương lượng, đề xuất hiện hành và danh sách tin nhắn phân trang (`U-08`, `R-05`). | Authenticated (All) | Đã triển khai |
| **064** | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/messages` | Citizen / Recycler | `SendMessage` | Gửi tin nhắn văn bản (`Text`) hoặc hình ảnh (`Image`) vào phòng thương lượng của đơn bàn giao (`U-08`, `R-05`). | Authenticated (All) | Đã triển khai |
| **065** | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/messages/attachments` | Citizen / Recycler | `UploadChatAttachment` | Tải ảnh chụp thực tế phế liệu lên Cloudflare R2 để đính kèm trực tiếp vào phòng chat (`U-08`, `R-05`). | Authenticated (All) | Đã triển khai |
| **066** | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/proposals` | Citizen / Recycler | `SendProposal` | Gửi thẻ đề xuất / phản đề xuất (`Proposal`): đơn giá thương lượng (VNĐ/kg), khối lượng ước tính và giờ hẹn (`U-08`, `R-05`). | Authenticated (All) | Đã triển khai |
| **067** | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/proposals/{messageId:guid}/respond` | Citizen / Recycler | `RespondToProposal` | Đối phương chấp nhận (`Accepted` – cập nhật điều khoản đơn) hoặc từ chối (`Rejected`) thẻ đề xuất giá (`U-08`, `R-05`). | Authenticated (All) | Đã triển khai |
| **068** | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/accept-terms` | Recycler | `AcceptOrderTerms` | Cơ sở thu gom chốt chấp nhận ngay điều khoản ban đầu của Người dân mà không cần phản đề xuất (`R-05`). | Recycler (Collector) | Đã triển khai |
| **069** | `M-08` | `PUT` | `/api/v1/trade/orders/{orderId:guid}/messages/read` | Citizen / Recycler | `MarkMessagesAsRead` | Đánh dấu toàn bộ tin nhắn chưa đọc của đối phương trong phòng thương lượng là đã xem (`U-08`, `R-05`). | Authenticated (All) | Đã triển khai |
| **070** | `M-08` | `WS` | `/hubs/negotiation-chat` | Citizen / Recycler | `NegotiationChatHub` | Kênh WebSocket SignalR song công: `JoinOrderChat`, nhận tin nhắn tức thời `ReceiveMessage`, trạng thái đang gõ `UserTyping` (`U-08`, `R-05`). | Authenticated (All) | Đã triển khai |

---

### PHÂN HỆ 09 (`M-09`): ĐƠN GIAO DỊCH TRỰC TIẾP & XÁC NHẬN QR (DIRECT TRADE ORDERS & QR CONFIRMATION)
* **Controller:** `TradeController` | **Base Route:** `/api/v1/trade` | **Quy mô:** `5` APIs (5 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **071** | `M-09` | `GET` | `/api/v1/trade/orders` | Citizen / Recycler | `GetOrders` | Tra cứu danh sách các đơn giao dịch thương mại phế liệu trực tiếp của người dùng (`U-06`, `R-03`). | Authenticated (All) | Đã triển khai |
| **072** | `M-09` | `POST` | `/api/v1/trade/orders` | Citizen (User) | `CreateOrder` | Khởi tạo đơn giao dịch mua bán phế liệu trực tiếp gắn với phòng thương lượng giá (`U-06`, `U-08`). | Citizen (User) | Đã triển khai |
| **073** | `M-09` | `PATCH` | `/api/v1/trade/orders/{orderId:guid}/status` | Authenticated | `UpdateOrderStatus` | Cập nhật trạng thái luồng xử lý đơn giao dịch (`Pending`, `InNegotiation`, `Agreed`, `Completed`). | Authenticated (All) | Đã triển khai |
| **074** | `M-09` | `PATCH` | `/api/v1/trade/orders/{orderId:guid}/time-slot` | Authenticated | `UpdateTimeSlot` | Thay đổi khung thời gian hẹn bàn giao giữa Người dân và Cơ sở thu gom (`U-06`, `R-03`). | Authenticated (All) | Đã triển khai |
| **075** | `M-09` | `POST` | `/api/v1/trade/receipts/confirm-qr` | Citizen / Recycler | `ConfirmReceiptQr` | Quét mã QR bảo mật trên biên nhận điện tử để xác nhận hoàn tất giao dịch trực tiếp tại điểm cân (`U-09`, `R-06`). | Authenticated (All) | Đã triển khai |

---

### PHÂN HỆ 10 (`M-10` / `FE-13`): GAMIFICATION, CÂY ẢO & ĐỔI QUÀ THƯỞNG (GAMIFICATION & REWARDS MODULE)
* **Use Cases bao phủ:** `U-13` (Daily Check-in), `U-14` (Redeem Points for Gifts), `U-15` (Upgrade Virtual Tree), `S-09` (Manage Eco Fact Content), `S-10` (Manage Gift Inventory), `A-07` (Manage Gamification Config), `A-08` (Configure Leaderboard Cycles)
* **Controllers:** `GamificationController` (hiện có), `StaffGamificationController` *(dự kiến)*, `AdminGamificationController` *(dự kiến)*
* **Quy mô:** `27` APIs (2 Đã triển khai, 25 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **076** | `M-10` | `GET` | `/api/v1/gamification/profile/{userId:guid}` | Authenticated | `GetProfile` | Xem hồ sơ Sống Xanh: tổng điểm Mầm Xanh tích lũy, giai đoạn Cây Ảo (`Virtual Tree`) và lượng CO2 giảm thải (`U-13`, `U-15`). | Authenticated (All) | Đã triển khai |
| **077** | `M-10` | `GET` | `/api/v1/gamification/leaderboard` | Public (All) | `GetLeaderboard` | Xem Bảng xếp hạng đóng góp tái chế dẫn đầu theo chu kỳ Tuần/Tháng/Quý (`A-08`, `BR-GAM-07`). | Public / Guest | Đã triển khai |
| **078** | `M-10` | `GET` | `/api/v1/gamification/check-in/status` | Citizen / Recycler | `GetDailyCheckInStatus` | Kiểm tra trạng thái điểm danh trong ngày (múi giờ `UTC+7`), số ngày chuỗi liên tiếp (`streak_days`) và mốc thưởng kế tiếp (`U-13`, `BR-GAM-02`). | Authenticated (All) | Dự kiến |
| **079** | `M-10` | `POST` | `/api/v1/gamification/check-in` | Citizen / Recycler | `PerformDailyCheckIn` | Thực hiện điểm danh hàng ngày để duy trì chuỗi Streak và nhận điểm Mầm Xanh (kiểm tra trần điểm ngày `BR-GAM-03`) (`U-13`). | Authenticated (All) | Dự kiến |
| **080** | `M-10` | `GET` | `/api/v1/gamification/points/ledger` | Citizen / Recycler | `GetPointLedger` | Tra cứu sổ cái lịch sử biến động điểm Mầm Xanh: cộng điểm bán rác, điểm danh, thưởng Top, trừ điểm đổi quà/nâng cấp cây (`U-13`, `U-14`, `U-15`). | Authenticated (All) | Dự kiến |
| **081** | `M-10` | `GET` | `/api/v1/gamification/gifts` | Authenticated | `GetAvailableGifts` | Duyệt kho quà tặng đang mở đổi thưởng (E-Voucher đối tác, sản phẩm thân thiện môi trường), mức điểm yêu cầu và tồn kho (`U-14`). | Authenticated (All) | Dự kiến |
| **082** | `M-10` | `POST` | `/api/v1/gamification/redemptions` | Citizen / Recycler | `RedeemGift` | Đổi điểm Mầm Xanh lấy E-Voucher hoặc quà hiện vật (khóa tồn kho nguyên tử chống tranh chấp `BR-GAM-06`, kiểm tra số dư `BR-GAM-05`) (`U-14`, `BR-GAM-08`). | Authenticated (All) | Dự kiến |
| **083** | `M-10` | `GET` | `/api/v1/gamification/redemptions/me` | Citizen / Recycler | `GetMyRedemptions` | Xem lịch sử đổi quà cá nhân, tra cứu mã E-Voucher đã cấp hoặc tiến độ giao nhận quà hiện vật (`U-14`). | Authenticated (All) | Dự kiến |
| **084** | `M-10` | `GET` | `/api/v1/gamification/tree/me` | Citizen (User) | `GetMyVirtualTree` | Xem chi tiết Cây Ảo cá nhân: cấp độ sinh trưởng hiện tại, tổng điểm đã tưới và số điểm cần để lên giai đoạn kế tiếp (`U-15`). | Citizen (User) | Dự kiến |
| **085** | `M-10` | `POST` | `/api/v1/gamification/tree/upgrade` | Citizen (User) | `UpgradeVirtualTree` | Phân bổ điểm thưởng nuôi dưỡng Cây Ảo lên giai đoạn phát triển mới và tự động mở khóa thẻ kiến thức Eco Fact (`U-15`, `BR-GAM-04`). | Citizen (User) | Dự kiến |
| **086** | `M-10` | `GET` | `/api/v1/gamification/eco-facts/me` | Citizen (User) | `GetUnlockedEcoFacts` | Xem bộ sưu tập các thẻ kiến thức môi trường (`Eco Facts`) đã mở khóa qua từng cột mốc trưởng thành của Cây Ảo (`U-15`). | Citizen (User) | Dự kiến |
| **087** | `M-10` | `GET` | `/api/v1/staff/eco-facts` | Staff / Admin | `GetEcoFacts` | Nhân viên tra cứu danh sách toàn bộ thẻ kiến thức môi trường gắn với các mốc cấp độ Cây Ảo (`S-09`). | Staff / Admin | Dự kiến |
| **088** | `M-10` | `POST` | `/api/v1/staff/eco-facts` | Staff / Admin | `CreateEcoFact` | Nhân viên biên soạn thẻ Eco Fact mới, gán mốc cấp độ Cây Ảo mở khóa và đính kèm hình minh họa (`S-09`). | Staff / Admin | Dự kiến |
| **089** | `M-10` | `PUT` | `/api/v1/staff/eco-facts/{factId:guid}` | Staff / Admin | `UpdateEcoFact` | Nhân viên chỉnh sửa nội dung thông điệp xanh, điều chỉnh mốc cấp độ cây hoặc trạng thái hiển thị của Eco Fact (`S-09`). | Staff / Admin | Dự kiến |
| **090** | `M-10` | `DELETE` | `/api/v1/staff/eco-facts/{factId:guid}` | Staff / Admin | `DeleteEcoFact` | Xóa thẻ Eco Fact nháp chưa phát hành hoặc chưa gắn với lịch sử mở khóa của người dùng (`S-09`). | Staff / Admin | Dự kiến |
| **091** | `M-10` | `GET` | `/api/v1/staff/gifts` | Staff / Admin | `GetGiftInventory` | Nhân viên quản lý toàn bộ danh mục kho quà tặng, số lượng voucher khả dụng, tồn kho vật lý và hạn sử dụng (`S-10`). | Staff / Admin | Dự kiến |
| **092** | `M-10` | `POST` | `/api/v1/staff/gifts` | Staff / Admin | `CreateGiftItem` | Nhân viên thêm mới vật phẩm đổi thưởng (E-Voucher / hiện vật xanh), cấu hình điểm quy đổi và số lượng tồn kho (`S-10`). | Staff / Admin | Dự kiến |
| **093** | `M-10` | `PUT` | `/api/v1/staff/gifts/{giftId:guid}` | Staff / Admin | `UpdateGiftItem` | Nhân viên cập nhật thông tin mô tả quà tặng, nhập bổ sung tồn kho vật lý hoặc điều chỉnh mức điểm đổi (`S-10`). | Staff / Admin | Dự kiến |
| **094** | `M-10` | `PATCH` | `/api/v1/staff/gifts/{giftId:guid}/status` | Staff / Admin | `ToggleGiftStatus` | Bật mở đổi thưởng hoặc tạm ẩn vật phẩm quà tặng khi hết hàng / hết hạn chương trình (`S-10`). | Staff / Admin | Dự kiến |
| **095** | `M-10` | `POST` | `/api/v1/staff/gifts/{giftId:guid}/vouchers/import` | Staff / Admin | `ImportVoucherCodes` | Nhập hàng loạt (Batch Import CSV/JSON) danh sách mã E-Voucher độc nhất vào kho phát thưởng tự động (`S-10`). | Staff / Admin | Dự kiến |
| **096** | `M-10` | `GET` | `/api/v1/staff/redemptions` | Staff / Admin | `GetRedemptionRequests` | Nhân viên tra cứu danh sách đơn đổi quà hiện vật của người dùng đang chờ đóng gói và bàn giao (`S-10`). | Staff / Admin | Dự kiến |
| **097** | `M-10` | `PATCH` | `/api/v1/staff/redemptions/{redemptionId:guid}/fulfill` | Staff / Admin | `FulfillRedemption` | Nhân viên xác nhận hoàn tất giao quà hiện vật (`Fulfilled`) hoặc hủy đơn kèm hoàn trả điểm nguyên vẹn (`S-10`). | Staff / Admin | Dự kiến |
| **098** | `M-10` | `GET` | `/api/v1/admin/gamification/config` | Admin | `GetGamificationConfig` | Quản trị viên xem cấu hình quy tắc Gamification: hệ số điểm/kg rác, trần điểm ngày, hệ số nhân Streak, ngưỡng cấp Cây Ảo (`A-07`). | Staff / Admin | Dự kiến |
| **099** | `M-10` | `PUT` | `/api/v1/admin/gamification/config` | Admin | `UpdateGamificationConfig` | Quản trị viên cập nhật tỷ lệ quy đổi điểm, giới hạn điểm/ngày (`BR-GAM-03`) và bảng ngưỡng điểm nâng cấp Cây Ảo (`A-07`, `BR-GAM-01`). | Staff / Admin | Dự kiến |
| **100** | `M-10` | `GET` | `/api/v1/admin/gamification/leaderboard-cycles` | Admin | `GetLeaderboardCycles` | Quản trị viên xem danh sách các chu kỳ thi đua Bảng xếp hạng (Tuần/Tháng) và cơ cấu giải thưởng điểm (`A-08`). | Staff / Admin | Dự kiến |
| **101** | `M-10` | `POST` | `/api/v1/admin/gamification/leaderboard-cycles` | Admin | `CreateLeaderboardCycle` | Quản trị viên khởi tạo chu kỳ Bảng xếp hạng mới, định nghĩa khung thời gian và mức thưởng điểm cho Top đầu (`A-08`, `BR-GAM-07`). | Staff / Admin | Dự kiến |
| **102** | `M-10` | `POST` | `/api/v1/admin/gamification/leaderboard-cycles/{cycleId:guid}/finalize` | Admin | `FinalizeLeaderboardCycle` | Chốt bảng tổng sắp cuối kỳ thi đua và tự động cộng điểm thưởng vinh danh cho các tài khoản đạt thứ hạng cao (`A-08`, `BR-GAM-07`). | Staff / Admin | Dự kiến |

---

### PHÂN HỆ 11 (`M-11` / `FE-10`): ĐÁNH GIÁ UY TÍN & GIẢI QUYẾT KHIẾU NẠI (RATING, REVIEW & COMPLAINT RESOLUTION)
* **Use Cases bao phủ:** `U-10` (Rate Recycler), `U-11` (Submit Complaint), `S-05` (Handle Complaints)
* **Controllers dự kiến:** `ReviewsController`, `ComplaintsController`, `StaffComplaintsController`
* **Quy mô:** `12` APIs (0 Đã triển khai, 12 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **103** | `M-11` | `POST` | `/api/v1/handover-requests/{requestId:guid}/reviews` | Citizen (User) | `CreateReview` | Người dân gửi đánh giá 1–5 sao và nhận xét cho cơ sở thu gom sau khi hoàn tất bàn giao (1 đánh giá duy nhất/đơn theo `BR-RVW-01`) (`U-10`). | Citizen (User) | Dự kiến |
| **104** | `M-11` | `GET` | `/api/v1/recyclers/{recyclerId:guid}/reviews` | Public (All) | `GetRecyclerReviews` | Xem danh sách nhận xét công khai, thống kê phân bố 1–5 sao và điểm uy tín trung bình của cơ sở thu gom (`U-10`, `BR-RVW-04`). | Public / Guest | Dự kiến |
| **105** | `M-11` | `GET` | `/api/v1/reviews/me` | Citizen (User) | `GetMyReviews` | Người dân tra cứu danh sách các đánh giá chất lượng đã gửi sau các lần giao dịch bán phế liệu (`U-10`). | Citizen (User) | Dự kiến |
| **106** | `M-11` | `DELETE` | `/api/v1/reviews/{reviewId:guid}` | Citizen (User) | `DeleteMyReview` | Người viết tự xóa bài đánh giá của mình và tính lại điểm sao trung bình của vựa (không cho phép sửa sao theo `BR-RVW-02`, `BR-RVW-03`) (`U-10`). | Citizen (User) | Dự kiến |
| **107** | `M-11` | `POST` | `/api/v1/reviews/{reviewId:guid}/report` | Authenticated | `ReportAbusiveReview` | Báo cáo bài đánh giá chứa nội dung xúc phạm, bôi nhọ hoặc spam để Nhân viên vận hành kiểm duyệt (`U-10`, `BR-RVW-03`). | Authenticated (All) | Dự kiến |
| **108** | `M-11` | `PATCH` | `/api/v1/staff/reviews/{reviewId:guid}/moderate` | Staff / Admin | `ModerateReview` | Nhân viên/Admin ẩn hoặc gỡ bỏ đánh giá vi phạm quy tắc cộng đồng kèm ghi chú lý do kiểm duyệt (`U-10`, `BR-RVW-03`). | Staff / Admin | Dự kiến |
| **109** | `M-11` | `POST` | `/api/v1/complaints/upload-evidence` | Citizen / Recycler | `UploadComplaintEvidence` | Tải ảnh chụp mặt cân, ảnh hiện trường hoặc biên lai thanh toán lên Cloudflare R2 làm bằng chứng khiếu nại (`U-11`, `BR-DSP-02`). | Authenticated (All) | Dự kiến |
| **110** | `M-11` | `POST` | `/api/v1/complaints` | Citizen / Recycler | `SubmitComplaint` | Người dân hoặc Cơ sở gửi phiếu khiếu nại chính thức (sai lệch cân, thanh toán, bỏ hẹn) trong thời hạn cho phép (`BR-DSP-01`) (`U-11`). | Authenticated (All) | Dự kiến |
| **111** | `M-11` | `GET` | `/api/v1/complaints/me` | Citizen / Recycler | `GetMyComplaints` | Tra cứu danh sách các phiếu khiếu nại đã gửi hoặc bị khiếu nại cùng trạng thái xử lý và kết luận của Staff (`U-11`). | Authenticated (All) | Dự kiến |
| **112** | `M-11` | `GET` | `/api/v1/staff/complaints` | Staff / Admin | `GetComplaintQueue` | Nhân viên xem hàng đợi toàn bộ phiếu khiếu nại tranh chấp (lọc theo trạng thái `Open`, `Investigating`, `Resolved`, loại lỗi) (`S-05`). | Staff / Admin | Dự kiến |
| **113** | `M-11` | `GET` | `/api/v1/staff/complaints/{complaintId:guid}` | Staff / Admin | `GetComplaintDossier` | Nhân viên mở hồ sơ trọng tài: đối chiếu ảnh bằng chứng hai bên, biên nhận cân điện tử và toàn bộ lịch sử chat bất biến (`S-05`, `BR-DSP-04`). | Staff / Admin | Dự kiến |
| **114** | `M-11` | `PATCH` | `/api/v1/staff/complaints/{complaintId:guid}/resolve` | Staff / Admin | `ResolveComplaint` | Nhân viên ghi nhận biên bản phân xử chính thức, chốt kết quả giao dịch/điểm thưởng và cộng mốc cảnh cáo nếu có vi phạm (`S-05`, `BR-DSP-05`, `BR-DSP-06`). | Staff / Admin | Dự kiến |

---

### PHÂN HỆ 12 (`M-12` / `FE-14`): GÓI DỊCH VỤ NỘI BỘ & TÍCH HỢP VIETQR (IN-SYSTEM SERVICE PACKAGES & VIETQR)
* **Use Cases bao phủ:** `U-16` (Citizen Purchase In-System Services via VietQR), `R-07` (Recycler Purchase In-System Services via VietQR), `A-10` (Configure In-System Service Fees), `A-11` (Manage Financial Reconciliation)
* **Controllers:** `ServicePaymentController` (hiện có), `ServicePackagesController` *(dự kiến)*, `AdminReconciliationController` *(dự kiến)*
* **Quy mô:** `15` APIs (5 Đã triển khai, 10 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **115** | `M-12` | `POST` | `/api/v1/service-payments/create` | Citizen / Recycler | `CreateServicePayment` | Khởi tạo đơn mua gói dịch vụ nội bộ (Citizen `U-16`: Ad-Free, EcoPlus; Recycler `R-07`: Tích xanh uy tín, Ghim nổi bật bản đồ) và sinh mã VietQR động (`BR-PAY-01`). | Authenticated (All) | Đã triển khai |
| **116** | `M-12` | `GET` | `/api/v1/service-payments/orders/{orderCode}` | Authenticated | `GetOrderStatus` | Tra cứu trạng thái thanh toán thời gian thực của đơn mua gói dịch vụ (`Pending`, `Paid`, `Failed`, `Cancelled`) (`U-16`, `R-07`). | Authenticated (All) | Đã triển khai |
| **117** | `M-12` | `GET` | `/api/v1/service-payments/orders` | Authenticated | `GetAllOrders` | Xem danh sách toàn bộ hóa đơn thanh toán gói dịch vụ nội bộ của tài khoản (`U-16`, `R-07`). | Authenticated (All) | Đã triển khai |
| **118** | `M-12` | `POST` | `/api/v1/service-payments/webhook` | Public (Gateway) | `ReceivePaymentWebhook` | Webhook tự động nhận thông báo biến động số dư từ cổng VietQR / PayOS, kiểm tra Idempotency và kích hoạt gói dịch vụ (`BR-PAY-02`, `BR-PAY-03`). | Public / Guest | Đã triển khai |
| **119** | `M-12` | `POST` | `/api/v1/service-payments/simulate-transfer/{orderCode}` | Public (Dev/Test) | `SimulateTransfer` | Giả lập sự kiện khách hàng quét mã VietQR chuyển khoản thành công phục vụ kiểm thử tích hợp tự động. | Public / Guest | Đã triển khai |
| **120** | `M-12` | `GET` | `/api/v1/service-packages` | Authenticated | `GetAvailablePackages` | Lấy bảng giá các gói dịch vụ đang mở bán lọc theo đối tượng (`Citizen`: EcoPlus/Ad-Free; `Recycler`: Blue Badge/Featured Marker) (`U-16`, `R-07`). | Authenticated (All) | Dự kiến |
| **121** | `M-12` | `GET` | `/api/v1/service-packages/subscriptions/me` | Citizen / Recycler | `GetMySubscriptions` | Xem các gói đặc quyền đang hoạt động của tài khoản, thời hạn còn lại và lịch sử gia hạn cộng dồn (`U-16`, `R-07`, `BR-PAY-04`). | Authenticated (All) | Dự kiến |
| **122** | `M-12` | `POST` | `/api/v1/service-payments/orders/{orderCode}/cancel` | Citizen / Recycler | `CancelPendingOrder` | Hủy mã đơn thanh toán VietQR đang chờ chuyển khoản (`Pending`) khi người dùng đổi ý chọn gói dịch vụ khác (`U-16`, `R-07`). | Authenticated (All) | Dự kiến |
| **123** | `M-12` | `GET` | `/api/v1/admin/service-packages` | Admin | `GetAllServicePackages` | Quản trị viên tra cứu toàn bộ danh mục gói dịch vụ thương mại, biểu phí, thời hạn và số lượng thuê bao đang đăng ký (`A-10`). | Staff / Admin | Dự kiến |
| **124** | `M-12` | `POST` | `/api/v1/admin/service-packages` | Admin | `CreateServicePackage` | Quản trị viên tạo gói dịch vụ mới: tên gói, vai trò áp dụng, mức phí (VNĐ), số ngày hiệu lực và danh sách đặc quyền (`A-10`, `BR-PAY-05`). | Staff / Admin | Dự kiến |
| **125** | `M-12` | `PUT` | `/api/v1/admin/service-packages/{packageId:guid}` | Admin | `UpdateServicePackage` | Quản trị viên cập nhật đơn giá và chu kỳ gói dịch vụ cho các lượt đăng ký mới (giữ nguyên gói đang hiệu lực theo `BR-PAY-05`) (`A-10`). | Staff / Admin | Dự kiến |
| **126** | `M-12` | `PATCH` | `/api/v1/admin/service-packages/{packageId:guid}/status` | Admin | `TogglePackageStatus` | Mở bán công khai hoặc ngừng cung cấp gói dịch vụ trên giao diện nâng cấp tài khoản (`A-10`). | Staff / Admin | Dự kiến |
| **127** | `M-12` | `GET` | `/api/v1/admin/reconciliations/ledger` | Admin | `GetPaymentLedger` | Quản trị viên theo dõi sổ cái dòng tiền VietQR vào hệ thống, lọc các giao dịch lệch tiền hoặc sai nội dung chuyển khoản (`Anomaly`) (`A-11`, `BR-PAY-06`). | Staff / Admin | Dự kiến |
| **128** | `M-12` | `PATCH` | `/api/v1/admin/reconciliations/orders/{orderCode}/resolve` | Admin | `ResolvePaymentAnomaly` | Đối soát thủ công giao dịch VietQR bất thường: xác nhận khớp lệnh bù hoặc ghi nhận hoàn tiền kèm mã giao dịch ngân hàng (`A-11`, `BR-PAY-06`). | Staff / Admin | Dự kiến |
| **129** | `M-12` | `GET` | `/api/v1/admin/reconciliations/export` | Admin | `ExportReconciliationReport` | Xuất bảng kê đối soát doanh thu dịch vụ VietQR theo kỳ kế toán (CSV/XLSX) (`A-11`). | Staff / Admin | Dự kiến |

---

### PHÂN HỆ 13 (`M-13` / `FE-15`): PHẢN HỒI LỖI AI & QUẢN TRỊ BỘ DỮ LIỆU HUẤN LUYỆN (AI MISCLASSIFICATION FEEDBACK & DATASET CURATION)
* **Use Cases bao phủ:** `U-04` (Report Incorrect AI Classification – phối hợp cùng endpoint `STT 024` ở `M-03`), `S-01` (Review Misclassified Images), `S-02` (Label Image Data), `S-03` (Update Verified Dataset)
* **Controllers dự kiến:** `StaffAiFeedbackController`, `StaffDatasetCurationController`
* **Quy mô:** `10` APIs tại Core .NET (0 Đã triển khai, 10 Dự kiến; phối hợp cùng `2` API nội bộ mới tại `AIS`)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **130** | `M-13` | `GET` | `/api/v1/waste-scan/feedback/me` | Citizen (User) | `GetMyFeedbackReports` | Người dân xem lại lịch sử các báo cáo nhận diện sai AI đã đóng góp và kết quả xác minh của đội ngũ vận hành (`U-04`). | Citizen (User) | Dự kiến |
| **131** | `M-13` | `GET` | `/api/v1/staff/ai-feedback` | Staff / Admin | `GetMisclassificationQueue` | Nhân viên xem hàng đợi ảnh rác bị báo nhận diện nhầm (lọc theo nhãn dự đoán sai, nhãn người dùng đề xuất, độ tin cậy) (`S-01`). | Staff / Admin | Dự kiến |
| **132** | `M-13` | `GET` | `/api/v1/staff/ai-feedback/{reportId:guid}` | Staff / Admin | `GetMisclassificationDetail` | Xem chi tiết mẫu ảnh nhận diện lỗi: ảnh gốc trên R2, phân phối xác suất của AI, nhãn đề xuất và metadata thiết bị chụp (`S-01`). | Staff / Admin | Dự kiến |
| **133** | `M-13` | `PATCH` | `/api/v1/staff/ai-feedback/{reportId:guid}/review` | Staff / Admin | `ReviewMisclassifiedImage` | Nhân viên thẩm định ảnh báo lỗi: chấp nhận đưa vào kho chờ gán nhãn chuẩn hoặc loại bỏ ảnh mờ/ngoài phạm vi (`Rejected`) (`S-01`, `BR-CLS-06`). | Staff / Admin | Dự kiến |
| **134** | `M-13` | `GET` | `/api/v1/staff/ai-labeling/queue` | Staff / Admin | `GetUnlabeledImages` | Lấy danh sách ảnh thô và ảnh báo lỗi đã duyệt đang chờ Nhân viên gán nhãn thực tế chuẩn (`Ground-Truth`) (`S-02`). | Staff / Admin | Dự kiến |
| **135** | `M-13` | `PUT` | `/api/v1/staff/ai-labeling/{itemId:guid}` | Staff / Admin | `AssignGroundTruthLabel` | Nhân viên gán nhãn danh mục phế liệu chuẩn hóa (`MasterWasteCategory`) và tọa độ vùng vật thể (YOLO Bounding Box tùy chọn) (`S-02`, `BR-CLS-06`). | Staff / Admin | Dự kiến |
| **136** | `M-13` | `POST` | `/api/v1/staff/ai-labeling/batch-assign` | Staff / Admin | `BatchAssignLabels` | Gán nhãn chuẩn hàng loạt cho tập ảnh đồng nhất đã được Nhân viên kiểm định chất lượng hình ảnh (`S-02`). | Staff / Admin | Dự kiến |
| **137** | `M-13` | `GET` | `/api/v1/staff/ai-datasets` | Staff / Admin | `GetDatasetVersions` | Xem danh sách các phiên bản bộ dữ liệu chuẩn (`Dataset Versions`), thống kê số lượng mẫu theo từng lớp rác và lịch sử cập nhật (`S-03`). | Staff / Admin | Dự kiến |
| **138** | `M-13` | `POST` | `/api/v1/staff/ai-datasets/versions` | Staff / Admin | `CreateVerifiedDatasetVersion` | Tuyển chọn ảnh sạch đã xác thực nhãn để đóng gói phiên bản Dataset mới (`Train/Val/Test` chống trùng lặp rò rỉ theo `BR-CLS-07`) (`S-03`). | Staff / Admin | Dự kiến |
| **139** | `M-13` | `GET` | `/api/v1/staff/ai-datasets/versions/{versionId:guid}/export` | Staff / Admin | `ExportVerifiedDataset` | Tải xuống gói dữ liệu chuẩn hóa (ZIP/Manifest chuẩn YOLO & ImageFolder) phục vụ huấn luyện lại mô hình AI (`S-03`). | Staff / Admin | Dự kiến |

---

### PHÂN HỆ 14 (`M-14` / `FE-16`): QUẢN TRỊ BÀI VIẾT BLOG & NỘI DUNG TRUYỀN THÔNG (BLOG & CONTENT MANAGEMENT)
* **Use Cases bao phủ:** `G-01` (Browse Public Eco Blogs), `S-08` (Create Blog Post), `A-09` (Approve / Reject Blog Post)
* **Controllers dự kiến:** `BlogsController`, `StaffBlogsController`, `AdminBlogsController`
* **Quy mô:** `10` APIs (0 Đã triển khai, 10 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **140** | `M-14` | `GET` | `/api/v1/blogs` | Public (All) | `GetPublishedBlogs` | Người dân và khách vãng lai xem danh sách bài viết chuyên đề kinh tế tuần hoàn & hướng dẫn phân loại rác đã xuất bản (`G-01`, `S-08`). | Public / Guest | Dự kiến |
| **141** | `M-14` | `GET` | `/api/v1/blogs/{slug}` | Public (All) | `GetPublishedBlogBySlug` | Xem nội dung chi tiết bài viết Blog theo đường dẫn thân thiện (`slug`), danh mục bài viết liên quan và tăng lượt đọc (`G-01`, `S-08`). | Public / Guest | Dự kiến |
| **142** | `M-14` | `POST` | `/api/v1/staff/blogs/upload-media` | Staff / Admin | `UploadBlogMedia` | Nhân viên tải ảnh bìa (`cover_image_url`) và ảnh minh họa nội dung bài viết lên Cloudflare R2 (`S-08`). | Staff / Admin | Dự kiến |
| **143** | `M-14` | `GET` | `/api/v1/staff/blogs` | Staff / Admin | `GetStaffBlogs` | Nhân viên quản lý danh sách bài viết đã tạo theo trạng thái biên tập (`Draft`, `Pending_Approval`, `Published`, `Rejected`) (`S-08`). | Staff / Admin | Dự kiến |
| **144** | `M-14` | `POST` | `/api/v1/staff/blogs` | Staff / Admin | `CreateBlogDraft` | Nhân viên soạn thảo bài viết mới bằng trình biên tập Rich-Text/Markdown và lưu bản nháp (`Draft`) (`S-08`, `BR-ADM-08`). | Staff / Admin | Dự kiến |
| **145** | `M-14` | `PUT` | `/api/v1/staff/blogs/{postId:guid}` | Staff / Admin | `UpdateBlogPost` | Nhân viên chỉnh sửa nội dung, ảnh bìa hoặc từ khóa bài viết đang ở trạng thái bản nháp hoặc bị trả về sửa (`S-08`). | Staff / Admin | Dự kiến |
| **146** | `M-14` | `DELETE` | `/api/v1/staff/blogs/{postId:guid}` | Staff / Admin | `DeleteBlogDraft` | Nhân viên xóa bài viết bản nháp chưa xuất bản khỏi hệ thống (`S-08`). | Staff / Admin | Dự kiến |
| **147** | `M-14` | `POST` | `/api/v1/staff/blogs/{postId:guid}/submit` | Staff / Admin | `SubmitBlogForApproval` | Nhân viên gửi bài viết hoàn chỉnh lên hàng đợi biên tập chờ Quản trị viên phê duyệt (`Draft` → `Pending_Approval`) (`S-08`, `BR-ADM-08`). | Staff / Admin | Dự kiến |
| **148** | `M-14` | `GET` | `/api/v1/admin/blogs/pending` | Admin | `GetPendingBlogs` | Quản trị viên xem hàng đợi các bài viết Blog do Nhân viên gửi lên đang chờ xét duyệt xuất bản (`A-09`). | Staff / Admin | Dự kiến |
| **149** | `M-14` | `PATCH` | `/api/v1/admin/blogs/{postId:guid}/review` | Admin | `ReviewBlogPost` | Quản trị viên phê duyệt đăng công khai (`Published`) hoặc từ chối trả về (`Rejected`) kèm nhận xét biên tập bắt buộc (`A-09`, `BR-ADM-08`). | Staff / Admin | Dự kiến |

---

### PHÂN HỆ 15 (`M-15` / `FE-17`, `FE-18`): QUẢN TRỊ TÀI KHOẢN, NHÂN SỰ, THÔNG BÁO & PHÂN TÍCH HỆ THỐNG (GOVERNANCE, STAFF, NOTIFICATIONS & ANALYTICS)
* **Use Cases bao phủ:** `A-01` (View Dashboard & Analytics), `A-02` (Export Data Reports & Audit Log), `S-06` (View User & Recycler Accounts), `S-07` (Lock / Delete Accounts), `A-03` (Manage Staff Accounts), `A-04` (Manage Staff Permissions), `S-11` (Staff Daily Check-in), `SU-04` (System In-App Notifications)
* **Controllers & Hubs dự kiến:** `AdminAnalyticsController`, `StaffAccountsController`, `AdminStaffController`, `StaffAttendanceController`, `NotificationsController`, `NotificationHub` (`/hubs/notifications`)
* **Quy mô:** `23` APIs = `22` REST + `1` WS Hub (0 Đã triển khai, 23 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **150** | `M-15` | `GET` | `/api/v1/admin/analytics/kpi-summary` | Admin | `GetExecutiveKpiSummary` | Hiển thị bảng chỉ số KPI điều hành: tổng sản lượng phế liệu tái chế (kg/tấn), độ chính xác mô hình AI, người dùng tích cực và doanh thu VietQR (`A-01`, `BR-ADM-07`). | Staff / Admin | Dự kiến |
| **151** | `M-15` | `GET` | `/api/v1/admin/analytics/charts` | Admin | `GetAnalyticsCharts` | Truy vấn dữ liệu biểu đồ thống kê theo kỳ: tỷ trọng từng nhóm phế liệu, xu hướng giao dịch khu vực và tỷ lệ lỗi nhận diện AI (`A-01`, `BR-ADM-07`). | Staff / Admin | Dự kiến |
| **152** | `M-15` | `GET` | `/api/v1/admin/reports/export` | Admin | `ExportSystemReport` | Trích xuất báo cáo tổng hợp giao dịch, sản lượng tái chế và vận hành ra tệp CSV/XLSX (ẩn dữ liệu nhạy cảm theo `BR-ADM-06`) (`A-02`). | Staff / Admin | Dự kiến |
| **153** | `M-15` | `GET` | `/api/v1/admin/audit-logs` | Admin | `GetSystemAuditLogs` | Tra cứu nhật ký kiểm toán hệ thống bất biến (`Audit Trail` chỉ đọc theo `BR-ADM-05`): lịch sử đổi cấu hình, phân quyền, khóa tài khoản, đối soát (`A-02`). | Staff / Admin | Dự kiến |
| **154** | `M-15` | `GET` | `/api/v1/staff/accounts` | Staff / Admin | `GetAccounts` | Nhân viên xem danh sách tài khoản Người dân và Cơ sở thu gom (lọc vai trò, trạng thái hoạt động, số lần cảnh cáo vi phạm) (`S-06`). | Staff / Admin | Dự kiến |
| **155** | `M-15` | `GET` | `/api/v1/staff/accounts/{userId:guid}` | Staff / Admin | `GetAccountDetail` | Xem chi tiết hồ sơ tài khoản, thống kê hoạt động giao dịch, lịch sử khiếu nại và nhật ký bị cảnh cáo/khóa trước đó (`S-06`). | Staff / Admin | Dự kiến |
| **156** | `M-15` | `PATCH` | `/api/v1/staff/accounts/{userId:guid}/lock` | Staff / Admin | `LockOrUnlockAccount` | Tạm khóa (`Suspended`) hoặc mở khóa tài khoản vi phạm chính sách kèm lý do bắt buộc và thu hồi ngay phiên đăng nhập hiện hành (`S-07`, `BR-ADM-03`). | Staff / Admin | Dự kiến |
| **157** | `M-15` | `DELETE` | `/api/v1/staff/accounts/{userId:guid}` | Staff / Admin | `DeactivateAccount` | Vô hiệu hóa mềm (`Soft-Deactivate`) tài khoản vi phạm nghiêm trọng kèm biên bản lý do, giữ nguyên vẹn lịch sử biên nhận cũ (`S-07`, `BR-ADM-04`). | Staff / Admin | Dự kiến |
| **158** | `M-15` | `GET` | `/api/v1/admin/staff` | Admin | `GetStaffAccounts` | Quản trị viên tra cứu danh sách tài khoản Nhân viên vận hành (`Staff`), trạng thái công tác và danh sách quyền hiện có (`A-03`). | Staff / Admin | Dự kiến |
| **159** | `M-15` | `POST` | `/api/v1/admin/staff` | Admin | `CreateStaffAccount` | Quản trị viên tạo mới tài khoản Nhân viên vận hành nội bộ (chỉ Admin được phép tạo theo quy tắc bảo mật `BR-ADM-01`) (`A-03`). | Staff / Admin | Dự kiến |
| **160** | `M-15` | `PUT` | `/api/v1/admin/staff/{staffId:guid}` | Admin | `UpdateStaffAccount` | Quản trị viên cập nhật thông tin nhân sự, bộ phận chuyên trách hoặc cấp lại mật khẩu tạm cho Nhân viên (`A-03`). | Staff / Admin | Dự kiến |
| **161** | `M-15` | `PATCH` | `/api/v1/admin/staff/{staffId:guid}/status` | Admin | `UpdateStaffStatus` | Đình chỉ hoặc ngừng kích hoạt tài khoản Nhân viên chuyển công tác/nghỉ việc và hủy toàn bộ phiên truy cập (`A-03`, `BR-ADM-03`). | Staff / Admin | Dự kiến |
| **162** | `M-15` | `GET` | `/api/v1/admin/permissions/catalog` | Admin | `GetPermissionCatalog` | Lấy danh mục chuẩn toàn bộ mã quyền RBAC nghiệp vụ (`VERIFY_RECYCLER`, `HANDLE_COMPLAINT`, `CURATE_DATASET`, `MANAGE_REWARDS`, `MANAGE_BLOG`...) (`A-04`). | Staff / Admin | Dự kiến |
| **163** | `M-15` | `GET` | `/api/v1/admin/staff/{staffId:guid}/permissions` | Admin | `GetStaffPermissions` | Xem chi tiết tập quyền RBAC hiện đang được gán cho một tài khoản Nhân viên cụ thể (`A-04`). | Staff / Admin | Dự kiến |
| **164** | `M-15` | `PUT` | `/api/v1/admin/staff/{staffId:guid}/permissions` | Admin | `UpdateStaffPermissions` | Cấu hình cấp mới hoặc thu hồi các mã quyền RBAC cho Nhân viên và áp dụng kiểm soát truy cập tức thời (`A-04`, `BR-ADM-02`). | Staff / Admin | Dự kiến |
| **165** | `M-15` | `POST` | `/api/v1/staff/attendance/check-in` | Staff | `CheckInShift` | Nhân viên chấm công bắt đầu ca làm việc hàng ngày để ghi nhận chuyên cần (không sinh điểm thưởng theo `BR-GAM-09`) (`S-11`). | Staff / Admin | Dự kiến |
| **166** | `M-15` | `POST` | `/api/v1/staff/attendance/check-out` | Staff | `CheckOutShift` | Nhân viên chấm công kết thúc ca trực vận hành và lưu ghi chú bàn giao ca (`S-11`). | Staff / Admin | Dự kiến |
| **167** | `M-15` | `GET` | `/api/v1/staff/attendance/me` | Staff | `GetMyAttendanceLogs` | Nhân viên tra cứu bảng chấm công cá nhân và lịch sử các ca trực trong tháng (`S-11`). | Staff / Admin | Dự kiến |
| **168** | `M-15` | `GET` | `/api/v1/admin/staff/attendance` | Admin | `GetAllStaffAttendance` | Quản trị viên theo dõi báo cáo chuyên cần và nhật ký ca trực hàng ngày của toàn bộ đội ngũ Nhân viên (`S-11`, `A-03`). | Staff / Admin | Dự kiến |
| **169** | `M-15` | `GET` | `/api/v1/notifications` | Authenticated | `GetMyNotifications` | Lấy danh sách thông báo hệ thống của người dùng (đơn thu gom, biên nhận, phán quyết khiếu nại, duyệt hồ sơ, thanh toán VietQR) (`SU-04`). | Authenticated (All) | Dự kiến |
| **170** | `M-15` | `PATCH` | `/api/v1/notifications/{notificationId:guid}/read` | Authenticated | `MarkNotificationRead` | Đánh dấu một thông báo cụ thể là đã đọc (`is_read = true`) (`SU-04`). | Authenticated (All) | Dự kiến |
| **171** | `M-15` | `PUT` | `/api/v1/notifications/read-all` | Authenticated | `MarkAllNotificationsRead` | Đánh dấu toàn bộ thông báo chưa đọc của tài khoản hiện tại là đã xem (`SU-04`). | Authenticated (All) | Dự kiến |
| **172** | `M-15` | `WS` | `/hubs/notifications` | Authenticated | `NotificationHub` | Kênh WebSocket SignalR đẩy thông báo thời gian thực (`ReceiveNotification`) ngay khi phát sinh sự kiện nghiệp vụ (`SU-04`, `EI-07`). | Authenticated (All) | Dự kiến |

---

### HẠ TẦNG HỆ THỐNG & KIỂM TRA SỨC KHỎE (`SYS`: SYSTEM INFRASTRUCTURE & HEALTH)
* **Host:** `Ecolink.WebApi` | **Entry:** `Program.cs` | **Quy mô:** `1` API (1 Đã triển khai, 0 Dự kiến)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **173** | `SYS` | `GET` | `/health` | Public (All) | `Minimal API` | Kiểm tra trạng thái sống của máy chủ WebApi (.NET 10), kết nối CSDL PostgreSQL/PostGIS và bộ nhớ đệm. | Public / Guest | Đã triển khai |

---

### DỊCH VỤ THỊ GIÁC MÁY TÍNH CHUYÊN BIỆT (`AIS`: DEDICATED AI VISION SERVICE – FASTAPI PYTHON)
* **Dịch vụ:** Dedicated AI Vision Microservice (`http://127.0.0.1:8000`) | **Tệp mã nguồn:** [`app.py`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/app.py)
* **Quy mô:** `11` APIs (9 Đã triển khai, 2 Dự kiến phục vụ `FE-15` Dataset Sync & Model Hot-Reload)

| STT | Mã Modul | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Action / Function | Mô tả chức năng & Use Case nghiệp vụ | Nhóm RBAC | Trạng thái |
|:---:|:---:|:---:|:---|:---|:---|:---|:---|:---:|
| **174** | `AIS` | `GET` | `/api/models` | Public (Internal) | `get_models` | Danh sách mô hình thị giác máy tính đang nạp trên RAM (`yolov8_detect`, `mobilenet_v3`, `efficientnet_b0`) (`U-01`). | Public / Guest | Đã triển khai |
| **175** | `AIS` | `POST` | `/api/classify` | Public (Internal) | `classify_waste` | Nhận diện ảnh rác bằng mô hình chỉ định, tính điểm tin cậy, kiểm tra ngưỡng OOD và trả gợi ý phân loại (`U-01`, `U-02`). | Public / Guest | Đã triển khai |
| **176** | `AIS` | `GET` | `/api/sample-images` | Public (Internal) | `get_sample_images` | Lấy danh sách ảnh mẫu chuẩn trong tập kiểm thử để người dùng kiểm tra nhanh khả năng nhận diện (`U-01`). | Public / Guest | Đã triển khai |
| **177** | `AIS` | `GET` | `/api/rules` | Public (Internal) | `get_rules` | Lấy tập quy tắc phân loại hiện hành từ cấu hình Rule Engine của AI Service (`U-03`, `A-06`). | Public / Guest | Đã triển khai |
| **178** | `AIS` | `POST` | `/api/rules` | Public (Internal) | `update_rules` | Đồng bộ hoặc cập nhật quy tắc hướng dẫn xử lý rác từ Core Backend sang AI Service (`A-06`). | Public / Guest | Đã triển khai |
| **179** | `AIS` | `POST` | `/api/feedback` | Public (Internal) | `submit_feedback` | Tiếp nhận mẫu ảnh và nhãn gợi ý khi mô hình nhận diện nhầm để lưu vào vùng đệm chờ kiểm duyệt (`U-04`). | Public / Guest | Đã triển khai |
| **180** | `AIS` | `GET` | `/api/feedback` | Public (Internal) | `get_feedback` | Truy xuất danh sách các mẫu phản hồi nhận diện sai đang lưu trên AI Service (`S-01`). | Public / Guest | Đã triển khai |
| **181** | `AIS` | `GET` | `/api/stats` | Public (Internal) | `get_stats` | Thống kê tổng lượt quét, tỷ lệ OOD, phân bố nhóm rác nhận diện và độ trễ suy luận (`A-01`). | Public / Guest | Đã triển khai |
| **182** | `AIS` | `GET` | `/` | Public (Internal) | `root` | Kiểm tra trạng thái hoạt động (Health Check) và thông tin phiên bản của AI Microservice (`SYS`). | Public / Guest | Đã triển khai |
| **183** | `AIS` | `POST` | `/api/datasets/curate` | Public (Internal) | `curate_verified_dataset` | Đồng bộ tập ảnh đã được Staff gán nhãn chuẩn sang cấu trúc thư mục huấn luyện `train/val/test` không rò rỉ mẫu (`S-03`, `BR-CLS-07`). | Public / Guest | Dự kiến |
| **184** | `AIS` | `POST` | `/api/models/reload` | Public (Internal) | `reload_models` | Nạp nóng (`Hot-reload`) trọng số mô hình AI phiên bản mới vào RAM sau khi hoàn tất huấn luyện lại mà không gián đoạn dịch vụ (`S-03`). | Public / Guest | Dự kiến |

---

## 3. HƯỚNG DẪN ĐỒNG BỘ GOOGLE SHEET (`Report_API List_SEP490_G141`) & DUY TRÌ TÀI LIỆU

### 3.1. Công thức tự động hóa cho Google Sheet trên Drive
Khi cập nhật tệp **`Report_API List_SEP490_G141`** gồm 2 sheet `Dashboard` và `API Inventory` (giả sử dữ liệu trong `API Inventory` bắt đầu từ dòng `5` đến dòng `188`, với các cột: `A = STT`, `B = Mã Modul`, `C = Phân hệ / Module`, `D = Phương thức`, `E = Route`, `F = Phân quyền`, `G = Action`, `H = Mô tả`, `I = Nhóm RBAC`, `J = Trạng thái`):

* **Ô Tổng Endpoint (`E2`):** `=COUNTA('API Inventory'!A5:A188)` → Kết quả: **`184`**
* **Ô Core .NET 10 (`F2`):** `=COUNTIF('API Inventory'!B5:B188, "<>AIS")` → Kết quả: **`173`**
* **Ô AI FastAPI (`G2`):** `=COUNTIF('API Inventory'!B5:B188, "AIS")` → Kết quả: **`11`**
* **Ô WebSocket (`H2`):** `=COUNTIF('API Inventory'!D5:D188, "WS")` → Kết quả: **`2`**
* **Đếm số API Đã triển khai theo từng Module (trên Sheet `Dashboard`):**
  `=COUNTIFS('API Inventory'!$B$5:$B$188, A5, 'API Inventory'!$J$5:$J$188, "Đã triển khai")`
* **Đếm số API Dự kiến theo từng Module (trên Sheet `Dashboard`):**
  `=COUNTIFS('API Inventory'!$B$5:$B$188, A5, 'API Inventory'!$J$5:$J$188, "Dự kiến")`

### 3.2. Quy trình 4 bước cập nhật khi triển khai code các Module dự kiến (`M-10` → `M-15`)
1. **Bước 1 – Triển khai Controller & XML Swagger Doc:** Giữ nguyên đường dẫn Route và HTTP Method đã quy hoạch ở Mục 2 (hoặc cập nhật lại nếu thay đổi thiết kế chi tiết), gắn đầy đủ `[Authorize(Roles = "...")]` và `[ProducesResponseType]`.
2. **Bước 2 – Chạy Script quét đối chiếu tự động:**
   ```powershell
   powershell -ExecutionPolicy Bypass -File scripts/scan_api_inventory.ps1
   ```
3. **Bước 3 – Chuyển trạng thái Endpoint:** Đổi cột `Trạng thái` của các dòng tương ứng từ `Dự kiến` sang `Đã triển khai`, đồng thời cập nhật lại số liệu cột `Đã code` / `Dự kiến` / `Tiến độ` tại **Mục 1.1**, **1.2**, **1.3**.
4. **Bước 4 – Bổ sung đặc tả Payload chi tiết:** Tạo tệp đặc tả JSON Request/Response tương ứng trong thư mục `documents/api document/` (ví dụ: `10_module_gamification_and_rewards_api.md` đến `15_module_governance_and_analytics_api.md`).
