# TÀI LIỆU PHÂN TÍCH VÀ ĐẶC TẢ BIỂU ĐỒ TUẦN TỰ (SEQUENCE DIAGRAMS)
## MODULE: HANDOVER REQUEST MANAGEMENT (`HandoverRequestService`)

---

### 1. Tổng quan Phân hệ (Module Overview)
Module **Handover Request Management** chịu trách nhiệm quản lý vòng đời yêu cầu đặt lịch hẹn và bàn giao phế liệu giữa **Người dân (Citizen)** và **Cơ sở thu gom (Recycler)**. Module hiện thực các Use Case cốt lõi:
- **`U-06`:** Người dân tạo yêu cầu bàn giao phế liệu (`Create Handover Request`).
- **`U-07`:** Người dân chỉnh sửa hoặc hủy yêu cầu bàn giao (`Update / Cancel Handover Request`).
- **`R-03`:** Cơ sở thu gom tiếp nhận hoặc từ chối yêu cầu (`Accept / Reject Handover Request`).
- **`R-04`:** Cơ sở thu gom hủy lịch hẹn đã chốt thu gom (`Cancel Collection Appointment`).
- **`JOB-03`:** Tiến trình nền tự động chuyển trạng thái đơn chờ quá hạn 24h sang `Expired` (`Auto-expire Pending Requests`).

---

### 2. Danh mục Biểu đồ Tuần tự (Diagram Index)

| STT | File Biểu đồ Mermaid | Tên Use Case / Chức năng | Các Actor & Thành phần Tham gia |
| :---: | :--- | :--- | :--- |
| **01** | [`01_u06_create_handover_request.mmd`](file:///d:/Project/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20handover%20request%20management/01_u06_create_handover_request.mmd) | **UC U-06:** Tạo yêu cầu bàn giao phế liệu | `Citizen`, `HandoverRequestController`, `HandoverRequestService`, `ApplicationDbContext` |
| **02** | [`02_u07_update_and_cancel_handover_request.mmd`](file:///d:/Project/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20handover%20request%20management/02_u07_update_and_cancel_handover_request.mmd) | **UC U-07:** Chỉnh sửa & Hủy yêu cầu phía người dân | `Citizen`, `HandoverRequestController`, `HandoverRequestService`, `ApplicationDbContext` |
| **03** | [`03_r03_accept_and_reject_handover_request.mmd`](file:///d:/Project/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20handover%20request%20management/03_r03_accept_and_reject_handover_request.mmd) | **UC R-03:** Cơ sở tiếp nhận / Từ chối yêu cầu | `Recycler`, `RecyclerHandoverController`, `HandoverRequestService`, `ApplicationDbContext` |
| **04** | [`04_r04_cancel_collection_appointment_and_job03_expiration.mmd`](file:///d:/Project/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20handover%20request%20management/04_r04_cancel_collection_appointment_and_job03_expiration.mmd) | **UC R-04 & JOB-03:** Hủy lịch hẹn & Job tự hủy đơn quá 24h | `Recycler`, `HandoverExpirationJob`, `HandoverRequestService`, `ApplicationDbContext` |

---

### 3. Ma trận Quy tắc Nghiệp vụ (Business Rules Traceability)

| Mã Nghiệp Vụ | Quy định chi tiết | Triển khai trong Luồng Biểu đồ |
| :--- | :--- | :--- |
| **`BR-HND-01`** | **Điều kiện tạo đơn hợp lệ:**<br>- Không tự gửi cho chính mình (`RecyclerId != UserId`).<br>- Recycler phải `Verified` và `Online`.<br>- Recycler có nhận các loại phế liệu trong đơn.<br>- Khối lượng từng loại $\ge$ `MinQuantityKg`. | Kiểm tra tại Bước 4–15 của biểu đồ `01_u06`. |
| **`BR-HND-LIMIT`** | **Giới hạn số đơn đang xử lý:** Mỗi người dân chỉ được có tối đa 3 đơn cùng lúc ở trạng thái `Pending`, `Negotiating`, `Accepted`, `Scheduled`. | Kiểm tra tại Bước 6–8 của biểu đồ `01_u06`. |
| **`BR-NEG-04`** | **Khung giờ hẹn hợp lệ:**<br>- Nằm trong khoảng $[UtcNow + 2h, UtcNow + 7d]$.<br>- Khớp giờ làm việc trong `WeeklyScheduleJson` (không rơi vào ngày đóng cửa, đúng khung giờ mở cửa ICT).<br>- Không được Accept đơn có giờ hẹn đã trôi qua trong quá khứ. | Kiểm tra tại biểu đồ `01_u06` (Bước 16–18) và `03_r03` (Bước 8). |
| **`BR-HND-02`** | **Máy trạng thái đơn hàng (State Machine):**<br>- Chỉ sửa khi còn `Pending`.<br>- Recycler chỉ Accept/Reject khi `Pending` hoặc `Negotiating`.<br>- Không được hủy đơn đã `Completed`. | Triển khai tại biểu đồ `02_u07` (Bước 5), `03_r03` (Bước 6, 17) và `04_r04` (Bước 5). |
| **`BR-HND-06`** | **Hủy muộn (Late Cancellation):**<br>- Bắt buộc có lý do hủy.<br>- Nếu hủy trong vòng 4 giờ trước lịch hẹn đã xác nhận (`ConfirmedTimeSlot - UtcNow <= 4h`), tự động gán cờ `IsLateCancellation = true` và trả mã `MSG-HND-06`. | Triển khai tại biểu đồ `02_u07` (Bước 17–20) và `04_r04` (Bước 7–9). |
| **`BR-HND-08`** | **Bảo vệ quyền riêng tư & Che giấu thông tin (Privacy Masking):**<br>- Khi đơn ở `Pending`, Recycler không xem được SĐT và địa chỉ nhà chi tiết của người dân.<br>- Tuyệt đối không trả toạ độ GPS của người dân.<br>- Chỉ mở thông tin liên hệ khi đơn chuyển sang `Accepted` (`isContactRevealed = true`). | Triển khai tại Bước 12–14 của biểu đồ `03_r03`. |
| **`BR-HND-03`** | **Quyền riêng tư đối tác:** Recycler chỉ có quyền xem và xử lý các đơn được gửi trực tiếp đến cơ sở của mình. | Kiểm tra `h.RecyclerId == recyclerId` tại Bước 4 biểu đồ `03_r03` & `04_r04`. |
| **`JOB-03`** | **Tự động hủy đơn chờ quá hạn:** Đơn ở trạng thái `Pending` quá 24h tự động chuyển sang `Expired` định kỳ 15 phút. | Triển khai tại Luồng 2 biểu đồ `04_r04`. |

