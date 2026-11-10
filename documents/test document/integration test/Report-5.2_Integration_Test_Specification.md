# REPORT 5.2 – API INTEGRATION TEST SPECIFICATION (TÀI LIỆU ĐẶC TẢ KIỂM THỬ TÍCH HỢP HỆ THỐNG)

- **Dự án:** Hệ thống Phân loại Rác Thông minh & Giao dịch Phế liệu (**EcoLink**)
- **Phiên bản:** 1.0
- **Ngày cập nhật:** 2026-11-10
- **Nhóm thực hiện:** Hoàng Thắng Nguyễn, Nguyen Thanh Luan (K18 HL), Giang Nguyễn Đức
- **Project Kiểm thử Tích hợp:** [`src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj`](../../../src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj) (`xUnit` + `HttpClient` End-to-End API Integration)
- **Phạm vi Kiểm thử Tích hợp (8 Core Modules – 236 Test Methods / 415 Expanded Test Cases):**
  1. **Module 1:** Authentication & Account Management ([`AuthTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/AuthTests.cs)) — **30 Test Methods** (`53` kịch bản dữ liệu)
  2. **Module 2:** AI Waste Classification & Rule Engine ([`WasteScanTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/WasteScanTests.cs)) — **19 Test Methods** (`25` kịch bản dữ liệu)
  3. **Module 3:** Recycler Onboarding & Verification ([`OnboardingTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/OnboardingTests.cs)) — **8 Test Methods** (`11` kịch bản dữ liệu)
  4. **Module 4:** Recycler Profile, Pricing & Availability ([`RecyclerTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/RecyclerTests.cs)) — **76 Test Methods** (`143` kịch bản dữ liệu)
  5. **Module 5:** Location-Based Recycler Discovery ([`DiscoveryTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/DiscoveryTests.cs)) — **8 Test Methods** (`13` kịch bản dữ liệu)
  6. **Module 6:** Handover Request Management ([`HandoverTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/HandoverTests.cs)) — **47 Test Methods** (`89` kịch bản dữ liệu)
  7. **Module 7:** Handover Completion & Transaction Receipt ([`ReceiptTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/ReceiptTests.cs)) — **38 Test Methods** (`65` kịch bản dữ liệu)
  8. **Module 8:** In-App Real-Time Negotiation Chatbox ([`NegotiationChatTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/NegotiationChatTests.cs)) — **10 Test Methods** (`16` kịch bản dữ liệu)

---

## 1. BẢNG TỔNG HỢP ĐỘ BAO PHỦ KIỂM THỬ TÍCH HỢP TOÀN HỆ THỐNG

| Mã Module | Tên Module Nghiệp vụ | File Test Thực Thi (`src/Ecolink.Backend/EcoLink.ApiTests/`) | Số Hàm Test (`[Fact]` + `[Theory]`) | Tổng Kịch bản (`InlineData` / `MemberData`) | Luồng Tích hợp Liên Module (Cross-Module Integration) |
| :---: | :--- | :--- | :---: | :---: | :--- |
| **M-01** | **Authentication & Account Management** | [`AuthTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/AuthTests.cs) | 30 | 53 | `Register` $\rightarrow$ `Login` $\rightarrow$ `JWT Middleware` $\rightarrow$ `/auth/me` $\rightarrow$ `VietQR Basic Auth` |
| **M-02** | **AI Waste Classification & Rule Engine** | [`WasteScanTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/WasteScanTests.cs) | 19 | 25 | `Multipart Image` $\rightarrow$ `WebApi` $\rightarrow$ `FastAPI AI Service` $\rightarrow$ `Rule Engine` $\rightarrow$ `Feedback` |
| **M-03** | **Recycler Onboarding & Verification** | [`OnboardingTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/OnboardingTests.cs) | 8 | 11 | `M1 Register` $\rightarrow$ `M3 Upload R2 & Submit` $\rightarrow$ `M3 Staff Approve` $\rightarrow$ `M1 Role Upgrade` $\rightarrow$ `M4 Seed Prices` |
| **M-04** | **Recycler Profile, Pricing & Availability** | [`RecyclerTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/RecyclerTests.cs) | 76 | 143 | `M3 Verification Status Gate` $\rightarrow$ `M4 Profile / Atomic Bulk Prices / Weekly Schedule / Online Toggle` |
| **M-05** | **Location-Based Recycler Discovery** | [`DiscoveryTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/DiscoveryTests.cs) | 8 | 13 | `M4 Online/Offline & Price Update` $\rightarrow$ `M5 PostGIS / Haversine Search` $\rightarrow$ `Fallback Suggestion` $\rightarrow$ `Geocoding Proxy` |
| **M-06** | **Handover Request Management** | [`HandoverTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/HandoverTests.cs) | 47 | 89 | `M4 Schedule & Active Price Gate` $\rightarrow$ `M6 Citizen Create / Update / Cancel` $\leftrightarrow$ `M6 Recycler Accept / Reject` |
| **M-07** | **Handover Completion & Receipt** | [`ReceiptTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/ReceiptTests.cs) | 38 | 65 | `M6 Accepted Order` $\rightarrow$ `M7 Preview & Issue Receipt` $\rightarrow$ `M7 Citizen Confirm / Dispute` $\rightarrow$ `M1 GreenPoints & TreeLevel` |
| **M-08** | **In-App Real-Time Negotiation Chatbox** | [`NegotiationChatTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/NegotiationChatTests.cs) | 10 | 16 | `M6 Pending Order` $\rightarrow$ `M8 Thread / Image Upload / Proposal / Counter-Proposal (Superseded)` $\rightarrow$ `M6 Lock AgreedPrice & TimeSlot` |
| **TỔNG** | **TOÀN BỘ 8 CORE MODULES** | **8 Files Test + 3 Files Support** | **236** | **415** | **Bao phủ 100% chuỗi tích hợp HTTP Pipeline + Database + RBAC** |

