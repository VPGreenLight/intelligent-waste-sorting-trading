# **THIẾT KẾ CƠ SỞ DỮ LIỆU HỆ THỐNG ECOLINK (DATABASE SCHEMA DESIGN)**

> **Hệ quản trị CSDL:** PostgreSQL 16+ (Kích hoạt phần mở rộng **PostGIS** cho dữ liệu không gian/GIS)  
> **Chuẩn thiết kế:** 3NF (Third Normal Form), Clean Architecture / Domain-Driven Design (DDD)  
> **Dự án:** EcoLink — Nền tảng phân loại rác thông minh tích hợp AI & Thu gom thương mại phế liệu (Capstone SEP490-G141)  
> **Tài liệu tham chiếu:** SRS Report 3, Business Rules Appendix (`BR-AUTH` đến `BR-ADM`), Use Case Specification (`G`, `SU`, `U`, `R`, `S`, `A`).

---

## **0. KIỂU DỮ LIỆU LIỆT KÊ HỆ THỐNG (POSTGRESQL ENUMS)**

Nhằm tối ưu hóa dung lượng lưu trữ, đảm bảo toàn vẹn dữ liệu ở tầng cơ sở dữ liệu và ngăn chặn dữ liệu không hợp lệ, hệ thống định nghĩa các kiểu `ENUM` sau:

```sql
-- 1. Quyền hạn và trạng thái tài khoản
CREATE TYPE user_role AS ENUM ('User', 'Recycler', 'Staff', 'Admin');
CREATE TYPE user_status AS ENUM ('Active', 'Locked', 'Suspended', 'Pending_Verification');
CREATE TYPE verification_status AS ENUM ('Pending', 'Verified', 'Rejected');
CREATE TYPE active_status AS ENUM ('Online', 'Offline');

-- 2. Phân loại và dữ liệu AI
CREATE TYPE category_status AS ENUM ('Active', 'Inactive');
CREATE TYPE scan_status AS ENUM ('Completed', 'Failed', 'Low_Confidence');
CREATE TYPE report_status AS ENUM ('Pending', 'Reviewed', 'Rejected');
CREATE TYPE dataset_split AS ENUM ('Train', 'Test', 'Validation');

-- 3. Giao dịch thu gom và khiếu nại
CREATE TYPE request_status AS ENUM ('Pending', 'Negotiating', 'Accepted', 'Scheduled', 'Completed', 'Rejected', 'Cancelled', 'Disputed', 'Expired');
CREATE TYPE message_type AS ENUM ('TEXT', 'IMAGE', 'PROPOSAL', 'SYSTEM');
CREATE TYPE complaint_status AS ENUM ('Pending', 'Investigating', 'Resolved', 'Dismissed');
CREATE TYPE complaint_outcome AS ENUM ('User_Compensated', 'Dismissed', 'Warning_Issued');
CREATE TYPE review_status AS ENUM ('Visible', 'Hidden', 'Reported', 'Deleted');

-- 4. Thanh toán dịch vụ nội bộ (VietQR / PayOS)
CREATE TYPE order_status AS ENUM ('Pending', 'Paid', 'Expired', 'Cancelled');
CREATE TYPE payment_status AS ENUM ('Pending', 'Success', 'Failed', 'Refunded');
CREATE TYPE target_role AS ENUM ('User', 'Recycler', 'All');

-- 5. Gamification, Nội dung & Khảo sát
CREATE TYPE point_action_type AS ENUM ('Handover_Reward', 'Daily_Checkin', 'Redeem_Gift', 'Tree_Care', 'Bonus', 'Adjustment', 'Reversal');
CREATE TYPE gift_type AS ENUM ('Physical', 'Voucher');
CREATE TYPE redemption_status AS ENUM ('Pending', 'Redeemed', 'Delivered', 'Expired', 'Cancelled');
CREATE TYPE post_status AS ENUM ('Draft', 'Pending_Approval', 'Approved', 'Rejected');
CREATE TYPE survey_status AS ENUM ('Draft', 'Active', 'Closed');
CREATE TYPE question_type AS ENUM ('Rating_1_5', 'Multiple_Choice', 'Text');
```

---

## **1. PHÂN HỆ TÀI KHOẢN & XÁC THỰC (ACCOUNTS & AUTHENTICATION)**

### **1.1. `User` (Tài khoản cốt lõi)**
Lưu trữ thông tin xác thực dùng chung cho toàn bộ các tác nhân trong hệ thống (Citizen, Recycler, Staff, Admin).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `user_id` | BIGSERIAL | PRIMARY KEY | Định danh duy nhất của tài khoản người dùng |
| `username` | VARCHAR(50) | NOT NULL, UNIQUE | Tên đăng nhập (chữ cái, số, không dấu) |
| `password_hash` | VARCHAR(255) | NULL | Mật khẩu băm (BCrypt/Argon2); NULL nếu đăng ký mạng xã hội |
| `full_name` | VARCHAR(100) | NOT NULL | Họ và tên hiển thị |
| `email` | VARCHAR(255) | NOT NULL, UNIQUE | Địa chỉ email liên hệ & đăng nhập (BR-AUTH-01) |
| `phone_number` | VARCHAR(20) | NULL | Số điện thoại liên lạc |
| `avatar_url` | TEXT | NULL | Đường dẫn ảnh đại diện trên Cloudflare R2 |
| `role` | user_role | NOT NULL, DEFAULT 'User' | Vai trò người dùng (User/Citizen, Recycler, Staff, Admin) |
| `is_google_auth` | BOOLEAN | NOT NULL, DEFAULT FALSE | Đăng ký/đăng nhập qua Google OAuth 2.0 (BR-AUTH-02, BR-AUTH-07) |
| `status` | user_status | NOT NULL, DEFAULT 'Active' | Trạng thái tài khoản (Active, Locked, Suspended) |
| `failed_login_attempts` | INT | NOT NULL, DEFAULT 0 | Đếm số lần nhập sai mật khẩu liên tiếp (BR-AUTH-04) |
| `lockout_until` | TIMESTAMPTZ | NULL | Thời điểm hết hạn khóa tài khoản tạm thời (BR-AUTH-04) |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm tạo tài khoản |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật hồ sơ gần nhất |

---

