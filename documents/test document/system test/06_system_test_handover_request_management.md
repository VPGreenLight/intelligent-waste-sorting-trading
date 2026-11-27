# REPORT 5.3 – SYSTEM TEST: MODULE 06: HANDOVER REQUEST SCHEDULING, PRIVACY MASKING & LIFECYCLE (U-06, U-07, R-03, R-04)

## 1. Workflow Summary Header (Theo mẫu Sheet `M06_Handover` – `SEP490_G141`)

| Workflow | `Module 06: Handover Request Scheduling, Privacy Masking & Lifecycle (U-06, U-07, R-03, R-04)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử End-to-End quy trình Tạo yêu cầu bàn giao (/user/handover), kiểm tra 1–5 ảnh rác, khối lượng tối thiểu, giới hạn 3 đơn active (BR-HND-LIMIT), che SĐT & địa chỉ khi chưa Accept (BR-HND-08), sửa/hủy đơn (U-07, BR-HND-06), Recycler tiếp nhận/từ chối/hủy lịch trên /recycler/requests (R-03, R-04) và JOB-03 tự động hết hạn. | | | |
| **Number of TCs** | **10** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 10 | 0 | 0 | 0 |
| *Round 2* | 10 | 0 | 0 | 0 |
| *Round 3* | 0 | 0 | 10 | 0 |

---

## 2. System Test Case Specification (`STC-HND-001` $\rightarrow$ `STC-HND-010`)

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Scenario 6.1: Tạo Yêu cầu Bàn giao Phế liệu Mới (/user/handover - U-06)** | | | | | | | | | |
| **STC-HND-001** | Tạo yêu cầu bàn giao mới thành công gửi tới Vựa EcoGreen Hòa Lạc (/user/handover) | 1. Đăng nhập citizen (2 đơn active sẵn có) -> mở /user/handover.<br>2. Chọn 'Vựa phế liệu EcoGreen Hòa Lạc', chọn Nhựa (5kg) + Kim loại (3kg), tải 2 ảnh phế liệu, chọn khung giờ hẹn (trong 30 ngày) & nhập địa chỉ thu gom.<br>3. Nhấn 'Gửi yêu cầu bàn giao'. | - Tạo thành công yêu cầu thứ 3 (Status='Pending'), sinh mã HND-yyyyMMdd-NNNN và lưu ảnh chụp giá (PriceSnapshotPerKg).<br>- Hiển thị ngay trong danh sách yêu cầu của tôi. | Citizen đang có < 3 đơn active; Vựa EcoGreen đang Online. | Passed | Passed | Pending | N/A | U-06 Happy Path |
| **STC-HND-002** | Chặn tạo thêm yêu cầu khi Citizen đã đạt ngưỡng tối đa 3 đơn active (BR-HND-LIMIT) | 1. Sau STC-HND-001 (citizen đã đủ 3 đơn active), tiếp tục mở form tạo đơn mới và bấm Gửi. | - Hệ thống chặn với thông báo vượt giới hạn tối đa 3 yêu cầu đang xử lý cùng lúc (BR-HND-LIMIT). | Citizen đang có 3 đơn active (Pending/Negotiating/Accepted/AwaitingConfirmation). | Passed | Passed | Pending | N/A | BR-HND-LIMIT (Boundary = 3) |
| **STC-HND-003** | Chặn đặt khung giờ hẹn ngoài giờ mở cửa, ngày nghỉ tuần của vựa hoặc quá 30 ngày (BR-NEG-04) | 1. Chọn khung giờ hẹn cách hiện tại < 2 giờ, hoặc trễ > 30 ngày, hoặc vào ngày vựa khai báo đóng cửa.<br>2. Bấm gửi. | - Từ chối kèm thông báo khung giờ hẹn không hợp lệ theo lịch tuần của cơ sở (BR-NEG-04). | Vựa đã cấu hình WeeklyScheduleJson. | Passed | Passed | Pending | N/A | BR-NEG-04 |
| **Scenario 6.2: Che giấu Thông tin Liên hệ trước khi Duyệt (BR-HND-08) & Sửa/Hủy bởi Người dân (U-07)** | | | | | | | | | |
| **STC-HND-004** | Che giấu SĐT và địa chỉ cụ thể của Citizen khi Recycler xem đơn Pending (BR-HND-08) | 1. Đăng nhập tài khoản recycler -> mở /recycler/requests.<br>2. Mở chi tiết một đơn đang ở trạng thái Pending. | - Chỉ hiển thị tên cơ bản, danh mục rác, khung giờ hẹn.<br>- Ô Số điện thoại và Địa chỉ cụ thể bị ẩn / hiển thị thông báo 'Mở khóa sau khi Chấp nhận đơn' (IsContactRevealed=false). | Đơn đang ở trạng thái Pending. | Passed | Passed | Pending | N/A | BR-HND-08 Privacy |
| **STC-HND-005** | Citizen chỉnh sửa thông tin đơn khi đang ở trạng thái Pending (U-07) | 1. Đăng nhập citizen -> mở chi tiết đơn Pending.<br>2. Bấm 'Chỉnh sửa', đổi khối lượng dự kiến và địa chỉ thu gom rồi Lưu. | - Cập nhật thành công thông tin đơn (Status giữ nguyên Pending). | Đơn thuộc citizen và đang Pending. | Passed | Passed | Pending | N/A | U-07 Update Pending |
| **STC-HND-006** | Chặn Citizen chỉnh sửa đơn sau khi Recycler đã Chấp nhận (Accepted) | 1. Thử bấm sửa một đơn đang ở trạng thái Accepted (VD: HND-DEMO-0003 của citizen_vip). | - Nút sửa bị ẩn/khóa trên UI; nếu gọi trực tiếp API PUT trả về lỗi BR-HND-02. | Đơn đã chuyển sang Accepted. | Passed | Passed | Pending | N/A | BR-HND-02 Lock Edit |
| **Scenario 6.3: Recycler Tiếp nhận, Từ chối & Hủy Lịch hẹn (/recycler/requests - R-03, R-04)** | | | | | | | | | |
| **STC-HND-007** | Recycler bấm Chấp nhận đơn (Accepted) -> Chốt lịch hẹn và mở khóa SĐT + Địa chỉ người dân | 1. Đăng nhập recycler -> mở /recycler/requests.<br>2. Chọn đơn Pending (HND-DEMO-0001), nhập ghi chú tiếp nhận và nhấn 'Chấp nhận đơn'. | - Trạng thái đơn chuyển Accepted, chốt ConfirmedTimeSlot.<br>- SĐT và địa chỉ thu gom đầy đủ của Citizen hiển thị ngay cho Recycler (IsContactRevealed=true).<br>- Gửi thông báo SignalR cho Citizen. | Đơn đang Pending và khung giờ hẹn chưa trôi qua. | Passed | Passed | Pending | N/A | R-03 Accept + Reveal Contact |
| **STC-HND-008** | Chặn chấp nhận đơn khi khung giờ đề xuất của đơn đã trôi qua trong quá khứ (BR-NEG-04) | 1. Chọn đơn có ProposedTimeSlot < thời điểm hiện tại.<br>2. Nhấn 'Chấp nhận đơn'. | - Hệ thống chặn và yêu cầu hai bên thương lượng lại khung giờ mới qua Chatbox hoặc từ chối đơn. | Đơn Pending có ProposedTimeSlot đã quá hạn. | Passed | Passed | Pending | N/A | Expired Slot Guard |
| **Scenario 6.4: Cô lập Dữ liệu Đối tác (BR-HND-03) & Tự động Hết hạn Đơn quá 24h (JOB-03)** | | | | | | | | | |
| **STC-HND-009** | Chặn Recycler B xem, chấp nhận hoặc từ chối đơn bàn giao gửi cho Recycler A (BR-HND-03) | 1. Đăng nhập tài khoản recycler_pro (B).<br>2. Thử truy cập chi tiết hoặc gọi Accept lên ID đơn thuộc về EcoGreen (A). | - Màn hình /recycler/requests chỉ liệt kê đơn của chính cơ sở đó; gọi trực tiếp trả về 404 NotFound (BR-HND-03). | Đơn thuộc sở hữu của cơ sở khác. | Passed | Passed | Pending | N/A | BR-HND-03 IDOR Protection |
| **STC-HND-010** | JOB-03 tự động chuyển các đơn Pending quá 24h không phản hồi sang Expired, giữ nguyên đơn Accepted | 1. Có đơn Pending tạo quá 24h và đơn Accepted tạo quá 24h.<br>2. Kích hoạt chu kỳ chạy Background Job JOB-03.<br>3. Kiểm tra danh sách trên /user/handover. | - Đơn Pending quá hạn chuyển sang Expired; đơn Accepted vẫn giữ nguyên trạng thái.<br>- Công dân được giải phóng slot đơn active. | Có đơn Pending > 24h. | Passed | Passed | Pending | N/A | JOB-03 Expiration |