---

## PHẦN 1: MODULE 1 – AUTHENTICATION & ACCOUNT MANAGEMENT (`AuthTests.cs`)

### 1.1. Bảng Tổng hợp Kiểm thử (Summary Table)

| Code Module | `AuthController` + `JwtAuthenticationMiddleware` (`AuthTests.cs`) | | |
| :--- | :---: | :---: | :---: |
| **Number of Test Methods / Expanded TCs** | **30 Methods (53 TCs)** | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** |
| *Round 1* | 30 | 0 | 0 |
| **Testing Type** | **Normal** | **Abnormal** | **Boundary** |
| **Count (Methods)** | **11** | **16** | **3** |

### 1.2. Danh sách Test Cases Tích hợp Tiêu biểu (`AuthTests.cs`)

| Test Case ID | Tên Hàm Test (`AuthTests.cs`) | Endpoint & Phương thức | Kịch bản Kiểm thử Tích hợp (HTTP $\rightarrow$ Controller $\rightarrow$ Service $\rightarrow$ PostgreSQL) | Kết quả Mong đợi (HTTP Status & Envelope) | Loại |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **ITC-M1-01** | `Health_ReturnsOk` | `GET /health` | Kiểm tra kết nối tổng thể của `Ecolink.WebApi` tới PostgreSQL và hạ tầng hệ thống. | `200 OK` | Normal |
| **ITC-M1-02** | `Register_WithValidData_Succeeds` | `POST /api/v1/auth/register` | Đăng ký tài khoản Citizen mới, kiểm tra lưu DB `users` + `user_eco_profiles` và trả về `user.id` hợp lệ. | `200 OK` / `201 Created`, `success = true` | Normal |
| **ITC-M1-03** | `Register_ResponseDoesNotLeakPassword` | `POST /api/v1/auth/register` | Kiểm tra bảo mật JSON Serialization: tuyệt đối không lộ `password` hoặc `passwordHash` trong HTTP body. | `200/201`, body không chứa chuỗi mật khẩu | Normal |
| **ITC-M1-04** | `Register_WithMaxLengthFields_Succeeds` | `POST /api/v1/auth/register` | Kiểm tra biên độ dài cột DB & Validator: `username` (50 ký tự), `password` (100 ký tự), `fullName` (100 ký tự). | `200/201`, `success = true` | Boundary |
| **ITC-M1-05** | `Register_WithMinLengthPassword_Succeeds` | `POST /api/v1/auth/register` | Kiểm tra biên dưới mật khẩu đúng 6 ký tự (`"abc123"`). | `200/201`, `success = true` | Boundary |
| **ITC-M1-06** | `Register_WithDuplicateUsername_Fails` | `POST /api/v1/auth/register` | Đăng ký trùng `username` đã tồn tại trong PostgreSQL (`BR-AUTH-01`). | `400 Bad Request`, `success = false` | Abnormal |
| **ITC-M1-07** | `Register_WithDuplicateEmail_Fails` | `POST /api/v1/auth/register` | Đăng ký trùng `email` đã tồn tại trong PostgreSQL (`BR-AUTH-01`). | `400 Bad Request`, `success = false` | Abnormal |
| **ITC-M1-08** | `Register_WithInvalidData_Returns400` *(11 kịch bản)* | `POST /api/v1/auth/register` | Kiểm tra `FluentValidation` pipeline với 11 biến thể lỗi (quá ngắn, quá dài, sai định dạng email, thiếu trường bắt buộc). | `400 Bad Request` | Abnormal |
| **ITC-M1-09** | `Register_ThenLogin_WorksWithUsernameAndEmail` | `POST /register` $\rightarrow$ `POST /login` | Chuỗi tích hợp: Đăng ký tài khoản mới $\rightarrow$ Đăng nhập ngay bằng cả `username` và `email` đều nhận được JWT hợp lệ. | `200 OK`, trả về `accessToken` | Normal |
| **ITC-M1-10** | `Login_WithDemoAccount_Succeeds` *(3 roles)* | `POST /api/v1/auth/login` | Đăng nhập với 3 vai trò `citizen`, `collector`, `admin`, kiểm tra thông tin định danh và `role` trong payload. | `200 OK`, `user.role` khớp | Normal |
| **ITC-M1-11** | `Login_ErrorMessage_DoesNotRevealWhichAccountsExist` | `POST /api/v1/auth/login` | Chống User Enumeration (`BR-AUTH-03`): Thông báo lỗi khi sai mật khẩu và khi tài khoản không tồn tại phải đồng nhất. | `400 Bad Request`, cùng `message` | Abnormal |
| **ITC-M1-12** | `Login_WithInjectionPayload_IsRejected` *(4 kịch bản)* | `POST /api/v1/auth/login` | Kiểm tra chống SQL Injection & XSS (`' OR '1'='1`, `admin'--`, `<script>alert(1)</script>`) qua tầng EF Core Parameterized Query. | `400 Bad Request`, không cấp token | Abnormal |
| **ITC-M1-13** | `Me_WithValidToken_ReturnsCurrentUser` *(3 roles)* | `GET /api/v1/auth/me` | Kiểm tra tích hợp JWT Middleware giải mã Claims và truy xuất hồ sơ kèm điểm xanh (`currentGreenPoints`, `treeLevel`). | `200 OK` | Normal |
| **ITC-M1-14** | `Me_WithTamperedToken_Returns401` | `GET /api/v1/auth/me` | Thay đổi 4 ký tự cuối chữ ký HMAC-SHA256 của JWT token $\rightarrow$ Middleware từ chối truy cập. | `401 Unauthorized` | Abnormal |
| **ITC-M1-15** | `VietQrToken_WithUserBearerToken_Returns401` | `POST /api/v1/auth/vietqr-token` | Kiểm tra tách biệt cơ chế xác thực Basic Auth của cổng VietQR với Bearer JWT của người dùng. | `401 Unauthorized` | Abnormal |

