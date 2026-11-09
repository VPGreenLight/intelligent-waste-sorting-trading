# TÀI LIỆU PHÂN TÍCH VÀ ĐẶC TẢ BIỂU ĐỒ TUẦN TỰ (SEQUENCE DIAGRAMS)
## MODULE: HANDOVER COMPLETION & TRANSACTION RECEIPT (`HandoverReceiptService`)

---

### 1. Tổng quan Phân hệ (Module Overview)
Module **Handover Completion & Transaction Receipt** chịu trách nhiệm hoàn tất quy trình giao nhận phế liệu thực tế giữa **Người dân (Citizen)** và **Cơ sở thu gom (Recycler)**. Module hiện thực tính năng lập biên nhận số, chốt đơn giá, ghi nhận khối lượng thực tế qua ảnh cân đồng hồ, tính toán tiền mặt thanh toán trực tiếp, tự động cộng điểm xanh và tra cứu lịch sử giao dịch.

Các chức năng nghiệp vụ cốt lõi:
- **`R-06`:** Cơ sở thu gom lập biên nhận bàn giao số sau khi cân thực tế (`Preview` & `Create Handover Receipt`).
- **`U-09`:** Người dân kiểm tra ảnh cân, xác nhận biên nhận và đóng giao dịch (`Confirm Handover Receipt`).
- **`JOB-01`:** Tiến trình nền tự động hoàn tất biên nhận chờ xác nhận quá 48 giờ (`Receipt Auto-Complete Job`).
- **`U-12`:** Xem lịch sử giao dịch thu gom đã hoàn tất kèm tổng khối lượng, tiền mặt và điểm xanh (`Transaction History`).

---

### 2. Danh mục Biểu đồ Tuần tự (Diagram Index)

