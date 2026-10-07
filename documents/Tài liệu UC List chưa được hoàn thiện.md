# **TÀI LIỆU DANH SÁCH USE CASE (USE CASE LIST) \- HỆ THỐNG ECOLINK**

### **1\. Phân hệ GUEST (Người dùng chưa đăng nhập)**

*Nhóm tính năng dành cho khách truy cập vãng lai chưa có tài khoản hoặc chưa đăng nhập.*

| ID | Tên UC (VI) | Tên UC (EN) | Độ phức tạp | Ghi chú |
| :---- | :---- | :---- | :---- | :---- |
| **G-01** | Xem trang chủ và Giới thiệu | View Homepage | Simple |  |
| **G-02** | Đăng ký tài khoản User | Register as User | Simple |  |
| **G-03** | Đăng ký tài khoản Thu mua (Buyer) | Register as Recycler | Medium | Yêu cầu upload giấy phép/hình ảnh |
| **G-04** | Đăng nhập hệ thống | Login | Simple |  |

### **2\. Phân hệ SYSTEM USER (Dùng chung)**

*Nhóm tính năng kế thừa cho tất cả các Role sau khi đã đăng nhập thành công (User, Recycler, Staff, Admin).*

| ID | Tên UC (VI) | Tên UC (EN) | Độ phức tạp | Ghi chú |
| :---- | :---- | :---- | :---- | :---- |
| **SU-01** | Xem và Cập nhật hồ sơ cá nhân | View & Update Profile | Simple |  |
| **SU-02** | Đổi mật khẩu | Change Password | Simple |  |
| **SU-03** | Xem thông báo hệ thống | View Notifications | Simple |  |
| **SU-04** | Đăng xuất | Logout | Simple |  |

### **3\. Phân hệ USER (Người dùng phổ thông)**

*Nhóm tính năng cốt lõi dành cho người dân phân loại và bán rác.*

| ID | Tên UC (VI) | Tên UC (EN) | Độ phức tạp | Ghi chú |
| :---- | :---- | :---- | :---- | :---- |
| **Nhóm AI & Hướng dẫn phân loại** |  |  |  |  |
| **U-01** | Nhận diện rác qua ảnh AI | Identify Waste via Photo | Medium | Gọi API Model AI |
| **U-02** | Tra cứu hướng dẫn phân loại rác | Get Sorting Guidance | Simple | Ánh xạ từ Rule Engine |
| **U-03** | Tìm kiếm & Xem chi tiết loại rác | Search & View Waste Details | Simple | Tìm thủ công |
| **U-04** | Báo cáo AI nhận diện sai | Report Incorrect AI | Simple | Gửi Correct Label |
| **Nhóm Kết nối & Giao dịch** |  |  |  |  |
| **U-05** | Tìm điểm thu mua theo GPS | Find Nearby Recyclers | Medium | PostGIS matching |
| **U-06** | Tạo yêu cầu bàn giao rác  | Create Handover Request | Medium | *Gắn kèm việc chọn khung giờ (Time slot) mong muốn vào Form tạo yêu cầu.*  |
| **U-07** | Cập nhật / Hủy yêu cầu bàn giao  | Update / Cancel Handover Request  | Simple | Cho phép dời giờ hẹn (nếu Recycler chưa Accept) hoặc Hủy hoàn toàn yêu cầu.  |
| **U-08** | Trao đổi qua Chatbox | Negotiate via Chatbox | Medium | *Dùng để thương lượng lại giá cả hoặc giờ giấc thu gom thực tế.*  |
| **U-09** | Xác nhận Biên nhận bàn giao | Confirm Handover Receipt | Medium | Chốt giao dịch |
| **U-10** | Đánh giá Cơ sở thu mua | Rate Recycler | Simple | Mở khi hoàn tất giao dịch |
| **U-11** | Gửi khiếu nại giao dịch | Submit Complaint | Medium |  |
| **Nhóm Gamification & Tiện ích** |  |  |  |  |
| **U-12** | Điểm danh hàng ngày | Daily Check-in | Simple |  |
| **U-13** | Đổi điểm lấy Quà tặng/Voucher | Redeem Points for Gifts | Medium |  |
| **U-14** | Nâng cấp Cây ảo & Mở Eco Fact | Upgrade Virtual Tree | Medium | Mở khóa kiến thức |
| **Nhóm Authentication** |  |  |  |  |
| **U-15** | Quên / Đặt lại mật khẩu | Reset Password | Medium | Gửi OTP/Link qua Email cho User |

### **4\. Phân hệ RECYCLER / BUYER (Đối tác thu mua)**

*Nhóm tính năng dành cho vựa ve chai, cơ sở tái chế.*