---

## PHẦN 2: MODULE 2 – AI WASTE CLASSIFICATION & RULE ENGINE (`WasteScanTests.cs`)

### 2.1. Bảng Tổng hợp Kiểm thử (Summary Table)

| Code Module | `WasteScanController` + `AiVisionHttpClient` + `WasteRuleEngineService` (`WasteScanTests.cs`) | | |
| :--- | :---: | :---: | :---: |
| **Number of Test Methods / Expanded TCs** | **19 Methods (25 TCs)** | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** |
| *Round 1* | 19 | 0 | 0 |
| **Testing Type** | **Normal** | **Abnormal** | **Boundary** |
| **Count (Methods)** | **9** | **8** | **2** |

### 2.2. Danh sách Test Cases Tích hợp Tiêu biểu (`WasteScanTests.cs`)

| Test Case ID | Tên Hàm Test (`WasteScanTests.cs`) | Endpoint & Phương thức | Kịch bản Kiểm thử Tích hợp (`WebApi` $\leftrightarrow$ `FastAPI AI Service` $\leftrightarrow$ `Rule Engine`) | Kết quả Mong đợi | Loại |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **ITC-M2-01** | `Models_ReturnsNonEmptyList` | `GET /api/v1/waste-scan/models` | Truy vấn danh sách mô hình AI khả dụng (`yolov8`, `mobilenet_v3`, `efficientnet`) từ AI Service qua Backend proxy. | `200 OK`, danh sách khác rỗng | Normal |
| **ITC-M2-02** | `Rules_ReturnsNonEmptyJson` | `GET /api/v1/waste-scan/rules` | Truy vấn toàn bộ tập luật phân loại rác của `WasteRuleEngineService` (màu thùng, hướng dẫn xử lý, cờ tái chế). | `200 OK`, JSON hợp lệ | Normal |
| **ITC-M2-03** | `Classify_WithDefaultModel_ReturnsExpectedShape` | `POST /api/v1/waste-scan/classify` | Gửi ảnh PNG sinh động qua `multipart/form-data` $\rightarrow$ AI phân loại $\rightarrow$ Ánh xạ luật xử lý rác và trả về kết quả. | `200 OK`, đủ trường phân loại | Normal |
| **ITC-M2-04** | `Classify_PredictionConfidence_IsBetween0And1` | `POST /api/v1/waste-scan/classify` | Kiểm tra biên xác suất dự đoán `confidence` chuẩn hóa trong đoạn $[0.0, 1.0]$. | `200 OK`, $0 \le \text{confidence} \le 1$ | Boundary |
| **ITC-M2-05** | `Classify_WithoutFile_Returns400` | `POST /api/v1/waste-scan/classify` | Gửi request `multipart/form-data` thiếu trường `file` (`BR-AI-01`). | `400 Bad Request` | Abnormal |
| **ITC-M2-06** | `Classify_WithNonImageFile_ReturnsClientError` | `POST /api/v1/waste-scan/classify` | Tải lên tệp văn bản `.txt` giả làm ảnh $\rightarrow$ Bộ lọc định dạng tệp chặn với mã lỗi `4xx`. | `400 / 415 Client Error` | Abnormal |
| **ITC-M2-07** | `Classify_WithCorruptedImage_DoesNotCauseServerError` | `POST /api/v1/waste-scan/classify` | Gửi mảng byte ảnh PNG bị hỏng cấu trúc chunk $\rightarrow$ Hệ thống xử lý an toàn, không văng `500 Internal Server Error`. | Status `< 500` | Abnormal |
| **ITC-M2-08** | `ClassifyThenFeedback_EndToEndFlow` | `POST /classify` $\rightarrow$ `POST /feedback` | Luồng tích hợp End-to-End: Quét ảnh rác bằng AI $\rightarrow$ Lấy nhãn dự đoán & độ tự tin $\rightarrow$ Gửi phản hồi hiệu chỉnh (`Feedback`) lưu phục vụ huấn luyện lại. | Cả 2 bước trả về `200 OK` | Normal |

---

## PHẦN 3: MODULE 3 – RECYCLER ONBOARDING & VERIFICATION (`OnboardingTests.cs`)

### 3.1. Bảng Tổng hợp Kiểm thử (Summary Table)

| Code Module | `RecyclerApplicationsController` + `RecyclerOnboardingService` (`OnboardingTests.cs`) | | |
| :--- | :---: | :---: | :---: |
| **Number of Test Methods / Expanded TCs** | **8 Methods (11 TCs)** | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** |
| *Round 1* | 8 | 0 | 0 |
| **Testing Type** | **Normal** | **Abnormal** | **Boundary** |
| **Count (Methods)** | **4** | **3** | **1** |

### 3.2. Danh sách Chi tiết Test Cases (`OnboardingTests.cs`)