### **1.2. `RecyclerProfile` (Hồ sơ Đối tác Thu mua / Vựa Ve chai)**
Áp dụng mô hình **Shared Primary Key** (Quan hệ 1 - 1 với `User`). Bổ sung cột không gian **PostGIS** và trạng thái trực tuyến.

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `recycler_id` | BIGINT | PRIMARY KEY, FOREIGN KEY | Khóa chính tham chiếu trực tiếp `User.user_id` (1 - 1) |
| `facility_name` | VARCHAR(150) | NOT NULL | Tên cơ sở thu mua / Tên vựa phế liệu |
| `representative_name`| VARCHAR(100) | NULL | Họ và tên người đại diện pháp luật |
| `business_address` | TEXT | NOT NULL | Địa chỉ hoạt động kinh doanh thực tế |
| `latitude` | NUMERIC(10,7) | NOT NULL | Tọa độ Vĩ độ GPS (WGS 84) |
| `longitude` | NUMERIC(11,7) | NOT NULL | Tọa độ Kinh độ GPS (WGS 84) |
| `geom` | GEOMETRY(Point, 4326) | NOT NULL | Đối tượng điểm không gian PostGIS phục vụ tìm kiếm bán kính |
| `operating_hours` | VARCHAR(100) | NULL | Khung giờ mở cửa (Ví dụ: "07:30 - 18:00") |
| `contact_phone` | VARCHAR(20) | NOT NULL | Số điện thoại liên hệ giao dịch |
| `description` | TEXT | NULL | Giới thiệu năng lực thu gom, phương tiện vận chuyển |
| `legal_permit_url` | TEXT | NULL | Giấy phép kinh doanh/môi trường để Staff thẩm định (UC G-03, S-04) |
| `business_photo_url` | TEXT | NULL | Hình ảnh mặt tiền cơ sở thu mua |
| `verification_status`| verification_status| NOT NULL, DEFAULT 'Pending' | Trạng thái thẩm định của Staff (Pending, Verified, Rejected) |
| `active_status` | active_status | NOT NULL, DEFAULT 'Offline' | Trạng thái Bật/Tắt hoạt động trên bản đồ thu gom (UC R-02) |
| `rejection_reason` | TEXT | NULL | Lý do Staff từ chối hồ sơ (BR-MTH-02, MSG-MTH-05) |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm gửi hồ sơ đăng ký |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật thông tin |

*Chỉ mục tối ưu:* `CREATE INDEX idx_recycler_geom ON RecyclerProfile USING GIST (geom);`

---

### **1.3. `StaffProfile` (Hồ sơ Nhân viên Vận hành)**
Áp dụng mô hình **Shared Primary Key** (Quan hệ 1 - 1 với `User`).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `staff_id` | BIGINT | PRIMARY KEY, FOREIGN KEY | Khóa chính tham chiếu trực tiếp `User.user_id` (1 - 1) |
| `staff_code` | VARCHAR(30) | NOT NULL, UNIQUE | Mã định danh nhân viên (Ví dụ: "STF-2026-001") |
| `department` | VARCHAR(100) | NULL | Phòng ban (Ví dụ: Vận hành AI, CSKH, Kiểm duyệt) |
| `position` | VARCHAR(100) | NULL | Chức vụ (Ví dụ: Chuyên viên dữ liệu, Quản trị viên đối tác) |
| `status` | VARCHAR(20) | NOT NULL, DEFAULT 'ACTIVE' | Trạng thái làm việc |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm tạo tài khoản nhân viên |

---

