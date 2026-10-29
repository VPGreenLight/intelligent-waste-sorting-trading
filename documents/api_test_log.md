# BÁO CÁO NHẬT KÝ KIỂM THỬ API (API TEST EXECUTION LOG)
**Dự án:** EcoLink – Nền tảng Phân loại Rác Thông minh & Giao dịch Phế liệu  
**Tài liệu:** Báo cáo Kiểm thử Tự động & Hướng dẫn Đối soát API (API Test Report & Quality Assurance)  
**Phiên bản:** 1.2.0  
**Ngày thực thi:** 25/10/2026  
**Môi trường kiểm thử:**
- **Core Backend Web API:** `http://localhost:5000` (ASP .NET Core 10 / Clean Architecture)
- **AI Vision Microservice:** `http://localhost:8000` (FastAPI / YOLOv8 / PyTorch)
- **Cơ sở dữ liệu & Caching:** PostgreSQL 16 + PostGIS 3.4 (`ecolink-db:5432`), Redis 7 (`ecolink-redis:6379`)

---

## 📊 TỔNG HỢP KẾT QUẢ KIỂM THỬ (TEST SUMMARY METRICS)

| Hạng mục kiểm thử | Số kịch bản (Scenarios) | Tổng số lượt test (Executions) | Số ca Đạt (PASS) | Số ca Lỗi (FAIL) | Tỷ lệ thành công (%) |
| :--- | :---: | :---: | :---: | :---: | :---: |
| **Module 1: Authentication & User (AUTH)** | 30 | 51 | 51 | 0 | 100% |
| **Module 2: Waste Scan & AI Vision (WS)** | 19 | 27 | 27 | 0 *(Đã khắc phục)* | 100% |
| **TOÀN HỆ THỐNG (TOTAL)** | **49** | **78** | **78** | **0** | **100%** |

> **Ghi chú cập nhật bản 1.2.0:**  
> - Đã khắc phục lỗi **WS-06** (Gửi request thiếu file ảnh nhưng trả về `HTTP 200` $\rightarrow$ đã chuẩn hóa trả về `HTTP 400 Bad Request`).
> - Đã khắc phục lỗi **WS-10** (Truyền tên model không tồn tại nhưng tự động fallback trả về `HTTP 200` $\rightarrow$ đã chuẩn hóa trả về `HTTP 400 Bad Request`).
> - Loại bỏ toàn bộ lỗi ký tự escape markdown, chuẩn hóa phân tách cột *Expected Result* và *Observed Result* theo chuẩn kiểm thử IEEE 829.

---

## 1. MODULE 1: AUTHENTICATION & IDENTITY (AUTH)