| Test Case ID | Tên Hàm Test (`OnboardingTests.cs`) | Endpoint & Phương thức | Kịch bản Kiểm thử Tích hợp Liên Module (`M1 Auth` $\rightarrow$ `M3 Onboarding` $\rightarrow$ `M4 Prices`) | Kết quả Mong đợi | Loại |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **ITC-M3-01** | `ONB01_UploadDocument_PermitAndFacilityPhoto_ReturnsValidAttachmentUrls` | `POST /api/v1/recycler-applications/upload-document` | Citizen tải lên ảnh Giấy phép kinh doanh (`permit`) và Ảnh cơ sở (`facility-photo`) qua `multipart/form-data` lên kho lưu trữ R2/Local. | `200 OK`, trả về `fileUrl` hợp lệ | Normal |
| **ITC-M3-02** | `ONB02_SubmitApplication_WithoutTokenOrAlreadyVerifiedCollector_FailsAppropriately` | `POST /api/v1/recycler-applications` | Kiểm tra bảo mật & `BR-MTH-06`: Không có token trả về `401`; tài khoản đã là `Collector` nộp đơn nâng cấp bị chặn `400 MSG-MTH-06`. | `401 Unauthorized` & `400 MSG-MTH-06` | Abnormal |
| **ITC-M3-03** | `ONB03_SubmitApplication_WithInvalidInput_Returns400WithMsgMth06` *(4 kịch bản)* | `POST /api/v1/recycler-applications` | Kiểm tra biên tọa độ WGS84 (`latitude = 95.5`, `longitude = 190.0`) và thiếu tên cơ sở / ảnh giấy phép. | `400 Bad Request`, `MSG-MTH-06` | Boundary |
| **ITC-M3-04** | `ONB04_SubmitApplication_ValidCitizen_CreatesPendingApplicationAndRetrievableViaGetMe` | `POST /recycler-applications` $\rightarrow$ `GET /me` | Citizen nộp hồ sơ hợp lệ $\rightarrow$ Khởi tạo `RecyclerProfile` trạng thái `Pending` kèm tọa độ PostGIS `Point(lng, lat)` $\rightarrow$ Tra cứu lại qua `/me`. | `201 Created` (`MSG-MTH-03`) & `200 OK` | Normal |
| **ITC-M3-05** | `ONB05_SubmitApplication_DuplicateWhenAlreadyPending_Returns400WithMsgMth06` | `POST /api/v1/recycler-applications` | Chặn nộp trùng hồ sơ khi đơn trước đó của cùng Citizen đang ở trạng thái `Pending` (`BR-MTH-06`). | `400 Bad Request`, `MSG-MTH-06` | Abnormal |
| **ITC-M3-06** | `ONB06_StaffReview_ListAndDetail_EnforcesStaffOrAdminRole` | `GET /api/v1/recycler-applications` & `/{userId}` | Kiểm tra RBAC (`S-04`): Citizen gọi bị chặn `403 Forbidden`; Staff/Admin gọi trả về danh sách phân trang và chi tiết hồ sơ. | `403 Forbidden` (Citizen) / `200 OK` (Admin) | Normal |
| **ITC-M3-07** | `ONB07_StaffReview_RejectRequiresReason_AndAllowsCitizenToResubmit` | `PUT /verification` $\rightarrow$ `POST /recycler-applications` | Staff từ chối thiếu lý do bị chặn `400`; từ chối có lý do chuyển sang `Rejected` (`MSG-MTH-05`); sau đó Citizen bổ sung giấy phép và nộp lại thành công (`Pending`). | `400` $\rightarrow$ `200 MSG-MTH-05` $\rightarrow$ `201 Created` | Normal |
| **ITC-M3-08** | `ONB08_StaffReview_ApproveApplication_UpgradesUserRoleToRecyclerAndSeedsDefaultPrices` | `PUT /verification` $\rightarrow$ `POST /login` $\rightarrow$ `GET /recycler/prices` | **Tích hợp 3 Module (M3 $\rightarrow$ M1 $\rightarrow$ M4):** Staff duyệt đơn (`Verified`, `MSG-MTH-04`) $\rightarrow$ User tự động nâng cấp Role thành `Recycler` $\rightarrow$ Đăng nhập lại nhận JWT mới $\rightarrow$ Truy cập bảng giá Module 4 thấy 5 danh mục giá mặc định đã được seed tự động. | `200 OK` (`MSG-MTH-04`), Role = `Recycler`, `prices.Count > 0` | Normal |

---

## PHẦN 4: MODULE 4 – RECYCLER PROFILE, PRICING & AVAILABILITY (`RecyclerTests.cs`)

### 4.1. Bảng Tổng hợp Kiểm thử (Summary Table)

| Code Module | `RecyclerController` + `RecyclerManagementService` (`RecyclerTests.cs`) | | |
| :--- | :---: | :---: | :---: |
| **Number of Test Methods / Expanded TCs** | **76 Methods (143 TCs)** | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** |
| *Round 1* | 76 | 0 | 0 |
| **Testing Type** | **Normal** | **Abnormal** | **Boundary** |
| **Count (Methods)** | **26** | **36** | **14** |

### 4.2. Danh sách Nhóm Test Cases Tích hợp (`RecyclerTests.cs`)

| Mã Nhóm TC | Nhóm Chức năng & Endpoint Kiểm thử | Số Hàm Test | Kịch bản Kiểm thử Tích hợp & Quy tắc Nghiệp vụ (`R-01` & `R-02`) | Kết quả Mong đợi |
| :--- | :--- | :---: | :--- | :--- |
| **ITC-M4-01..06** | **RBAC & Auth Gate** (`/api/v1/recycler/*`) | 6 | Kiểm tra toàn bộ 9 endpoints của Module 4: Không có token trả về `401`; Token của `Citizen` bị chặn `403 Forbidden`. | `401 Unauthorized` / `403 Forbidden` |
| **ITC-M4-07..16** | **Quản lý Hồ sơ Cơ sở** (`GET` & `PUT /api/v1/recycler/profile`) | 10 | Xem và cập nhật tên cơ sở, người đại diện, địa chỉ, tọa độ GPS (`[-90, 90]`, `[-180, 180]`), mã số thuế; đảm bảo giao dịch nguyên tử khi dữ liệu không hợp lệ. | `200 OK` / `400 Bad Request` |
| **ITC-M4-17..42** | **Quản lý Bảng giá Đơn lẻ** (`GET`, `POST`, `PUT /api/v1/recycler/prices`) | 26 | Thêm dòng giá mới (`201 Created`), chặn trùng loại phế liệu, kiểm tra biên `pricePerKg` (`0.01` .. `1.000.000` VND/kg) và `minQuantityKg` (`0.1` .. `10.000` kg), chặn tắt dòng giá đang hoạt động cuối cùng (`BR-PRC-03` / `MSG-PRC-02`). | `200/201` / `400 MSG-PRC-02` |
| **ITC-M4-43..54** | **Lưu Bảng giá Hàng loạt Atomic** (`PUT /api/v1/recycler/prices`) | 12 | Cập nhật đồng loạt nhiều loại phế liệu trong một Database Transaction: nếu 1 phần tử vi phạm hoặc tất cả `isActive = false`, toàn bộ giao dịch được rollback, giữ nguyên bảng giá cũ. | `200 OK` (`MSG-PRC-01`) / `400 Rollback` |
| **ITC-M4-55..63** | **Trạng thái Hoạt động Online/Offline** (`GET` & `PATCH /api/v1/recycler/availability`) | 9 | Chuyển đổi `Online` $\leftrightarrow$ `Offline`; chặn cơ sở `Pending` hoặc không có bảng giá active bật `Online` (`BR-MTH-01` / `BR-PRC-03`); tự động xóa `unavailableReason` khi bật lại `Online`. | `200 OK` / `400 Bad Request` |
| **ITC-M4-64..76** | **Lịch Mở cửa Hàng tuần** (`PUT /api/v1/recycler/availability/schedule`) | 13 | Thiết lập lịch 7 ngày trong tuần (`00:00` – `23:59`), tự động tổng hợp chuỗi `openingHoursSummary`, kiểm tra `openTime < closeTime` và chặn cơ sở chưa xác minh (`Pending`). | `200 OK` / `400 Bad Request` |

