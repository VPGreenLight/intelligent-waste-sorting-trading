# BẢNG TỔNG HỢP & THỐNG KÊ SỐ LƯỢNG API TOÀN BỘ HỆ THỐNG
## (API INVENTORY & STATISTICAL DASHBOARD)

**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phiên bản tài liệu:** `v1.2.0` (Cập nhật sau khi hoàn thành Module 08 & đồng bộ Migration CSDL)  
**Thời điểm cập nhật:** 09/11/2026  
**Công nghệ Backend:** ASP.NET Core 10 WebApi (Clean Architecture, Npgsql PostGIS, SignalR WebSocket)  
**Công nghệ AI Service:** Python 3.11 FastAPI (YOLOv8 + MobileNetV3 + EfficientNet)  
**Tình trạng tài liệu:** **Tài liệu sống (Living Document)** – Được rà soát, đối chiếu và cập nhật liên tục sau mỗi chu kỳ phát triển phân hệ hoặc hoàn thiện chức năng mới.

---

## 1. BẢNG TỔNG HỢP SỐ LƯỢNG API (EXECUTIVE SUMMARY DASHBOARD)

Tính đến phiên bản hiện tại, toàn bộ hệ sinh thái **EcoLink** có tổng cộng **`86 Endpoint`**, bao gồm:
* **76 REST Endpoints** thuộc máy chủ nghiệp vụ chính Core Backend (`Ecolink.WebApi`).
* **1 SignalR WebSocket Hub** phục vụ đàm phán trực tiếp thời gian thực (`/hubs/negotiation-chat`).
* **9 REST Endpoints** thuộc máy chủ thị giác máy tính Dedicated AI Vision Service (`FastAPI`).

```
+------------------------------------------------------------------------------------+
|                         TỔNG SỐ LƯỢNG API TOÀN HỆ THỐNG                           |
|                                    86 ENDPOINTS                                    |
+------------------------------------------+-----------------------------------------+
|     Core .NET 10 WebApi: 77 Endpoints    |    Dedicated AI Service: 9 Endpoints   |
|   (76 REST APIs + 1 SignalR WebSockets)  |           (FastAPI Python 3.11)         |
+------------------------------------------+-----------------------------------------+
```

---

### 1.1. Thống kê số lượng API theo Module nghiệp vụ

| Mã Module | Tên Module / Phân hệ nghiệp vụ | Controller / Service phụ trách | REST | WS Hub | Tổng API | Tỷ trọng | Trạng thái phát triển |
|:---:|:---|:---|:---:|:---:|:---:|:---:|:---:|
| **M-01** | **Authentication & Account** | `AuthController` | 14 | 0 | **14** | 16.3% | Đã hoàn thiện 100% |
| **M-02** | **Recycler Onboarding & Verification** | `RecyclerApplicationsController` | 6 | 0 | **6** | 7.0% | Đã hoàn thiện 100% |
| **M-03** | **AI Waste Classification & Rule Engine** | `WasteScanController`, `WasteCategoryController` | 5 | 0 | **5** | 5.8% | Đã hoàn thiện 100% |
| **M-04** | **Recycler Profile, Pricing & Availability**| `RecyclerController` | 9 | 0 | **9** | 10.5% | Đã hoàn thiện 100% |
| **M-05** | **Location-Based Recycler Discovery** | `RecyclersDiscoveryController`, `GeocodingController`, `TradeController` | 4 | 0 | **4** | 4.7% | Đã hoàn thiện 100% |
| **M-06** | **Handover Request Management** | `HandoverRequestController`, `RecyclerHandoverController` | 10 | 0 | **10** | 11.6% | Đã hoàn thiện 100% |
| **M-07** | **Handover Completion, Receipt & History**| `RecyclerReceiptController`, `HandoverReceiptController`, `TransactionControllers` | 8 | 0 | **8** | 9.3% | Đã hoàn thiện 100% |
| **M-08** | **In-App Real-Time Negotiation Chatbox** | `NegotiationChatController`, `NegotiationChatHub` | 7 | 1 | **8** | 9.3% | Đã hoàn thiện 100% |
| **M-09** | **Orders, Gamification & Service Payment**| `TradeController`, `GamificationController`, `ServicePaymentController` | 12 | 0 | **12** | 14.0% | Đã hoàn thiện 100% |
| **SYS** | **System Infrastructure & Health** | Minimal API (`Program.cs`) | 1 | 0 | **1** | 1.2% | Đã hoàn thiện 100% |
| **AIS** | **Dedicated AI Vision Service (Python)** | FastAPI (`app.py`) | 9 | 0 | **9** | 10.5% | Đã hoàn thiện 100% |
| **TỔNG**| **TOÀN BỘ HỆ THỐNG ECOLINK** | **15 Controllers + 1 Hub + 1 Minimal + 1 AI App** | **85** | **1** | **86** | **100%** | **Sẵn sàng vận hành** |