| ID | Tên UC (VI) | Tên UC (EN) | Độ phức tạp | Ghi chú |
| :---- | :---- | :---- | :---- | :---- |
| **R-01** | Cập nhật Danh mục & Bảng giá | Manage Accepted Waste & Pricing | Medium | Thiết lập điều kiện mua |
| **R-02** | Bật/Tắt trạng thái hoạt động | Toggle Active Status | Simple | Hiển thị/Ẩn trên bản đồ GPS |
| **R-03** | Tiếp nhận/Từ chối yêu cầu | Accept/Reject Handover Request | Medium | *Nếu Accept đồng nghĩa với việc chốt khung giờ thu gom User đề xuất.*  |
| **R-04** | Hủy lịch hẹn thu gom | Confirm/Cancel Collection | Simple | *Recycler hủy đi thu gom giữa chừng (kèm lý do).*  |
| **R-05** | Trao đổi qua Chatbox | Negotiate via Chatbox | Medium | *Dùng để thương lượng lại giờ giấc hoặc giá cả nếu không khớp lịch.*  |
| **R-06** | Tạo Biên nhận bàn giao  | Create Handover Receipt | Medium | Nhập khối lượng thực & Ảnh xác nhận bàn giao. |
| **Nhóm Authentication** |  |  |  |  |
| **R-07** | Quên / Đặt lại mật khẩu | Reset Password | Medium | Gửi OTP/Link xác thực qua Email đăng ký của Cơ sở thu mua |

### **5\. Phân hệ STAFF (Nhân viên vận hành)**

*Nhóm tính năng dành cho đội ngũ Back-office xử lý dữ liệu và hỗ trợ.*

| ID | Tên UC (VI) | Tên UC (EN) | Độ phức tạp | Ghi chú |
| :---- | :---- | :---- | :---- | :---- |
| **Nhóm Vận hành Dữ liệu AI** |  |  |  |  |
| **S-01** | Đánh giá ảnh AI nhận diện sai | Review Misclassified Images | Medium |  |
| **S-02** | Gán nhãn dữ liệu ảnh | Label Image Data | Simple | \<\<extend\>\> từ S-01 |
| **S-03** | Cập nhật Tập dữ liệu kiểm chứng | Update Verified Dataset | Medium |  |
| **Nhóm Vận hành & Nội dung** |  |  |  |  |
| **S-04** | Thẩm định hồ sơ Đối tác thu mua | Verify Recycler Application | Medium |  |
| **S-05** | Xử lý khiếu nại giao dịch | Handle Complaints | Medium |  |
| **S-06** | Xem danh sách & Chi tiết User/Buyer | View Users/Buyers Accounts | Simple | (Bỏ trường Status theo MoM-14) |
| **S-07** | Khóa/Xóa tài khoản User/Buyer | Lock/Delete Accounts | Simple | Xử lý vi phạm |
| **S-08** | Soạn thảo bài đăng Blog | Create Blog Post | Medium | Chờ Admin duyệt |
| **S-09** | Quản lý Nội dung Eco Fact | Manage Eco Fact Content | Medium | Thêm/Sửa/Xóa kiến thức |
| **S-10** | Quản lý Kho Quà tặng | Manage Gift Inventory | Medium | Thêm/Sửa/Xóa quà vật lý/Voucher |
| **S-11** | Điểm danh nhân viên | Staff Daily Check-in | Simple |  |

### **6\. Phân hệ ADMIN (Quản trị viên cấp cao)**

*Nhóm tính năng cấu hình hệ thống, phân quyền và thống kê.*

| ID | Tên UC (VI) | Tên UC (EN) | Độ phức tạp | Ghi chú |
| :---- | :---- | :---- | :---- | :---- |
| **Nhóm Thống kê & Quản trị Hệ thống** |  |  |  |  |
| **A-01** | Xem Dashboard Thống kê | View Dashboard & Analytics | Medium |  |
| **A-02** | Xuất báo cáo dữ liệu | Export Data Reports | Simple | CSV/Excel |
| **A-03** | Quản lý Tài khoản Staff | Manage Staff Accounts | Medium | Thêm/Sửa/Xóa nhân sự |
| **A-04** | Phân quyền Staff | Manage Staff Permissions | Medium | Cấp/Thu hồi quyền |
| **Nhóm Cấu hình Nghiệp vụ** |  |  |  |  |
| **A-05** | Quản lý Danh mục rác hệ thống | Manage Master Waste Categories | Medium | Thêm/Sửa/Xóa (CRUD) |
| **A-06** | Thiết lập Chính sách phân loại | Configure Sorting Policy (Rule Engine) | Medium | Cập nhật hướng dẫn xử lý rác |
| **A-07** | Quản lý Cấu hình Gamification | Manage Gamification Config | Medium | Thiết lập tỷ lệ điểm, mốc level Cây |
| **A-08** | Cấu hình Chu kỳ Bảng xếp hạng | Configure Leaderboard Cycles | Simple | Tuần/Tháng/Quý |
| **A-09** | Duyệt / Hủy bài đăng Blog | Approve / Reject Blog Post | Simple | Kiểm duyệt từ Staff |
| **A-10** | Cấu hình Phí giao dịch | Configure Transaction Fee | Simple | Cài đặt phí thanh toán VietQR cho các dịch vụ nâng cấp (loại bỏ quảng cáo, mua tích xanh,...) |
| **A-11** | Quản lý Đối soát & Dòng tiền | Manage Financial Reconciliation | Medium | Theo dõi luồng thanh toán dịch vụ hệ thống qua VietQR và đối soát doanh thu. |