---

## PHẦN 5: MODULE 5 – LOCATION-BASED RECYCLER DISCOVERY (`DiscoveryTests.cs`)

### 5.1. Bảng Tổng hợp Kiểm thử (Summary Table)

| Code Module | `RecyclersDiscoveryController` + `GeocodingController` + `TradeController` (`DiscoveryTests.cs`) | | |
| :--- | :---: | :---: | :---: |
| **Number of Test Methods / Expanded TCs** | **8 Methods (13 TCs)** | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** |
| *Round 1* | 8 | 0 | 0 |
| **Testing Type** | **Normal** | **Abnormal** | **Boundary** |
| **Count (Methods)** | **5** | **2** | **1** |

### 5.2. Danh sách Chi tiết Test Cases (`DiscoveryTests.cs`)

| Test Case ID | Tên Hàm Test (`DiscoveryTests.cs`) | Endpoint & Phương thức | Kịch bản Kiểm thử Tích hợp Liên Module (`M4 Availability/Prices` $\rightarrow$ `M5 Discovery`) | Kết quả Mong đợi | Loại |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **ITC-M5-01** | `DSC01_SearchNearby_ValidHoaLacCoordinates_ReturnsVerifiedOnlineRecyclersSortedByDistanceAsc` | `GET /api/v1/recyclers/nearby` | Tìm kiếm quanh tọa độ Khu CNC Hòa Lạc (`21.0124, 105.5253`, bán kính `15km`) $\rightarrow$ Trả về các cơ sở `Verified` + `Online` sắp xếp tăng dần theo `distanceKm` (`BR-MTH-04`). | `200 OK` (`MSG-GEN-01`), `items` tăng dần theo `distanceKm` | Normal |
| **ITC-M5-02** | `DSC02_SearchNearby_InvalidCoordinatesRadiusOrCategory_Returns400WithMsgMth02` *(6 kịch bản)* | `GET /api/v1/recyclers/nearby` | Kiểm tra `NearbyRecyclerFilterValidator`: tọa độ `(0,0)`, vĩ độ `95.5`, kinh độ `-185.0`, bán kính `< 0.5km` hoặc `> 50km`, danh mục `"trash"` không tái chế. | `400 Bad Request`, `MSG-MTH-02` | Boundary |
| **ITC-M5-03** | `DSC03_SearchNearby_FilterByCategoryAndSortByPriceDesc_ReturnsMatchingFacilitiesOrderedByPrice` | `GET /api/v1/recyclers/nearby` | Lọc theo `category=metal` và sắp xếp `sortBy=price_desc` (`BR-MTH-07`) $\rightarrow$ Chỉ trả về cơ sở có thu mua kim loại và sắp xếp giảm dần theo đơn giá kim loại. | `200 OK`, giá `metal` giảm dần | Normal |
| **ITC-M5-04** | `DSC04_SearchNearby_ZeroResultsInRadius_ReturnsMsgMth01AndFallbackSuggestion` | `GET /api/v1/recyclers/nearby` | Tìm kiếm tại tọa độ xa không có cơ sở trong bán kính `4km` $\rightarrow$ Trả về `MSG-MTH-01` kèm `fallbackSuggestion` gợi ý mở rộng bán kính lên `10km` ($4 \times 2.5$) và hướng dẫn phân loại rác tại nguồn. | `200 OK`, `MSG-MTH-01`, `suggestedExpandedRadiusKm = 10` | Normal |
| **ITC-M5-05** | `DSC05_CrossModule_WhenRecyclerTogglesAvailability_DiscoveryReflectsOnlineStatusImmediately` | `PATCH /recycler/availability` $\rightarrow$ `GET /recyclers/nearby` | **Tích hợp Liên Module (M4 $\rightarrow$ M5):** Cơ sở thu gom chuyển sang `Offline` ở Module 4 $\rightarrow$ Biến mất ngay khỏi kết quả tìm kiếm bản đồ Module 5; khi bật lại `Online` $\rightarrow$ Xuất hiện trở lại ngay lập tức. | Đồng bộ tức thời giữa M4 và M5 | Normal |
| **ITC-M5-06** | `DSC06_GetRecyclerPublicDetail_ValidVerifiedRecycler_ReturnsProfilePricesAndDistanceKm` | `GET /api/v1/recyclers/{recyclerId}` | Xem trang chi tiết công khai của cơ sở trên bản đồ kèm tọa độ người dùng $\rightarrow$ Trả về thông tin cơ sở, khoảng cách `distanceKm` và bảng giá `materials`. | `200 OK` (`MSG-GEN-01`) | Normal |
| **ITC-M5-07** | `DSC07_GetRecyclerPublicDetail_NonExistentId_Returns404NotFound` | `GET /api/v1/recyclers/{recyclerId}` | Truy vấn chi tiết cơ sở với `Guid` không tồn tại. | `404 Not Found` | Abnormal |
| **ITC-M5-08** | `DSC08_TradePartnersNearby_AndGeocodingProxy_ValidateAndReturnExpectedEnvelopes` | `GET /trade/partners/nearby` & `GET /geocoding/search` | Kiểm tra tích hợp endpoint đối tác giao dịch lân cận và Geocoding Proxy (chặn từ khóa `< 2` ký tự với `400`, trả về danh sách địa danh với từ khóa `"Hoa Lac"`). | `200 OK` & `400 Bad Request` | Normal |

