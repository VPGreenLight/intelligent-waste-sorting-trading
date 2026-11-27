# REPORT 5.3 – SYSTEM TEST: MODULE 04: RECYCLER PROFILE, PRICING SHEET & AVAILABILITY SCHEDULE MANAGEMENT (R-01, R-02)

## 1. Workflow Summary Header (Theo mẫu Sheet `M04_Recycler` – `SEP490_G141`)

| Workflow | `Module 04: Recycler Profile, Pricing Sheet & Availability Schedule Management (R-01, R-02)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử End-to-End cổng quản lý đối tác Recycler (/recycler/profile, /recycler/catalog): cập nhật thông tin & tọa độ GPS thực tế, quản trị đơn giá / lưu hàng loạt bảng giá, chặn tắt dòng giá cuối cùng khi đang Online (BR-MTH-03), bật/tắt trạng thái Online/Offline và thiết lập lịch mở cửa 7 ngày. | | | |
| **Number of TCs** | **10** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 10 | 0 | 0 | 0 |
| *Round 2* | 10 | 0 | 0 | 0 |
| *Round 3* | 0 | 0 | 10 | 0 |

---

## 2. System Test Case Specification (`STC-RCY-001` $\rightarrow$ `STC-RCY-010`)

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Scenario 4.1: Quản lý Hồ sơ Cơ sở & Ghim Vị trí GPS (/recycler/profile)** | | | | | | | | | |
| **STC-RCY-001** | Xem đầy đủ thông tin hồ sơ, loại hình và số loại phế liệu đang mua tại /recycler/profile | 1. Đăng nhập recycler.<br>2. Mở menu 'Hồ sơ cơ sở' (/recycler/profile). | - Hiển thị đúng tên cơ sở ('Vựa phế liệu EcoGreen Hòa Lạc'), loại hình Facility, địa chỉ, tọa độ GPS trên bản đồ, khung giờ và ảnh kho bãi. | Tài khoản recycler đang Active và Verified. | Passed | Passed | Pending | N/A | GET /api/v1/recycler/profile |
| **STC-RCY-002** | Cập nhật địa chỉ, mô tả, giờ mở cửa và ghim vị trí mới trên bản đồ /recycler/profile | 1. Nhấn vào bản đồ chọn tọa độ mới quanh Hòa Lạc, sửa mô tả năng lực thu mua.<br>2. Nhấn 'Lưu thay đổi' và kiểm tra lại trên /map. | - Cập nhật thành công (200 OK), PostGIS Point (SRID 4326) cập nhật theo vị trí mới.<br>- Ghim trên /map di chuyển đúng tọa độ mới. | Đang ở /recycler/profile. | Passed | Passed | Pending | N/A | Update Profile + PostGIS Sync |
| **STC-RCY-003** | Chặn cập nhật hồ sơ với tọa độ gốc (0, 0) hoặc SĐT rỗng | 1. Xóa số điện thoại hoặc nhập tọa độ (0, 0) rồi bấm Lưu. | - Hệ thống chặn với thông báo lỗi (MSG-MTH-06 / FluentValidation), không ghi đè dữ liệu hỏng vào DB. | Đang ở /recycler/profile. | Passed | Passed | Pending | N/A | BR-MTH-03 |
| **STC-RCY-004** | Hồ sơ đang bị Rejected khi tự cập nhật lại chuyển về Pending chờ Staff duyệt (BR-MTH-02) | 1. Dùng tài khoản có hồ sơ Rejected gọi cập nhật hồ sơ.<br>2. Kiểm tra trạng thái hồ sơ. | - VerificationStatus tự động đặt về Pending, xóa RejectionReason cũ để quay lại hàng đợi Staff. | Hồ sơ ở trạng thái Rejected. | Passed | Passed | Pending | N/A | BR-MTH-02 |
| **Scenario 4.2: Quản trị Bảng giá Phế liệu & Lưu Hàng loạt (/recycler/catalog - R-01)** | | | | | | | | | |
| **STC-RCY-005** | Thêm mới một loại phế liệu thương mại vào bảng giá thu mua (/recycler/catalog) | 1. Mở /recycler/catalog.<br>2. Chọn loại rác chưa có (VD: 'Thủy tinh'), nhập đơn giá '2500' đ/kg, tối thiểu '3' kg và điều kiện thu gom.<br>3. Nhấn 'Thêm vào bảng giá'. | - Tạo dòng giá thành công (IsActive=true, IsOutdated=false).<br>- Bảng giá trên /map cập nhật hiển thị thêm mục Thủy tinh. | Cơ sở chưa có dòng giá Thủy tinh. | Passed | Passed | Pending | N/A | POST /api/v1/recycler/prices |
| **STC-RCY-006** | Chặn thêm đơn giá số âm, số thập phân hoặc trùng lặp loại phế liệu | 1. Thử nhập đơn giá 0đ hoặc 2500.5đ.<br>2. Thử thêm trùng danh mục 'Nhựa' đã có. | - Hệ thống từ chối với lỗi đơn giá phải là số nguyên dương VND/kg và cảnh báo danh mục đã tồn tại trong bảng giá. | Đang ở /recycler/catalog. | Passed | Passed | Pending | N/A | Pricing Validation |
| **STC-RCY-007** | Chặn tắt dòng giá Active cuối cùng khi cơ sở đang Online trên bản đồ (BR-MTH-03) | 1. Tắt lần lượt các dòng giá cho tới khi chỉ còn 1 dòng Active duy nhất.<br>2. Thử tắt nốt dòng giá cuối cùng khi trạng thái cơ sở vẫn đang 'Online'. | - Hệ thống chặn và thông báo lỗi (MSG-MTH-06): cơ sở đang mở cửa phải có tối thiểu 1 loại phế liệu nhận mua. | Cơ sở đang Online và còn 1 dòng giá Active. | Passed | Passed | Pending | N/A | EF-03 / BR-MTH-03 |
| **Scenario 4.3: Điều khiển Trạng thái Online/Offline & Lịch Hoạt động Tuần (/recycler - R-02)** | | | | | | | | | |
| **STC-RCY-008** | Bật/tắt công tắc 'Sẵn sàng nhận đơn (Offline/Online)' kèm lý do tạm nghỉ (BusyReason) | 1. Bấm chuyển trạng thái sang 'Tạm ngưng / Offline', nhập lý do 'Xe đi gom hàng ngoại thành' (< 100 ký tự) và Lưu.<br>2. Kiểm tra lại cơ sở trên bản đồ /map. | - Cơ sở ẩn ngay lập tức khỏi kết quả tìm kiếm mới trên /map (BR-MTH-06).<br>- Toàn bộ đơn bàn giao đang xử lý (Pending/Accepted) vẫn giữ nguyên. | Cơ sở đang Online. | Passed | Passed | Pending | N/A | BR-MTH-06 |
| **STC-RCY-009** | Chặn cơ sở bật Online khi bảng giá chưa có mặt hàng nào Active (BR-MTH-03) | 1. Chuẩn bị tài khoản cơ sở vừa duyệt nhưng tắt hết bảng giá (ActivePriceCount = 0).<br>2. Thử bật công tắc sang Online. | - Chặn bật Online và hiển thị thông báo yêu cầu thiết lập ít nhất 1 mặt hàng thu mua & vị trí hợp lệ (MSG-MTH-06). | Cơ sở Verified nhưng ActivePriceCount = 0. | Passed | Passed | Pending | N/A | BR-MTH-03 |
| **STC-RCY-010** | Thiết lập lịch mở cửa 7 ngày trong tuần và tự động tổng hợp chuỗi OperatingHours | 1. Mở mục cấu hình Lịch hoạt động tuần.<br>2. Đặt Thứ 2–Thứ 6: 08:00–17:30; Thứ 7: 08:00–12:00; Chủ Nhật: Đóng cửa.<br>3. Nhấn Lưu. | - Chuỗi OperatingHours tự động gộp thành 'T2-T6: 08:00 - 17:30; T7: 08:00 - 12:00'.<br>- Dữ liệu đồng bộ lên thẻ cơ sở trên /map. | Đã đăng nhập recycler. | Passed | Passed | Pending | N/A | Weekly Schedule Summary |