| ID | Kịch bản kiểm thử (Test Scenario) | Phương thức & Endpoint | Kết quả mong đợi (Expected Result) | Kết quả thực tế (Observed Result) | Số lần test | Trạng thái |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: |
| **AUTH-01** | Kiểm tra Health check hệ thống | `GET /health` | HTTP 200; hệ thống hoạt động bình thường | HTTP 200; `{"status": "Healthy"}` | 1 | PASS |
| **AUTH-02** | Đăng ký tài khoản hợp lệ | `POST /api/v1/auth/register` | HTTP 201 Created; `success = true`; trả về thông tin user mới | HTTP 201; `success = true`; user object đầy đủ id, email | 1 | PASS |
| **AUTH-03** | Đăng ký tài khoản và kiểm tra không lộ mật khẩu | `POST /api/v1/auth/register` | Trường `password` và `passwordHash` không xuất hiện trong payload phản hồi | Đạt; payload không chứa trường mật khẩu thô hoặc mã băm | 1 | PASS |
| **AUTH-04** | Đăng ký không có số điện thoại (trường tùy chọn) | `POST /api/v1/auth/register` | Đăng ký thành công; `phoneNumber = null` | HTTP 201; Đăng ký thành công | 1 | PASS |
| **AUTH-05** | Đăng ký với độ dài trường tối đa (username 50, password 100, fullName 100 ký tự) | `POST /api/v1/auth/register` | Đăng ký thành công; không bị cắt ngắn dữ liệu | HTTP 201; Đăng ký thành công | 1 | PASS |
| **AUTH-06** | Đăng ký với độ dài mật khẩu tối thiểu (6 ký tự) | `POST /api/v1/auth/register` | Đăng ký thành công | HTTP 201; Đăng ký thành công | 1 | PASS |
| **AUTH-07** | Đăng ký với Username đã tồn tại | `POST /api/v1/auth/register` | HTTP 400 Bad Request; `success = false`; mã lỗi `MSG-AUTH-01` | HTTP 400; `success = false`; "Tên đăng nhập đã tồn tại" | 1 | PASS |
| **AUTH-08** | Đăng ký với Email đã tồn tại | `POST /api/v1/auth/register` | HTTP 400 Bad Request; `success = false`; mã lỗi `MSG-AUTH-01` | HTTP 400; `success = false`; "Email này đã được sử dụng" | 1 | PASS |
| **AUTH-09** | Đăng ký với dữ liệu sai định dạng (11 trường hợp vi phạm quy tắc) | `POST /api/v1/auth/register` | HTTP 400 Bad Request cho toàn bộ 11 ca kiểm thử validation | HTTP 400 cho cả 11 ca (username ngắn/dài, email sai, pass ngắn...) | 11 | PASS |
| **AUTH-10** | Đăng ký với JSON body rỗng (`{}`) | `POST /api/v1/auth/register` | HTTP 400 Bad Request; thông báo thiếu các trường bắt buộc | HTTP 400; `MSG-GEN-01` lỗi validation | 1 | PASS |
| **AUTH-11** | Đăng ký nhưng không gửi request body | `POST /api/v1/auth/register` | HTTP 400 Bad Request hoặc HTTP 415 Unsupported Media Type | HTTP 415 / 400 từ ASP.NET Core Middleware | 1 | PASS |
| **AUTH-12** | Đăng ký sau đó đăng nhập bằng cả username và email | `POST /api/v1/auth/login` | HTTP 200 OK; trả về AccessToken (JWT) và RefreshToken | HTTP 200; token hợp lệ, giải mã đúng claims | 1 | PASS |
| **AUTH-13** | Đăng nhập bằng 3 tài khoản demo sẵn có (`citizen`, `collector`, `admin`) | `POST /api/v1/auth/login` | HTTP 200 OK; trả về token với Role tương ứng chính xác | HTTP 200 cho cả 3 tài khoản; phân quyền chuẩn Role | 3 | PASS |
| **AUTH-14** | Kiểm tra cấu trúc phản hồi đăng nhập (điểm xanh, cấp độ cây, số kg phế liệu) | `POST /api/v1/auth/login` | Cấu trúc dữ liệu số nguyên chuẩn; không để lộ hash mật khẩu | HTTP 200; các trường `greenPoints`, `treeLevel` đúng kiểu dữ liệu | 1 | PASS |
| **AUTH-15** | Kiểm tra thời gian hết hạn của Access Token | `POST /api/v1/auth/login` | `expiresAt` nằm trong tương lai (sau thời điểm hiện tại 2 giờ) | HTTP 200; `expiresAt` hợp lệ và đúng múi giờ UTC | 1 | PASS |
| **AUTH-16** | Đăng nhập với mật khẩu không chính xác | `POST /api/v1/auth/login` | HTTP 400 Bad Request; không cấp phát token | HTTP 400; `MSG-AUTH-01` | 1 | PASS |
| **AUTH-17** | Đăng nhập với tài khoản không tồn tại | `POST /api/v1/auth/login` | HTTP 400 Bad Request; `success = false` | HTTP 400; `MSG-AUTH-01` | 1 | PASS |
| **AUTH-18** | So sánh thông báo lỗi giữa sai mật khẩu và sai tài khoản (Ngăn ngừa User Enumeration) | `POST /api/v1/auth/login` | Thông điệp lỗi giống hệt nhau ("Tên đăng nhập, email hoặc mật khẩu không chính xác.") | Đạt; không thể suy đoán sự tồn tại của tài khoản qua thông báo | 1 | PASS |
| **AUTH-19** | Đăng nhập với body không hợp lệ (body rỗng, thiếu username, thiếu password...) | `POST /api/v1/auth/login` | HTTP 400 Bad Request | HTTP 400 cho cả 5 kịch bản | 5 | PASS |
| **AUTH-20** | Đăng nhập không gửi request body | `POST /api/v1/auth/login` | HTTP 400 Bad Request hoặc HTTP 415 Unsupported Media Type | HTTP 415 / 400 | 1 | PASS |
| **AUTH-21** | Đăng nhập với các chuỗi injection (SQL Injection, XSS, NoSQLi) | `POST /api/v1/auth/login` | HTTP 400 Bad Request; chặn đứng injection, không rò rỉ dữ liệu | HTTP 400; hệ thống xử lý an toàn qua EF Core Parameterization | 4 | PASS |
| **AUTH-22** | Gọi `/me` lấy hồ sơ cá nhân bằng token của 3 tài khoản demo | `GET /api/v1/auth/me` | HTTP 200 OK; trả về đúng định danh của từng người dùng | HTTP 200; thông tin người dùng khớp với token bearer | 3 | PASS |
| **AUTH-23** | Gọi `/me` bằng token của người dùng vừa mới đăng ký | `GET /api/v1/auth/me` | HTTP 200 OK; trả về đúng username vừa tạo | HTTP 200; dữ liệu chính xác | 1 | PASS |
| **AUTH-24** | Gọi `/me` không kèm Authorization Header | `GET /api/v1/auth/me` | HTTP 401 Unauthorized | HTTP 401 Unauthorized | 1 | PASS |
| **AUTH-25** | Gọi `/me` với token không hợp lệ (chuỗi rỗng, chuỗi 'abc', JWT giả tạo) | `GET /api/v1/auth/me` | HTTP 401 Unauthorized cho cả 3 trường hợp | HTTP 401 Unauthorized | 3 | PASS |
| **AUTH-26** | Gọi `/me` với token bị chỉnh sửa chữ ký số (Tampered JWT) | `GET /api/v1/auth/me` | HTTP 401 Unauthorized; phát hiện chữ ký HMAC SHA256 không khớp | HTTP 401 Unauthorized | 1 | PASS |
| **AUTH-27** | Lấy danh sách tài khoản demo phục vụ phát triển *(Chỉ kích hoạt tại Dev/Staging)* | `GET /api/v1/auth/demo-users` | HTTP 200 OK; danh sách tài khoản demo không rỗng | HTTP 200; danh sách gồm citizen, collector, admin | 1 | PASS |
| **AUTH-28** | Kiểm tra endpoint `demo-users` không để lộ chuỗi mã hóa mật khẩu | `GET /api/v1/auth/demo-users` | Không chứa trường `passwordHash` trong JSON trả về | Đạt; trường hash mật khẩu đã bị loại bỏ | 1 | PASS |
| **AUTH-29** | Gọi endpoint sinh token đối tác `vietqr-token` không có thông tin xác thực | `POST /api/v1/auth/vietqr-token` | HTTP 401 Unauthorized | HTTP 401 Unauthorized | 1 | PASS |
| **AUTH-30** | Gọi `vietqr-token` bằng Bearer Token người dùng thay vì Basic Auth đối tác | `POST /api/v1/auth/vietqr-token` | HTTP 401 Unauthorized; endpoint yêu cầu Client ID / Secret Basic Auth | HTTP 401 Unauthorized; từ chối Bearer User Token | 1 | PASS |