| STT | File Biểu đồ Mermaid | Tên Use Case / Chức năng | Các Actor & Thành phần Tham gia |
| :---: | :--- | :--- | :--- |
| **01** | [`01_r06_create_handover_receipt.mmd`](file:///d:/Project/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20handover%20completion%20&%20transaction%20receipt/01_r06_create_handover_receipt.mmd) | **UC R-06:** Lập biên nhận bàn giao số sau khi cân thực tế | `Recycler`, `RecyclerReceiptController`, `HandoverReceiptService`, `ApplicationDbContext` |
| **02** | [`02_u09_confirm_handover_receipt.mmd`](file:///d:/Project/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20handover%20completion%20&%20transaction%20receipt/02_u09_confirm_handover_receipt.mmd) | **UC U-09:** Người dân xác nhận biên nhận & hoàn tất giao dịch | `Citizen`, `HandoverReceiptController`, `HandoverReceiptService`, `ApplicationDbContext`, `IGamificationService` |
| **03** | [`03_job01_auto_complete_expired_receipt.mmd`](file:///d:/Project/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20handover%20completion%20&%20transaction%20receipt/03_job01_auto_complete_expired_receipt.mmd) | **JOB-01:** Tự động hoàn tất biên nhận quá hạn 48 giờ | `ReceiptAutoCompleteJob`, `IServiceScopeFactory`, `HandoverReceiptService`, `ApplicationDbContext`, `IGamificationService` |
| **04** | [`04_u12_transaction_history.mmd`](file:///d:/Project/intelligent-waste-sorting-trading/documents/diagram/sequence%20diagram/module%20handover%20completion%20&%20transaction%20receipt/04_u12_transaction_history.mmd) | **UC U-12:** Xem lịch sử giao dịch thu gom đã hoàn tất | `Citizen / Recycler`, `Transaction Controllers`, `HandoverReceiptService`, `ApplicationDbContext` |

> [!NOTE]
> Biểu đồ cấu trúc lớp Clean Architecture tương ứng của module được lưu tại:  
> [`module_handover_completion_and_receipt_clean_architecture_class_diagram_ultra_compact.mmd`](file:///d:/Project/intelligent-waste-sorting-trading/documents/diagram/class%20diagram/module%20handover%20completion%20&%20transaction%20receipt/module_handover_completion_and_receipt_clean_architecture_class_diagram_ultra_compact.mmd).

---

### 3. Ma trận Quy tắc Nghiệp vụ (Business Rules Traceability)

| Mã Nghiệp Vụ | Tên Quy Tắc | Mô Tả Chi Tiết & Ràng Buộc Kỹ Thuật | Vị Trí Triển Khai Trong Biểu Đồ |
| :--- | :--- | :--- | :--- |
| **`BR-HND-02`** | **Máy trạng thái bàn giao** | Biên nhận chỉ được phát hành khi yêu cầu ở trạng thái `Accepted` hoặc `Scheduled`. Khi phát hành, đơn chuyển sang `AwaitingConfirmation`. Chỉ đơn `AwaitingConfirmation` mới được hoàn tất sang `Completed`. | - Biểu đồ 01 (Bước 38)<br/>- Biểu đồ 02 (Bước 17)<br/>- Biểu đồ 03 (Bước 8) |
| **`BR-HND-03`** | **Kiểm soát truy cập theo cặp đối tác** | Chỉ cơ sở được chỉ định (`RecyclerId == currentUser`) mới có quyền phát hành biên nhận; chỉ người dân chủ đơn (`UserId == currentUser`) mới có quyền xác nhận biên nhận. | - Biểu đồ 01 (Bước 35)<br/>- Biểu đồ 02 (Bước 11)<br/>- Biểu đồ 04 (Bước 8) |
| **`BR-HND-04`** | **Bằng chứng cân đo & Mã biên nhận** | Bắt buộc tải lên tối thiểu 1 ảnh và tối đa 5 ảnh chụp cân đồng hồ rõ số kg (`scale_proof_photos`). Ảnh hóa đơn giấy là tùy chọn. Mã biên nhận sinh tự động theo định dạng `RCP-yyyy-NNNN`. | - Biểu đồ 01 (Bước 46–50, 58) |
| **`BR-HND-05`** | **Khóa đơn giá giao dịch (Price Locking)** | Khối lượng thực tế phải $> 0$, làm tròn tối đa 2 chữ số thập phân. Đơn giá `AgreedPricePerKg` được lấy từ bảng giá hiệu lực hiện thời của cơ sở và khóa cố định vào biên nhận, không thay đổi ngay cả khi cơ sở cập nhật giá sau này. | - Biểu đồ 01 (Bước 52–57) |
| **`BR-HND-07`** | **Tính bất biến của giao dịch (Immutability)** | Khi đơn đã chuyển sang `Completed` (do người dân xác nhận hoặc do JOB-01 hoàn tất), biên nhận bị khóa vĩnh viễn, không thể chỉnh sửa, hủy bỏ hoặc xác nhận lại. | - Biểu đồ 02 (Bước 15)<br/>- Biểu đồ 03 (Bước 11) |
| **`BR-GAM-01`** | **Quy đổi & Tích lũy Điểm Xanh** | Khi giao dịch hoàn tất thành công, hệ thống tự động quy đổi điểm xanh theo công thức: $\text{Points} = \text{Round}(\text{TotalWeightKg} \times 10)$. Điểm được cộng cho cả người dân và cơ sở thu gom qua `IGamificationService`. | - Biểu đồ 02 (Bước 22–24)<br/>- Biểu đồ 03 (Bước 14–15) |
| **`MSG-HND-04`** | **Cảnh báo độ lệch khối lượng** | Nếu độ lệch khối lượng thực tế so với ước tính vượt quá $\pm 20\%$ ($|\text{DeviationPercent}| > 20\%$), hệ thống đánh dấu cờ `HasDeviationWarning = true` và hiển thị thông báo cảnh báo trực quan nhưng không chặn tiến trình. | - Biểu đồ 01 (Bước 23, 57, 64)<br/>- Biểu đồ 02 (Bước 8) |
| **`AF-01`** | **Xử lý dòng phế liệu phát sinh tại chỗ** | Cho phép cơ sở bổ sung các loại phế liệu phát sinh ngoài dự kiến (`IsExtra = true`). Loại phế liệu phát sinh bắt buộc phải nằm trong danh mục hoạt động thu mua của cơ sở. | - Biểu đồ 01 (Bước 52–54) |
| **`JOB-01`** | **Tự động đóng biên nhận quá hạn 48h** | Background Service định kỳ 60 phút quét các biên nhận ở trạng thái `AwaitingConfirmation` đã tạo quá 48 giờ để tự động hoàn tất và cộng điểm thưởng xanh. | - Biểu đồ 03 (Toàn bộ luồng) |