### **1.4. `StaffPermissionAssignment` (Phân quyền Nhân viên - Bổ sung mới)**
Hỗ trợ Use Case **A-04: Quản lý Phân quyền Staff** và quy tắc **BR-ADM-02: Phân tách vai trò**.

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `assignment_id` | BIGSERIAL | PRIMARY KEY | Khóa chính bản ghi phân quyền |
| `staff_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `StaffProfile.staff_id` |
| `permission_code` | VARCHAR(50) | NOT NULL | Mã quyền (VD: `REVIEW_AI`, `HANDLE_COMPLAINT`, `VERIFY_RECYCLER`, `MANAGE_BLOG`, `MANAGE_INVENTORY`) |
| `granted_by` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` (Admin đã cấp quyền) |
| `granted_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cấp quyền |

*Ràng buộc toàn vẹn:* `UNIQUE (staff_id, permission_code)`

---

### **1.5. `PasswordResetToken` (Mã đặt lại mật khẩu)**

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `token_id` | BIGSERIAL | PRIMARY KEY | Khóa chính bản ghi token |
| `user_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` |
| `token_hash` | VARCHAR(255) | NOT NULL, UNIQUE | Mã token băm bảo mật gửi qua Email (BR-AUTH-06) |
| `expires_at` | TIMESTAMPTZ | NOT NULL | Thời điểm token hết hạn |
| `used_at` | TIMESTAMPTZ | NULL | Thời điểm sử dụng (dùng 1 lần duy nhất) |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm sinh yêu cầu đặt lại mật khẩu |

---

## **2. PHÂN HỆ AI & QUẢN TRỊ DANH MỤC RÁC (AI & WASTE MANAGEMENT)**

### **2.1. `MasterWasteCategory` (Danh mục rác hệ thống)**

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `category_id` | BIGSERIAL | PRIMARY KEY | Khóa chính định danh loại rác |
| `category_code` | VARCHAR(50) | NOT NULL, UNIQUE | Mã loại rác chuẩn (`plastic`, `paper`, `glass`, `metal`, `cardboard`, `trash`) |
| `category_name` | VARCHAR(100) | NOT NULL | Tên hiển thị (Nhựa tái chế, Giấy vụn, Kim loại...) |
| `description` | TEXT | NULL | Mô tả đặc điểm nhận diện |
| `standard_bin_color`| VARCHAR(50) | NULL | Màu sắc thùng rác chuẩn quy định (Cam, Xanh lá, Trắng...) |
| `icon_url` | TEXT | NULL | Đường dẫn biểu tượng danh mục |
| `is_recyclable` | BOOLEAN | NOT NULL, DEFAULT TRUE | Có khả năng tái chế thương mại hay không |
| `status` | category_status | NOT NULL, DEFAULT 'Active'| Trạng thái (Active, Inactive) (BR-SRT-04: chỉ ngừng dùng, không xóa) |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm tạo danh mục |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật |

---

### **2.2. `SortingGuidanceRule` (Chính sách & Hướng dẫn phân loại - Rule Engine)**
Quan hệ 1 - 1 với `MasterWasteCategory` theo **BR-SRT-01** (mỗi danh mục có đúng một quy tắc hướng dẫn hoạt động).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `rule_id` | BIGSERIAL | PRIMARY KEY | Khóa chính quy tắc hướng dẫn |
| `category_id` | BIGINT | NOT NULL, UNIQUE, FOREIGN KEY | Tham chiếu `MasterWasteCategory.category_id` (1 - 1) |
| `title` | VARCHAR(150) | NOT NULL | Tiêu đề hướng dẫn |
| `preparation_instruction` | TEXT | NOT NULL | Bước chuẩn bị trước khi vứt (rửa sạch, bẹp chai...) |
| `sorting_instruction` | TEXT | NOT NULL | Hướng dẫn phân loại và chọn đúng thùng |
| `disposal_instruction` | TEXT | NULL | Hướng dẫn bàn giao cho vựa ve chai hoặc điểm tập kết |
| `warning` | TEXT | NULL | Cảnh báo an toàn (nắp nhọn, nguy cơ cháy nổ, pin...) |
| `region` | VARCHAR(100) | NOT NULL, DEFAULT 'VN_NATIONAL' | Khu vực áp dụng quy chuẩn |
| `status` | VARCHAR(20) | NOT NULL, DEFAULT 'ACTIVE' | Trạng thái hiệu lực của hướng dẫn |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm ban hành hướng dẫn |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật gần nhất |

---

### **2.3. `AIScanHistory` (Lịch sử quét ảnh AI)**
Lưu vết các phiên nhận diện hình ảnh của công dân (hỗ trợ cả người dùng vãng lai/Guest theo NFR).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `scan_id` | BIGSERIAL | PRIMARY KEY | Khóa chính phiên quét ảnh |
| `user_id` | BIGINT | NULL, FOREIGN KEY | Tham chiếu `User.user_id` (NULL nếu là Guest quét) |
| `image_url` | TEXT | NOT NULL | Vị trí lưu trữ ảnh trên Cloudflare R2 |
| `predicted_category_id` | BIGINT | NULL, FOREIGN KEY | Tham chiếu `MasterWasteCategory.category_id` dự đoán |
| `confidence_score` | NUMERIC(6,5) | NULL | Điểm tin cậy của mô hình YOLOv8 (0.00000 - 1.00000) |
| `model_version` | VARCHAR(100) | NULL | Phiên bản mô hình (Ví dụ: `yolov8n-ecolink-v2.1`) |
| `scan_status` | scan_status | NOT NULL, DEFAULT 'Completed' | Trạng thái kết quả (Completed, Failed, Low_Confidence) |
| `latitude` | NUMERIC(10,7) | NULL | Vị trí quét ảnh (phục vụ thống kê mật độ rác thải theo vùng) |
| `longitude` | NUMERIC(11,7) | NULL | Kinh độ vị trí quét |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm thực hiện quét |

*Khuyến nghị Partitioning:* Range Partitioning theo cột `created_at` (hàng tháng/quý) khi số lượng quét đạt hàng triệu lượt (NFR-SCA-04).

---

### **2.4. `AIScanDetection` (Chi tiết Bounding Box đa vật thể - Bảng Core)**
Hỗ trợ mô hình Two-Stage Detection (YOLOv8) phát hiện đồng thời nhiều vật thể rác thải trong một bức ảnh duy nhất.

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `detection_id` | BIGSERIAL | PRIMARY KEY | Khóa chính của vật thể nhận diện |
| `scan_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `AIScanHistory.scan_id` (Quan hệ 1 - N) |
| `category_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `MasterWasteCategory.category_id` |
| `confidence_score` | NUMERIC(6,5) | NOT NULL | Độ tin cậy riêng của hộp bao bọc này |
| `x_min` | NUMERIC(10,7) | NOT NULL | Tọa độ chuẩn hóa biên trái (0.0 - 1.0) |
| `y_min` | NUMERIC(10,7) | NOT NULL | Tọa độ chuẩn hóa biên trên (0.0 - 1.0) |
| `x_max` | NUMERIC(10,7) | NOT NULL | Tọa độ chuẩn hóa biên phải (0.0 - 1.0) |
| `y_max` | NUMERIC(10,7) | NOT NULL | Tọa độ chuẩn hóa biên dưới (0.0 - 1.0) |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm ghi nhận |

---

### **2.5. `MisclassifiedReport` (Báo cáo phản hồi AI nhận diện sai)**
Đảm bảo tách bạch rõ ràng 3 nhãn phân loại theo quy tắc **BR-CLS-05** và Use Case **S-01, S-02**.

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `report_id` | BIGSERIAL | PRIMARY KEY | Khóa chính báo cáo sai sót AI |
| `scan_id` | BIGINT | NOT NULL, UNIQUE, FOREIGN KEY | Tham chiếu `AIScanHistory.scan_id` (1 - 1) |
| `user_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` người báo cáo |
| `predicted_category_id` | BIGINT | NOT NULL, FOREIGN KEY | Nhãn AI đã dự đoán ban đầu |
| `user_suggested_category_id`| BIGINT | NOT NULL, FOREIGN KEY | Nhãn thực tế do người dùng đề xuất |
| `staff_verified_category_id`| BIGINT | NULL, FOREIGN KEY | Nhãn chính xác cuối cùng do Staff thẩm định |
| `description` | TEXT | NULL | Mô tả chi tiết của người dùng về sai sót |
| `status` | report_status | NOT NULL, DEFAULT 'Pending' | Trạng thái duyệt (Pending, Reviewed, Rejected) |
| `reviewed_by_staff_id` | BIGINT | NULL, FOREIGN KEY | Tham chiếu `StaffProfile.staff_id` nhân viên thẩm định |
| `review_note` | TEXT | NULL | Ghi chú thẩm định của Staff |
| `reviewed_at` | TIMESTAMPTZ | NULL | Thời điểm Staff phê duyệt |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm gửi phản hồi |

---

### **2.6. `VerifiedDataset` (Tập dữ liệu kiểm thử & Tái huấn luyện AI)**
Tuân thủ **BR-CLS-06 & BR-CLS-07** (phân tách độc lập giữa Train và Test, không rò rỉ dữ liệu).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `dataset_id` | BIGSERIAL | PRIMARY KEY | Khóa chính của mẫu dữ liệu huấn luyện |
| `category_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `MasterWasteCategory.category_id` đã gán nhãn |
| `image_url` | TEXT | NOT NULL | Đường dẫn ảnh mẫu trên kho lưu trữ |
| `annotation_url` | TEXT | NULL | Đường dẫn file nhãn tọa độ bounding box (`.txt` định dạng YOLO) |
| `source` | VARCHAR(255) | NULL | Nguồn gốc ảnh (Phản hồi người dùng, Thu thập thực địa, TrashNet) |
| `dataset_split` | dataset_split | NOT NULL, DEFAULT 'Train' | Phân loại tập (Train, Test, Validation) (BR-CLS-07) |
| `verified_by_staff_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `StaffProfile.staff_id` nhân viên xác thực nhãn |
| `verified_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm xác thực nhãn |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm nhập vào kho dữ liệu |

---

## **3. PHÂN HỆ THU GOM, THƯƠNG LƯỢNG & BÀN GIAO (COLLECTION & TRADING)**

### **3.1. `RecyclerPriceList` (Bảng giá thu mua của Vựa Ve chai)**

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `price_id` | BIGSERIAL | PRIMARY KEY | Khóa chính mục giá thu mua |
| `recycler_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `RecyclerProfile.recycler_id` |
| `category_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `MasterWasteCategory.category_id` |
| `price_per_kg` | NUMERIC(12,2) | NOT NULL | Giá thu mua đề xuất trên mỗi kg (VNĐ) |
| `min_quantity_kg` | NUMERIC(12,3) | NOT NULL, DEFAULT 1.000 | Khối lượng tối thiểu để nhận thu gom |
| `effective_from` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm giá bắt đầu có hiệu lực |
| `effective_to` | TIMESTAMPTZ | NULL | Thời điểm giá hết hạn (NULL là vô thời hạn) |
| `status` | VARCHAR(20) | NOT NULL, DEFAULT 'ACTIVE' | Trạng thái (ACTIVE, INACTIVE) |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật giá |

*Ràng buộc toàn vẹn:* `UNIQUE (recycler_id, category_id)` (với điều kiện giá đang `ACTIVE`).

---