---

## 2. MODULE 2: WASTE SCAN & AI CLASSIFICATION (WS)

*Áp dụng trên 4 endpoints chính của phân hệ nhận diện rác:*
1. `POST /api/v1/waste-scan/classify` (Phân tích ảnh rác bằng AI & ánh xạ quy tắc)
2. `GET /api/v1/waste-scan/models` (Danh mục các mô hình AI hỗ trợ)
3. `GET /api/v1/waste-scan/rules` (Danh mục quy tắc phân loại rác tĩnh)
4. `POST /api/v1/waste-scan/feedback` (Báo cáo kết quả AI nhận diện sai)

| ID | Kịch bản kiểm thử (Test Scenario) | Phương thức & Endpoint | Kết quả mong đợi (Expected Result) | Kết quả thực tế (Observed Result) | Số lần test | Trạng thái |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: |
| **WS-01** | Lấy danh sách các mô hình AI có sẵn | `GET /api/v1/waste-scan/models` | HTTP 200 OK; danh sách gồm 4 mô hình (`yolov8_detect`, `mobilenet_v3`, `efficientnet_b0`, `yolov8n`) | HTTP 200; danh sách trả về đầy đủ, `default_model` là `yolov8_detect` | 1 | PASS |
| **WS-02** | Lấy danh mục quy tắc phân loại rác tĩnh | `GET /api/v1/waste-scan/rules` | HTTP 200 OK; trả về cấu trúc hướng dẫn phân loại theo thùng màu | HTTP 200; JSON đầy đủ các nhóm rác và quy chuẩn thùng | 1 | PASS |
| **WS-03** | Phân loại ảnh rác bằng mô hình mặc định | `POST /api/v1/waste-scan/classify` | HTTP 200 OK; trả về kết quả AI (nhãn rác, độ tin cậy) kèm quy tắc hướng dẫn | HTTP 200; nhận diện chính xác vật thể kèm thùng rác quy chuẩn | 1 | PASS |
| **WS-04** | Phân loại ảnh rác với mô hình được chỉ định rõ | `POST /api/v1/waste-scan/classify` | HTTP 200 OK; `success = true`; mô hình sử dụng khớp với tham số gửi lên | HTTP 200; kết quả phân loại thành công | 1 | PASS |
| **WS-05** | Kiểm tra miền giá trị của độ tin cậy (Confidence Score) | `POST /api/v1/waste-scan/classify` | Độ tin cậy nằm trong khoảng $[0.0, 1.0]$ | Đạt; giá trị luôn thuộc đoạn $[0.0, 1.0]$ | 1 | PASS |
| **WS-06** | Gửi yêu cầu phân loại nhưng không đính kèm file ảnh | `POST /api/v1/waste-scan/classify` | **HTTP 400 Bad Request**; mã lỗi `MSG-CLS-04` ("Vui lòng tải lên file ảnh hợp lệ.") | **HTTP 400 Bad Request**; `success = false`; `MSG-CLS-04` *(Đã sửa lỗi trả về 200)* | 1 | **PASS** *(Fixed)* |
| **WS-07** | Gửi tập tin ảnh có dung lượng 0 byte (Empty file) | `POST /api/v1/waste-scan/classify` | HTTP 400 Bad Request; từ chối file rỗng | HTTP 400 Bad Request; `MSG-CLS-04` | 1 | PASS |
| **WS-08** | Gửi tập tin không phải ảnh (file `.txt`, `.pdf`, `.exe`) | `POST /api/v1/waste-scan/classify` | HTTP 400 Bad Request; mã lỗi `MSG-CLS-05` (Chỉ chấp nhận jpg, jpeg, png, webp) | HTTP 400 Bad Request; `MSG-CLS-05` | 1 | PASS |
| **WS-09** | Gửi tập tin ảnh bị hỏng cấu trúc dữ liệu (Corrupted Image) | `POST /api/v1/waste-scan/classify` | HTTP 400 Bad Request; từ chối ảnh hỏng, không gây crash sập server (No 500) | HTTP 400 Bad Request; thông báo lỗi đọc ảnh | 1 | PASS |
| **WS-10** | Gửi yêu cầu với tên mô hình AI không tồn tại (`model = "unknown_xyz"`) | `POST /api/v1/waste-scan/classify` | **HTTP 400 Bad Request**; mã lỗi `MSG-CLS-06` thông báo mô hình không hợp lệ | **HTTP 400 Bad Request**; danh sách mô hình hợp lệ *(Đã sửa lỗi tự ý fallback sang 200)* | 1 | **PASS** *(Fixed)* |
| **WS-11** | Gửi request dạng `application/json` thay vì `multipart/form-data` | `POST /api/v1/waste-scan/classify` | HTTP 415 Unsupported Media Type | HTTP 415 Unsupported Media Type từ Web API Middleware | 1 | PASS |
| **WS-12** | Gửi phản hồi báo AI nhận diện sai với đầy đủ dữ liệu | `POST /api/v1/waste-scan/feedback` | HTTP 200 OK; `success = true`; ghi nhận thành công | HTTP 200; thông báo ghi nhận thành công | 1 | PASS |
| **WS-13** | Gửi phản hồi báo sai chỉ với các trường tối thiểu | `POST /api/v1/waste-scan/feedback` | HTTP 200 OK; ghi nhận thành công | HTTP 200 OK | 1 | PASS |
| **WS-14** | Gửi phản hồi với kiểu dữ liệu độ tin cậy sai (chuỗi thay vì float) | `POST /api/v1/waste-scan/feedback` | HTTP 400 Bad Request; lỗi parse JSON Schema | HTTP 400 Bad Request | 1 | PASS |
| **WS-15** | Gửi phản hồi không đính kèm Request Body | `POST /api/v1/waste-scan/feedback` | HTTP 400 Bad Request | HTTP 400 Bad Request | 1 | PASS |
| **WS-16** | Gửi phản hồi với cú pháp JSON bị lỗi cú pháp (Malformed JSON) | `POST /api/v1/waste-scan/feedback` | HTTP 400 Bad Request | HTTP 400 Bad Request | 1 | PASS |
| **WS-17** | Thực thi luồng trọn vẹn: Quét ảnh $\rightarrow$ Nhận diện $\rightarrow$ Gửi phản hồi báo sai | Chuỗi 2 API: `/classify` sau đó `/feedback` | Cả 2 bước đều hoàn tất thành công; liên kết dữ liệu nhất quán | Hoàn tất chuỗi; feedback ghi nhận đúng nhãn dự đoán của bước 1 | 1 | PASS |
| **WS-18** | Truy cập các endpoint Waste Scan không kèm Authentication Token | Cả 4 endpoints của phân hệ Waste Scan | HTTP 200 OK (Chế độ Public Scanner cho người dùng vãng lai) | HTTP 200 OK cho cả 4 endpoints | 4 | PASS |
| **WS-19** | Truy cập các endpoint với Authentication Token không hợp lệ | Cả 4 endpoints của phân hệ Waste Scan | Hệ thống không bị crash sập (No 500); xử lý an toàn | Đạt; không phát sinh lỗi HTTP 500 | 4 | PASS |

> **⚠️ Lưu ý Khuyến nghị Vận hành & Bảo mật:**  
> - Tại **WS-18**, việc cho phép `POST /api/v1/waste-scan/feedback` hoạt động công khai không cần Token là phù hợp cho giai đoạn thử nghiệm (Beta). Tuy nhiên, khi đưa lên môi trường **Production thương mại**, khuyến nghị áp dụng thêm **Rate Limiting** (giới hạn tối đa 5 lượt phản hồi/phút/IP) hoặc yêu cầu Bearer Token để ngăn chặn hành vi spam dữ liệu làm nhiễm độc mô hình retrain (Data Poisoning Attack).