---

### 1.2. Thống kê số lượng theo Phương thức (HTTP Methods & Protocols)

```
  [GET]     ################################# (41) - 47.7%
  [POST]    ########################### (33) - 38.4%
  [PUT]     ######## (8) - 9.3%
  [PATCH]   ### (3) - 3.5%
  [WS]      # (1) - 1.2%
```

| Phương thức HTTP / Giao thức | Ý nghĩa sử dụng chính trong dự án | Số lượng | Tỷ lệ (%) |
|:---|:---|:---:|:---:|
| **GET** | Truy vấn dữ liệu, tìm kiếm GPS, tra cứu lịch sử, xuất báo cáo, đọc tin nhắn | **41** | 47.7% |
| **POST** | Đăng ký, đăng nhập, nộp đơn, tạo yêu cầu thu gom, phát hành biên nhận, gửi tin nhắn, webhook | **33** | 38.4% |
| **PUT** | Cập nhật hồ sơ, sửa yêu cầu, lưu hàng loạt bảng giá, đánh dấu đã đọc, duyệt đơn | **8** | 9.3% |
| **PATCH** | Bật/tắt trạng thái Online tức thời, cập nhật trạng thái đơn thu gom, sửa khung giờ hẹn | **3** | 3.5% |
| **WebSocket** | Kết nối SignalR song công (Full-duplex) đàm phán và thương lượng giá thời gian thực | **1** | 1.2% |
| **Tổng cộng** | | **86** | **100%** |

---

### 1.3. Thống kê theo Cơ chế Phân quyền Truy cập (Role-Based Access Control)

| Cấp độ quyền truy cập | Đối tượng được phép truy cập | Số lượng API | Ghi chú & Ví dụ tiêu biểu |
|:---|:---|:---:|:---|
| **Public / Guest** | Mọi người dùng (kể cả chưa đăng nhập) | **33** | Đăng ký, đăng nhập, quét rác AI, tìm kiếm điểm thu gom bản đồ, tra cứu quy tắc rác, webhook thanh toán |
| **Citizen (User)** | Người dân đã xác thực tài khoản | **16** | Tạo đơn hẹn thu gom, đàm phán giá chatbox, xác nhận biên nhận, khiếu nại, xem lịch sử giao dịch |
| **Recycler (Collector)** | Đối tác vựa phế liệu đã được phê duyệt | **17** | Cài đặt bảng giá, bật/tắt mở cửa, nhận/từ chối đơn thu gom, phát hành biên nhận cân điện tử |
| **Staff / Admin** | Nhân viên vận hành và Quản trị viên | **3** | Thẩm định hồ sơ mở vựa phế liệu, xét duyệt chứng từ, xử lý khiếu nại tranh chấp |
| **Authenticated (All)** | Mọi người dùng đã đăng nhập hợp lệ | **17** | Đổi mật khẩu, xem hồ sơ cá nhân, tải ảnh lên Cloudflare R2, tra cứu danh mục rác chuẩn |
| **Tổng cộng** | | **86** | |

---

## 2. DANH MỤC CHI TIẾT 100% CÁC API ENDPOINTS TOÀN DỰ ÁN

---

### PHÂN HỆ 01: XÁC THỰC & QUẢN TRỊ TÀI KHOẢN (AUTHENTICATION & ACCOUNT)
* **Tài liệu đặc tả chi tiết:** [`01_module_authentication_api.md`](./01_module_authentication_api.md)
* **Controller:** `AuthController` | **Base Route:** `/api/v1/auth`