**Tổng hợp số lượng UC:**

* Tổng số lượng UC thực tế: **46 UCs**  
* Phân loại chi tiết: **22 Simple** (chức năng cơ bản, CRUD, tra cứu), **23 Medium** (giao dịch, AI, GPS, tích hợp API, đặt lại mật khẩu).

#### **1\. Group Phân hệ ADMIN (Cấu hình & Quản trị Master Data)**

* **UC Gốc:** A-05: Quản lý Danh mục rác hệ thống (Manage Master Waste Categories)  
  * $\rightarrow$ **Tách thành các UC con:**  
    * A-05a: Tạo danh mục rác mới (Create Waste Category) — *Simple*  
    * A-05b: Xem danh sách & Chi tiết danh mục rác (View Waste Categories & Details) — *Simple*  
    * A-05c: Cập nhật danh mục rác (Update Waste Category) — *Simple*  
    * A-05d: Ngừng sử dụng / Ẩn danh mục rác (Deprecate / Deactivate Waste Category) — *Simple*  
* **UC Gốc:** A-06: Thiết lập Chính sách phân loại (Configure Sorting Policy / Rule Engine)  
  * $\rightarrow$ **Tách thành các UC con:**  
    * A-06a: Thêm quy tắc hướng dẫn xử lý rác (Create Sorting Guidance Rule) — *Simple*  
    * A-06b: Tra cứu / Xem nội dung dquy tắc (View Sorting Guidance Rules) — *Simple*  
    * A-06c: Cập nhật quy tắc hướng dẫn (Update Sorting Guidance Rule) — *Simple*  
    * A-06d: Gỡ bỏ / Lưu trữ quy tắc (Retire Sorting Guidance Rule) — *Simple*  
* **UC Gốc:** A-07: Quản lý Cấu hình Gamification (Manage Gamification Config)  
  * $\rightarrow$ **Tách thành các UC con:**  
    * A-07a: Cấu hình tỷ lệ quy đổi điểm phế liệu (Configure Point Conversion Rates) — *Simple*  
    * A-07b: Cấu hình hạn mức điểm nhận/ngày (Configure Point Limits & Anti-Fraud Caps) — *Simple*  
    * A-07c: Cấu hình ngưỡng điểm tăng cấp Cây ảo (Configure Level Progression Thresholds) — *Simple*  
    * A-07d: Cấu hình Metadata & Biểu tượng cấp độ Cây (Manage Tree Level Metadata & Badges) — *Simple*  
* **UC Gốc:** A-03: Quản lý Tài khoản Staff (Manage Staff Accounts) & A-04: Phân quyền Staff  
  * $\rightarrow$ **Tách thành các UC con:**  
    * A-03a: Tạo tài khoản Staff (Create Staff Account) — *Simple*  
    * A-03b: Cập nhật thông tin Staff (Update Staff Account Info) — *Simple*  
    * A-03c: Khóa / Xóa tài khoản Staff (Lock / Delete Staff Account) — *Simple*  
    * A-04a: Cấp / Thu hồi quyền hạn Staff (Assign / Revoke Staff Permissions) — *Medium*

#### **2\. Group Phân hệ STAFF (Vận hành & Quản lý Nội dung)**

* **UC Gốc:** S-10: Quản lý Kho Quà tặng (Manage Gift Inventory)  
  * $\rightarrow$ **Tách thành các UC con:**  
    * S-10a: Thêm quà tặng / Mã Voucher mới (Create Gift / Voucher) — *Simple*  
    * S-10b: Xem danh sách & Số lượng tồn kho quà (View Gift Details & Stock) — *Simple*  
    * S-10c: Cập nhật thông tin quà tặng (Update Gift Info) — *Simple*  
    * S-10d: Xóa / Ngừng áp dụng quà tặng (Delete / Deactivate Gift) — *Simple*  