### **3.2. `HandoverRequest` (Yêu cầu bàn giao & Khung giờ thu gom)**
Tuân thủ **BR-NEG-04 & BR-NEG-05** (gộp khung giờ thu gom vào yêu cầu, loại bỏ module đặt lịch riêng biệt).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `request_id` | BIGSERIAL | PRIMARY KEY | Khóa chính yêu cầu bàn giao |
| `request_code` | VARCHAR(50) | NOT NULL, UNIQUE | Mã yêu cầu thu gom (Ví dụ: `HND-2026-0892`) |
| `user_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` (Công dân tạo đơn) |
| `recycler_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `RecyclerProfile.recycler_id` (Cơ sở tiếp nhận) |
| `pickup_address` | TEXT | NOT NULL | Địa chỉ công dân hẹn đến thu gom |
| `latitude` | NUMERIC(10,7) | NULL | Vĩ độ vị trí hẹn thu gom |
| `longitude` | NUMERIC(11,7) | NULL | Kinh độ vị trí hẹn thu gom |
| `geom` | GEOMETRY(Point, 4326) | NULL | Điểm không gian PostGIS hỗ trợ tính đường đi gom rác |
| `waste_photo_url` | TEXT | NULL | Ảnh chụp đống phế liệu thực tế của công dân lúc tạo đơn |
| `proposed_time_slot` | TIMESTAMPTZ | NOT NULL | Khung giờ công dân đề xuất mong muốn (BR-NEG-04) |
| `confirmed_time_slot`| TIMESTAMPTZ | NULL | Khung giờ chính thức chốt khi Recycler ấn Accept (BR-NEG-05) |
| `actual_handover_at` | TIMESTAMPTZ | NULL | Thời điểm thực tế hai bên gặp mặt và cân rác |
| `status` | request_status | NOT NULL, DEFAULT 'Pending' | Trạng thái (Pending, Negotiating, Accepted, Scheduled, Completed, Rejected, Cancelled, Disputed) |
| `user_note` | TEXT | NULL | Ghi chú của người dân (chỉ dẫn vào ngõ hẻm...) |
| `recycler_note` | TEXT | NULL | Ghi chú của cơ sở thu mua |
| `cancel_reason` | TEXT | NULL | Lý do hủy đơn hoặc từ chối đơn (UC U-07, R-04) |
| `cancelled_by` | BIGINT | NULL, FOREIGN KEY | Tham chiếu `User.user_id` bên thực hiện thao tác hủy |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm gửi yêu cầu bàn giao |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật trạng thái gần nhất |

---

### **3.3. `HandoverRequestItem` (Chi tiết các loại rác trong đơn - Bảng Core)**
Cho phép một đơn bàn giao thu gom bao gồm nhiều loại phế liệu khác nhau (Nhựa, Giấy, Lon nhôm...).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `item_id` | BIGSERIAL | PRIMARY KEY | Khóa chính mục hàng phế liệu |
| `request_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `HandoverRequest.request_id` |
| `category_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `MasterWasteCategory.category_id` |
| `estimated_weight_kg`| NUMERIC(12,3) | NOT NULL, DEFAULT 0.000 | Khối lượng người dân ước tính ban đầu (kg) |
| `actual_weight_kg` | NUMERIC(12,3) | NULL | Khối lượng thực tế Recycler đo đạc tại chỗ (kg) |
| `agreed_price_per_kg`| NUMERIC(12,2) | NULL | Đơn giá hai bên đã chốt thương lượng (VNĐ/kg) |
| `item_note` | TEXT | NULL | Ghi chú về chất lượng phế liệu |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm thêm vào yêu cầu |

---

### **3.4. `ChatMessage` (Tin nhắn thương lượng 2 chiều qua SignalR)**
Tuân thủ **BR-NEG-01 & BR-NEG-02** (lưu vết bất biến làm bằng chứng tranh chấp, mở chat kể cả sau khi Completed).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `message_id` | BIGSERIAL | PRIMARY KEY | Khóa chính tin nhắn trao đổi |
| `request_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `HandoverRequest.request_id` |
| `sender_user_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` bên gửi tin nhắn |
| `message_type` | message_type | NOT NULL, DEFAULT 'TEXT' | Kiểu tin nhắn (TEXT, IMAGE, PROPOSAL, SYSTEM) |
| `message_content` | TEXT | NULL | Nội dung tin nhắn văn bản |
| `attachment_url` | TEXT | NULL | Đường dẫn ảnh/file đính kèm trên R2 |
| `proposal_data` | JSONB | NULL | Cấu trúc dữ liệu đề nghị giá/giờ mới (phục vụ hiển thị tương tác) |
| `is_read` | BOOLEAN | NOT NULL, DEFAULT FALSE | Trạng thái người nhận đã xem hay chưa |
| `sent_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm gửi tin nhắn |
| `read_at` | TIMESTAMPTZ | NULL | Thời điểm xem tin nhắn |

---

### **3.5. `HandoverReceipt` (Biên lai bàn giao hoàn tất)**
Xác nhận hoàn tất giao dịch và cấp điểm thưởng cho cả hai bên (**BR-HND-04 & BR-GAM-01**).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `receipt_id` | BIGSERIAL | PRIMARY KEY | Khóa chính biên nhận giao dịch |
| `request_id` | BIGINT | NOT NULL, UNIQUE, FOREIGN KEY | Tham chiếu duy nhất `HandoverRequest.request_id` (1 - 1) |
| `receipt_code` | VARCHAR(50) | NOT NULL, UNIQUE | Mã biên lai giao dịch (Ví dụ: `RCP-2026-9921`) |
| `user_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` (Công dân nhận tiền mặt) |
| `recycler_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `RecyclerProfile.recycler_id` (Cơ sở trả tiền) |
| `total_weight_kg` | NUMERIC(12,3) | NOT NULL | Tổng khối lượng phế liệu thực tế đã cân (kg) |
| `total_amount_cash` | NUMERIC(14,2) | NOT NULL | Tổng số tiền mặt thực tế đã thanh toán trực tiếp (VNĐ) |
| `points_awarded` | BIGINT | NOT NULL, DEFAULT 0 | Số điểm xanh thưởng hệ thống cấp cho giao dịch này |
| `scale_proof_photo_url` | TEXT | NOT NULL | Ảnh chụp đồng hồ cân phế liệu thực tế (BR-HND-04) |
| `receipt_image_url` | TEXT | NULL | Ảnh chụp biên bản/hóa đơn giấy ký tay (nếu có) |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm Recycler lập biên lai |
| `confirmed_by_user_at` | TIMESTAMPTZ | NOT NULL | Thời điểm công dân bấm xác nhận hoàn tất (BR-HND-04) |

---