---

## PHẦN 6: MODULE 6 – HANDOVER REQUEST MANAGEMENT (`HandoverTests.cs`)

### 6.1. Bảng Tổng hợp Kiểm thử (Summary Table)

| Code Module | `HandoverRequestController` + `RecyclerHandoverController` (`HandoverTests.cs`) | | |
| :--- | :---: | :---: | :---: |
| **Number of Test Methods / Expanded TCs** | **47 Methods (89 TCs)** | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** |
| *Round 1* | 47 | 0 | 0 |
| **Testing Type** | **Normal** | **Abnormal** | **Boundary** |
| **Count (Methods)** | **17** | **23** | **7** |

### 6.2. Danh sách Nhóm Test Cases Tích hợp (`HandoverTests.cs`)

| Mã Nhóm TC | Nhóm Chức năng & Endpoint Kiểm thử | Số Hàm Test | Kịch bản Kiểm thử Tích hợp & Quy tắc Nghiệp vụ (`U-06`, `U-07`, `R-03`, `R-04`) | Kết quả Mong đợi |
| :--- | :--- | :---: | :--- | :--- |
| **ITC-M6-01..04** | **RBAC & Auth Gate** (`/api/v1/handover-requests` & `/recycler/handover-requests`) | 4 | Kiểm tra phân quyền chặt chẽ giữa `Citizen` (người tạo đơn) và `Recycler` (người nhận đơn); chặn truy cập chéo vai trò với `403 Forbidden`. | `401 Unauthorized` / `403 Forbidden` |
| **ITC-M6-05..21** | **Khởi tạo Yêu cầu Bàn giao (`U-06`)** (`POST /api/v1/handover-requests`) | 17 | Sinh mã `HND-yyyyMMdd-NNNN`; chụp nhanh `PriceSnapshotPerKg` từ bảng giá Module 4 (`BR-HND-03`); ẩn số điện thoại khi `Pending` (`BR-PRIV-01`); chặn tạo đơn khi cơ sở `Offline`, đóng cửa theo lịch tuần, không thu mua loại rác đó, hoặc Citizen vượt quá **3 đơn đang mở** (`BR-HND-02`). | `201 Created` (`MSG-HND-01`) / `400 Bad Request` |
| **ITC-M6-22..28** | **Tra cứu & Bảo mật Quyền riêng tư** (`GET /handover-requests` & `/{id}`) | 7 | Kiểm tra phân trang, lọc theo trạng thái; Citizen B không được xem đơn của Citizen A (`403`); Recycler B không được xem đơn gửi cho Recycler A; chỉ hiển thị số điện thoại sau khi `Accepted` (`BR-PRIV-01`). | `200 OK` / `403 Forbidden` |
| **ITC-M6-29..35** | **Cập nhật & Hủy đơn phía Citizen (`U-07`)** (`PUT /{id}` & `POST /{id}/cancel`) | 7 | Cho phép sửa địa chỉ/ghi chú/giờ hẹn khi `Pending`; khóa sửa đổi sau khi `Accepted`; kiểm tra cờ cảnh báo hủy muộn `IsLateCancellation = true` khi hủy sát giờ hẹn `< 2h` (`BR-HND-05`). | `200 OK` (`MSG-HND-03`) / `400 Bad Request` |
| **ITC-M6-36..47** | **Phản hồi Đơn phía Cơ sở (`R-03`, `R-04`)** (`POST /accept`, `/reject`, `/cancel`) | 12 | Cơ sở chấp nhận đơn (`Accepted`, mở khóa SĐT hai bên); từ chối đơn bắt buộc có lý do (`1..500` ký tự – `BR-HND-06`); hủy đơn sau khi đã nhận kèm lý do bắt buộc; chặn mọi thao tác chuyển trạng thái ngược chiều từ trạng thái kết thúc (`Completed`/`Cancelled`/`Rejected`). | `200 OK` (`MSG-HND-02`) / `400 Bad Request` |

---

## PHẦN 7: MODULE 7 – HANDOVER COMPLETION & TRANSACTION RECEIPT (`ReceiptTests.cs`)

### 7.1. Bảng Tổng hợp Kiểm thử (Summary Table)

| Code Module | `RecyclerReceiptController` + `HandoverReceiptController` + `TransactionControllers` (`ReceiptTests.cs`) | | |
| :--- | :---: | :---: | :---: |
| **Number of Test Methods / Expanded TCs** | **38 Methods (65 TCs)** | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** |
| *Round 1* | 38 | 0 | 0 |
| **Testing Type** | **Normal** | **Abnormal** | **Boundary** |
| **Count (Methods)** | **14** | **18** | **6** |

### 7.2. Danh sách Nhóm Test Cases Tích hợp (`ReceiptTests.cs`)

