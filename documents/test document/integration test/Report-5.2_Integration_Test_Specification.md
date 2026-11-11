# REPORT 5.2 – INTEGRATION TEST REPORT DOCUMENT (SEP490_G141)

---

## SHEET 1: `Cover` – INTEGRATION TEST REPORT DOCUMENT

| Field | Value | Field | Value |
| :--- | :--- | :--- | :--- |
| **Project Name** | Intelligent Waste Sorting and Trading Platform (**EcoLink**) | **Creator** | Hoàng Thắng Nguyễn, Nguyen Thanh Luan (K18 HL), Giang Nguyễn Đức |
| **Project Code** | `SEP490_G141` | **Issue Date** | `2026-11-11` |
| **Document Code** | `SEP490_G141_ITR_v1.0` | **Version** | `1.0` |

### Record of Change

| Effective Date | Version | Change Item | \*A,D,M | Change description | Reference |
| :---: | :---: | :--- | :---: | :--- | :--- |
| `2026-11-01` | `0.1` | Module 01, 02, 04, 06, 07 Integration Test Suite | **A** | Khởi tạo bộ kiểm thử tích hợp API cho 5 phân hệ cốt lõi (`AuthTests`, `WasteScanTests`, `RecyclerTests`, `HandoverTests`, `ReceiptTests`). | `Report-3_SRS`, `Report-4_SDD`, `00_api_inventory_and_endpoint_statistics.md` |
| `2026-11-10` | `0.9` | Module 03, 05, 08 Cross-Module Integration Tests | **A** | Bổ sung 3 bộ kiểm thử tích hợp liên module (`OnboardingTests`, `DiscoveryTests`, `NegotiationChatTests`) và hợp nhất vào `src/Ecolink.Backend/EcoLink.ApiTests`. | `Report-5.0_Test_Plan`, `Report-5.1_Unit_Test_Specification.md` |
| `2026-11-11` | `1.0` | Full 8-Module + `SecurityTest` Integration Specification | **M** | Chuẩn hóa toàn bộ tài liệu `Report-5.2` theo đúng biểu mẫu `Report-5.2_Integration Test_SEP490_G141.xlsx` (140 Feature TCs + 16 Security TCs = **156 TCs**) và tách 9 tệp đặc tả chi tiết theo từng module. | `Report-5.2_Integration Test_SEP490_G141.xlsx` |

*(Ghi chú: `*A` = Added, `D` = Deleted, `M` = Modified)*

---

## SHEET 2: `Test Cases` – TEST CASE LIST

| Field | Value |
| :--- | :--- |
| **Project Name** | Intelligent Waste Sorting and Trading Platform (**EcoLink**) |
| **Project Code** | `SEP490_G141` |
| **Test Environment Setup Description** | **1. Server:**<br>- Core Backend: `ASP.NET Core 10.0 WebApi` (`Ecolink.WebApi`, Clean Architecture, SignalR Hub `/hubs/negotiation-chat`).<br>- Dedicated AI Vision Microservice: `Python 3.11 FastAPI` (`YOLOv8n` + `MobileNetV3` + `EfficientNet-B0`, `http://127.0.0.1:8000`).<br>**2. Database & Storage:**<br>- Relational & Spatial DB: `PostgreSQL 16` + `PostGIS 3.4` (`ecolink_db`, SRID 4326 Geography Point).<br>- Object Storage: `Cloudflare R2` (S3-compatible API) / Local Fallback Storage.<br>- In-Memory Cache: `IMemoryCache` / `Redis 7`.<br>**3. Web Browser & Integration Test Runner:**<br>- Test Harness: `.NET 10.0 xUnit Runner` (`src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj`, `HttpClient` End-to-End Pipeline).<br>- Client Browser: Google Chrome / Microsoft Edge `v130+`. |

### Danh mục Chức năng Kiểm thử Tích hợp (Function List)

| No | Function Name | Sheet Name | Description | Pre-Condition | Tài liệu Chi tiết Từng Module |
| :---: | :--- | :--- | :--- | :--- | :--- |
| **1** | Function 1.1: System Health & Citizen Registration | `M01_Auth` | Kiểm tra `/health` và đăng ký tài khoản `Citizen` (`POST /api/v1/auth/register`), khởi tạo `UserEcoProfile`, chặn trùng `username`/`email` (`BR-AUTH-01`). | PostgreSQL `ecolink_db` sẵn sàng; bảng `users` đã migrate. | [`01_integration_test_authentication.md`](./01_integration_test_authentication.md) |
| **2** | Function 1.2: Recycler Direct Registration | `M01_Auth` | Đăng ký trực tiếp tài khoản `Recycler` (`POST /api/v1/auth/register-recycler`), tạo `RecyclerProfile` trạng thái `Pending` kèm tọa độ PostGIS. | Chưa tồn tại `username`/`email` trùng trong DB. | [`01_integration_test_authentication.md`](./01_integration_test_authentication.md) |
| **3** | Function 1.3: User Login & Brute-Force Lockout | `M01_Auth` | Đăng nhập bằng `username`/`email` (`POST /api/v1/auth/login`), chống User Enumeration (`BR-AUTH-03`), khóa 15 phút sau 5 lần sai (`BR-AUTH-04`). | Đã seed tài khoản `citizen`, `collector`, `staff`, `admin`. | [`01_integration_test_authentication.md`](./01_integration_test_authentication.md) |
| **4** | Function 1.4: Current User Profile & JWT Verification | `M01_Auth` | Xác thực JWT Bearer token và truy xuất hồ sơ kèm điểm xanh qua `GET /api/v1/auth/me`. | Người dùng đã đăng nhập và có JWT hợp lệ. | [`01_integration_test_authentication.md`](./01_integration_test_authentication.md) |
| **5** | Function 1.5: Token Rotation & Password Recovery | `M01_Auth` | Xoay vòng Refresh Token chống Replay Attack (`BR-AUTH-05`), Quên/Đặt lại mật khẩu OTP (`BR-AUTH-09`) và cập nhật hồ sơ cá nhân. | Tài khoản đang hoạt động (`Active`). | [`01_integration_test_authentication.md`](./01_integration_test_authentication.md) |
| **6** | Function 1.6: Demo Accounts & VietQR Partner Token | `M01_Auth` | Kiểm tra `/api/v1/auth/demo-users` không lộ hash mật khẩu và cổng Basic Auth `/api/v1/auth/vietqr-token`. | Hệ thống hoạt động bình thường. | [`01_integration_test_authentication.md`](./01_integration_test_authentication.md) |
| **7** | Function 2.1: AI Model Catalog & Waste Rule Engine | `M02_WasteScan` | Truy vấn danh sách mô hình AI (`GET /models`), tập luật phân loại rác (`GET /rules`) và danh mục phế liệu chuẩn (`GET /waste-categories`). | `Ecolink.WebApi` và `FastAPI AI Service` đang chạy. | [`02_integration_test_ai_classification_and_rule_engine.md`](./02_integration_test_ai_classification_and_rule_engine.md) |
| **8** | Function 2.2: AI Image Classification & OOD Gate | `M02_WasteScan` | Gửi ảnh rác qua `POST /api/v1/waste-scan/classify`, kiểm tra độ tự tin $[0,1]$, ngưỡng OOD (`BR-AI-02`) và xử lý tệp lỗi (`BR-AI-01`). | Dịch vụ AI Vision đã nạp trọng số mô hình. | [`02_integration_test_ai_classification_and_rule_engine.md`](./02_integration_test_ai_classification_and_rule_engine.md) |
| **9** | Function 2.3: Classification Feedback & Public Access | `M02_WasteScan` | Gửi phản hồi hiệu chỉnh nhãn AI (`POST /api/v1/waste-scan/feedback`) và kiểm tra luồng khép kín `Classify` $\rightarrow$ `Feedback`. | Hệ thống hoạt động bình thường. | [`02_integration_test_ai_classification_and_rule_engine.md`](./02_integration_test_ai_classification_and_rule_engine.md) |
| **10** | Function 3.1: Business License & Facility Photo Upload | `M03_Onboarding` | Tải chứng từ kinh doanh (`permit`) và ảnh cơ sở (`facility-photo`) lên R2/Local (`POST /api/v1/recycler-applications/upload-document`). | Người dùng `Citizen` đã đăng nhập. | [`03_integration_test_recycler_onboarding_and_verification.md`](./03_integration_test_recycler_onboarding_and_verification.md) |
| **11** | Function 3.2: Citizen Upgrade Application Submission | `M03_Onboarding` | Nộp hồ sơ nâng cấp lên `Recycler` (`POST /api/v1/recycler-applications`), chặn nộp trùng (`BR-MTH-06`), tra cứu tiến độ (`GET /me`). | Tài khoản đang có `Role = Citizen`. | [`03_integration_test_recycler_onboarding_and_verification.md`](./03_integration_test_recycler_onboarding_and_verification.md) |
| **12** | Function 3.3: Staff/Admin Application Queue & Detail | `M03_Onboarding` | Nhân viên `Staff`/`Admin` lọc danh sách hồ sơ chờ duyệt và xem chi tiết chứng từ (`GET /api/v1/recycler-applications`). | Đã đăng nhập tài khoản `Staff` hoặc `Admin`. | [`03_integration_test_recycler_onboarding_and_verification.md`](./03_integration_test_recycler_onboarding_and_verification.md) |
| **13** | Function 3.4: Verification, Resubmission & Role/Price Seed | `M03_Onboarding` | Từ chối hồ sơ kèm lý do (`MSG-MTH-05`) cho phép nộp lại; Phê duyệt (`MSG-MTH-04`) tự động nâng cấp Role sang `Recycler` (**M1**) và seed bảng giá (**M4**). | Hồ sơ đang ở trạng thái `Pending`. | [`03_integration_test_recycler_onboarding_and_verification.md`](./03_integration_test_recycler_onboarding_and_verification.md) |
| **14** | Function 4.1: RBAC & Facility Profile Management | `M04_Recycler` | Kiểm soát quyền `Recycler` và xem/cập nhật thông tin hồ sơ, tọa độ GPS (`GET/PUT /api/v1/recycler/profile`). | Đã đăng nhập tài khoản `Recycler`. | [`04_integration_test_recycler_profile_pricing_and_availability.md`](./04_integration_test_recycler_profile_pricing_and_availability.md) |
| **15** | Function 4.2: Single Waste Category Price Configuration | `M04_Recycler` | Thêm/Sửa đơn giá từng loại phế liệu (`GET/POST /prices`, `PUT /prices/{id}`), chặn tắt dòng giá active cuối cùng (`BR-PRC-03` / `MSG-PRC-02`). | Bảng `waste_categories` đã có dữ liệu chuẩn. | [`04_integration_test_recycler_profile_pricing_and_availability.md`](./04_integration_test_recycler_profile_pricing_and_availability.md) |
| **16** | Function 4.3: Atomic Bulk Price Save | `M04_Recycler` | Lưu đồng loạt bảng giá trong 1 giao dịch nguyên tử (`PUT /api/v1/recycler/prices`), tự động rollback nếu có phần tử lỗi. | Đã đăng nhập tài khoản `Recycler`. | [`04_integration_test_recycler_profile_pricing_and_availability.md`](./04_integration_test_recycler_profile_pricing_and_availability.md) |
| **17** | Function 4.4: Online/Offline Availability Toggle | `M04_Recycler` | Bật/Tắt trạng thái nhận đơn tức thời (`GET/PATCH /api/v1/recycler/availability`), chặn cơ sở `Pending` bật `Online` (`BR-MTH-01`). | Cơ sở đã được duyệt (`Verified`) và có bảng giá active. | [`04_integration_test_recycler_profile_pricing_and_availability.md`](./04_integration_test_recycler_profile_pricing_and_availability.md) |
| **18** | Function 4.5: Weekly Operating Schedule Configuration | `M04_Recycler` | Thiết lập lịch mở cửa 7 ngày trong tuần (`PUT /api/v1/recycler/availability/schedule`) và tổng hợp `openingHoursSummary`. | Cơ sở đã được duyệt (`Verified`). | [`04_integration_test_recycler_profile_pricing_and_availability.md`](./04_integration_test_recycler_profile_pricing_and_availability.md) |
| **19** | Function 5.1: Spatial Radius Search & Validation | `M05_Discovery` | Tìm kiếm cơ sở theo tọa độ GPS (`GET /api/v1/recyclers/nearby`), kiểm tra biên bán kính $[0.5, 50]\text{ km}$ và tọa độ (`MSG-MTH-02`). | Có cơ sở `Verified` + `Online` trong CSDL. | [`05_integration_test_location_based_recycler_discovery.md`](./05_integration_test_location_based_recycler_discovery.md) |
| **20** | Function 5.2: Category Filter, Price Sort & Fallback | `M05_Discovery` | Lọc theo loại phế liệu, sắp xếp `price_desc` (`BR-MTH-07`), gợi ý mở rộng bán kính (`MSG-MTH-01`) và đồng bộ tức thời với trạng thái Online ở **M4**. | Cơ sở thu gom đã cấu hình bảng giá ở Module 4. | [`05_integration_test_location_based_recycler_discovery.md`](./05_integration_test_location_based_recycler_discovery.md) |
| **21** | Function 5.3: Public Detail, Trade Partners & Geocoding | `M05_Discovery` | Xem chi tiết công khai cơ sở (`GET /recyclers/{id}`), đối tác lân cận (`GET /trade/partners/nearby`) và Geocoding Proxy (`GET /geocoding/search`). | Hệ thống hoạt động bình thường. | [`05_integration_test_location_based_recycler_discovery.md`](./05_integration_test_location_based_recycler_discovery.md) |
| **22** | Function 6.1: Handover RBAC Gate | `M06_Handover` | Kiểm soát phân quyền giữa `Citizen` (`/api/v1/handover-requests`) và `Recycler` (`/api/v1/recycler/handover-requests`). | Hệ thống hoạt động bình thường. | [`06_integration_test_handover_request_management.md`](./06_integration_test_handover_request_management.md) |
| **23** | Function 6.2: Handover Request Creation & Price Snapshot | `M06_Handover` | Tạo yêu cầu bàn giao (`POST /handover-requests`), chụp nhanh `PriceSnapshotPerKg` (`BR-HND-03`), giới hạn tối đa 3 đơn mở (`BR-HND-02`). | Cơ sở nhận đơn đang `Online` và trong giờ mở cửa. | [`06_integration_test_handover_request_management.md`](./06_integration_test_handover_request_management.md) |
| **24** | Function 6.3: Listing, Filtering & Phone Privacy | `M06_Handover` | Lọc danh sách đơn theo trạng thái/ngày, chống IDOR và ẩn số điện thoại khi đơn đang `Pending` (`BR-PRIV-01`). | Đã có đơn bàn giao trong CSDL. | [`06_integration_test_handover_request_management.md`](./06_integration_test_handover_request_management.md) |
| **25** | Function 6.4: Citizen Update & Late Cancellation | `M06_Handover` | Sửa đơn khi `Pending` (`PUT /{id}`), khóa sửa sau khi `Accepted`, hủy đơn và gắn cờ cảnh báo hủy muộn `< 2h` (`BR-HND-05`). | Đơn bàn giao thuộc quyền sở hữu của `Citizen`. | [`06_integration_test_handover_request_management.md`](./06_integration_test_handover_request_management.md) |
| **26** | Function 6.5: Recycler Accept, Reject & Cancel | `M06_Handover` | Cơ sở chấp nhận đơn (`POST /accept` – mở khóa SĐT), từ chối (`POST /reject`) hoặc hủy đơn (`POST /cancel`) bắt buộc có lý do (`BR-HND-06`). | Đơn bàn giao gửi đến cơ sở của `Recycler`. | [`06_integration_test_handover_request_management.md`](./06_integration_test_handover_request_management.md) |
| **27** | Function 7.1: Receipt RBAC & Ownership Protection | `M07_Receipt` | Kiểm soát phân quyền và chống IDOR trên toàn bộ 8 endpoints biên nhận và lịch sử giao dịch. | Hệ thống hoạt động bình thường. | [`07_integration_test_handover_completion_and_transaction_receipt.md`](./07_integration_test_handover_completion_and_transaction_receipt.md) |
| **28** | Function 7.2: Receipt Preview & Deviation Warning | `M07_Receipt` | Xem trước biên nhận (`POST /receipt/preview`) và cảnh báo khi khối lượng thực tế lệch $> 20\%$ (`MSG-RCP-03` – `BR-TRN-02`). | Đơn bàn giao đang ở trạng thái `Accepted`. | [`07_integration_test_handover_completion_and_transaction_receipt.md`](./07_integration_test_handover_completion_and_transaction_receipt.md) |
| **29** | Function 7.3: Digital Receipt Issuance & Scale Photo | `M07_Receipt` | Phát hành biên nhận `RCP-yyyy-NNNN` (`POST /receipt`), bắt buộc ảnh đồng hồ cân khi $\ge 10\text{ kg}$ (`BR-TRN-01` / `MSG-RCP-04`), hạn tự động duyệt 24h. | Đơn bàn giao đang ở trạng thái `Accepted`. | [`07_integration_test_handover_completion_and_transaction_receipt.md`](./07_integration_test_handover_completion_and_transaction_receipt.md) |
| **30** | Function 7.4: Confirm Receipt, GreenPoints & Dispute | `M07_Receipt` | `Citizen` xác nhận biên nhận (`POST /confirm`) cộng `GreenPoints` (**M1 Gamification**) hoặc khiếu nại sai lệch (`POST /dispute`). | Đơn đang ở trạng thái `AwaitingConfirmation`. | [`07_integration_test_handover_completion_and_transaction_receipt.md`](./07_integration_test_handover_completion_and_transaction_receipt.md) |
| **31** | Function 7.5: Transaction History & Summary Aggregation | `M07_Receipt` | Tra cứu lịch sử giao dịch hoàn tất (`GET /transactions`, `/recycler/transactions`), lọc ngày/từ khóa và cộng dồn thống kê `summary`. | Đã có giao dịch ở trạng thái `Completed`. | [`07_integration_test_handover_completion_and_transaction_receipt.md`](./07_integration_test_handover_completion_and_transaction_receipt.md) |
| **32** | Function 8.1: Negotiation Thread Access Control | `M08_Negotiation` | Mở luồng chat theo đơn (`GET /api/v1/trade/orders/{orderId}/messages`) và chặn người ngoài đơn (`BR-NEG-01`). | Đơn bàn giao đã được khởi tạo ở Module 6. | [`08_integration_test_in_app_realtime_negotiation_chatbox.md`](./08_integration_test_in_app_realtime_negotiation_chatbox.md) |
| **33** | Function 8.2: Attachment Upload & Text/Image Messaging | `M08_Negotiation` | Tải ảnh phế liệu đính kèm (`POST /attachments`) và gửi tin nhắn `Text`/`Image` (`POST /messages`), cập nhật `unreadCount`. | Người gửi là thành viên của đơn bàn giao. | [`08_integration_test_in_app_realtime_negotiation_chatbox.md`](./08_integration_test_in_app_realtime_negotiation_chatbox.md) |
| **34** | Function 8.3: Price/Time Proposal & Superseding | `M08_Negotiation` | Gửi Đề xuất giá/giờ (`POST /proposals`) và Phản đề xuất tự động ghi đè đề xuất cũ sang `Superseded` (`BR-NEG-04`). | Đơn đang ở trạng thái cho phép thương lượng. | [`08_integration_test_in_app_realtime_negotiation_chatbox.md`](./08_integration_test_in_app_realtime_negotiation_chatbox.md) |
| **35** | Function 8.4: Proposal Response, Lock Terms & Mark Read | `M08_Negotiation` | Chấp nhận/Từ chối Đề xuất (`POST /respond`), khóa cứng giá & giờ vào đơn Module 6 (`BR-NEG-06`), chấp nhận nhanh (`/accept-terms`) và đánh dấu đã đọc (`PUT /read`). | Đã có Proposal đang `Pending` hoặc tin nhắn chưa đọc. | [`08_integration_test_in_app_realtime_negotiation_chatbox.md`](./08_integration_test_in_app_realtime_negotiation_chatbox.md) |
| **36** | Function 9.1: Cross-Module Security & Authorization | `SecurityTest` | Kiểm thử bảo mật tích hợp: Chống rò rỉ dữ liệu, User Enumeration, Brute-Force, Replay Attack, SQLi/XSS, RBAC & IDOR. | Toàn bộ 8 Module đang hoạt động. | [`09_integration_test_security.md`](./09_integration_test_security.md) |