### **3.6. `TransactionReview` (Đánh giá 2 chiều & Kiểm duyệt vi phạm)**
Thay thế và nâng cấp `RecyclerRating` thành đánh giá 2 chiều theo quy tắc **BR-RVW-01, BR-RVW-02, BR-RVW-03**.

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `review_id` | BIGSERIAL | PRIMARY KEY | Khóa chính đánh giá giao dịch |
| `request_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `HandoverRequest.request_id` |
| `reviewer_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` bên thực hiện đánh giá |
| `reviewee_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` bên được nhận đánh giá |
| `rating_stars` | SMALLINT | NOT NULL | Số sao đánh giá từ 1 đến 5 |
| `comment` | TEXT | NULL | Nhận xét chi tiết về thái độ, độ chính xác của phế liệu |
| `status` | review_status | NOT NULL, DEFAULT 'Visible' | Trạng thái hiển thị (Visible, Hidden, Reported, Deleted) (BR-RVW-03) |
| `moderated_by` | BIGINT | NULL, FOREIGN KEY | Tham chiếu `User.user_id` (Staff/Admin ẩn bài nếu vi phạm) |
| `moderation_reason` | TEXT | NULL | Lý do kiểm duyệt ẩn đánh giá |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm đăng đánh giá |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật gần nhất |

*Ràng buộc toàn vẹn:* `UNIQUE (request_id, reviewer_id)` (mỗi bên chỉ đánh giá 1 lần duy nhất cho mỗi giao dịch hoàn tất).

---

### **3.7. `TransactionComplaint` (Khiếu nại & Tranh chấp giao dịch)**
Tuân thủ quy tắc **BR-DSP-01 đến BR-DSP-06** (Staff làm trọng tài giải quyết, đưa ra quyết định ràng buộc).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `complaint_id` | BIGSERIAL | PRIMARY KEY | Khóa chính hồ sơ khiếu nại |
| `request_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `HandoverRequest.request_id` |
| `submitted_by` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` bên gửi khiếu nại |
| `complaint_type` | VARCHAR(50) | NOT NULL | Lý do (Sai khối lượng, Gian lận giá, Thái độ xấu...) |
| `description` | TEXT | NOT NULL | Nội dung tường trình chi tiết sự việc |
| `evidence_url` | TEXT | NULL | Ảnh chụp/bằng chứng chứng minh sai lệch |
| `status` | complaint_status | NOT NULL, DEFAULT 'Pending' | Trạng thái (Pending, Investigating, Resolved, Dismissed) |
| `assigned_staff_id`| BIGINT | NULL, FOREIGN KEY | Tham chiếu `StaffProfile.staff_id` nhân viên tiếp nhận |
| `resolution_outcome`| complaint_outcome| NULL | Kết quả phân xử (User_Compensated, Dismissed, Warning_Issued) |
| `resolution_note` | TEXT | NULL | Chi tiết phán quyết và giải trình của Staff |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm nộp khiếu nại |
| `resolved_at` | TIMESTAMPTZ | NULL | Thời điểm Staff ra quyết định giải quyết |

---

## **4. PHÂN HỆ DỊCH VỤ NỘI BỘ & THANH TOÁN VIETQR (SERVICE & PAYMENT)**

### **4.1. `ServicePackage` (Gói dịch vụ nâng cấp EcoLink)**
Dành cho các gói nâng cao (Gói tắt quảng cáo cho Citizen, Huy hiệu Tích xanh xác minh uy tín cho Recycler - UC A-10).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `package_id` | BIGSERIAL | PRIMARY KEY | Khóa chính gói dịch vụ |
| `package_code` | VARCHAR(50) | NOT NULL, UNIQUE | Mã gói (Ví dụ: `RECYCLER_VERIFIED_BADGE`, `NO_ADS_MONTHLY`) |
| `package_name` | VARCHAR(100) | NOT NULL | Tên gói dịch vụ |
| `target_role` | target_role | NOT NULL, DEFAULT 'All' | Đối tượng áp dụng (User, Recycler, All) |
| `description` | TEXT | NULL | Mô tả quyền lợi của gói |
| `price_vnd` | NUMERIC(14,2) | NOT NULL | Cước phí dịch vụ niêm yết (VNĐ) |
| `duration_days` | INTEGER | NOT NULL | Thời hạn sử dụng tính theo ngày (Ví dụ: 30, 90, 365) |
| `features` | JSONB | NULL | Cấu hình các đặc quyền mở khóa dưới dạng JSON |
| `status` | VARCHAR(20) | NOT NULL, DEFAULT 'ACTIVE' | Trạng thái mở bán gói |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm tạo gói dịch vụ |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật cấu hình |

---

### **4.2. `ServiceOrder` (Đơn hàng & Thanh toán VietQR Động)**
Tuân thủ **BR-PAY-01 & BR-PAY-02** (xử lý Idempotency, lưu trữ chuỗi mã hóa QR và thông tin đối soát - UC A-11).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `order_id` | BIGSERIAL | PRIMARY KEY | Khóa chính đơn mua dịch vụ |
| `order_code` | VARCHAR(50) | NOT NULL, UNIQUE | Mã đơn hàng đối soát duy nhất (Ví dụ: `SRV-2026-00431`) |
| `user_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` người thanh toán |
| `package_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `ServicePackage.package_id` |
| `amount_vnd` | NUMERIC(14,2) | NOT NULL | Số tiền cần thanh toán chính xác (VNĐ) |
| `payment_method` | VARCHAR(30) | NOT NULL, DEFAULT 'VIETQR' | Phương thức thanh toán (VIETQR, PAYOS) |
| `qr_payload` | TEXT | NULL | Chuỗi dữ liệu chuẩn EMVCo dùng để render mã VietQR động |
| `qr_code_url` | TEXT | NULL | Đường dẫn ảnh mã QR sinh sẵn từ Payment Gateway |
| `payment_reference` | VARCHAR(255) | NULL, UNIQUE | Mã tham chiếu giao dịch đối soát của ngân hàng/PayOS |
| `gateway_transaction_no`| VARCHAR(100)| NULL | Số giao dịch thực tế trên cổng thanh toán |
| `payment_status` | payment_status | NOT NULL, DEFAULT 'Pending' | Trạng thái thanh toán (Pending, Success, Failed, Refunded) |
| `order_status` | order_status | NOT NULL, DEFAULT 'Pending' | Trạng thái đơn hàng (Pending, Paid, Expired, Cancelled) |
| `webhook_payload` | JSONB | NULL | Toàn bộ dữ liệu callback từ cổng thanh toán (đối soát kiểm toán) |
| `paid_at` | TIMESTAMPTZ | NULL | Thời điểm nhận xác nhận tiền vào tài khoản |
| `starts_at` | TIMESTAMPTZ | NULL | Thời điểm gói bắt đầu có hiệu lực |
| `expires_at` | TIMESTAMPTZ | NULL | Thời điểm gói hết hạn dịch vụ |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm khởi tạo đơn hàng |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật trạng thái |

---

## **5. PHÂN HỆ GAMIFICATION, KHẢO SÁT & NỘI DUNG (GAMIFICATION, CONTENT & ADMIN)**

### **5.1. `UserPointWallet` (Ví điểm xanh Gamification)**
Lưu số dư điểm thưởng (áp dụng cho cả User và Recycler theo **BR-GAM-08**).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `wallet_id` | BIGSERIAL | PRIMARY KEY | Khóa chính ví điểm |
| `user_id` | BIGINT | NOT NULL, UNIQUE, FOREIGN KEY | Tham chiếu `User.user_id` (1 - 1) |
| `current_balance` | BIGINT | NOT NULL, DEFAULT 0 | Số dư điểm hiện tại khả dụng để đổi quà |
| `total_earned` | BIGINT | NOT NULL, DEFAULT 0 | Tổng điểm tích lũy trọn đời (xếp hạng Leaderboard) |
| `total_spent` | BIGINT | NOT NULL, DEFAULT 0 | Tổng điểm đã sử dụng đổi quà/chăm cây |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm biến động số dư gần nhất |

*Ràng buộc toàn vẹn:* `CHECK (current_balance >= 0)` (Số dư không được phép âm - BR-GAM-03).

---