| Mã Nhóm TC | Nhóm Chức năng & Endpoint Kiểm thử | Số Hàm Test | Kịch bản Kiểm thử Tích hợp & Quy tắc Nghiệp vụ (`R-06`, `U-09`, `U-12`) | Kết quả Mong đợi |
| :--- | :--- | :---: | :--- | :--- |
| **ITC-M7-01..04** | **RBAC & Auth Gate** (`/receipt`, `/confirm`, `/dispute`, `/transactions`) | 4 | Kiểm tra phân quyền giữa Citizen và Recycler trên toàn bộ 8 endpoints biên nhận và lịch sử giao dịch. | `401 Unauthorized` / `403 Forbidden` |
| **ITC-M7-05..10** | **Xem trước Biên nhận & Cảnh báo Lệch Cân** (`POST /receipt/preview`) | 6 | Tính thử thành tiền $\sum (\text{ActualWeightKg} \times \text{UnitPricePerKg})$ làm tròn VND; kiểm tra ngưỡng biên độ lệch cân $\le 20\%$ (`hasDeviationWarning = false`) và $> 20\%$ (`hasDeviationWarning = true`, `MSG-RCP-03`) mà không ghi xuống DB. | `200 OK` |
| **ITC-M7-11..23** | **Phát hành Biên nhận Điện tử (`R-06`)** (`POST /recycler/handover-requests/{id}/receipt`) | 13 | Phát hành biên nhận `RCP-yyyy-NNNN`, chuyển đơn sang `AwaitingConfirmation`, đặt hạn `AutoConfirmAt = IssuedAt + 24h` (`BR-TRN-03`); bắt buộc có ảnh chụp mặt đồng hồ cân (`scalePhotoUrls`) khi tổng khối lượng $\ge 10\text{ kg}$ (`BR-TRN-01` / `MSG-RCP-04`); chặn phát hành biên nhận trùng lặp (`BR-TRN-04`). | `201 Created` (`MSG-RCP-01`) / `400 MSG-RCP-04` |
| **ITC-M7-24..31** | **Xác nhận Hoàn tất & Cộng Điểm Xanh (`U-09`)** (`POST /receipt/confirm`) | 8 | **Tích hợp Liên Module (M7 $\rightarrow$ M1 Gamification):** Citizen xác nhận biên nhận $\rightarrow$ Trạng thái chuyển sang `Completed` $\rightarrow$ Cộng `GreenPoints` ($\lfloor \text{TotalKg} \times 10 \rfloor$) và cập nhật `TotalRecycledKg` + `TreeLevel` trong bảng `users` / `user_eco_profiles` (`BR-TRN-05`). | `200 OK` (`MSG-RCP-02`), điểm xanh tăng chính xác |
| **ITC-M7-32..38** | **Khiếu nại Biên nhận & Lịch sử Giao dịch (`U-09`, `U-12`)** (`POST /receipt/dispute` & `GET /transactions`) | 7 | Citizen khiếu nại biên nhận kèm lý do bắt buộc (`Disputed`); kiểm tra biên nhận bị khiếu nại không xuất hiện trong lịch sử giao dịch hoàn tất; kiểm tra bộ lọc ngày (`fromDate`, `toDate`), tìm kiếm từ khóa và tổng hợp `totalCompletedOrders`, `totalWeightKg`, `totalAmount`. | `200 OK` / `400 Bad Request` |

---

## PHẦN 8: MODULE 8 – IN-APP REAL-TIME NEGOTIATION CHATBOX (`NegotiationChatTests.cs`)

### 8.1. Bảng Tổng hợp Kiểm thử (Summary Table)

| Code Module | `NegotiationChatController` + `NegotiationChatService` (`NegotiationChatTests.cs`) | | |
| :--- | :---: | :---: | :---: |
| **Number of Test Methods / Expanded TCs** | **10 Methods (16 TCs)** | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** |
| *Round 1* | 10 | 0 | 0 |
| **Testing Type** | **Normal** | **Abnormal** | **Boundary** |
| **Count (Methods)** | **6** | **2** | **2** |

### 8.2. Danh sách Chi tiết Test Cases (`NegotiationChatTests.cs`)