* **UC Gốc:** S-09: Quản lý Nội dung Eco Fact (Manage Eco Fact Content)  
  * $\rightarrow$ **Tách thành các UC con:**  
    * S-09a: Tạo bài viết kiến thức Eco Fact (Create Eco Fact Content) — *Simple*  
    * S-09b: Xem danh sách & Chi tiết Eco Fact (View Eco Fact Content) — *Simple*  
    * S-09c: Cập nhật bài viết Eco Fact (Update Eco Fact Content) — *Simple*  
    * S-09d: Xóa bài viết Eco Fact (Delete Eco Fact Content) — *Simple*  
    * S-09e: Cấu hình quy tắc mở khóa Eco Fact (Configure Knowledge Unlock Rules) — *Medium*  
* **UC Gốc:** S-06 & S-07: Quản lý Tài khoản User / Buyer (Manage User & Buyer Accounts)  
  * $\rightarrow$ **Tách thành các UC con:**  
    * S-06a: Xem chi tiết thông tin & Lịch sử User/Buyer (View User/Buyer Account Details) — *Simple*  
    * S-07a: Khóa / Mở khóa tài khoản User/Buyer kèm lý do (Lock / Unlock Account) — *Simple*  
    * S-07b: Vô hiệu hóa tài khoản vi phạm (Deactivate Account) — *Simple*  
* **UC Gốc:** S-08 (Staff) & A-09 (Admin): Quản lý Bài viết Blog (Manage Blog Posts)  
  * $\rightarrow$ **Tách thành các UC con theo quy trình kiểm duyệt (MOM-14):**  
    * S-08a: Soạn thảo bài đăng Blog (Create Blog Post Draft) — *Medium*  
    * S-08b: Xem danh sách & Nội dung bài Blog (View Blog Details) — *Simple*  
    * A-09a: Phê duyệt xuất bản bài Blog (Approve Blog Post) — *Simple*  
    * A-09b: Từ chối / Hủy bài Blog (Reject / Cancel Blog Post) — *Simple*

#### **3\. Group Phân hệ RECYCLER / BUYER (Đối tác thu mua)**

* **UC Gốc:** R-01: Cập nhật Danh mục & Bảng giá thu mua (Manage Accepted Waste & Pricing)  
  * $\rightarrow$ **Tách thành các UC con:**  
    * R-01a: Thiết lập loại rác thu mua & Đơn giá/kg (Set Accepted Waste Category & Unit Price) — *Medium*  
    * R-01b: Cập nhật / Ngừng thu mua loại rác (Update / Deactivate Accepted Category) — *Simple*  
    * R-01c: Thiết lập điều kiện & Số lượng thu gom tối thiểu (Set Acceptance Conditions) — *Simple*

### **Bảng tổng hợp ánh xạ Tách UC trong Báo cáo SRS (Report 3\)**

| UC Macro / Tổng hợp (Cũ) | Phân rã thành các UC Nguyên tử (Mới) | Role thực hiện | Mức độ phức tạp |
| :---- | :---- | :---- | :---- |
| **Manage Master Waste Categories** | Create Waste Category / View Categories / Update Category / Deprecate Category | Admin | Simple |
| **Configure Sorting Policy** | Create Rule / View Rules / Update Rule / Retire Rule | Admin | Simple |
| **Manage Gamification Config** | Config Rates / Config Point Caps / Config Tree Thresholds / Manage Badges | Admin | Simple |
| **Manage Staff Accounts** | Create Staff / Update Staff Info / Lock Staff Account / Assign Permission | Admin | Simple / Medium |
| **Manage Gift Inventory** | Create Gift / View Gift Stock / Update Gift / Deactivate Gift | Staff | Simple |
| **Manage Eco Fact Content** | Create Fact / View Fact / Update Fact / Delete Fact / Config Unlock Rules | Staff | Simple / Medium |
| **Manage User/Buyer Accounts** | View Account Details / Lock or Unlock Account / Deactivate Account | Staff | Simple |
| **Manage Blog Posts** | Create Blog Draft / View Blog / Approve Blog / Reject Blog | Staff & Admin | Simple / Medium |
| **Manage Accepted Waste & Pricing** | Set Category & Price / Update Category / Set Minimum Acceptance Conditions | Recycler | Simple / Medium |

##### 

| Primary Actor(s): |  | Secondary Actor(s): |  |
| :---- | :---- | :---- | :---- |
| **Description:** |  |  |  |
| **Preconditions:** |  |  |  |
| **Postconditions:** |  |  |  |
| **Normal Flow:** |  |  |  |
| **Alternative Flow:** |  |  |  |
| **Extension Flow:** |  |  |  |
| **Exceptions Flow:** |  |  |  |
| **Business Rule** |  |  |  |