### **5.2. `PointTransactionHistory` (Lịch sử biến động điểm thưởng)**
Tuân thủ tính Idempotent theo **BR-GAM-01**, lưu vết chi tiết từng điểm cộng/trừ.

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `transaction_id` | BIGSERIAL | PRIMARY KEY | Khóa chính giao dịch điểm |
| `wallet_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `UserPointWallet.wallet_id` |
| `user_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` (hỗ trợ truy vấn nhanh lịch sử) |
| `points` | BIGINT | NOT NULL | Số điểm biến động (+ cộng điểm, - trừ điểm) |
| `action_type` | point_action_type| NOT NULL | Loại hành vi (Handover_Reward, Daily_Checkin, Redeem_Gift, Tree_Care...) |
| `source_type` | VARCHAR(50) | NULL | Bảng nguồn phát sinh (Ví dụ: `HandoverReceipt`, `DailyCheckinLog`) |
| `source_id` | BIGINT | NULL | ID của bản ghi nguồn tương ứng |
| `idempotency_key` | VARCHAR(100) | NULL, UNIQUE | Khóa chặn xử lý trùng lặp giao dịch điểm (BR-GAM-01) |
| `description` | TEXT | NULL | Diễn giải lý do biến động điểm hiển thị trên App |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm phát sinh giao dịch điểm |

---

### **5.3. `DailyCheckinLog` (Lịch sử Điểm danh hàng ngày)**
Tuân thủ **BR-GAM-09 & MSG-GAM-08** (tính điểm theo chuỗi ngày liên tục Streak).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `checkin_id` | BIGSERIAL | PRIMARY KEY | Khóa chính lượt điểm danh |
| `user_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` |
| `checkin_date` | DATE | NOT NULL | Ngày điểm danh (Định dạng YYYY-MM-DD theo múi giờ VN) |
| `streak_count` | INTEGER | NOT NULL, DEFAULT 1 | Số ngày điểm danh liên tiếp đạt được (BR-GAM-09) |
| `points_awarded` | BIGINT | NOT NULL, DEFAULT 0 | Số điểm được cộng trong lần điểm danh này |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm bấm điểm danh |

*Ràng buộc toàn vẹn:* `UNIQUE (user_id, checkin_date)` (Mỗi tài khoản chỉ được điểm danh đúng 1 lượt/ngày).

---

### **5.4. `VirtualTreeProgress` (Tiến trình nuôi Cây ảo & Mở khóa kiến thức)**
Tuân thủ **BR-GAM-06 & BR-GAM-08** (giới hạn lượt tưới/ngày, tiến trình tăng 1 chiều).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `progress_id` | BIGSERIAL | PRIMARY KEY | Khóa chính tiến trình cây |
| `user_id` | BIGINT | NOT NULL, UNIQUE, FOREIGN KEY | Tham chiếu `User.user_id` (1 - 1, chỉ dành riêng cho Citizen) |
| `tree_level` | INTEGER | NOT NULL, DEFAULT 1 | Cấp độ hiện tại của cây (1: Mầm non, 2: Cây non, 3: Cây lớn...) |
| `growth_points` | BIGINT | NOT NULL, DEFAULT 0 | Điểm sinh trưởng tích lũy |
| `total_watered` | INTEGER | NOT NULL, DEFAULT 0 | Tổng số lượt đã tưới cây trọn đời |
| `daily_water_count` | INTEGER | NOT NULL, DEFAULT 0 | Số lượt đã tưới trong ngày hôm nay (BR-GAM-06) |
| `last_water_date` | DATE | NULL | Ngày tưới gần nhất (dùng để reset `daily_water_count` về 0 mỗi ngày mới) |
| `last_watered_at` | TIMESTAMPTZ | NULL | Thời điểm tưới cây gần nhất |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật trạng thái cây |

---

### **5.5. `GiftInventory` (Kho Quà tặng & Voucher đổi thưởng)**
Hỗ trợ cả quà vật lý và voucher điện tử theo Use Case **S-10: Quản lý Kho Quà tặng**.

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `gift_id` | BIGSERIAL | PRIMARY KEY | Khóa chính phần quà |
| `gift_name` | VARCHAR(150) | NOT NULL | Tên phần thưởng hiển thị |
| `gift_type` | gift_type | NOT NULL, DEFAULT 'Physical' | Loại quà (Physical: Quà hiện vật, Voucher: Mã giảm giá) |
| `description` | TEXT | NULL | Mô tả chi tiết điều kiện áp dụng |
| `image_url` | TEXT | NULL | Ảnh minh họa quà tặng |
| `points_required` | BIGINT | NOT NULL | Số điểm xanh cần thiết để quy đổi |
| `stock_quantity` | INTEGER | NOT NULL, DEFAULT 0 | Số lượng còn lại trong kho (BR-GAM-04) |
| `status` | VARCHAR(20) | NOT NULL, DEFAULT 'ACTIVE' | Trạng thái sẵn sàng đổi (ACTIVE, INACTIVE) |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm thêm quà vào kho |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật tồn kho |

*Ràng buộc toàn vẹn:* `CHECK (stock_quantity >= 0)`.

---

### **5.6. `GiftRedemption` (Lịch sử Đổi thưởng & Cấp Voucher)**
Tuân thủ **BR-GAM-03, BR-GAM-04 & BR-GAM-05** (trừ tồn kho và cấp mã voucher nguyên tử).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `redemption_id` | BIGSERIAL | PRIMARY KEY | Khóa chính lượt đổi quà |
| `user_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` người đổi |
| `gift_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `GiftInventory.gift_id` |
| `points_spent` | BIGINT | NOT NULL | Số điểm đã bị khấu trừ |
| `quantity` | INTEGER | NOT NULL, DEFAULT 1 | Số lượng quà đổi |
| `redemption_code` | VARCHAR(50) | NOT NULL, UNIQUE | Mã đối soát phiếu nhận quà hệ thống |
| `voucher_code` | VARCHAR(100) | NULL | Mã kích hoạt ưu đãi (nếu quà là Voucher điện tử) |
| `expires_at` | TIMESTAMPTZ | NULL | Thời hạn sử dụng của voucher (BR-GAM-05) |
| `status` | redemption_status| NOT NULL, DEFAULT 'Pending' | Trạng thái (Pending, Redeemed, Delivered, Expired, Cancelled) |
| `delivery_address` | TEXT | NULL | Địa chỉ nhận hàng (nếu quà là hiện vật vật lý) |
| `redeemed_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm thực hiện đổi điểm |
| `fulfilled_at` | TIMESTAMPTZ | NULL | Thời điểm Staff bàn giao quà thành công |

---

### **5.7. `EcoFact` (Kiến thức Môi trường mở khóa theo Cây)**
Tuân thủ **BR-GAM-06 & UC U-14, S-09** (mở khóa kiến thức tương ứng theo Cấp độ Cây ảo).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `fact_id` | BIGSERIAL | PRIMARY KEY | Khóa chính mẩu tin kiến thức |
| `title` | VARCHAR(200) | NOT NULL | Tiêu đề sự thật môi trường |
| `content` | TEXT | NOT NULL | Nội dung giáo dục ý thức phân loại & tái chế |
| `source_url` | TEXT | NULL | Đường dẫn tài liệu nguồn dẫn chứng |
| `image_url` | TEXT | NULL | Ảnh minh họa kiến thức |
| `unlock_tree_level`| INTEGER | NOT NULL, DEFAULT 1 | Cấp độ Cây ảo cần đạt để mở khóa đọc Fact này |
| `status` | VARCHAR(20) | NOT NULL, DEFAULT 'ACTIVE' | Trạng thái hiển thị |
| `created_by_staff_id`| BIGINT | NULL, FOREIGN KEY | Tham chiếu `StaffProfile.staff_id` người soạn thảo |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật |

---

### **5.8. `Survey` (Khảo sát Định kỳ Khách hàng - Bổ sung mới)**
Hỗ trợ quy tắc **BR-RVW-04 & BR-RVW-05** và **MSG-RVW-04** (Staff cấu hình khảo sát chất lượng dịch vụ định kỳ).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `survey_id` | BIGSERIAL | PRIMARY KEY | Khóa chính đợt khảo sát |
| `title` | VARCHAR(200) | NOT NULL | Tiêu đề cuộc khảo sát |
| `description` | TEXT | NULL | Mục đích và nội dung khảo sát |
| `target_role` | target_role | NOT NULL, DEFAULT 'All' | Đối tượng nhận khảo sát (User, Recycler, All) |
| `status` | survey_status | NOT NULL, DEFAULT 'Draft' | Trạng thái (Draft, Active, Closed) |
| `start_date` | TIMESTAMPTZ | NOT NULL | Thời điểm bắt đầu mở nhận phản hồi |
| `end_date` | TIMESTAMPTZ | NOT NULL | Thời điểm đóng khảo sát |
| `created_by_staff_id`| BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `StaffProfile.staff_id` người tạo đợt khảo sát |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật |

---

### **5.9. `SurveyQuestion` (Câu hỏi trong bảng khảo sát - Bổ sung mới)**

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `question_id` | BIGSERIAL | PRIMARY KEY | Khóa chính câu hỏi |
| `survey_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `Survey.survey_id` |
| `question_text` | TEXT | NOT NULL | Nội dung câu hỏi khảo sát |
| `question_type` | question_type | NOT NULL | Loại câu hỏi (Rating_1_5, Multiple_Choice, Text) |
| `options` | JSONB | NULL | Danh sách các phương án lựa chọn (nếu là trắc nghiệm) |
| `order_index` | INTEGER | NOT NULL, DEFAULT 0 | Thứ tự hiển thị câu hỏi |
| `is_required` | BOOLEAN | NOT NULL, DEFAULT TRUE | Bắt buộc trả lời hay không |