---

## SHEET 3: `Test Statistics` – INTEGRATION TEST STATISTICS

| Field | Value | Field | Value |
| :--- | :--- | :--- | :--- |
| **Project Name** | Intelligent Waste Sorting and Trading Platform (**EcoLink**) | **Creator** | Hoàng Thắng Nguyễn, Nguyen Thanh Luan (K18 HL), Giang Nguyễn Đức |
| **Project Code** | `SEP490_G141` | **Reviewer/Approver** | GVHD / Hội đồng Chấm Đồ án SEP490 |
| **Document Code** | `SEP490_G141_Test Report_v1.0` | **Issue Date** | `2026-11-11` |
| **Notes** | Release 1.0 bao gồm đầy đủ 8 Core Business Modules (`M01_Auth` $\rightarrow$ `M08_Negotiation`) và 1 Sheet Kiểm thử Bảo mật Tích hợp (`SecurityTest`), thực thi tự động qua project [`src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj`](../../../src/Ecolink.Backend/EcoLink.ApiTests/EcoLink.ApiTests.csproj). | | |

### Bảng Thống kê Kết quả Kiểm thử Tích hợp (Integration Test Statistics Table)

| No | Module code | Passed | Failed | Pending | N/A | Number of test cases | File Đặc tả Chi tiết & File Code Test |
| :---: | :--- | :---: | :---: | :---: | :---: | :---: | :--- |
| **1** | `M01_Auth` (Authentication & Account Management) | 20 | 0 | 0 | 0 | **20** | [`01_integration_test_authentication.md`](./01_integration_test_authentication.md) / [`AuthTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/AuthTests.cs) |
| **2** | `M02_WasteScan` (AI Waste Classification & Rule Engine) | 15 | 0 | 0 | 0 | **15** | [`02_integration_test_ai_classification_and_rule_engine.md`](./02_integration_test_ai_classification_and_rule_engine.md) / [`WasteScanTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/WasteScanTests.cs) |
| **3** | `M03_Onboarding` (Recycler Onboarding & Verification) | 12 | 0 | 0 | 0 | **12** | [`03_integration_test_recycler_onboarding_and_verification.md`](./03_integration_test_recycler_onboarding_and_verification.md) / [`OnboardingTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/OnboardingTests.cs) |
| **4** | `M04_Recycler` (Recycler Profile, Pricing & Availability) | 24 | 0 | 0 | 0 | **24** | [`04_integration_test_recycler_profile_pricing_and_availability.md`](./04_integration_test_recycler_profile_pricing_and_availability.md) / [`RecyclerTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/RecyclerTests.cs) |
| **5** | `M05_Discovery` (Location-Based Recycler Discovery) | 12 | 0 | 0 | 0 | **12** | [`05_integration_test_location_based_recycler_discovery.md`](./05_integration_test_location_based_recycler_discovery.md) / [`DiscoveryTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/DiscoveryTests.cs) |
| **6** | `M06_Handover` (Handover Request Management) | 22 | 0 | 0 | 0 | **22** | [`06_integration_test_handover_request_management.md`](./06_integration_test_handover_request_management.md) / [`HandoverTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/HandoverTests.cs) |
| **7** | `M07_Receipt` (Handover Completion & Transaction Receipt) | 20 | 0 | 0 | 0 | **20** | [`07_integration_test_handover_completion_and_transaction_receipt.md`](./07_integration_test_handover_completion_and_transaction_receipt.md) / [`ReceiptTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/ReceiptTests.cs) |
| **8** | `M08_Negotiation` (In-App Real-Time Negotiation Chatbox) | 15 | 0 | 0 | 0 | **15** | [`08_integration_test_in_app_realtime_negotiation_chatbox.md`](./08_integration_test_in_app_realtime_negotiation_chatbox.md) / [`NegotiationChatTests.cs`](../../../src/Ecolink.Backend/EcoLink.ApiTests/NegotiationChatTests.cs) |
| **9** | `SecurityTest` (Cross-Module Security & Authorization) | 16 | 0 | 0 | 0 | **16** | [`09_integration_test_security.md`](./09_integration_test_security.md) |
| | **Sub total** | **156** | **0** | **0** | **0** | **156** | **236 Automated Test Methods (415 Data Scenarios)** |

- **Test coverage:** `100,00 %` (`156 / 156` Test Cases Executed)
- **Test successful coverage:** `100,00 %` (`156 / 156` Test Cases Passed)

---

## SHEET 4: `M01_Auth` – AUTHENTICATION & ACCOUNT MANAGEMENT
*(Chi tiết đầy đủ xem tại: [`01_integration_test_authentication.md`](./01_integration_test_authentication.md))*

| Feature | `Authentication & Account Management (M01_Auth)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử tích hợp Đăng ký Citizen/Recycler (`U-01`, `R-00`), Đăng nhập JWT & Chống Brute-Force (`U-02`, `BR-AUTH-04`), Refresh Token Rotation (`BR-AUTH-05`), Quên/Đặt lại mật khẩu (`U-03`, `BR-AUTH-09`) và `/api/v1/auth/me`. | | | |
| **Number of TCs** | **20** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 20 | 0 | 0 | 0 |
| *Round 2* | 0 | 0 | 20 | 0 |
| *Round 3* | 0 | 0 | 20 | 0 |

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Function 1.1: System Health & Citizen Registration** | | | | | | | | | |
| **ITC-AUTH-001** | Kiểm tra kết nối tổng thể của `Ecolink.WebApi` tới PostgreSQL qua `/health`. | 1. Gửi `GET /health`. | HTTP `200 OK`, trạng thái `Healthy`. | `Ecolink.WebApi` và PostgreSQL đang chạy. | Passed | Pending | Pending | N/A | `AuthTests.Health_ReturnsOk` |
| **ITC-AUTH-002** | Đăng ký tài khoản `Citizen` mới hợp lệ và tự động khởi tạo `UserEcoProfile`. | 1. Gửi `POST /api/v1/auth/register` với `username`, `email`, `password`, `fullName`, `phoneNumber`. | HTTP `200/201`, `success = true`, tạo `UserEcoProfile` (`treeLevel = 1`). | Chưa tồn tại `username`/`email` trùng. | Passed | Pending | Pending | N/A | `AuthTests.Register_WithValidData_Succeeds` |
| **ITC-AUTH-003** | Đăng ký tài khoản `Citizen` khi bỏ trống `phoneNumber`. | 1. Gửi `POST /api/v1/auth/register` không có trường `phoneNumber`. | HTTP `200/201`, `success = true`, `phoneNumber = null`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `AuthTests.Register_WithoutOptionalPhoneNumber_Succeeds` |
| **ITC-AUTH-004** | Kiểm tra biên độ dài trường khi đăng ký (`username` 50, `password` 6 & 100, `fullName` 100 ký tự). | 1. Gửi `POST /api/v1/auth/register` ở biên trên và biên dưới độ dài. | HTTP `200/201`, lưu đầy đủ không lỗi cắt chuỗi. | Chưa tồn tại tài khoản trùng. | Passed | Pending | Pending | N/A | `AuthTests.Register_WithMaxLengthFields_Succeeds` |
| **ITC-AUTH-005** | Chặn đăng ký khi trùng `username` đã tồn tại (`BR-AUTH-01`). | 1. Gửi `POST /api/v1/auth/register` với `username` đã có trong DB. | HTTP `400 Bad Request`, `MSG-AUTH-01`. | `username` đã tồn tại trong bảng `users`. | Passed | Pending | Pending | N/A | `AuthTests.Register_WithDuplicateUsername_Fails` |
| **ITC-AUTH-006** | Chặn đăng ký khi trùng `email` đã tồn tại (`BR-AUTH-01`). | 1. Gửi `POST /api/v1/auth/register` với `email` đã có trong DB. | HTTP `400 Bad Request`, `MSG-AUTH-01`. | `email` đã tồn tại trong bảng `users`. | Passed | Pending | Pending | N/A | `AuthTests.Register_WithDuplicateEmail_Fails` |
| **ITC-AUTH-007** | Kiểm tra `FluentValidation` chặn 11 kịch bản dữ liệu đăng ký không hợp lệ. | 1. Gửi `POST /api/v1/auth/register` với 11 biến thể lỗi định dạng/thiếu trường. | HTTP `400 Bad Request` trên cả 11 kịch bản. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `AuthTests.Register_WithInvalidData_Returns400` |
| **Function 1.2: Recycler Direct Registration** | | | | | | | | | |
| **ITC-AUTH-008** | Đăng ký trực tiếp `Recycler` tạo đồng thời `RecyclerProfile` trạng thái `Pending` kèm tọa độ PostGIS. | 1. Gửi `POST /api/v1/auth/register-recycler` kèm thông tin cơ sở và tọa độ GPS. | HTTP `200/201`, `Role = Recycler`, `VerificationStatus = Pending`. | Chưa tồn tại `username`/`email` trùng. | Passed | Pending | Pending | N/A | `RecyclerHelper.PendingRecyclerToken` |
| **Function 1.3: User Login & Brute-Force Lockout** | | | | | | | | | |
| **ITC-AUTH-009** | Đăng ký xong $\rightarrow$ Đăng nhập ngay bằng cả `username` và `email`. | 1. Đăng ký tài khoản mới.<br>2. Đăng nhập bằng `username` và bằng `email`. | HTTP `200 OK`, trả về `accessToken` hợp lệ cho cả 2 cách. | Tài khoản vừa đăng ký thành công. | Passed | Pending | Pending | N/A | `AuthTests.Register_ThenLogin_WorksWithUsernameAndEmail` |
| **ITC-AUTH-010** | Đăng nhập thành công với 3 tài khoản mẫu (`citizen`, `collector`, `admin`). | 1. Gửi `POST /api/v1/auth/login` cho từng vai trò. | HTTP `200 OK`, trả về đúng `role`, `currentGreenPoints`, `treeLevel`. | CSDL đã seed tài khoản demo. | Passed | Pending | Pending | N/A | `AuthTests.Login_WithDemoAccount_Succeeds` |
| **ITC-AUTH-011** | Đăng nhập sai mật khẩu và sai tài khoản trả về cùng thông báo (`BR-AUTH-03`). | 1. Gửi `POST /login` với mật khẩu sai và với user không tồn tại. | HTTP `400 Bad Request`, thông báo lỗi đồng nhất. | Tài khoản `citizen` tồn tại. | Passed | Pending | Pending | N/A | `AuthTests.Login_ErrorMessage_DoesNotRevealWhichAccountsExist` |
| **ITC-AUTH-012** | Khóa tài khoản 15 phút sau 5 lần đăng nhập sai liên tiếp (`BR-AUTH-04`). | 1. Nhập sai mật khẩu 5 lần liên tiếp, lần 6 nhập mật khẩu đúng. | HTTP `400 Bad Request` (`MSG-AUTH-03`), tài khoản bị khóa 15 phút. | Tài khoản đang `Active`. | Passed | Pending | Pending | N/A | `BR-AUTH-04` Lockout Flow |
| **ITC-AUTH-013** | Từ chối đăng nhập khi thiếu trường hoặc chứa payload SQL Injection / XSS. | 1. Gửi `POST /login` với `{}` hoặc `' OR '1'='1`. | HTTP `400 Bad Request`, `accessToken = null`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `AuthTests.Login_WithInjectionPayload_IsRejected` |
| **Function 1.4: Current User Profile & JWT Verification** | | | | | | | | | |
| **ITC-AUTH-014** | Lấy thông tin người dùng hiện tại qua `GET /api/v1/auth/me` với JWT hợp lệ. | 1. Gửi `GET /api/v1/auth/me` kèm Bearer token của từng vai trò. | HTTP `200 OK`, trả về đúng `id`, `username`, `role`. | Đã đăng nhập hợp lệ. | Passed | Pending | Pending | N/A | `AuthTests.Me_WithValidToken_ReturnsCurrentUser` |
| **ITC-AUTH-015** | Chặn `GET /api/v1/auth/me` khi không có token, token sai hoặc chữ ký bị giả mạo. | 1. Gửi `GET /api/v1/auth/me` không token hoặc sửa 4 ký tự cuối của JWT. | HTTP `401 Unauthorized`. | Middleware JWT kiểm tra chữ ký HMAC-SHA256. | Passed | Pending | Pending | N/A | `AuthTests.Me_WithTamperedToken_Returns401` |
| **Function 1.5: Token Rotation, Password Recovery & Profile Update** | | | | | | | | | |
| **ITC-AUTH-016** | Xoay vòng Refresh Token và phát hiện Token Replay Attack (`BR-AUTH-05`). | 1. Gọi `POST /refresh-token` lấy token mới.<br>2. Gửi lại `refreshToken` cũ đã thu hồi. | Bước 1: `200 OK`. Bước 2: `400/401` (`MSG-AUTH-10`), thu hồi toàn bộ phiên. | Đã có `refreshToken` hợp lệ. | Passed | Pending | Pending | N/A | `BR-AUTH-05` Token Rotation |
| **ITC-AUTH-017** | Quên mật khẩu (`POST /forgot-password`), Rate Limit 60s (`BR-AUTH-09`) và Đặt lại mật khẩu (`POST /reset-password`). | 1. Gửi yêu cầu quên mật khẩu.<br>2. Đặt lại mật khẩu mới bằng mã OTP. | HTTP `200 OK` (`MSG-AUTH-08`, `MSG-AUTH-09`), cập nhật `PasswordHash`. | Tài khoản tồn tại trong DB. | Passed | Pending | Pending | N/A | `BR-AUTH-09` & `BR-AUTH-06` |
| **ITC-AUTH-018** | Cập nhật hồ sơ (`PUT /profile`), đổi mật khẩu (`POST /change-password`) và đăng xuất (`POST /logout`). | 1. Gọi tuần tự 3 endpoints với JWT hợp lệ. | Cả 3 bước trả về HTTP `200 OK`, cập nhật DB chính xác. | Đã đăng nhập hợp lệ. | Passed | Pending | Pending | N/A | Account Lifecycle |
| **Function 1.6: Demo Accounts & VietQR Partner Token** | | | | | | | | | |
| **ITC-AUTH-019** | Kiểm tra `GET /api/v1/auth/demo-users` không làm lộ `passwordHash`. | 1. Gửi `GET /api/v1/auth/demo-users`. | HTTP `200 OK`, response không chứa `"passwordhash"`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `AuthTests.DemoUsers_DoesNotExposePasswordHash` |
| **ITC-AUTH-020** | Kiểm tra `POST /api/v1/auth/vietqr-token` từ chối request thiếu Basic Auth hoặc dùng Bearer JWT của user. | 1. Gửi `POST /api/v1/auth/vietqr-token` với Bearer token của `citizen`. | HTTP `401 Unauthorized`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `AuthTests.VietQrToken_WithUserBearerToken_Returns401` |

---

## SHEET 5: `M02_WasteScan` – AI WASTE CLASSIFICATION & RULE ENGINE
*(Chi tiết đầy đủ xem tại: [`02_integration_test_ai_classification_and_rule_engine.md`](./02_integration_test_ai_classification_and_rule_engine.md))*

| Feature | `AI Waste Classification & Rule Engine (M02_WasteScan)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử tích hợp Nhận diện ảnh rác bằng AI (`U-04`), danh sách mô hình (`GET /models`), tập luật phân loại (`GET /rules`, `GET /waste-categories`), kiểm tra ngưỡng độ tự tin OOD (`BR-AI-02`), xử lý ảnh lỗi (`BR-AI-01`) và gửi phản hồi (`POST /feedback`). | | | |
| **Number of TCs** | **15** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 15 | 0 | 0 | 0 |
| *Round 2* | 0 | 0 | 15 | 0 |
| *Round 3* | 0 | 0 | 15 | 0 |

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Function 2.1: AI Model Catalog & Waste Sorting Rule Engine** | | | | | | | | | |
| **ITC-AI-001** | Lấy danh sách các mô hình AI khả dụng qua `GET /api/v1/waste-scan/models`. | 1. Gửi `GET /api/v1/waste-scan/models`. | HTTP `200 OK`, danh sách mô hình khác rỗng. | `Ecolink.WebApi` & `FastAPI` đang chạy. | Passed | Pending | Pending | N/A | `WasteScanTests.Models_ReturnsNonEmptyList` |
| **ITC-AI-002** | Truy vấn toàn bộ tập luật phân loại rác qua `GET /api/v1/waste-scan/rules`. | 1. Gửi `GET /api/v1/waste-scan/rules`. | HTTP `200 OK`, trả về đủ luật cho 6 nhóm rác. | `WasteRuleEngineService` sẵn sàng. | Passed | Pending | Pending | N/A | `WasteScanTests.Rules_ReturnsNonEmptyJson` |
| **ITC-AI-003** | Lấy danh mục loại phế liệu chuẩn qua `GET /api/v1/waste-categories`. | 1. Gửi `GET /api/v1/waste-categories`. | HTTP `200 OK`, trả về `plastic`, `paper`, `cardboard`, `glass`, `metal`. | Bảng `waste_categories` đã seed. | Passed | Pending | Pending | N/A | `RecyclerHelper.GetCategoriesAsync` |
| **Function 2.2: AI Image Classification & OOD Confidence Gate** | | | | | | | | | |
| **ITC-AI-004** | Phân loại ảnh rác thành công với mô hình mặc định (`POST /api/v1/waste-scan/classify`). | 1. Đóng gói ảnh PNG `64x64` vào `multipart/form-data` và gửi `POST /classify`. | HTTP `200 OK`, trả về nhãn phân loại, `confidence` và hướng dẫn xử lý. | Dịch vụ AI đang hoạt động. | Passed | Pending | Pending | N/A | `WasteScanTests.Classify_WithDefaultModel_ReturnsExpectedShape` |
| **ITC-AI-005** | Phân loại ảnh rác khi chỉ định tường minh `modelName`. | 1. Lấy tên model từ `/models` và gửi `POST /classify` kèm `modelName`. | HTTP `200 OK`, suy luận đúng mô hình chỉ định. | Model tồn tại trong danh sách `/models`. | Passed | Pending | Pending | N/A | `WasteScanTests.Classify_WithExplicitModel_Succeeds` |
| **ITC-AI-006** | Kiểm tra biên xác suất dự đoán `confidence` thuộc $[0.0, 1.0]$. | 1. Gửi ảnh PNG qua `POST /classify` và đọc `confidence`. | HTTP `200 OK`, $0.0 \le \text{confidence} \le 1.0$. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `WasteScanTests.Classify_PredictionConfidence_IsBetween0And1` |
| **ITC-AI-007** | Chặn request phân loại khi không đính kèm trường `file` (`BR-AI-01`). | 1. Gửi `POST /classify` thiếu trường `file`. | HTTP `400 Bad Request`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `WasteScanTests.Classify_WithoutFile_Returns400` |
| **ITC-AI-008** | Chặn request phân loại khi tệp ảnh rỗng (`0 byte`). | 1. Gửi `POST /classify` với `empty.png` (`0 byte`). | HTTP `400 Bad Request`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `WasteScanTests.Classify_WithEmptyFile_Returns400` |
| **ITC-AI-009** | Chặn request khi tải lên tệp văn bản `.txt` giả làm ảnh (`BR-AI-01`). | 1. Gửi `POST /classify` với tệp `note.txt` (`text/plain`). | HTTP `4xx Client Error` (`400`/`415`). | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `WasteScanTests.Classify_WithNonImageFile_ReturnsClientError` |
| **ITC-AI-010** | Kiểm tra gửi ảnh PNG bị hỏng cấu trúc byte không gây lỗi `500`. | 1. Gửi mảng byte hỏng với header `image/png` vào `POST /classify`. | HTTP Status `< 500`, bắt ngoại lệ an toàn. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `WasteScanTests.Classify_WithCorruptedImage_DoesNotCauseServerError` |
| **ITC-AI-011** | Từ chối khi truyền `modelName` không tồn tại hoặc gửi JSON thay vì `multipart/form-data`. | 1. Gửi `modelName="unknown"` hoặc body `application/json`. | HTTP `400` / `415 Client Error`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `WasteScanTests.Classify_WithUnknownModel_ReturnsClientError` |
| **Function 2.3: User Classification Feedback & Public Access** | | | | | | | | | |
| **ITC-AI-012** | Gửi phản hồi hiệu chỉnh kết quả AI (`POST /api/v1/waste-scan/feedback`) thành công. | 1. Gửi `POST /feedback` với `predictedClass`, `actualClass`, `confidence`, `comment`. | HTTP `200 OK`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `WasteScanTests.Feedback_WithValidData_ReturnsOk` |
| **ITC-AI-013** | Chặn phản hồi khi sai kiểu dữ liệu `confidence` hoặc JSON sai cú pháp. | 1. Gửi `POST /feedback` với `confidence="abc"` hoặc JSON hỏng. | HTTP `400 Bad Request`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `WasteScanTests.Feedback_WithWrongTypeForConfidence_Returns400` |
| **ITC-AI-014** | Luồng tích hợp End-to-End: Quét ảnh rác (`POST /classify`) $\rightarrow$ Gửi phản hồi (`POST /feedback`). | 1. Gọi `POST /classify` lấy kết quả dự đoán.<br>2. Dùng kết quả đó gửi `POST /feedback`. | Cả 2 bước đều trả về HTTP `200 OK`. | `Ecolink.WebApi` & `FastAPI` đang chạy. | Passed | Pending | Pending | N/A | `WasteScanTests.ClassifyThenFeedback_EndToEndFlow` |
| **ITC-AI-015** | Kiểm tra quyền truy cập Public (Guest) trên cả 4 endpoints của Module 2. | 1. Gọi `models`, `rules`, `classify`, `feedback` không cần token. | HTTP `200 OK` trên cả 4 endpoints. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `WasteScanTests.Endpoint_WithoutToken_IsAccessible` |

---

## SHEET 6: `M03_Onboarding` – RECYCLER ONBOARDING & VERIFICATION
*(Chi tiết đầy đủ xem tại: [`03_integration_test_recycler_onboarding_and_verification.md`](./03_integration_test_recycler_onboarding_and_verification.md))*

| Feature | `Recycler Onboarding & Verification (M03_Onboarding)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử tích hợp Tiếp nhận & Xét duyệt cơ sở thu gom (`U-17`, `S-04`): Upload chứng từ R2, Nộp hồ sơ nâng cấp, Chặn nộp trùng (`BR-MTH-06`), Staff/Admin xét duyệt Từ chối (`MSG-MTH-05`) / Phê duyệt (`MSG-MTH-04`) nâng cấp Role (**M1**) & seed bảng giá (**M4**). | | | |
| **Number of TCs** | **12** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 12 | 0 | 0 | 0 |
| *Round 2* | 0 | 0 | 12 | 0 |
| *Round 3* | 0 | 0 | 12 | 0 |

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Function 3.1: Business License & Facility Photo Upload** | | | | | | | | | |
| **ITC-ONB-001** | Tải ảnh Giấy phép kinh doanh (`permit`) và Ảnh cơ sở (`facility-photo`) thành công. | 1. Gửi `POST /api/v1/recycler-applications/upload-document` với ảnh PNG cho cả 2 loại chứng từ. | HTTP `200 OK`, trả về `fileUrl` hợp lệ. | `Citizen` đã đăng nhập. | Passed | Pending | Pending | N/A | `OnboardingTests.ONB01` |
| **ITC-ONB-002** | Chặn tải chứng từ khi chưa đăng nhập hoặc truyền sai `documentType`. | 1. Gửi `POST /upload-document` không token hoặc `documentType="invalid"`. | HTTP `401 Unauthorized` / `400 Bad Request`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | Upload Auth & Validation |
| **Function 3.2: Citizen Upgrade Application Submission** | | | | | | | | | |
| **ITC-ONB-003** | Chặn nộp đơn khi không có token (`401`) hoặc tài khoản đã là `Collector` (`400 MSG-MTH-06`). | 1. Gửi `POST /recycler-applications` không token và với token `collector`. | Bước 1: `401`. Bước 2: `400 MSG-MTH-06`. | Tài khoản `collector` đã `Verified`. | Passed | Pending | Pending | N/A | `OnboardingTests.ONB02` |
| **ITC-ONB-004** | Kiểm tra biên tọa độ không hợp lệ (`latitude = 95.5`, `longitude = 190.0`) khi nộp đơn. | 1. Gửi `POST /recycler-applications` với tọa độ vượt biên WGS84. | HTTP `400 Bad Request`, `MSG-MTH-06`. | Tài khoản `Citizen` mới. | Passed | Pending | Pending | N/A | `OnboardingTests.ONB03` |
| **ITC-ONB-005** | Chặn nộp đơn khi thiếu `facilityName` hoặc `businessLicenseUrl`. | 1. Gửi `POST /recycler-applications` với chuỗi rỗng ở trường bắt buộc. | HTTP `400 Bad Request`, `MSG-MTH-06`. | Tài khoản `Citizen` mới. | Passed | Pending | Pending | N/A | `OnboardingTests.ONB03` |
| **ITC-ONB-006** | `Citizen` nộp đơn hợp lệ (`201 Created`) và tra cứu lại qua `GET /me`. | 1. Gửi `POST /recycler-applications`.<br>2. Gọi `GET /api/v1/recycler-applications/me`. | Bước 1: `201 Created` (`MSG-MTH-03`), `Pending`. Bước 2: `200 OK`. | Tài khoản `Citizen` hợp lệ. | Passed | Pending | Pending | N/A | `OnboardingTests.ONB04` |
| **ITC-ONB-007** | Chặn nộp trùng hồ sơ khi đơn trước đó đang `Pending` (`BR-MTH-06`). | 1. Nộp đơn lần 1 (`201`).<br>2. Nộp tiếp lần 2 khi đang `Pending`. | Lần 2 bị chặn với HTTP `400 Bad Request`, `MSG-MTH-06`. | Đơn lần 1 đang `Pending`. | Passed | Pending | Pending | N/A | `OnboardingTests.ONB05` |
| **Function 3.3: Staff/Admin Application Queue & Detail Inspection** | | | | | | | | | |
| **ITC-ONB-008** | Chặn `Citizen` truy cập danh sách thẩm định và chi tiết hồ sơ của `Staff`/`Admin`. | 1. Dùng token `Citizen` gọi `GET /recycler-applications` và `/{userId}`. | HTTP `403 Forbidden`. | Đã có hồ sơ trong hệ thống. | Passed | Pending | Pending | N/A | `OnboardingTests.ONB06` |
| **ITC-ONB-009** | `Staff`/`Admin` lọc danh sách hồ sơ `Pending` và xem chi tiết chứng từ. | 1. Dùng token `admin` gọi `GET /recycler-applications?status=Pending` và `/{userId}`. | HTTP `200 OK`, trả về danh sách phân trang và chi tiết hồ sơ. | Đã có hồ sơ `Pending`. | Passed | Pending | Pending | N/A | `OnboardingTests.ONB06` |
| **Function 3.4: Verification, Resubmission & Cross-Module Role/Price Upgrade** | | | | | | | | | |
| **ITC-ONB-010** | Chặn `Staff`/`Admin` từ chối hồ sơ nhưng để trống `rejectionReason`. | 1. Gửi `PUT /{userId}/verification` với `isApproved = false`, `rejectionReason = ""`. | HTTP `400 Bad Request`, hồ sơ giữ nguyên `Pending`. | Hồ sơ đang `Pending`. | Passed | Pending | Pending | N/A | `OnboardingTests.ONB07` |
| **ITC-ONB-011** | Từ chối hồ sơ kèm lý do (`MSG-MTH-05`) $\rightarrow$ `Citizen` bổ sung chứng từ và nộp lại thành công. | 1. `Admin` từ chối có lý do (`Rejected`).<br>2. `Citizen` gửi lại `POST /recycler-applications`. | Bước 1: `200 OK` (`MSG-MTH-05`). Bước 2: `201 Created`, quay lại `Pending`. | Hồ sơ đang `Pending`. | Passed | Pending | Pending | N/A | `OnboardingTests.ONB07` |
| **ITC-ONB-012** | **Tích hợp M3 $\rightarrow$ M1 $\rightarrow$ M4:** Phê duyệt hồ sơ (`MSG-MTH-04`) nâng cấp Role thành `Recycler` và seed bảng giá mặc định. | 1. `Admin` duyệt đơn (`isApproved = true`).<br>2. User đăng nhập lại lấy JWT mới.<br>3. Gọi `GET /api/v1/recycler/prices`. | Bước 1: `200 OK` (`MSG-MTH-04`). Bước 2: `Role = Recycler`. Bước 3: `prices.Count > 0`. | Hồ sơ đang `Pending`. | Passed | Pending | Pending | N/A | `OnboardingTests.ONB08` |

---

## SHEET 7: `M04_Recycler` – RECYCLER PROFILE, PRICING & AVAILABILITY
*(Chi tiết đầy đủ xem tại: [`04_integration_test_recycler_profile_pricing_and_availability.md`](./04_integration_test_recycler_profile_pricing_and_availability.md))*

| Feature | `Recycler Profile, Pricing & Availability (M04_Recycler)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử tích hợp Quản lý hồ sơ cơ sở, Bảng giá đơn lẻ & hàng loạt nguyên tử (`R-01`), Quy tắc tối thiểu 1 loại phế liệu active (`BR-PRC-03`), Bật/Tắt `Online`/`Offline` (`R-02`, `BR-MTH-01`) và Lịch mở cửa 7 ngày trong tuần. | | | |
| **Number of TCs** | **24** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 24 | 0 | 0 | 0 |
| *Round 2* | 0 | 0 | 24 | 0 |
| *Round 3* | 0 | 0 | 24 | 0 |

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Function 4.1: RBAC Authorization & Facility Profile Management** | | | | | | | | | |
| **ITC-PRC-001** | Kiểm tra RBAC trên toàn bộ 9 endpoints `/api/v1/recycler/*`: chặn Guest (`401`) và `Citizen` (`403`). | 1. Gọi 9 endpoints không token và với token `Citizen`. | Không token: `401`. Token `Citizen`: `403 Forbidden`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `RecyclerTests` RBAC |
| **ITC-PRC-002** | Xem hồ sơ cơ sở (`GET /api/v1/recycler/profile`) với `collector` (`Verified`) và Recycler mới (`Pending`). | 1. Gửi `GET /profile` với 2 tài khoản. | `200 OK`, trả về đúng `verificationStatus` và tọa độ. | Đã có 2 tài khoản `Recycler`. | Passed | Pending | Pending | N/A | `RecyclerTests.Profile_Get` |
| **ITC-PRC-003** | Cập nhật hồ sơ cơ sở hợp lệ (`PUT /api/v1/recycler/profile`) lưu bền vững xuống DB. | 1. Gửi `PUT /profile` cập nhật tên, địa chỉ, tọa độ GPS, mã số thuế. | HTTP `200 OK`, đọc lại từ `GET /profile` khớp 100%. | Đã đăng nhập `collector`. | Passed | Pending | Pending | N/A | `RecyclerTests.Profile_Update` |
| **ITC-PRC-004** | Kiểm tra biên độ dài trường và tọa độ cực trị (`lat = ±90`, `lng = ±180`) khi cập nhật hồ sơ. | 1. Gửi `PUT /profile` tại các giá trị biên hợp lệ. | HTTP `200 OK`. | Đã đăng nhập `collector`. | Passed | Pending | Pending | N/A | `RecyclerTests` Boundary |
| **ITC-PRC-005** | Chặn 17 kịch bản cập nhật hồ sơ không hợp lệ và bảo toàn dữ liệu cũ. | 1. Gửi `PUT /profile` với 17 biến thể lỗi và đọc lại hồ sơ. | HTTP `400 Bad Request`, dữ liệu cũ không bị thay đổi. | Đã đăng nhập `collector`. | Passed | Pending | Pending | N/A | `RecyclerTests.Profile_Update_WithInvalidData` |
| **Function 4.2: Single Waste Category Price Configuration** | | | | | | | | | |
| **ITC-PRC-006** | Xem bảng giá (`GET /prices`) và lọc chỉ lấy dòng đang thu mua (`onlyActive=true`). | 1. Tạm ngưng 1 loại rác, gọi `GET /prices` và `GET /prices?onlyActive=true`. | `onlyActive=true` loại bỏ các dòng tạm ngưng. | Cơ sở đã có bảng giá. | Passed | Pending | Pending | N/A | `RecyclerTests.Prices_Get` |
| **ITC-PRC-007** | Thêm mới đơn giá cho loại phế liệu chưa có (`POST /api/v1/recycler/prices`). | 1. Gửi `POST /prices` cho danh mục `cardboard`. | HTTP `201 Created` (`MSG-PRC-01`), `isActive = true`. | `cardboard` chưa có trong bảng giá. | Passed | Pending | Pending | N/A | `RecyclerTests.Prices_Create` |
| **ITC-PRC-008** | Kiểm tra biên `pricePerKg` (`0.01`..`1.000.000`) và `minQuantityKg` (`0.1`..`10.000`). | 1. Gửi `PUT /prices/{id}` ở biên dưới và biên trên. | HTTP `200 OK`. | Dòng giá tồn tại. | Passed | Pending | Pending | N/A | `RecyclerTests.Prices_Update` |
| **ITC-PRC-009** | Chặn thêm trùng loại phế liệu đã có (`400`) hoặc `wasteCategoryId` không tồn tại (`404`). | 1. Gửi `POST /prices` trùng `plastic` và với `Guid` lạ. | Bước 1: `400 Bad Request`. Bước 2: `404 Not Found`. | `plastic` đã có trong bảng giá. | Passed | Pending | Pending | N/A | `RecyclerTests.Prices_Create_Duplicate` |
| **ITC-PRC-010** | Chặn cập nhật giá ngoài biên (`price <= 0`, `> 1.000.000`, `minQty < 0.1`, `conditions > 300` ký tự). | 1. Gửi `PUT /prices/{id}` với 7 biến thể ngoài biên. | HTTP `400 Bad Request`, giữ nguyên giá cũ. | Dòng giá tồn tại. | Passed | Pending | Pending | N/A | `RecyclerTests.Prices_Update_WithInvalidValues` |
| **ITC-PRC-011** | Chống IDOR: Chặn `Recycler B` sửa dòng giá của `Recycler A`. | 1. Dùng token `PendingRecycler` gọi `PUT /prices/{collectorPriceId}`. | HTTP `404 Not Found`. | Có 2 tài khoản `Recycler`. | Passed | Pending | Pending | N/A | `RecyclerTests.Prices_Update_OtherRecyclersPriceRow` |
| **ITC-PRC-012** | Chặn tắt dòng giá đang hoạt động cuối cùng (`BR-PRC-03` / `MSG-PRC-02`). | 1. Khi chỉ còn 1 dòng active, gửi `PUT /prices/{id}` với `isActive = false`. | HTTP `400 Bad Request`, `MSG-PRC-02`. | Chỉ còn 1 dòng giá active. | Passed | Pending | Pending | N/A | `RecyclerTests.Prices_Update_DeactivatingLastActiveCategory` |
| **Function 4.3: Atomic Bulk Price Save** | | | | | | | | | |
| **ITC-PRC-013** | Lưu đồng loạt bảng giá (`PUT /api/v1/recycler/prices`) cập nhật, thêm mới và tạm ngưng trong 1 lần gọi. | 1. Gửi `PUT /prices` với mảng `items` hỗn hợp. | HTTP `200 OK` (`MSG-PRC-01`), cập nhật đồng bộ. | Đã đăng nhập `collector`. | Passed | Pending | Pending | N/A | `RecyclerTests.Prices_BulkSave` |
| **ITC-PRC-014** | Chặn lưu hàng loạt khi tất cả các mục đều đặt `isActive = false` (`MSG-PRC-02`). | 1. Gửi `PUT /prices` với mọi phần tử `isActive = false`. | HTTP `400 Bad Request`, `MSG-PRC-02`. | Cơ sở đang có bảng giá active. | Passed | Pending | Pending | N/A | `RecyclerTests.Prices_BulkSave_AllInactive` |
| **ITC-PRC-015** | Kiểm tra tính nguyên tử (Rollback) của `PUT /prices` khi 1 phần tử trong danh sách bị lỗi. | 1. Gửi phần tử 1 hợp lệ, phần tử 2 có `price = -5`. | HTTP `400 Bad Request`, phần tử 1 cũng được rollback. | Đã đăng nhập `collector`. | Passed | Pending | Pending | N/A | `RecyclerTests.Prices_BulkSave_IsAtomic` |
| **ITC-PRC-016** | Chặn `PUT /prices` khi mảng rỗng, trùng `wasteCategoryId` trong request hoặc danh mục không tồn tại. | 1. Gửi `items = []`, trùng ID hoặc `Guid.NewGuid()`. | HTTP `400 Bad Request` / `404 Not Found`. | Đã đăng nhập `collector`. | Passed | Pending | Pending | N/A | `RecyclerTests.Prices_BulkSave_DuplicateCategories` |
| **Function 4.4: Real-Time Online/Offline Availability Toggle** | | | | | | | | | |
| **ITC-PRC-017** | Xem trạng thái hoạt động (`GET /api/v1/recycler/availability`). | 1. Gửi `GET /availability`. | HTTP `200 OK`, trả về `canAcceptNewOrders`, `weeklySchedule`. | Đã đăng nhập `collector`. | Passed | Pending | Pending | N/A | `RecyclerTests.Availability_Get` |
| **ITC-PRC-018** | Chuyển sang `Offline` kèm lý do bận và bật lại `Online` tự động xóa lý do bận. | 1. Gửi `PATCH /availability` (`isOnline=false`), sau đó (`isOnline=true`). | HTTP `200 OK`, `unavailableReason` tự động về `null` khi `Online`. | Cơ sở đã `Verified`. | Passed | Pending | Pending | N/A | `RecyclerTests.Availability_Update` |
| **ITC-PRC-019** | Chặn cơ sở `Pending` bật `Online` (`BR-MTH-01`) hoặc nhập lý do bận `> 200` ký tự. | 1. Dùng `PendingRecycler` gửi `PATCH /availability` (`isOnline=true`). | HTTP `400 Bad Request`. | Tài khoản đang `Pending`. | Passed | Pending | Pending | N/A | `RecyclerTests.PendingRecycler_Availability_GoOnline` |
| **Function 4.5: Weekly Operating Schedule Configuration** | | | | | | | | | |
| **ITC-PRC-020** | Cập nhật lịch mở cửa 7 ngày (`PUT /api/v1/recycler/availability/schedule`) và sinh `openingHoursSummary`. | 1. Gửi `PUT /schedule` với T2–T7 mở cửa, CN nghỉ. | HTTP `200 OK`, `openingHoursSummary = "T2-T7: 07:30-17:30; CN: Nghỉ"`. | Cơ sở đã `Verified`. | Passed | Pending | Pending | N/A | `RecyclerTests.Schedule_Update_WithValidWeek` |
| **ITC-PRC-021** | Cập nhật lịch với nhiều khung giờ khác nhau hoặc đóng cửa cả tuần. | 1. Gửi `PUT /schedule` với T2–T6, T7 khác giờ và cả tuần nghỉ. | HTTP `200 OK`, tóm tắt lịch chính xác. | Cơ sở đã `Verified`. | Passed | Pending | Pending | N/A | `RecyclerTests.Schedule_Update_GroupsDays` |
| **ITC-PRC-022** | Cập nhật một phần lịch tuần và bỏ qua giờ mở/đóng cho ngày nghỉ (`isClosed = true`). | 1. Gửi `PUT /schedule` chỉ 2 ngày hoặc ngày nghỉ `openTime = null`. | HTTP `200 OK`. | Cơ sở đã `Verified`. | Passed | Pending | Pending | N/A | `RecyclerTests.Schedule_Update_PartialWeek` |
| **ITC-PRC-023** | Chặn 12 kịch bản lịch mở cửa không hợp lệ (`close <= open`, sai định dạng `HH:mm`, trùng thứ). | 1. Gửi `PUT /schedule` với 12 biến thể lỗi. | HTTP `400 Bad Request`, giữ nguyên lịch cũ. | Cơ sở đã `Verified`. | Passed | Pending | Pending | N/A | `RecyclerTests.Schedule_Update_WithInvalidData` |
| **ITC-PRC-024** | Chặn cơ sở đang chờ duyệt (`Pending`) cập nhật lịch mở cửa hàng tuần. | 1. Dùng `PendingRecycler` gửi `PUT /schedule`. | HTTP `400 Bad Request`. | Tài khoản đang `Pending`. | Passed | Pending | Pending | N/A | `RecyclerTests.PendingRecycler_Schedule_Update` |

---

## SHEET 8: `M05_Discovery` – LOCATION-BASED RECYCLER DISCOVERY
*(Chi tiết đầy đủ xem tại: [`05_integration_test_location_based_recycler_discovery.md`](./05_integration_test_location_based_recycler_discovery.md))*

| Feature | `Location-Based Recycler Discovery (M05_Discovery)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử tích hợp Tìm kiếm cơ sở theo tọa độ GPS (`U-05`, `BR-MTH-04`), kiểm tra biên bán kính & tọa độ (`MSG-MTH-02`), lọc theo loại phế liệu & sắp xếp `price_desc` (`BR-MTH-07`), gợi ý mở rộng bán kính (`MSG-MTH-01`), đồng bộ tức thời với Module 4, chi tiết cơ sở và Geocoding Proxy. | | | |
| **Number of TCs** | **12** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 12 | 0 | 0 | 0 |
| *Round 2* | 0 | 0 | 12 | 0 |
| *Round 3* | 0 | 0 | 12 | 0 |

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Function 5.1: Spatial Radius Search & Coordinate Validation** | | | | | | | | | |
| **ITC-DSC-001** | Tìm kiếm quanh tọa độ Khu CNC Hòa Lạc (`21.0124, 105.5253`) trả về danh sách sắp xếp tăng dần theo `distanceKm` (`BR-MTH-04`). | 1. Gửi `GET /api/v1/recyclers/nearby?lat=21.0124&lng=105.5253&radiusKm=15`. | HTTP `200 OK` (`MSG-GEN-01`), `items` tăng dần theo `distanceKm`. | Có cơ sở `Verified` + `Online` tại Hòa Lạc. | Passed | Pending | Pending | N/A | `DiscoveryTests.DSC01` |
| **ITC-DSC-002** | Chặn truy vấn khi tọa độ bằng `(0, 0)` trả về `MSG-MTH-02`. | 1. Gửi `GET /recyclers/nearby?lat=0&lng=0&radiusKm=10`. | HTTP `400 Bad Request`, `MSG-MTH-02`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `DiscoveryTests.DSC02` |
| **ITC-DSC-003** | Chặn truy vấn khi vĩ độ/kinh độ vượt biên WGS84 (`lat = 95.5`, `lng = -185.0`). | 1. Gửi `GET /recyclers/nearby` với tọa độ ngoài biên. | HTTP `400 Bad Request`, `MSG-MTH-02`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `DiscoveryTests.DSC02` |
| **ITC-DSC-004** | Chặn truy vấn khi bán kính `< 0.5km` (`0.2km`) hoặc `> 50km` (`65km`). | 1. Gửi `GET /recyclers/nearby` với `radiusKm=0.2` và `65.0`. | HTTP `400 Bad Request`, `MSG-MTH-02`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `DiscoveryTests.DSC02` |
| **ITC-DSC-005** | Chặn truy vấn khi lọc theo loại rác không tái chế (`category=trash`). | 1. Gửi `GET /recyclers/nearby` với `category=trash`. | HTTP `400 Bad Request`, `MSG-MTH-02`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `DiscoveryTests.DSC02` |
| **Function 5.2: Category Filtering, Price Sorting & Fallback Suggestion** | | | | | | | | | |
| **ITC-DSC-006** | Lọc theo `category=metal` và sắp xếp `sortBy=price_desc` (`BR-MTH-07`). | 1. Gửi `GET /recyclers/nearby?lat=21.0124&lng=105.5253&radiusKm=20&category=metal&sortBy=price_desc`. | HTTP `200 OK`, các cơ sở sắp xếp giảm dần theo đơn giá `metal`. | Có ít nhất 2 cơ sở thu mua kim loại. | Passed | Pending | Pending | N/A | `DiscoveryTests.DSC03` |
| **ITC-DSC-007** | Khi không có kết quả (`totalFound = 0`), trả về `MSG-MTH-01` kèm `fallbackSuggestion` mở rộng bán kính gấp $2.5$ lần. | 1. Gửi `GET /recyclers/nearby?lat=15.0&lng=112.0&radiusKm=4&category=paper`. | HTTP `200 OK` (`MSG-MTH-01`), `suggestedExpandedRadiusKm = 10.0`. | Không có cơ sở quanh `(15.0, 112.0)`. | Passed | Pending | Pending | N/A | `DiscoveryTests.DSC04` |
| **ITC-DSC-008** | **Tích hợp M4 $\rightarrow$ M5:** Khi cơ sở bật/tắt `Online`/`Offline` ở Module 4, bản đồ Module 5 cập nhật tức thời. | 1. Chuyển `collector` sang `Offline` rồi tìm kiếm.<br>2. Bật lại `Online` rồi tìm kiếm. | Khi `Offline` biến mất khỏi kết quả; khi `Online` xuất hiện lại ngay. | Cơ sở `collector` đã `Verified`. | Passed | Pending | Pending | N/A | `DiscoveryTests.DSC05` |
| **Function 5.3: Public Recycler Detail, Trade Partners & Geocoding Proxy** | | | | | | | | | |
| **ITC-DSC-009** | Xem chi tiết công khai cơ sở trên bản đồ (`GET /api/v1/recyclers/{recyclerId}`). | 1. Gửi `GET /api/v1/recyclers/{collectorUserId}?lat=21.0124&lng=105.5253`. | HTTP `200 OK`, trả về `facilityName`, `distanceKm`, `materials`. | Cơ sở `collector` đã `Verified`. | Passed | Pending | Pending | N/A | `DiscoveryTests.DSC06` |
| **ITC-DSC-010** | Truy vấn chi tiết cơ sở với `Guid` không tồn tại trả về `404 Not Found`. | 1. Gửi `GET /api/v1/recyclers/{Guid.NewGuid()}`. | HTTP `404 Not Found`. | `Guid` không tồn tại. | Passed | Pending | Pending | N/A | `DiscoveryTests.DSC07` |
| **ITC-DSC-011** | Tìm kiếm đối tác giao dịch lân cận qua `GET /api/v1/trade/partners/nearby`. | 1. Gửi `GET /api/v1/trade/partners/nearby?lat=21.0124&lng=105.5253&category=plastic&radiusKm=15`. | HTTP `200 OK`, mảng đối tác khác rỗng. | Có cơ sở thu mua `plastic` tại Hòa Lạc. | Passed | Pending | Pending | N/A | `DiscoveryTests.DSC08` |
| **ITC-DSC-012** | Kiểm tra Geocoding Proxy (`GET /api/v1/geocoding/search`) chặn từ khóa `< 2` ký tự và tìm kiếm hợp lệ. | 1. Gửi `?q=a` (`400`) và `?q=Hoa+Lac&limit=3` (`200`). | Bước 1: `400 Bad Request`. Bước 2: `200 OK` kèm mảng `items`. | `IGeocodingProxyService` hoạt động. | Passed | Pending | Pending | N/A | `DiscoveryTests.DSC08` |

---

## SHEET 9: `M06_Handover` – HANDOVER REQUEST MANAGEMENT
*(Chi tiết đầy đủ xem tại: [`06_integration_test_handover_request_management.md`](./06_integration_test_handover_request_management.md))*

| Feature | `Handover Request Management (M06_Handover)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử tích hợp Quản lý Yêu cầu Bàn giao (`U-06`, `U-07`, `R-03`, `R-04`): Tạo đơn `HND-yyyyMMdd-NNNN`, chụp nhanh `PriceSnapshotPerKg` (`BR-HND-03`), giới hạn 3 đơn mở (`BR-HND-02`), ẩn/hiện SĐT (`BR-PRIV-01`), cảnh báo hủy muộn `< 2h` (`BR-HND-05`) và nhận/từ chối/hủy đơn (`BR-HND-06`). | | | |
| **Number of TCs** | **22** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 22 | 0 | 0 | 0 |
| *Round 2* | 0 | 0 | 22 | 0 |
| *Round 3* | 0 | 0 | 22 | 0 |

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Function 6.1: Role-Based Access Control (RBAC) Gate** | | | | | | | | | |
| **ITC-HND-001** | Chặn truy cập toàn bộ 10 endpoints quản lý đơn khi không có JWT token (`401`). | 1. Gọi 10 endpoints không đính kèm Bearer token. | HTTP `401 Unauthorized` trên cả 10 endpoints. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `HandoverTests` 401 |
| **ITC-HND-002** | Chặn gọi chéo vai trò giữa `Citizen` và `Recycler` (`403 Forbidden`). | 1. Dùng token `collector` gọi API Citizen và ngược lại. | HTTP `403 Forbidden`. | Đã đăng nhập 2 vai trò. | Passed | Pending | Pending | N/A | `HandoverTests` 403 |
| **Function 6.2: Citizen Handover Request Creation & Price Snapshot** | | | | | | | | | |
| **ITC-HND-003** | Tạo đơn bàn giao hợp lệ (`201 Created`), sinh mã `HND-yyyyMMdd-NNNN`, chụp nhanh `PriceSnapshotPerKg` và ẩn SĐT. | 1. Gửi `POST /api/v1/handover-requests` với `10kg plastic`. | HTTP `201 Created` (`MSG-HND-01`), `priceSnapshotPerKg = 8000`, `citizenPhone = null`. | Cơ sở `collector` đang `Online`. | Passed | Pending | Pending | N/A | `HandoverTests.Create_WithValidSingleItem` |
| **ITC-HND-004** | Tạo đơn nhiều loại rác và giữ cố định `PriceSnapshotPerKg` ngay cả khi cơ sở đổi giá sau đó (`BR-HND-03`). | 1. Tạo đơn `plastic` + `metal`.<br>2. Cơ sở đổi giá ở M4.<br>3. Đọc lại đơn. | `PriceSnapshotPerKg` và `EstimatedTotalAmount` của đơn đã tạo giữ nguyên không đổi. | Cơ sở thu mua cả 2 loại rác. | Passed | Pending | Pending | N/A | `HandoverTests.PriceSnapshot_RemainsUnchanged` |
| **ITC-HND-005** | Kiểm tra giá trị biên khi tạo đơn (`0.1kg`, `10.000kg`, `5` ảnh, giờ hẹn `> 30` phút). | 1. Gửi `POST /handover-requests` tại các điểm biên hợp lệ. | HTTP `201 Created`. | `Citizen` có `< 3` đơn mở. | Passed | Pending | Pending | N/A | `HandoverTests` Boundary |
| **ITC-HND-006** | Chặn tạo đơn với 18 kịch bản dữ liệu không hợp lệ. | 1. Gửi `POST /handover-requests` với 18 biến thể lỗi. | HTTP `400 Bad Request` trên cả 18 kịch bản. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `HandoverTests.Create_WithInvalidData` |
| **ITC-HND-007** | **Tích hợp M4 $\rightarrow$ M6:** Chặn tạo đơn khi cơ sở `Pending`, `Offline`, không thu mua loại rác đó hoặc ngoài giờ mở cửa. | 1. Thử tạo đơn tới cơ sở `Offline`, loại rác bị tắt hoặc ngoài khung giờ lịch tuần. | HTTP `400 Bad Request` (hoặc `404` nếu không tồn tại). | Đã cấu hình trạng thái ở Module 4. | Passed | Pending | Pending | N/A | `HandoverTests.Create_ToOfflineRecycler` |
| **ITC-HND-008** | Giới hạn tối đa **3 yêu cầu đang xử lý** trên mỗi `Citizen` (`BR-HND-02` / `MSG-HND-05`). | 1. Tạo 3 đơn `Pending` (`201`).<br>2. Tạo đơn thứ 4 (`400 MSG-HND-05`).<br>3. Hủy 1 đơn rồi tạo lại (`201`). | Đơn thứ 4 bị chặn `MSG-HND-05` cho đến khi hủy bớt 1 đơn. | Tài khoản `citizen` hoạt động. | Passed | Pending | Pending | N/A | `HandoverTests.Create_FourthActiveRequest` |
| **Function 6.3: Citizen & Recycler Listing, Filtering & Privacy Protection** | | | | | | | | | |
| **ITC-HND-009** | Lọc danh sách đơn theo `status` (Citizen) và theo khoảng ngày `fromDate`..`toDate` (Recycler). | 1. Gọi `GET /handover-requests?status=...` và `/recycler/handover-requests?fromDate=...`. | HTTP `200 OK`, trả về đúng các đơn khớp bộ lọc. | Đã có đơn trong CSDL. | Passed | Pending | Pending | N/A | `HandoverTests.CitizenList_FilterByStatus` |
| **ITC-HND-010** | Chống IDOR: `Citizen 2` và `Recycler 2` không được xem, sửa, nhận hoặc hủy đơn của người khác. | 1. Dùng token bên thứ ba gọi vào đơn của `Citizen 1` / `Collector 1`. | HTTP `403 Forbidden` trên mọi thao tác. | Có tài khoản thứ ba độc lập. | Passed | Pending | Pending | N/A | `HandoverTests.OtherCitizen_CannotView` |
| **Function 6.4: Citizen Update & Cancellation Workflow** | | | | | | | | | |
| **ITC-HND-011** | `Citizen` cập nhật đơn khi đang `Pending` (`PUT /api/v1/handover-requests/{id}`) tính lại tổng tiền. | 1. Gửi `PUT /{id}` đổi loại rác, khối lượng, địa chỉ, giờ hẹn. | HTTP `200 OK`, tính lại `estimatedTotalAmount` chính xác. | Đơn đang `Pending`. | Passed | Pending | Pending | N/A | `HandoverTests.Update_WhilePending` |
| **ITC-HND-012** | Khóa chỉnh sửa đơn (`PUT /{id}`) sau khi cơ sở đã chấp nhận (`Accepted` – `BR-HND-04`). | 1. Sau khi `Accept`, `Citizen` gọi `PUT /{id}`. | HTTP `400 Bad Request`. | Đơn đã `Accepted`. | Passed | Pending | Pending | N/A | `HandoverTests.Update_AfterAccepted` |
| **ITC-HND-013** | `Citizen` hủy đơn trước giờ hẹn $\ge 2$ tiếng thì `isLateCancellation = false`. | 1. Tạo đơn hẹn sau 26h, `Accept`, rồi `Citizen` gọi `POST /{id}/cancel`. | HTTP `200 OK` (`MSG-HND-03`), `isLateCancellation = false`. | Đơn cách giờ hẹn $> 2$ giờ. | Passed | Pending | Pending | N/A | `HandoverTests.CitizenCancel_WhenAcceptedMoreThan2Hours` |
| **ITC-HND-014** | Cảnh báo hủy muộn (`BR-HND-05`): `Citizen` hủy đơn đã `Accepted` trong vòng $< 2$ tiếng trước giờ hẹn thì `isLateCancellation = true`. | 1. Tạo đơn hẹn sau 60 phút, `Accept`, rồi `Citizen` gọi `POST /{id}/cancel`. | HTTP `200 OK` (`MSG-HND-03`), `isLateCancellation = true`. | Đơn đã `Accepted` và còn $< 2$ giờ trước giờ hẹn. | Passed | Pending | Pending | N/A | `HandoverTests.CitizenCancel_WhenAcceptedWithin2Hours` |
| **Function 6.5: Recycler Accept, Reject & Cancel Workflow** | | | | | | | | | |
| **ITC-HND-015** | `Recycler` chấp nhận đơn (`POST /accept`) chuyển sang `Accepted` và mở khóa SĐT hai bên (`BR-PRIV-01`). | 1. `Collector` gọi `POST /api/v1/recycler/handover-requests/{id}/accept`. | HTTP `200 OK` (`MSG-HND-02`), hiển thị `citizenPhone` & `recyclerPhone`. | Đơn đang `Pending`. | Passed | Pending | Pending | N/A | `HandoverTests.Accept_WhilePending` |
| **ITC-HND-016** | `Recycler` chấp nhận đơn kèm giờ hẹn điều chỉnh mới và chặn nếu giờ hẹn trong quá khứ. | 1. Gửi `POST /accept` với `confirmedTimeSlot` tương lai (`200`) và quá khứ (`400`). | Giờ tương lai: `200 OK`. Giờ quá khứ: `400 Bad Request`. | Đơn đang `Pending`. | Passed | Pending | Pending | N/A | `HandoverTests.Accept_WithCustomFutureTimeSlot` |
| **ITC-HND-017** | `Recycler` từ chối đơn (`POST /reject`) bắt buộc lý do `1..500` ký tự (`BR-HND-06`). | 1. Gửi `POST /reject` rỗng (`400`) và có lý do hợp lệ (`200`). | Lý do rỗng: `400`. Có lý do: `200 OK` (`MSG-HND-04`), `status = Rejected`. | Đơn đang `Pending`. | Passed | Pending | Pending | N/A | `HandoverTests.Reject_WithoutReason_Returns400` |
| **ITC-HND-018** | `Recycler` hủy đơn đã nhận (`POST /cancel`) bắt buộc có lý do (`BR-HND-06`). | 1. Khi đơn `Accepted`, gửi `POST /cancel` rỗng (`400`) và có lý do (`200`). | Lý do rỗng: `400`. Có lý do: `200 OK` (`MSG-HND-03`), `cancelledBy = Recycler`. | Đơn đang `Accepted`. | Passed | Pending | Pending | N/A | `HandoverTests.RecyclerCancel_WithoutReason` |
| **ITC-HND-019** | Chặn `Recycler` gọi `/cancel` khi đơn còn `Pending` hoặc gọi `/reject` sau khi đã `Accepted`. | 1. Gọi chéo 2 API sai trạng thái. | HTTP `400 Bad Request`. | Đơn ở trạng thái `Pending` / `Accepted`. | Passed | Pending | Pending | N/A | `HandoverTests.RecyclerCancel_WhileStillPending` |
| **ITC-HND-020** | Đơn đã kết thúc (`Rejected`/`Cancelled`) không thể chuyển trạng thái thêm lần nữa. | 1. Gọi `update`, `accept`, `reject`, `cancel` trên đơn đã đóng. | HTTP `400 Bad Request`. | Đơn đã `Rejected` hoặc `Cancelled`. | Passed | Pending | Pending | N/A | `HandoverTests.TerminalRequest_CannotTransitionAgain` |
| **ITC-HND-021** | Truy vấn đơn với `Guid` không tồn tại trả về `404 Not Found` trên cả 7 endpoints tác vụ. | 1. Gọi 7 endpoints với `Guid.NewGuid()`. | HTTP `404 Not Found`. | `Guid` không tồn tại. | Passed | Pending | Pending | N/A | `HandoverTests.UnknownRequestId_Returns404` |
| **ITC-HND-022** | Kiểm tra tính duy nhất của mã `HND-yyyyMMdd-NNNN` khi tạo nhiều đơn liên tiếp trong ngày. | 1. Tạo 3 đơn liên tiếp và so sánh `code`. | Cả 3 mã đều đúng định dạng và khác nhau hoàn toàn. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `HandoverTests.Create_ThreeRequestsInSameDay` |

---

## SHEET 10: `M07_Receipt` – HANDOVER COMPLETION & TRANSACTION RECEIPT
*(Chi tiết đầy đủ xem tại: [`07_integration_test_handover_completion_and_transaction_receipt.md`](./07_integration_test_handover_completion_and_transaction_receipt.md))*

| Feature | `Handover Completion, Receipt & Transaction History (M07_Receipt)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử tích hợp Xem trước biên nhận & cảnh báo lệch cân $> 20\%$ (`MSG-RCP-03`), Phát hành biên nhận `RCP-yyyy-NNNN` bắt buộc ảnh cân khi $\ge 10\text{ kg}$ (`BR-TRN-01` / `MSG-RCP-04`), Xác nhận hoàn tất cộng `GreenPoints` (**M1 Gamification**), Khiếu nại (`Disputed`) và Lịch sử giao dịch (`U-12`). | | | |
| **Number of TCs** | **20** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 20 | 0 | 0 | 0 |
| *Round 2* | 0 | 0 | 20 | 0 |
| *Round 3* | 0 | 0 | 20 | 0 |

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Function 7.1: RBAC Authorization & Ownership Protection** | | | | | | | | | |
| **ITC-RCP-001** | Chặn truy cập 8 endpoints biên nhận & lịch sử khi không có token (`401`) hoặc gọi chéo vai trò (`403`). | 1. Gọi 8 endpoints không token và chéo token. | Không token: `401`. Chéo vai trò: `403 Forbidden`. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `ReceiptTests` RBAC |
| **ITC-RCP-002** | Chống IDOR: `Citizen 2` và `Recycler 2` không được truy cập biên nhận của đơn không thuộc về mình. | 1. Dùng token bên thứ ba gọi `GET/POST /receipt`, `/confirm`, `/dispute`. | HTTP `403 Forbidden`. | Đơn thuộc về `Citizen 1` & `Collector 1`. | Passed | Pending | Pending | N/A | `ReceiptTests.OtherCitizen_CannotAccessReceipt` |
| **Function 7.2: Receipt Calculation Preview & Weight Deviation Warning** | | | | | | | | | |
| **ITC-RCP-003** | Xem trước biên nhận (`POST /receipt/preview`) tính đúng tổng tiền và không lưu xuống DB. | 1. Gửi `POST /receipt/preview`, sau đó gọi `GET /receipt`. | Preview trả về `200 OK`; `GET /receipt` vẫn là `404`. | Đơn đang `Accepted`. | Passed | Pending | Pending | N/A | `ReceiptTests.Preview_Within20PercentDeviation` |
| **ITC-RCP-004** | Kiểm tra biên độ lệch cân nặng đúng $20\%$ (`hasDeviationWarning = false`) và $> 20\%$ (`MSG-RCP-03`). | 1. Đơn ước tính `10kg`, preview với `8.0kg` ($20\%$) và `7.9kg` ($21\%$). | `8.0kg`: không cảnh báo. `7.9kg`: `hasDeviationWarning = true`, `MSG-RCP-03`. | Đơn ước tính `10kg` đang `Accepted`. | Passed | Pending | Pending | N/A | `ReceiptTests.Preview_Over20PercentDeviation` |
| **ITC-RCP-005** | `POST /receipt/preview` dùng mặc định `PriceSnapshotPerKg` khi không truyền `unitPricePerKg`. | 1. Gửi `POST /receipt/preview` với `unitPricePerKg = null`. | HTTP `200 OK`, dùng đúng đơn giá snapshot. | Đơn đang `Accepted`. | Passed | Pending | Pending | N/A | `ReceiptTests.Preview_WithoutUnitPrice` |
| **Function 7.3: Digital Receipt Issuance & Scale Photo Enforcement** | | | | | | | | | |
| **ITC-RCP-006** | Phát hành biên nhận `< 10kg` (`201 Created`), sinh mã `RCP-yyyy-NNNN`, đặt hạn `AutoConfirmAt = +24h`. | 1. Gửi `POST /receipt` với `actualWeightKg = 5.0`, không cần ảnh cân. | HTTP `201 Created` (`MSG-RCP-01`), `status = AwaitingConfirmation`. | Đơn đang `Accepted`. | Passed | Pending | Pending | N/A | `ReceiptTests.Create_Under10Kg` |
| **ITC-RCP-007** | Bắt buộc ảnh chụp mặt đồng hồ cân (`scalePhotoUrls`) khi tổng khối lượng thực tế $\ge 10\text{ kg}$ (`BR-TRN-01` / `MSG-RCP-04`). | 1. Gửi `9.99kg` thiếu ảnh cân (`201`).<br>2. Gửi `10.0kg` thiếu ảnh cân (`400 MSG-RCP-04`).<br>3. Gửi `10.0kg` có ảnh cân (`201`). | `10.0kg` thiếu ảnh cân bị chặn `400 MSG-RCP-04`; có ảnh cân trả về `201 Created`. | Đơn đang `Accepted`. | Passed | Pending | Pending | N/A | `ReceiptTests.Create_10KgOrMore` |
| **ITC-RCP-008** | Chặn phát hành biên nhận trùng lặp lần 2 trên cùng một đơn (`BR-TRN-04`). | 1. Gửi `POST /receipt` lần 2 cho cùng `requestId`. | HTTP `400 Bad Request`. | Đơn đã có biên nhận. | Passed | Pending | Pending | N/A | `ReceiptTests.Create_DuplicateReceipt` |
| **ITC-RCP-009** | Chặn phát hành biên nhận khi đơn đang `Pending` hoặc đã `Cancelled`/`Rejected`. | 1. Gọi `POST /receipt` trên đơn chưa `Accepted`. | HTTP `400 Bad Request`. | Đơn không ở trạng thái `Accepted`. | Passed | Pending | Pending | N/A | `ReceiptTests.Create_WhenRequestNotAccepted` |
| **ITC-RCP-010** | Chặn phát hành biên nhận với 14 kịch bản dữ liệu không hợp lệ. | 1. Gửi `POST /receipt` với 14 biến thể lỗi cân nặng/đơn giá. | HTTP `400 Bad Request`, đơn giữ nguyên `Accepted`. | Đơn đang `Accepted`. | Passed | Pending | Pending | N/A | `ReceiptTests.Create_WithInvalidData` |
| **ITC-RCP-011** | Khóa hủy đơn (`POST /cancel`) sau khi biên nhận đã được phát hành (`AwaitingConfirmation`). | 1. Cả `Citizen` và `Collector` gọi `POST /cancel` sau khi xuất biên nhận. | HTTP `400 Bad Request`. | Đơn đang `AwaitingConfirmation`. | Passed | Pending | Pending | N/A | `ReceiptTests.AfterReceiptIssued_CannotCancel` |
| **Function 7.4: Citizen Receipt Confirmation, Green Points Gamification & Dispute** | | | | | | | | | |
| **ITC-RCP-012** | **Tích hợp M7 $\rightarrow$ M1:** `Citizen` xác nhận biên nhận (`POST /confirm`) hoàn tất đơn và cộng `GreenPoints` vào `/auth/me`. | 1. Xuất biên nhận `5.5kg` (`55 points`).<br>2. Gọi `POST /confirm` và kiểm tra `/auth/me`. | HTTP `200 OK` (`MSG-RCP-02`), `currentGreenPoints` tăng `+55`, `totalRecycledKg` tăng `+5.5`. | Đơn đang `AwaitingConfirmation`. | Passed | Pending | Pending | N/A | `ReceiptTests.Confirm_WhenAwaitingConfirmation` |
| **ITC-RCP-013** | Chống cộng điểm kép: Gọi `POST /confirm` lần 2 bị chặn và không cộng trùng điểm. | 1. Gọi `POST /confirm` lần 2 trên đơn đã `Completed`. | HTTP `400 Bad Request`, điểm xanh giữ nguyên. | Đơn đã `Completed`. | Passed | Pending | Pending | N/A | `ReceiptTests.Confirm_SecondTime` |
| **ITC-RCP-014** | `Citizen` khiếu nại biên nhận (`POST /dispute`) bắt buộc có lý do, chuyển sang `Disputed` và chưa cộng điểm. | 1. Gửi `POST /dispute` rỗng (`400`) và có lý do (`200`). | Có lý do: `200 OK`, `status = Disputed`, chưa cộng điểm. | Đơn đang `AwaitingConfirmation`. | Passed | Pending | Pending | N/A | `ReceiptTests.Dispute_WithValidReason` |
| **ITC-RCP-015** | Khóa chéo giữa `Confirm` và `Dispute`: Đơn đã `Confirm` không thể `Dispute` và ngược lại. | 1. Gọi `Dispute` sau khi `Confirm` và gọi `Confirm` sau khi `Dispute`. | HTTP `400 Bad Request`. | Đơn đã `Completed` / `Disputed`. | Passed | Pending | Pending | N/A | `ReceiptTests.Dispute_AfterConfirmed` |
| **Function 7.5: Citizen & Recycler Transaction History & Summary Aggregation** | | | | | | | | | |
| **ITC-RCP-016** | Giao dịch `Completed` xuất hiện ngay trong `GET /transactions` và `GET /recycler/transactions`. | 1. Hoàn tất 1 đơn rồi truy vấn lịch sử của cả 2 bên. | HTTP `200 OK`, xuất hiện đầy đủ ở cả 2 bên. | Đã có giao dịch `Completed`. | Passed | Pending | Pending | N/A | `ReceiptTests.Transactions_AfterConfirm` |
| **ITC-RCP-017** | Đơn đang `AwaitingConfirmation` hoặc `Disputed` không tính vào lịch sử giao dịch hoàn tất. | 1. Gọi `GET /transactions` kiểm tra đơn chưa hoàn tất. | Không xuất hiện trong danh sách `Completed`. | Có đơn `AwaitingConfirmation`/`Disputed`. | Passed | Pending | Pending | N/A | `ReceiptTests.Transactions_ExcludesUnconfirmed` |
| **ITC-RCP-018** | Kiểm tra khối thống kê `summary` cộng dồn chính xác `totalCompletedOrders`, `totalWeightKg`, `totalAmount`, `totalPointsEarned`. | 1. Hoàn tất thêm 1 đơn `4kg * 8000đ = 32000đ` và đối chiếu `summary`. | `summary` tăng đúng `+1` đơn, `+4kg`, `+32000đ`, `+40` điểm. | Hệ thống hoạt động bình thường. | Passed | Pending | Pending | N/A | `ReceiptTests.Transactions_Summary` |
| **ITC-RCP-019** | Kiểm tra lọc theo khoảng ngày (`fromDate`, `toDate`) và từ khóa `search` trên lịch sử giao dịch. | 1. Gọi `GET /transactions?search=...&fromDate=...&toDate=...`. | HTTP `200 OK` (hoặc `400` nếu `fromDate > toDate`). | Đã có giao dịch `Completed`. | Passed | Pending | Pending | N/A | `ReceiptTests.Transactions_FilterByKeyword` |
| **ITC-RCP-020** | Kiểm tra tính duy nhất của mã biên nhận `RCP-yyyy-NNNN`. | 1. Xuất 2 biên nhận liên tiếp và so sánh `code`. | Cả 2 mã đều đúng định dạng và khác nhau hoàn toàn. | Đã có 2 đơn `Accepted`. | Passed | Pending | Pending | N/A | `ReceiptTests.Create_MultipleReceipts` |

---

## SHEET 11: `M08_Negotiation` – IN-APP REAL-TIME NEGOTIATION CHATBOX
*(Chi tiết đầy đủ xem tại: [`08_integration_test_in_app_realtime_negotiation_chatbox.md`](./08_integration_test_in_app_realtime_negotiation_chatbox.md))*

| Feature | `In-App Real-Time Negotiation Chatbox (M08_Negotiation)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử tích hợp Chat Thương lượng Thời gian thực (`U-08`, `R-05`): Quyền truy cập phòng chat (`BR-NEG-01`), Upload ảnh đính kèm & gửi tin nhắn `Text`/`Image`, Gửi Đề xuất giá/giờ & Phản đề xuất ghi đè `Superseded` (`BR-NEG-04`), Chấp nhận Đề xuất khóa giá & giờ vào đơn Module 6 (`BR-NEG-06`), Chấp nhận nhanh (`/accept-terms`) và Đánh dấu đã đọc (`PUT /read`). | | | |
| **Number of TCs** | **15** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 15 | 0 | 0 | 0 |
| *Round 2* | 0 | 0 | 15 | 0 |
| *Round 3* | 0 | 0 | 15 | 0 |

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Function 8.1: Negotiation Thread Access Control** | | | | | | | | | |
| **ITC-NEG-001** | Chủ đơn (`Citizen`) và cơ sở nhận đơn (`Collector`) truy cập được phòng chat (`GET /messages`). | 1. Gọi `GET /api/v1/trade/orders/{orderId}/messages` với token 2 bên. | HTTP `200 OK` (`MSG-NEG-THREAD-OK`). | Đơn `{orderId}` tồn tại. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG01` |
| **ITC-NEG-002** | Chặn người dùng thứ ba ngoài đơn truy cập phòng chat (`BR-NEG-01`). | 1. Dùng token `outsider` gọi `GET /messages`. | HTTP `400/403` (`MSG-AUTH-03`). | Tài khoản `outsider` không thuộc đơn. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG01` |
| **Function 8.2: Chat Attachment Upload & Text/Image Messaging** | | | | | | | | | |
| **ITC-NEG-003** | Tải ảnh thực tế phế liệu đính kèm phòng chat (`POST /attachments`). | 1. Gửi ảnh PNG qua `POST /api/v1/trade/orders/{orderId}/messages/attachments`. | HTTP `200 OK` (`MSG-NEG-UPLOAD-OK`), trả về `fileUrl`. | Thành viên đơn đã đăng nhập. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG02` |
| **ITC-NEG-004** | Gửi tin nhắn `Text` và `Image` (`POST /messages`) thành công và tăng `unreadCount` đối phương. | 1. `Citizen` gửi 1 tin `Text` và 1 tin `Image`.<br>2. `Collector` kiểm tra `unreadCount`. | HTTP `201 Created` (`MSG-NEG-02`), `unreadCount >= 2`. | Đơn `{orderId}` đang mở. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG02` |
| **ITC-NEG-005** | Chặn gửi tin nhắn khi văn bản rỗng hoặc vượt quá `1000` ký tự (`1005` ký tự). | 1. Gửi `POST /messages` với chuỗi rỗng và chuỗi `1005` ký tự. | HTTP `400 Bad Request`. | Đơn `{orderId}` đang mở. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG03` |
| **ITC-NEG-006** | Chặn gửi tin nhắn `Image` thiếu `attachmentUrl` hoặc loại `"Audio"` không hỗ trợ. | 1. Gửi `POST /messages` với `attachmentUrl = ""` hoặc `messageType = "Audio"`. | HTTP `400 Bad Request`. | Đơn `{orderId}` đang mở. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG03` |
| **Function 8.3: Price/Time-Slot Proposal & Counter-Proposal Superseding** | | | | | | | | | |
| **ITC-NEG-007** | Chặn gửi Đề xuất khi đơn giá `<= 0` hoặc `> 1.000.000 VND/kg` (`BR-NEG-03`). | 1. Gửi `POST /proposals` với `proposedPricePerKg = 0` và `1500000`. | HTTP `400 Bad Request`. | Đơn `{orderId}` đang mở. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG04` |
| **ITC-NEG-008** | Chặn gửi Đề xuất khi khối lượng `< 0.1kg` hoặc giờ hẹn trong quá khứ (`BR-NEG-03`, `BR-NEG-05`). | 1. Gửi `POST /proposals` với `proposedWeightKg = 0.05` và giờ quá khứ. | HTTP `400 Bad Request`. | Đơn `{orderId}` đang mở. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG04` |
| **ITC-NEG-009** | Phản đề xuất B tự động ghi đè Đề xuất A trước đó sang `Superseded` (`BR-NEG-04`). | 1. `Citizen` gửi Proposal A (`9000đ`).<br>2. `Collector` gửi Proposal B (`8500đ`). | Proposal A chuyển sang `Superseded`, `activeProposal` là B (`Pending`). | Đơn `{orderId}` đang mở. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG05` |
| **Function 8.4: Proposal Response, Order Terms Locking & Read Receipts** | | | | | | | | | |
| **ITC-NEG-010** | Chặn người gửi tự chấp nhận Đề xuất do chính mình tạo ra (`BR-NEG-04`). | 1. `Citizen` gửi Proposal rồi tự gọi `POST /respond` (`isAccepted = true`). | HTTP `400 Bad Request` (`BR-NEG-04`). | Proposal đang `Pending`. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG06` |
| **ITC-NEG-011** | Đối phương từ chối Đề xuất (`Rejected`) và cho phép gửi lại Đề xuất mới. | 1. `Collector` từ chối Proposal 1.<br>2. `Citizen` gửi Proposal 2 mới. | Bước 1: `200 OK` (`MSG-NEG-REJECT-OK`). Bước 2: `201 Created`. | Đơn đang thương lượng. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG07` |
| **ITC-NEG-012** | **Tích hợp M8 $\rightarrow$ M6:** Chấp nhận Đề xuất khóa cứng `AgreedPricePerKg` & `ConfirmedTimeSlot` vào đơn bàn giao (`BR-NEG-06`). | 1. `Collector` chấp nhận Proposal (`8800đ`, `12kg`).<br>2. Kiểm tra đơn ở Module 6. | HTTP `200 OK` (`MSG-NEG-03`), `agreedPricePerKg = 8800`, đơn M6 chuyển sang `Accepted`. | Proposal đang `Pending`. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG08` |
| **ITC-NEG-013** | Khóa gửi thêm Proposal mới sau khi hai bên đã chốt thỏa thuận (`BR-NEG-02`). | 1. Gửi thêm `POST /proposals` trên đơn đã `Accepted`. | HTTP `400 Bad Request` (`BR-NEG-02`). | Đàm phán đã `Accepted`. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG08` |
| **ITC-NEG-014** | `Recycler` chấp nhận trực tiếp điều khoản ban đầu qua `POST /accept-terms`. | 1. Tạo đơn mới, `Collector` gọi `POST /accept-terms`. | HTTP `200 OK` (`MSG-NEG-03`), `negotiationStatus = Accepted`. | Đơn đang `Pending`. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG09` |
| **ITC-NEG-015** | Đánh dấu đã đọc toàn bộ tin nhắn (`PUT /messages/read`) đưa `unreadCount` về `0`. | 1. Gửi 2 tin nhắn.<br>2. Gọi `PUT /messages/read` và kiểm tra lại thread. | HTTP `200 OK` (`MSG-NEG-READ-OK`), `unreadCount = 0`. | Có tin nhắn chưa đọc. | Passed | Pending | Pending | N/A | `NegotiationChatTests.NEG10` |

---

## SHEET 12: `SecurityTest` – CROSS-MODULE SECURITY & AUTHORIZATION INTEGRATION
*(Chi tiết đầy đủ xem tại: [`09_integration_test_security.md`](./09_integration_test_security.md))*

| Test Case ID | Scenario Name | Role / Session | Coverage Technique | Given | When | Then (Expected: HTTP Status / Visibility) | Status | Defect ID | Notes |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :---: | :---: | :--- |
| **TC-SEC-001** | Ẩn số điện thoại hai bên khi đơn bàn giao đang `Pending` (`BR-PRIV-01`) | Citizen / Recycler (own order) | Use Case Testing | Đơn bàn giao mới tạo ở trạng thái `Pending` | Gọi `GET /api/v1/handover-requests/{id}` hoặc `/recycler/handover-requests/{id}` | HTTP `200 OK`; `citizenPhone = null` và `recyclerPhone = null` — chỉ mở khóa sau khi `Accepted`. | Passed | N/A | `HandoverTests` |
| **TC-SEC-002** | Không rò rỉ `password` hoặc `passwordHash` trong response JSON | Guest (no session) | Error Guessing | API `POST /auth/register` và `GET /auth/demo-users` | Gửi request đăng ký hoặc lấy danh sách demo users | HTTP `200/201`; raw JSON tuyệt đối không chứa mật khẩu hoặc `passwordHash`. | Passed | N/A | `AuthTests` |
| **TC-SEC-003** | Unauthenticated request đến các protected URLs | Guest (không có session/token) | Error Guessing | Không đính kèm header `Authorization: Bearer` | Gọi `/auth/me`, `/recycler/*`, `/handover-requests/*`, `/transactions` | HTTP `401 Unauthorized` trên toàn bộ protected endpoints. | Passed | N/A | All Modules |
| **TC-SEC-004** | Sai email/username và sai mật khẩu trả về thông báo lỗi giống nhau (`BR-AUTH-03`) | Guest (no session) | Decision Table | Tài khoản `"citizen"` tồn tại, `"nouser_xyz"` không tồn tại | Gửi `POST /auth/login` với cả 2 tài khoản cùng mật khẩu sai | HTTP `400 Bad Request` với cùng `message` — không tiết lộ tài khoản nào tồn tại. | Passed | N/A | `AuthTests` |
| **TC-SEC-005** | Khóa tài khoản 15 phút sau 5 lần đăng nhập sai liên tiếp (`BR-AUTH-04`) | Guest (no session) | Boundary Value Analysis | Tài khoản có `FailedLoginAttempts = 4` | Nhập sai lần thứ 5, sau đó nhập đúng mật khẩu ở lần thứ 6 | HTTP `400 Bad Request` (`MSG-AUTH-03`), từ chối đăng nhập trong suốt 15 phút khóa. | Passed | N/A | `ITC-AUTH-012` |
| **TC-SEC-006** | Từ chối JWT Token bị chỉnh sửa chữ ký HMAC-SHA256 | Authenticated (tampered token) | Error Guessing | Có một JWT hợp lệ của `citizen` | Sửa 4 ký tự cuối phần Signature và gọi `GET /auth/me` | HTTP `401 Unauthorized`. | Passed | N/A | `AuthTests` |
| **TC-SEC-007** | Phát hiện Token Replay Attack và thu hồi toàn bộ phiên (`BR-AUTH-05`) | Citizen / Recycler | Use Case Testing | `RefreshToken` cũ đã `IsRevoked = true` | Gửi lại `RefreshToken` cũ vào `POST /auth/refresh-token` | HTTP `400/401` (`MSG-AUTH-10`), thu hồi toàn bộ chuỗi Refresh Token của user. | Passed | N/A | `ITC-AUTH-016` |
| **TC-SEC-008** | Chặn SQL Injection và XSS payload trên khung đăng nhập | Guest (no session) | Error Guessing | Chuỗi `' OR '1'='1`, `admin'--`, `<script>alert(1)</script>` | Gửi `POST /auth/login` với payload độc hại | HTTP `400 Bad Request`, `accessToken = null`. | Passed | N/A | `AuthTests` |
| **TC-SEC-009** | Chặn leo thang đặc quyền: `Citizen` gọi API thẩm định của `Staff`/`Admin` | Citizen (authenticated) | Decision Table | Đăng nhập với `Role = Citizen` | Gọi `GET /api/v1/recycler-applications` | HTTP `403 Forbidden`. | Passed | N/A | `OnboardingTests` |
| **TC-SEC-010** | Chặn `Citizen` gọi API bảng giá và lịch hoạt động của `Recycler` | Citizen (authenticated) | Decision Table | Đăng nhập với `Role = Citizen` | Gọi `/api/v1/recycler/profile`, `/prices`, `/availability`, `/schedule` | HTTP `403 Forbidden` trên cả 9 endpoints. | Passed | N/A | `RecyclerTests` |
| **TC-SEC-011** | Chặn `Recycler` chưa duyệt (`Pending`) bật `Online` trên bản đồ (`BR-MTH-01`) | Pending Recycler | Decision Table | Hồ sơ có `VerificationStatus = Pending` | Gửi `PATCH /api/v1/recycler/availability` (`isOnline = true`) | HTTP `400 Bad Request`. | Passed | N/A | `RecyclerTests` |
| **TC-SEC-012** | Chống IDOR trên bảng giá: `Recycler B` sửa dòng giá của `Recycler A` | Recycler B | Use Case Testing | Dòng giá `{priceId}` thuộc về `Collector A` | `Recycler B` gọi `PUT /api/v1/recycler/prices/{priceId}` | HTTP `404 Not Found`, giá của `Collector A` không đổi. | Passed | N/A | `RecyclerTests` |
| **TC-SEC-013** | Chống IDOR trên đơn bàn giao: Người dùng ngoài đơn xem/sửa/nhận/hủy đơn | Citizen B / Recycler B | Use Case Testing | Đơn `{requestId}` thuộc về `Citizen A` và `Collector A` | `Citizen B` hoặc `Recycler B` gọi tác vụ lên `{requestId}` | HTTP `403 Forbidden`. | Passed | N/A | `HandoverTests` |
| **TC-SEC-014** | Chống IDOR trên biên nhận: Người ngoài đơn xem/xác nhận/khiếu nại biên nhận | Citizen B / Recycler B | Use Case Testing | Biên nhận thuộc về `Citizen A` và `Collector A` | `Citizen B` hoặc `Recycler B` gọi `/receipt`, `/confirm`, `/dispute` | HTTP `403 Forbidden`. | Passed | N/A | `ReceiptTests` |
| **TC-SEC-015** | Chặn người ngoài đơn truy cập phòng chat thương lượng (`BR-NEG-01`) | Citizen C (outsider) | Use Case Testing | Phòng chat `{orderId}` chỉ gồm `Citizen A` và `Collector A` | `Citizen C` gọi `GET/POST /api/v1/trade/orders/{orderId}/messages` | HTTP `400/403` (`MSG-AUTH-03`). | Passed | N/A | `NegotiationChatTests` |
| **TC-SEC-016** | Chặn người gửi Đề xuất giá tự bấm chấp nhận Đề xuất của chính mình (`BR-NEG-04`) | Citizen / Recycler (sender) | Business Rule Testing | `Citizen A` vừa gửi `Proposal` đang `Pending` | Chính `Citizen A` gọi `POST /proposals/{id}/respond` (`isAccepted = true`) | HTTP `400 Bad Request` (`BR-NEG-04`). | Passed | N/A | `NegotiationChatTests` |