| STT | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Tên Action (C#) | Mô tả chức năng & Use Case nghiệp vụ |
|:---:|:---:|:---|:---|:---|:---|
| **01** | `POST` | `/api/v1/auth/register` | Public (Guest) | `Register` | Đăng ký tài khoản mới cho Người dân (`Citizen`), kích hoạt tài khoản ngay. |
| **02** | `POST` | `/api/v1/auth/register-recycler` | Public (Guest) | `RegisterRecycler` | Đăng ký tài khoản cho Cơ sở thu gom, tạo đồng thời hồ sơ cơ sở ở trạng thái `Pending`. |
| **03** | `POST` | `/api/v1/auth/login` | Public (Guest) | `Login` | Đăng nhập tài khoản bằng Username/Password, chống Brute-force khóa sau 5 lần sai. |
| **04** | `GET` | `/api/v1/auth/google-config` | Public (Guest) | `GetGoogleConfig` | Trả về `ClientId` Google OAuth 2.0 để frontend khởi tạo nút đăng nhập Google. |
| **05** | `POST` | `/api/v1/auth/google` | Public (Guest) | `GoogleLogin` | Đăng nhập/Đăng ký nhanh bằng Google ID Token (`email`, `sub`, `name`, `picture`). |
| **06** | `POST` | `/api/v1/auth/refresh-token` | Public (Guest) | `RefreshToken` | Cấp mới Access Token (30 phút) bằng Refresh Token (7 ngày), cơ chế xoay vòng chống Replay Attack. |
| **07** | `POST` | `/api/v1/auth/forgot-password` | Public (Guest) | `ForgotPassword` | Yêu cầu khôi phục mật khẩu, sinh OTP 6 số ngẫu nhiên gửi tới Gmail người dùng qua SMTP. |
| **08** | `POST` | `/api/v1/auth/reset-password` | Public (Guest) | `ResetPassword` | Xác thực mã OTP và cập nhật mật khẩu mới cho tài khoản. |
| **09** | `PUT` | `/api/v1/auth/profile` | Authenticated | `UpdateProfile` | Cập nhật họ tên, số điện thoại, ảnh đại diện (`avatar_url`) của người dùng hiện tại. |
| **10** | `POST` | `/api/v1/auth/change-password` | Authenticated | `ChangePassword` | Đổi mật khẩu tài khoản yêu cầu xác nhận lại mật khẩu cũ. |
| **11** | `POST` | `/api/v1/auth/logout` | Authenticated | `Logout` | Thu hồi Refresh Token hiện tại, xóa phiên đăng nhập trong Redis/CSDL. |
| **12** | `GET` | `/api/v1/auth/me` | Authenticated | `GetCurrentUser` | Lấy đầy đủ thông tin định danh, vai trò, trạng thái hồ sơ của tài khoản đang đăng nhập. |
| **13** | `GET` | `/api/v1/auth/demo-users` | Public (Dev/Test) | `GetDemoUsers` | Danh sách tài khoản thử nghiệm nhanh (Citizen, Recycler, Staff) phục vụ môi trường kiểm thử. |
| **14** | `POST` | `/api/v1/auth/vietqr-token` | Authenticated | `GenerateVietQrToken` | Sinh chữ ký token bảo mật phục vụ tích hợp thanh toán VietQR / PayOS. |

---

### PHÂN HỆ 02: TIẾP NHẬN & XÉT DUYỆT CƠ SỞ THU GOM (RECYCLER ONBOARDING)
* **Tài liệu đặc tả chi tiết:** [`02_module_recycler_onboarding_api.md`](./02_module_recycler_onboarding_api.md)
* **Controller:** `RecyclerApplicationsController` | **Base Route:** `/api/v1/recycler-applications`

| STT | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Tên Action (C#) | Mô tả chức năng & Use Case nghiệp vụ |
|:---:|:---:|:---|:---|:---|:---|
| **15** | `POST` | `/api/v1/recycler-applications/upload-document` | Authenticated | `UploadDocument` | Tải chứng từ kinh doanh / giấy phép môi trường / ảnh cơ sở lên Cloudflare R2 (`multipart/form-data`). |
| **16** | `POST` | `/api/v1/recycler-applications` | Citizen (User) | `SubmitApplication` | Nộp đơn đăng ký mở cơ sở thu mua: thông tin cơ sở, địa chỉ, tọa độ GPS (PostGIS Point), link chứng từ. |
| **17** | `GET` | `/api/v1/recycler-applications/me` | Authenticated | `GetMyApplication` | Người nộp đơn theo dõi tiến độ thẩm định hồ sơ của mình (`Pending`, `Verified`, `Rejected`). |
| **18** | `GET` | `/api/v1/recycler-applications` | Staff / Admin | `GetApplications` | Nhân viên vận hành lấy danh sách các đơn đăng ký cần thẩm định (lọc theo trạng thái, có phân trang). |
| **19** | `GET` | `/api/v1/recycler-applications/{userId:guid}` | Staff / Admin | `GetApplicationDetail` | Xem chi tiết hồ sơ thẩm định của 1 cơ sở kèm hình ảnh chứng từ và thông tin liên hệ. |
| **20** | `PUT` | `/api/v1/recycler-applications/{userId:guid}/verification` | Staff / Admin | `ReviewApplication` | Phê duyệt (`Verified` – nâng cấp vai trò thành Recycler) hoặc Từ chối (`Rejected` kèm lý do chi tiết). |

---

### PHÂN HỆ 03: PHÂN LOẠI RÁC AI & DANH MỤC PHẾ LIỆU (AI WASTE SCAN & CATEGORIES)
* **Tài liệu đặc tả chi tiết:** [`03_module_ai_waste_classification_api.md`](./03_module_ai_waste_classification_api.md)
* **Controllers:** `WasteScanController` (`/api/v1/waste-scan`), `WasteCategoryController` (`/api/v1/waste-categories`)

| STT | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Tên Action (C#) | Mô tả chức năng & Use Case nghiệp vụ |
|:---:|:---:|:---|:---|:---|:---|
| **21** | `POST` | `/api/v1/waste-scan/classify` | Public / Citizen | `ClassifyImage` | Gửi ảnh rác chụp từ camera lên Backend để gọi AI nhận diện, kiểm tra ngưỡng OOD, trả về nhãn & hướng dẫn phân loại. |
| **22** | `GET` | `/api/v1/waste-scan/models` | Public (All) | `GetAvailableModels` | Lấy danh sách các mô hình thị giác máy tính đang sẵn sàng (`yolov8`, `mobilenet_v3`, `efficientnet`). |
| **23** | `GET` | `/api/v1/waste-scan/rules` | Public (All) | `GetAllRules` | Tra cứu toàn bộ tập luật phân loại rác thông minh của Rule Engine (màu thùng, hướng dẫn rửa, mẹo tái chế). |
| **24** | `POST` | `/api/v1/waste-scan/feedback` | Authenticated | `SubmitFeedback` | Người dùng gửi phản hồi khi AI nhận diện nhầm, lưu vết phục vụ fine-tuning mô hình. |
| **25** | `GET` | `/api/v1/waste-categories` | Public / All | `GetAll` | Lấy toàn bộ danh mục loại phế liệu chuẩn hệ thống (Mã, Tên, Màu thùng chuẩn, Icon URL, Cờ có thể tái chế). |

---

### PHÂN HỆ 04: HỒ SƠ CƠ SỞ, BẢNG GIÁ & LỊCH HOẠT ĐỘNG (RECYCLER PROFILE, PRICING & AVAILABILITY)
* **Tài liệu đặc tả chi tiết:** [`04_module_recycler_profile_pricing_availability_api.md`](./04_module_recycler_profile_pricing_availability_api.md)
* **Controller:** `RecyclerController` | **Base Route:** `/api/v1/recycler`

| STT | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Tên Action (C#) | Mô tả chức năng & Use Case nghiệp vụ |
|:---:|:---:|:---|:---|:---|:---|
| **26** | `GET` | `/api/v1/recycler/profile` | Recycler | `GetProfile` | Cơ sở thu gom xem đầy đủ thông tin hồ sơ của vựa (tên, địa chỉ, GPS, giờ mở cửa, số điện thoại, ảnh). |
| **27** | `PUT` | `/api/v1/recycler/profile` | Recycler | `UpdateProfile` | Cập nhật thông tin vận hành của cơ sở (tên cơ sở, hotline liên hệ, giờ hoạt động chuẩn, ảnh biển hiệu). |
| **28** | `GET` | `/api/v1/recycler/prices` | Recycler | `GetPrices` | Xem toàn bộ bảng giá thu mua các loại phế liệu hiện đang được cơ sở cấu hình (`price_per_kg`, `min_kg`). |
| **29** | `POST` | `/api/v1/recycler/prices` | Recycler | `AddPrice` | Thêm đơn giá thu mua mới cho 1 loại phế liệu chưa có trong danh mục của vựa. |
| **30** | `PUT` | `/api/v1/recycler/prices/{priceId:guid}` | Recycler | `UpdatePrice` | Chỉnh sửa đơn giá thu mua, khối lượng cân tối thiểu hoặc kích hoạt/tạm dừng thu mua loại phế liệu. |
| **31** | `PUT` | `/api/v1/recycler/prices` | Recycler | `BulkSavePrices` | Lưu đồng loạt toàn bộ bảng giá thu mua nhiều loại phế liệu trong 1 giao dịch nguyên tử (Atomic). |
| **32** | `GET` | `/api/v1/recycler/availability` | Recycler | `GetAvailability` | Xem trạng thái nhận đơn tức thời (`Online` / `Offline` / `Busy`) và lý do bận tạm thời. |
| **33** | `PATCH`| `/api/v1/recycler/availability` | Recycler | `UpdateAvailability` | Bật/tắt trạng thái nhận đơn ngay lập tức trên bản đồ tìm kiếm (`Online` $\leftrightarrow$ `Offline`). |
| **34** | `PUT` | `/api/v1/recycler/availability/schedule` | Recycler | `UpdateWeeklySchedule` | Thiết lập lịch hoạt động định kỳ theo từng thứ trong tuần (Thứ 2 - Chủ Nhật, giờ mở/đóng cửa). |

---

### PHÂN HỆ 05: KHÁM PHÁ & TÌM KIẾM ĐIỂM THU GOM THEO TỌA ĐỘ (RECYCLER DISCOVERY)
* **Tài liệu đặc tả chi tiết:** [`05_module_location_based_recycler_discovery_api.md`](./05_module_location_based_recycler_discovery_api.md)
* **Controllers:** `RecyclersDiscoveryController`, `GeocodingController`, `TradeController`

| STT | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Tên Action (C#) | Mô tả chức năng & Use Case nghiệp vụ |
|:---:|:---:|:---|:---|:---|:---|
| **35** | `GET` | `/api/v1/recyclers/nearby` | Public / Citizen | `FindNearest` | Tìm kiếm các cơ sở thu gom xung quanh tọa độ GPS trong bán kính $R$ (km) bằng hàm không gian PostGIS, lọc theo loại rác. |
| **36** | `GET` | `/api/v1/recyclers/{recyclerId:guid}` | Public / Citizen | `GetPublicDetail` | Trang hồ sơ công khai của cơ sở: khoảng cách tính theo GPS người dùng, bảng giá niêm yết, lịch mở cửa, cờ nhận đơn. |
| **37** | `GET` | `/api/v1/geocoding/search` | Public (All) | `Search` | Gợi ý địa chỉ và chuyển đổi từ tên địa điểm/đường phố sang tọa độ Latitude/Longitude. |
| **38** | `GET` | `/api/v1/trade/partners/nearby` | Public / Citizen | `GetNearbyPartners` | Tìm các đối tác thu mua phế liệu xung quanh phục vụ khởi tạo luồng giao dịch trực tiếp. |

---

### PHÂN HỆ 06: QUẢN LÝ YÊU CẦU BÀN GIAO PHẾ LIỆU (HANDOVER REQUEST MANAGEMENT)
* **Tài liệu đặc tả chi tiết:** [`06_module_handover_request_management_api.md`](./06_module_handover_request_management_api.md)
* **Controllers:** `HandoverRequestController` (Phía Người dân), `RecyclerHandoverController` (Phía Cơ sở)

| STT | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Tên Action (C#) | Mô tả chức năng & Use Case nghiệp vụ |
|:---:|:---:|:---|:---|:---|:---|
| **39** | `POST` | `/api/v1/handover-requests` | Citizen (User) | `Create` | Người dân tạo yêu cầu hẹn gom rác (`HND-yyyyMMdd-NNNN`): chọn cơ sở, địa chỉ lấy rác, loại rác ước tính, khung giờ đề xuất. |
| **40** | `GET` | `/api/v1/handover-requests` | Citizen (User) | `GetMine` | Người dân xem danh sách các đơn yêu cầu thu gom của mình (lọc theo trạng thái: Pending, Accepted, Completed...). |
| **41** | `GET` | `/api/v1/handover-requests/{id:guid}` | Citizen (User) | `GetById` | Người dân xem chi tiết tiến độ đơn hẹn thu gom và khung giờ đã được hai bên chốt. |
| **42** | `PUT` | `/api/v1/handover-requests/{id:guid}` | Citizen (User) | `Update` | Người dân cập nhật ghi chú hoặc đề xuất khung giờ mới khi đơn vẫn ở trạng thái chờ duyệt. |
| **43** | `POST` | `/api/v1/handover-requests/{id:guid}/cancel` | Citizen (User) | `Cancel` | Người dân hủy yêu cầu hẹn thu gom kèm lý do (kiểm tra quy tắc phạt hủy muộn nếu sát giờ hẹn). |
| **44** | `GET` | `/api/v1/recycler/handover-requests` | Recycler | `GetIncoming` | Cơ sở thu gom xem danh sách các đơn yêu cầu bàn giao gửi đến cơ sở của mình (lọc trạng thái, ngày). |
| **45** | `GET` | `/api/v1/recycler/handover-requests/{id:guid}` | Recycler | `GetById` | Cơ sở xem chi tiết đơn thu gom (ảnh chụp rác, khối lượng ước lượng, địa chỉ thu gom). |
| **46** | `POST` | `/api/v1/recycler/handover-requests/{id:guid}/accept` | Recycler | `Accept` | Cơ sở chấp nhận yêu cầu thu gom, chốt khung giờ hẹn chính thức và mở quyền xem hotline liên hệ. |
| **47** | `POST` | `/api/v1/recycler/handover-requests/{id:guid}/reject` | Recycler | `Reject` | Cơ sở từ chối yêu cầu thu gom (do quá tải, quá xa, hoặc không thu mua loại phế liệu đó). |
| **48** | `POST` | `/api/v1/recycler/handover-requests/{id:guid}/cancel` | Recycler | `Cancel` | Cơ sở hủy đơn thu gom đã nhận trong trường hợp bất khả kháng kèm lý do bắt buộc. |

---

### PHÂN HỆ 07: BIÊN NHẬN SỐ & LỊCH SỬ GIAO DỊCH (HANDOVER COMPLETION, RECEIPT & HISTORY)
* **Tài liệu đặc tả chi tiết:** [`07_module_handover_completion_and_receipt_api.md`](./07_module_handover_completion_and_receipt_api.md)
* **Controllers:** `RecyclerReceiptController`, `HandoverReceiptController`, `TransactionControllers`

| STT | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Tên Action (C#) | Mô tả chức năng & Use Case nghiệp vụ |
|:---:|:---:|:---|:---|:---|:---|
| **49** | `POST` | `/api/v1/recycler/handover-requests/{requestId:guid}/receipt/preview` | Recycler | `Preview` | Cơ sở xem trước biên nhận (tính thử tổng tiền theo đơn giá đã chốt, cảnh báo nếu khối lượng lệch $> 20\%$). |
| **50** | `POST` | `/api/v1/recycler/handover-requests/{requestId:guid}/receipt` | Recycler | `Create` | Cơ sở phát hành biên nhận chính thức (`RCP-yyyy-NNNN`), lưu khối lượng cân thực tế kèm ảnh đồng hồ cân bắt buộc. |
| **51** | `GET` | `/api/v1/recycler/handover-requests/{requestId:guid}/receipt` | Recycler | `Get` | Cơ sở xem lại nội dung biên nhận đã phát hành cho đơn thu gom. |
| **52** | `GET` | `/api/v1/recycler/transactions` | Recycler | `GetHistory` | Cơ sở tra cứu lịch sử toàn bộ các giao dịch đã hoàn tất: tổng khối lượng thu mua, tổng tiền đã chi trả. |
| **53** | `GET` | `/api/v1/handover-requests/{requestId:guid}/receipt` | Citizen (User) | `Get` | Người dân kiểm tra biên nhận do vựa gửi: khối lượng cân thực tế, đơn giá, tổng tiền, ảnh bằng chứng cân. |
| **54** | `POST` | `/api/v1/handover-requests/{requestId:guid}/receipt/confirm` | Citizen (User) | `Confirm` | Người dân xác nhận đã nhận đủ tiền mặt, hoàn tất giao dịch vĩnh viễn và cộng điểm xanh cho cả 2 bên. |
| **55** | `POST` | `/api/v1/handover-requests/{requestId:guid}/receipt/dispute` | Citizen (User) | `Dispute` | Người dân khiếu nại sai lệch biên nhận, chuyển hồ sơ sang trạng thái `Disputed` để Staff thẩm định. |
| **56** | `GET` | `/api/v1/transactions` | Citizen (User) | `GetHistory` | Người dân xem lịch sử giao dịch bán phế liệu: tổng tiền đã nhận, khối lượng rác đã tái chế, điểm tích lũy. |

---

### PHÂN HỆ 08: CHAT THƯƠNG LƯỢNG THỜI GIAN THỰC (REAL-TIME NEGOTIATION CHATBOX)
* **Tài liệu đặc tả chi tiết:** [`08_module_in_app_realtime_negotiation_chatbox_api.md`](./08_module_in_app_realtime_negotiation_chatbox_api.md)
* **Controller:** `NegotiationChatController` (`/api/v1/trade/orders/{orderId}`) | **Hub:** `NegotiationChatHub` (`/hubs/negotiation-chat`)

| STT | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Tên Action (C#) | Mô tả chức năng & Use Case nghiệp vụ |
|:---:|:---:|:---|:---|:---|:---|
| **57** | `GET` | `/api/v1/trade/orders/{orderId:guid}/messages` | Citizen / Recycler | `GetChatThread` | Lấy tổng quan cuộc trò chuyện theo đơn: trạng thái đàm phán, đề xuất hiện hành, danh sách tin nhắn có phân trang. |
| **58** | `POST` | `/api/v1/trade/orders/{orderId:guid}/messages` | Citizen / Recycler | `SendMessage` | Gửi tin nhắn văn bản (`Text`) hoặc hình ảnh (`Image`) vào phòng chat của đơn bàn giao. |
| **59** | `POST` | `/api/v1/trade/orders/{orderId:guid}/messages/attachments` | Citizen / Recycler | `UploadChatAttachment` | Tải ảnh chụp thực tế phế liệu lên Cloudflare R2 để gửi kèm tin nhắn trao đổi. |
| **60** | `POST` | `/api/v1/trade/orders/{orderId:guid}/proposals` | Citizen / Recycler | `SendProposal` | Gửi bản đề xuất / phản đề xuất mới (`Proposal`): đơn giá mới (VND/kg), khối lượng ước lượng, khung giờ hẹn thu gom. |
| **61** | `POST` | `/api/v1/trade/orders/{orderId:guid}/proposals/{messageId:guid}/respond` | Citizen / Recycler | `RespondToProposal` | Đối phương chấp nhận (`Accepted` – cập nhật giá/giờ đơn) hoặc từ chối (`Rejected`) bản đề xuất. |
| **62** | `POST` | `/api/v1/trade/orders/{orderId:guid}/accept-terms` | Recycler | `AcceptOrderTerms` | Cơ sở thu gom chấp nhận trực tiếp điều khoản ban đầu của người dân mà không cần phản đề xuất. |
| **63** | `PUT` | `/api/v1/trade/orders/{orderId:guid}/messages/read` | Citizen / Recycler | `MarkMessagesAsRead` | Đánh dấu toàn bộ tin nhắn chưa đọc của đối phương trong phòng chat là đã xem (`is_read = true`). |
| **64** | `WS` | `/hubs/negotiation-chat` | Citizen / Recycler | `NegotiationChatHub` | Kênh WebSocket SignalR: tham gia phòng (`JoinOrderChat`), nhận tin nhắn tức thời (`ReceiveMessage`), thông báo đối phương đang gõ (`UserTyping`). |

---

### PHÂN HỆ 09: GIAO DỊCH, GAMIFICATION & THANH TOÁN DỊCH VỤ (TRADING, GAMIFICATION & PAYMENT)
* **Controllers:** `TradeController`, `GamificationController`, `ServicePaymentController`

| STT | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Tên Action (C#) | Mô tả chức năng & Use Case nghiệp vụ |
|:---:|:---:|:---|:---|:---|:---|
| **65** | `GET` | `/api/v1/trade/orders` | Citizen / Recycler | `GetOrders` | Tra cứu danh sách các đơn giao dịch thương mại rác của người dùng. |
| **66** | `POST` | `/api/v1/trade/orders` | Citizen (User) | `CreateOrder` | Khởi tạo đơn giao dịch mua bán phế liệu trực tiếp với vựa thu gom. |
| **67** | `PATCH`| `/api/v1/trade/orders/{orderId:guid}/status` | Authenticated | `UpdateOrderStatus` | Cập nhật trạng thái luồng xử lý đơn giao dịch (`Pending`, `InNegotiation`, `Agreed`, `Completed`). |
| **68** | `PATCH`| `/api/v1/trade/orders/{orderId:guid}/time-slot` | Authenticated | `UpdateTimeSlot` | Thay đổi khung thời gian hẹn giao dịch giữa hai bên. |
| **69** | `POST` | `/api/v1/trade/receipts/confirm-qr` | Citizen / Recycler | `ConfirmReceiptQr` | Quét mã QR bảo mật trên biên nhận để xác nhận hoàn tất giao dịch tại điểm thu mua. |
| **70** | `GET` | `/api/v1/gamification/profile/{userId:guid}` | Authenticated | `GetProfile` | Xem thông tin hồ sơ Xanh: tổng điểm tích lũy, giai đoạn phát triển của Cây ảo (Virtual Tree), lượng CO2 đã giảm thải. |
| **71** | `GET` | `/api/v1/gamification/leaderboard` | Public (All) | `GetLeaderboard` | Bảng xếp hạng cư dân sống xanh dẫn đầu theo tuần/tháng để vinh danh và nhận quà đối thưởng. |
| **72** | `POST` | `/api/v1/service-payments/create` | Recycler / Partner | `CreateServicePayment` | Tạo đơn nạp tiền hoặc thanh toán phí dịch vụ nền tảng EcoLink. |
| **73** | `GET` | `/api/v1/service-payments/orders/{orderCode}` | Authenticated | `GetOrderStatus` | Tra cứu trạng thái thanh toán của giao dịch nạp phí (`Pending`, `Paid`, `Failed`, `Cancelled`). |
| **74** | `GET` | `/api/v1/service-payments/orders` | Authenticated | `GetAllOrders` | Danh sách toàn bộ các hóa đơn nạp tiền/phí dịch vụ của tài khoản. |
| **75** | `POST` | `/api/v1/service-payments/webhook` | Public (Gateway) | `ReceivePaymentWebhook` | Webhook tự động nhận thông báo thanh toán thành công từ cổng VietQR / PayOS để cộng hạn mức. |
| **76** | `POST` | `/api/v1/service-payments/simulate-transfer/{orderCode}`| Public (Dev/Test) | `SimulateTransfer` | Giả lập sự kiện khách hàng đã quét mã chuyển tiền thành công phục vụ chạy thử nghiệm. |

---

### HẠ TẦNG HỆ THỐNG & KIỂM TRA SỨC KHỎE (SYSTEM HEALTH CHECK)
* **Host:** `Ecolink.WebApi` | **Entry:** `Program.cs`

| STT | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Tên Action (C#) | Mô tả chức năng & Use Case nghiệp vụ |
|:---:|:---:|:---|:---|:---|:---|
| **77** | `GET` | `/health` | Public (All) | Minimal API | Kiểm tra trạng thái máy chủ WebApi, phiên bản .NET 10, kết nối CSDL PostgreSQL và bộ nhớ đệm Redis. |

---

### DEDICATED AI VISION SERVICE (FASTAPI PYTHON SERVICE)
* **Dịch vụ:** Dedicated AI Vision Microservice (`http://127.0.0.1:8000`)
* **Tệp mã nguồn:** [`app.py`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/app.py)

| STT | Phương thức | Tuyến đường Endpoint (Route) | Phân quyền (RBAC) | Tên Hàm Python | Mô tả chức năng & Mục đích phục vụ |
|:---:|:---:|:---|:---|:---|:---|
| **78** | `GET` | `/api/models` | Public (Internal) | `get_models` | Danh sách mô hình AI đang nạp trong bộ nhớ RAM (`yolov8_detect`, `mobilenet_v3`, `efficientnet_b0`). |
| **79** | `POST` | `/api/classify` | Public (Internal) | `classify_waste` | Nhận diện rác bằng mô hình AI chỉ định, tính điểm tin cậy, so sánh ngưỡng nhận dạng và gợi ý phân loại. |
| **80** | `GET` | `/api/sample-images` | Public (Internal) | `get_sample_images` | Lấy danh sách đường dẫn ảnh mẫu trong tập dữ liệu thử nghiệm để người dùng test nhanh nhận diện. |
| **81** | `GET` | `/api/rules` | Public (Internal) | `get_rules` | Lấy danh mục luật phân loại từ tệp cấu hình Rule Engine của AI Service. |
| **82** | `POST` | `/api/rules` | Public (Internal) | `update_rules` | Cập nhật hoặc bổ sung quy tắc xử lý rác cho mô hình AI. |
| **83** | `POST` | `/api/feedback` | Public (Internal) | `submit_feedback` | Tiếp nhận ảnh và nhãn đúng khi mô hình dự đoán sai để lưu vào kho dữ liệu retraining. |
| **84** | `GET` | `/api/feedback` | Public (Internal) | `get_feedback` | Xem danh sách các phản hồi nhận diện sai từ người dùng. |
| **85** | `GET` | `/api/stats` | Public (Internal) | `get_stats` | Thống kê số lượng lượt nhận diện thành công, thất bại, độ trễ xử lý (Latency ms) của AI Service. |
| **86** | `GET` | `/` | Public (Internal) | `root` | Trang thông tin chào mừng và kiểm tra sức khỏe của AI Microservice. |

---

## 3. QUY TRÌNH DUY TRÌ & CẬP NHẬT TÀI LIỆU NÀY KHI PHÁT TRIỂN MODULE MỚI

Để đảm bảo tài liệu danh mục API luôn phản ánh chính xác 100% hiện trạng mã nguồn thực tế của dự án, mỗi khi phát triển thêm **Module mới** hoặc bổ sung **API Endpoint mới**, lập trình viên cần tuân thủ quy trình 4 bước chuẩn sau:

```
[BƯỚC 1: Code Controller & Swagger XML Doc]
         │
         ▼
[BƯỚC 2: Chạy Script Quét Kiểm Tra Đối Chiếu]
         │
         ▼
[BƯỚC 3: Cập nhật Bảng Thống kê & Danh mục trong file này]
         │
         ▼
[BƯỚC 4: Tạo / Cập nhật File Tài liệu chi tiết của Module]
```

### Bước 1: Viết API tuân thủ quy chuẩn mã nguồn
1. Mọi Action method trong Controller bắt buộc phải có:
   * Thuộc tính HTTP Verb rõ ràng: `[HttpGet]`, `[HttpPost]`, `[HttpPut]`, `[HttpDelete]`, `[HttpPatch]`.
   * Khối chú thích XML `<summary>` ghi rõ chức năng và use case (ví dụ: `/// <summary>Xác nhận biên nhận (U-09).</summary>`).
   * Thuộc tính `[ProducesResponseType(typeof(ApiResponse<TDto>), StatusCodes.Status200OK)]`.
   * Khai báo phân quyền rõ ràng với `[Authorize(Roles = "...")]` hoặc `[AllowAnonymous]`.

### Bước 2: Chạy script tự động quét đối chiếu số lượng
Dự án đã trang bị sẵn script PowerShell quét tự động:
* **Tệp script:** `scripts/scan_api_inventory.ps1`
* **Lệnh thực thi trong PowerShell:**
  ```powershell
  powershell -ExecutionPolicy Bypass -File scripts/scan_api_inventory.ps1
  ```
Script sẽ tự động duyệt toàn bộ các Controller trong thư mục `src/Ecolink.Backend/Ecolink.WebApi/Controllers/`, đếm tổng số action và xuất ra danh sách đối chiếu.

### Bước 3: Cập nhật số lượng vào bảng thống kê
1. Điều chỉnh số lượng tại **Mục 1.1** (Bảng theo Module), **Mục 1.2** (Bảng theo Method) và **Mục 1.3** (Bảng theo RBAC).
2. Thêm dòng tương ứng vào **Mục 2** (Danh mục chi tiết) theo đúng số thứ tự tiếp theo (`STT = N + 1`).
3. Cập nhật `Phiên bản tài liệu` và `Thời điểm cập nhật` ở đầu trang.

### Bước 4: Cập nhật file chi tiết của Module
Bổ sung tài liệu đặc tả request/response JSON payload chi tiết vào tệp tương ứng của module đó (ví dụ: `documents/api document/0X_module_..._api.md`).