---

### **5.10. `SurveyResponse` (Câu trả lời khảo sát - Bổ sung mới)**
Tuân thủ **BR-RVW-05** (kết quả khảo sát tổng hợp ẩn danh, mỗi người dùng trả lời 1 lần).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `response_id` | BIGSERIAL | PRIMARY KEY | Khóa chính phản hồi khảo sát |
| `survey_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `Survey.survey_id` |
| `user_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` người tham gia |
| `answers` | JSONB | NOT NULL | Toàn bộ câu trả lời dạng JSON cấu trúc theo mã câu hỏi |
| `submitted_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm nộp bài khảo sát |

*Ràng buộc toàn vẹn:* `UNIQUE (survey_id, user_id)` (Mỗi người chỉ được nộp bài 1 lần cho mỗi đợt khảo sát).

---

### **5.11. `BlogPost` (Bài viết Kiến thức Tuyên truyền & Tái chế)**
Tuân thủ **BR-ADM-08, UC S-08 & A-09** (Staff soạn thảo dạng Draft, Admin kiểm duyệt Approved trước khi công khai).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `post_id` | BIGSERIAL | PRIMARY KEY | Khóa chính bài viết |
| `author_staff_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `StaffProfile.staff_id` nhân viên biên soạn |
| `title` | VARCHAR(250) | NOT NULL | Tiêu đề bài viết |
| `slug` | VARCHAR(280) | NOT NULL, UNIQUE | Chuỗi định danh thân thiện URL (SEO Friendly) |
| `summary` | TEXT | NULL | Tóm tắt ngắn nội dung bài viết |
| `content` | TEXT | NOT NULL | Nội dung chi tiết bài viết (Markdown / HTML) |
| `cover_image_url` | TEXT | NULL | Ảnh bìa bài viết trên R2 |
| `status` | post_status | NOT NULL, DEFAULT 'Draft' | Trạng thái (Draft, Pending_Approval, Approved, Rejected) |
| `approved_by_admin_id`| BIGINT | NULL, FOREIGN KEY | Tham chiếu `User.user_id` (Admin duyệt bài) |
| `rejection_reason` | TEXT | NULL | Lý do Admin từ chối duyệt bài |
| `approved_at` | TIMESTAMPTZ | NULL | Thời điểm phê duyệt |
| `published_at` | TIMESTAMPTZ | NULL | Thời điểm chính thức công khai cho cộng đồng xem |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm khởi tạo |
| `updated_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm cập nhật nội dung |

---

### **5.12. `Notification` (Thông báo Đa kênh In-app & Push FCM)**

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `notification_id`| BIGSERIAL | PRIMARY KEY | Khóa chính thông báo |
| `user_id` | BIGINT | NOT NULL, FOREIGN KEY | Tham chiếu `User.user_id` người nhận |
| `title` | VARCHAR(200) | NOT NULL | Tiêu đề thông báo |
| `message` | TEXT | NOT NULL | Nội dung chi tiết thông báo |
| `notification_type`| VARCHAR(50)| NOT NULL | Phân loại (`TRANSACTION`, `AI_SCAN`, `GAMIFICATION`, `SYSTEM`, `COMPLAINT`) |
| `reference_type` | VARCHAR(50) | NULL | Bảng liên kết (`HandoverRequest`, `TransactionComplaint`, `ServiceOrder`) |
| `reference_id` | BIGINT | NULL | ID của bản ghi liên kết tương ứng |
| `is_read` | BOOLEAN | NOT NULL, DEFAULT FALSE | Trạng thái đã xem hay chưa |
| `read_at` | TIMESTAMPTZ | NULL | Thời điểm bấm xem thông báo |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm tạo thông báo |

*Khuyến nghị Partitioning:* Range Partitioning theo cột `created_at` khi số lượng thông báo tăng cao theo thời gian.

---

### **5.13. `AuditLog` (Nhật ký Kiểm toán Hệ thống Bất biến)**
Tuân thủ **BR-ADM-05 & BR-ADM-06** (ghi nhận dấu vết kiểm toán bất biến các thao tác nhạy cảm của Admin và Staff).

| Cột | Kiểu dữ liệu | Ràng buộc | Diễn giải / Nghiệp vụ |
| :--- | :--- | :--- | :--- |
| `audit_id` | BIGSERIAL | PRIMARY KEY | Khóa chính bản ghi kiểm toán |
| `actor_user_id` | BIGINT | NULL, FOREIGN KEY | Tham chiếu `User.user_id` người thực hiện hành động |
| `action` | VARCHAR(100) | NOT NULL | Tên thao tác (Ví dụ: `LOCK_ACCOUNT`, `VERIFY_RECYCLER`, `RESOLVE_COMPLAINT`, `EXPORT_REPORT`) |
| `entity_type` | VARCHAR(100) | NULL | Tên đối tượng bị tác động (`User`, `RecyclerProfile`, `MasterWasteCategory`) |
| `entity_id` | VARCHAR(100) | NULL | ID của bản ghi bị tác động |
| `old_values` | JSONB | NULL | Giá trị dữ liệu trước khi chỉnh sửa |
| `new_values` | JSONB | NULL | Giá trị dữ liệu sau khi chỉnh sửa |
| `ip_address` | INET | NULL | Địa chỉ IP máy khách gửi yêu cầu (BR-ADM-05) |
| `user_agent` | TEXT | NULL | Thông tin trình duyệt/thiết bị thao tác |
| `created_at` | TIMESTAMPTZ | NOT NULL, DEFAULT NOW() | Thời điểm diễn ra sự kiện (Bất biến, không thể sửa/xóa) |