| Test Case ID | Tên Hàm Test (`NegotiationChatTests.cs`) | Endpoint & Phương thức | Kịch bản Kiểm thử Tích hợp Liên Module (`M6 Handover` $\leftrightarrow$ `M8 Negotiation Chat`) | Kết quả Mong đợi | Loại |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **ITC-M8-01** | `NEG01_GetChatThread_ParticipantsAllowed_OutsiderBlockedWithBrNeg01` | `GET /api/v1/trade/orders/{orderId}/messages` | Kiểm tra `BR-NEG-01`: Chủ đơn (`Citizen`) và Cơ sở nhận đơn (`Collector`) truy cập được luồng chat (`200 OK`); tài khoản thứ ba ngoài đơn bị chặn (`400/403`). | `200 OK` (`MSG-NEG-THREAD-OK`) & `400/403` | Normal |
| **ITC-M8-02** | `NEG02_SendMessage_TextAndUploadedAttachment_PersistsInThreadAndIncrementsUnread` | `POST /attachments` $\rightarrow$ `POST /messages` | Tải ảnh thực tế phế liệu lên qua `/attachments` (`MSG-NEG-UPLOAD-OK`) $\rightarrow$ Gửi tin nhắn `Text` và `Image` $\rightarrow$ Kiểm tra `unreadCount` phía đối phương tăng lên $\ge 2$. | `200 OK` & `201 Created` (`MSG-NEG-02`) | Normal |
| **ITC-M8-03** | `NEG03_SendMessage_InvalidPayload_Returns400BadRequest` *(4 kịch bản)* | `POST /api/v1/trade/orders/{orderId}/messages` | Kiểm tra biên và ngoại lệ tin nhắn: văn bản rỗng, văn bản vượt quá `1000` ký tự (`1005` ký tự), tin nhắn `Image` thiếu URL, loại tin nhắn `"Audio"` không hỗ trợ. | `400 Bad Request` | Boundary |
| **ITC-M8-04** | `NEG04_SendProposal_InvalidPriceWeightOrPastTimeSlot_Returns400BadRequest` *(4 kịch bản)* | `POST /api/v1/trade/orders/{orderId}/proposals` | Kiểm tra `BR-NEG-03` & `BR-NEG-05`: đơn giá `0đ`, đơn giá vượt trần `1.500.000đ/kg`, khối lượng `0.05kg` (`< 0.1kg`), khung giờ hẹn trong quá khứ. | `400 Bad Request` | Boundary |
| **ITC-M8-05** | `NEG05_SendProposal_AndCounterProposal_SupersedesPreviousActiveProposal_BrNeg04` | `POST /proposals` (Citizen) $\rightarrow$ `POST /proposals` (Collector) | Citizen gửi Đề xuất A (`9.000đ/kg`) $\rightarrow$ Collector gửi Phản đề xuất B (`8.500đ/kg`) $\rightarrow$ Đề xuất A tự động chuyển sang `Superseded` và `activeProposal` của phòng chat trỏ sang B (`BR-NEG-04`). | `201 Created` (`MSG-NEG-01`), A = `Superseded`, B = `Pending` | Normal |
| **ITC-M8-06** | `NEG06_RespondToProposal_SenderCannotRespondToOwnProposal_Returns400WithBrNeg04` | `POST /proposals/{messageId}/respond` | Người tạo Đề xuất tự gọi API chấp nhận đề xuất của chính mình $\rightarrow$ Bị chặn bởi quy tắc `BR-NEG-04`. | `400 Bad Request`, `BR-NEG-04` | Abnormal |
| **ITC-M8-07** | `NEG07_RespondToProposal_CounterpartyRejects_MarksProposalRejectedAndAllowsNewProposal` | `POST /respond` (`isAccepted=false`) $\rightarrow$ `POST /proposals` | Collector từ chối Đề xuất 1 (`MSG-NEG-REJECT-OK`, `ProposalStatus = Rejected`) $\rightarrow$ Phiên đàm phán giữ trạng thái `InNegotiation` và cho phép Citizen gửi Đề xuất 2 mới. | `200 OK` (`MSG-NEG-REJECT-OK`) $\rightarrow$ `201 Created` | Normal |
| **ITC-M8-08** | `NEG08_RespondToProposal_CounterpartyAccepts_LocksAgreedPriceAndTimeSlotIntoOrder_BrNeg06` | `POST /respond` (`isAccepted=true`) $\rightarrow$ `GET /handover-requests/{id}` | **Tích hợp Liên Module (M8 $\rightarrow$ M6):** Collector chấp nhận Đề xuất (`8.800đ/kg`, `12kg`) $\rightarrow$ Khóa cứng `AgreedPricePerKg = 8800`, `EstimatedTotalAmount = 105600` và `ConfirmedTimeSlot` vào đơn bàn giao Module 6 (`BR-NEG-06`), đồng thời chặn gửi thêm Proposal mới (`BR-NEG-02`). | `200 OK` (`MSG-NEG-03`) & khóa `BR-NEG-02` | Normal |
| **ITC-M8-09** | `NEG09_AcceptOrderTerms_DirectAcceptByRecyclerWithoutProposal_LocksNegotiationStatusAccepted` | `POST /api/v1/trade/orders/{orderId}/accept-terms` | Cơ sở thu gom chấp nhận trực tiếp điều khoản ban đầu của Citizen mà không cần qua bước tạo Proposal trung gian. | `200 OK` (`MSG-NEG-03`), `negotiationStatus = Accepted` | Normal |
| **ITC-M8-10** | `NEG10_MarkMessagesAsRead_UpdatesUnreadCountToZeroForRecipient` | `PUT /api/v1/trade/orders/{orderId}/messages/read` | Đánh dấu toàn bộ tin nhắn chưa đọc của đối phương là đã đọc (`IsRead = true`, `ReadAt = UtcNow`) và xác nhận `unreadCount == 0`. | `200 OK` (`MSG-NEG-READ-OK`), `unreadCount = 0` | Normal |

---

## PHẦN 9: HƯỚNG DẪN CHẠY BỘ KIỂM THỬ TÍCH HỢP (`EcoLink.ApiTests`)

### 9.1. Điều kiện Tiên quyết (Pre-conditions)
1. Cơ sở dữ liệu PostgreSQL (`ecolink_db`, bật extension `postgis`) đã được cập nhật đầy đủ Migration mới nhất và nạp dữ liệu mẫu (Demo Accounts: `citizen`, `collector`, `staff`, `admin`).
2. Máy chủ Backend `Ecolink.WebApi` đang chạy tại địa chỉ mặc định `http://localhost:5000` (hoặc cấu hình qua biến môi trường `ECOLINK_BASE_URL`).

### 9.2. Các Lệnh Thực thi Kiểm thử Tích hợp
- **Biên dịch kiểm tra toàn bộ dự án Integration Test:**
  ```powershell
  dotnet build "src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj"
  ```
- **Chạy toàn bộ 8 Module Integration Tests (236 Test Methods / 415 Kịch bản):**
  ```powershell
  dotnet test "src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj"
  ```
- **Chạy riêng từng Module theo `Trait Category`:**
  ```powershell
  dotnet test "src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj" --filter "Category=Auth"
  dotnet test "src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj" --filter "Category=WasteScan"
  dotnet test "src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj" --filter "Category=Onboarding"
  dotnet test "src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj" --filter "Category=Recycler"
  dotnet test "src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj" --filter "Category=Discovery"
  dotnet test "src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj" --filter "Category=Handover"
  dotnet test "src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj" --filter "Category=Receipt"
  dotnet test "src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj" --filter "Category=NegotiationChat"
  ```