---

## **6. TỔNG HỢP DANH SÁCH BẢNG & CHIẾN LƯỢC TỐI ƯU TRUY VẤN (INDEXING STRATEGY)**

### **6.1. Bảng đối chiếu 28 Bảng dữ liệu theo Phân hệ**

| STT | Tên bảng | Phân hệ chính | Quan hệ / Vai trò |
| :---: | :--- | :--- | :--- |
| 1 | `User` | Accounts & Auth | Bảng người dùng cốt lõi (Citizen, Recycler, Staff, Admin) |
| 2 | `RecyclerProfile` | Accounts & Auth | Hồ sơ cơ sở thu mua (Shared PK 1 - 1 với User, kèm PostGIS) |
| 3 | `StaffProfile` | Accounts & Auth | Hồ sơ nhân viên vận hành (Shared PK 1 - 1 với User) |
| 4 | `StaffPermissionAssignment` | Accounts & Auth | Quản lý phân quyền chi tiết cho Staff (A-04, BR-ADM-02) |
| 5 | `PasswordResetToken` | Accounts & Auth | Token đặt lại mật khẩu an toàn qua Email (BR-AUTH-06) |
| 6 | `MasterWasteCategory` | AI & Waste | Danh mục rác thải chuẩn của hệ thống |
| 7 | `SortingGuidanceRule` | AI & Waste | Quy tắc hướng dẫn phân loại rác (1 - 1 với Category) |
| 8 | `AIScanHistory` | AI & Waste | Lịch sử nhận diện hình ảnh của AI Scanner |
| 9 | `AIScanDetection` | AI & Waste | Tọa độ Bounding Box đa vật thể rác thải (YOLOv8) |
| 10 | `MisclassifiedReport` | AI & Waste | Báo cáo AI phân loại sai từ người dùng (BR-CLS-05) |
| 11 | `VerifiedDataset` | AI & Waste | Tập dữ liệu ảnh kiểm chứng tái huấn luyện mô hình |
| 12 | `RecyclerPriceList` | Collection & Trade | Bảng giá thu mua phế liệu của từng vựa ve chai |
| 13 | `HandoverRequest` | Collection & Trade | Đơn hẹn bàn giao rác tích hợp chốt khung giờ (BR-NEG-04/05) |
| 14 | `HandoverRequestItem` | Collection & Trade | Chi tiết các loại phế liệu trong một đơn thu gom |
| 15 | `ChatMessage` | Collection & Trade | Tin nhắn thương lượng giá & lịch hẹn trực tiếp (SignalR) |
| 16 | `HandoverReceipt` | Collection & Trade | Biên lai hoàn tất bàn giao & làm bằng chứng thanh toán |
| 17 | `TransactionReview` | Collection & Trade | Đánh giá 2 chiều có kiểm duyệt vi phạm (BR-RVW-01 đến 03) |
| 18 | `TransactionComplaint` | Collection & Trade | Xử lý khiếu nại và phán quyết tranh chấp của Staff |
| 19 | `ServicePackage` | Service & Payment | Các gói dịch vụ nội bộ (Tắt quảng cáo, Huy hiệu xác minh) |
| 20 | `ServiceOrder` | Service & Payment | Đơn thanh toán dịch vụ qua mã VietQR động & PayOS |
| 21 | `UserPointWallet` | Gamification | Ví lưu trữ số dư Điểm xanh (Cả Citizen và Recycler) |
| 22 | `PointTransactionHistory`| Gamification | Nhật ký biến động điểm thưởng đảm bảo Idempotency |
| 23 | `DailyCheckinLog` | Gamification | Điểm danh hàng ngày tính chuỗi Streak liên tục (BR-GAM-09) |
| 24 | `VirtualTreeProgress` | Gamification | Tiến trình chăm sóc Cây ảo & giới hạn lượt tưới/ngày |
| 25 | `GiftInventory` | Gamification | Kho quà tặng vật lý và voucher điện tử |
| 26 | `GiftRedemption` | Gamification | Lịch sử đổi quà và cấp mã ưu đãi |
| 27 | `EcoFact` | Gamification | Mẩu tin môi trường mở khóa theo Cấp độ Cây ảo |
| 28 | `Survey` | Survey & Feedback | Đợt khảo sát định kỳ chất lượng dịch vụ (BR-RVW-04/05) |
| 29 | `SurveyQuestion` | Survey & Feedback | Danh sách câu hỏi khảo sát đa dạng định dạng |
| 30 | `SurveyResponse` | Survey & Feedback | Kết quả trả lời khảo sát phục vụ thống kê ẩn danh |
| 31 | `BlogPost` | Content & Marketing | Bài viết blog tuyên truyền do Staff viết và Admin duyệt |
| 32 | `Notification` | System & Realtime | Thông báo đẩy đa kênh (Web In-app, Firebase FCM) |
| 33 | `AuditLog` | System Administration| Nhật ký kiểm toán an ninh bất biến ghi vết thao tác nhạy cảm |

---

### **6.2. Chiến lược Đánh Chỉ mục (Index Design)**

```sql
-- 1. PostGIS Spatial GIST Index (Tăng tốc tìm kiếm điểm thu gom lân cận)
CREATE INDEX idx_recycler_geom ON RecyclerProfile USING GIST (geom);
CREATE INDEX idx_handover_geom ON HandoverRequest USING GIST (geom);

-- 2. Chỉ mục tối ưu hóa cho Giao dịch & Bàn giao (Handover & Trade)
CREATE INDEX idx_handover_user_status ON HandoverRequest (user_id, status, created_at DESC);
CREATE INDEX idx_handover_recycler_status ON HandoverRequest (recycler_id, status, created_at DESC);
CREATE INDEX idx_handover_items_request ON HandoverRequestItem (request_id);

-- 3. Chỉ mục tối ưu hóa Chat thời gian thực
CREATE INDEX idx_chat_request_sent ON ChatMessage (request_id, sent_at ASC);

-- 4. Chỉ mục tối ưu hóa Gamification & Điểm thưởng
CREATE INDEX idx_point_history_user ON PointTransactionHistory (user_id, created_at DESC);
CREATE INDEX idx_checkin_user_date ON DailyCheckinLog (user_id, checkin_date);

-- 5. Chỉ mục tối ưu hóa Quét ảnh AI & Phản hồi
CREATE INDEX idx_ai_scan_user ON AIScanHistory (user_id, created_at DESC);
CREATE INDEX idx_ai_detection_scan ON AIScanDetection (scan_id);

-- 6. Chỉ mục tối ưu hóa Đơn hàng VietQR & Thông báo
CREATE INDEX idx_service_order_code ON ServiceOrder (order_code);
CREATE INDEX idx_notification_user_unread ON Notification (user_id, is_read, created_at DESC);
CREATE INDEX idx_audit_created ON AuditLog (created_at DESC);
```
