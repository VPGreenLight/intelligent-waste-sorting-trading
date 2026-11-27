# REPORT 5.3 – SYSTEM TEST: MODULE 07: DIGITAL HANDOVER RECEIPT, SCALE PROOF & TRANSACTION HISTORY (R-06, U-09, U-12)

## 1. Workflow Summary Header (Theo mẫu Sheet `M07_Receipt` – `SEP490_G141`)

| Workflow | `Module 07: Digital Handover Receipt, Scale Proof & Transaction History (R-06, U-09, U-12)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử End-to-End lập biên nhận số trên /recycler/receipt (R-06): tải ảnh đồng hồ cân, xem trước độ lệch > 20%, bắt buộc ảnh cân khi >= 10kg (BR-TRN-01), phát hành mã RCP-yyyy-NNNN; Người dân xác nhận trên /user/receipt (U-09) khóa giao dịch bất biến (BR-HND-07), nhận túi phân bón cho Cây ảo hoặc bấm Khiếu nại (Disputed); và tra cứu lịch sử /user/transactions & /recycler/transactions. | | | |
| **Number of TCs** | **10** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 7 | 3 | 0 | 0 |
| *Round 2* | 10 | 0 | 0 | 0 |
| *Round 3* | 0 | 0 | 10 | 0 |

---

## 2. System Test Case Specification (`STC-RCP-001` $\rightarrow$ `STC-RCP-010`)

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Scenario 7.1: Cân Đo, Xem Trước Độ lệch & Lập Biên nhận Số (/recycler/receipt - R-06)** | | | | | | | | | |
| **STC-RCP-001** | Xem trước biên nhận (Preview) và hiển thị cảnh báo khi khối lượng thực tế lệch > 20% (BR-TRN-02) | 1. Mở /recycler/receipt cho đơn đã Accepted (ước tính 5kg).<br>2. Nhập khối lượng cân thực tế 7.5kg (lệch +50%) và quan sát hộp tổng hợp. | - UI tự động tính tổng tiền thanh toán theo đơn giá chốt và bật cảnh báo lệch khối lượng > 20% (DeviationPercent = 50%, HasDeviationWarning = true). | Có đơn Accepted. | Passed | Passed | Pending | N/A | Preview & Deviation Warning |
| **STC-RCP-002** | Bắt buộc tải ảnh đồng hồ cân (Scale Proof) khi tổng khối lượng >= 10kg (BR-TRN-01) | 1. Nhập tổng khối lượng thực tế >= 10kg (VD: 12.5kg).<br>2. Không tải ảnh đồng hồ cân và bấm 'Phát hành biên nhận'. | - Hệ thống chặn lại kèm thông báo (MSG-RCP-04): Giao dịch từ 10kg trở lên bắt buộc phải đính kèm ảnh chụp đồng hồ cân. | Tổng cân >= 10kg. | Passed | Passed | Pending | N/A | BR-TRN-01 Boundary >= 10kg |
| **STC-RCP-003** | Tải ảnh đồng hồ cân lên R2 và phát hành thành công biên nhận số (RCP-yyyy-NNNN) | 1. Tải ảnh đồng hồ cân qua khung upload (POST .../receipt/upload-proof).<br>2. Nhấn 'Phát hành biên nhận'. | - Tạo bản ghi HandoverReceipt + TransactionReceipt và tin nhắn hệ thống trong Chat.<br>- Đơn chuyển sang trạng thái AwaitingConfirmation.<br>- Thời gian đếm ngược tự động xác nhận sau 24h (JOB-01) được kích hoạt. | Đơn đang Accepted và có ảnh cân hợp lệ. | Failed | Passed | Pending | **INC-2026-RCP-08** | R-06 Issue Digital Receipt \| Round 1 Failed (INC-2026-RCP-08): lệch DTO CreateReceiptRequest & 404 endpoint lập biên nhận -> đã đồng bộ Round 2 Passed |
| **Scenario 7.2: Người dân Xác nhận Biên nhận / Khởi tạo Khiếu nại (/user/receipt - U-09)** | | | | | | | | | |
| **STC-RCP-004** | Citizen mở /user/receipt kiểm tra ảnh đồng hồ cân và bấm 'Xác nhận biên nhận' (U-09) | 1. Đăng nhập citizen -> mở /user/receipt cho biên nhận RCP-DEMO-0002 (6.5kg).<br>2. Đối chiếu khối lượng, tổng tiền mặt và ảnh đồng hồ cân.<br>3. Nhấn 'Xác nhận biên nhận'. | - Trạng thái đơn chuyển Completed, khóa biên nhận vĩnh viễn không thể sửa/xóa (BR-HND-07).<br>- Cộng túi phân hữu cơ cho Cây ảo và mở form Đánh giá sao (U-10); PointsAwarded từ cân rác bằng 0 theo thiết kế chống gian lận. | Biên nhận đang ở trạng thái AwaitingConfirmation. | Failed | Passed | Pending | **INC-2026-RCP-08** | U-09 Confirm & BR-HND-07 \| Round 1 Failed (INC-2026-RCP-08): xác nhận biên nhận fallback dữ liệu giả -> đã nối API thật Round 2 Passed |
| **STC-RCP-005** | Citizen bấm Khiếu nại từ màn hình biên nhận khi phát hiện sai lệch cân đo | 1. Mở biên nhận ở trạng thái AwaitingConfirmation.<br>2. Nhấn 'Khiếu nại sai lệch', chọn lý do và đính kèm ảnh đối chứng rồi gửi. | - Đơn chuyển ngay sang trạng thái Disputed và tạo phiếu Complaint chuyển tới hàng đợi Staff (/staff/complaints). | Biên nhận đang AwaitingConfirmation. | Passed | Passed | Pending | N/A | Receipt Dispute Flow |
| **STC-RCP-006** | Chặn Recycler hoặc Citizen khác xác nhận hộ biên nhận (Chống IDOR) | 1. Dùng token của recycler hoặc citizen_vip thử gọi xác nhận cho biên nhận của citizen. | - API từ chối (403/404), trạng thái biên nhận giữ nguyên. | Biên nhận thuộc về citizen. | Passed | Passed | Pending | N/A | Authorization Check |
| **Scenario 7.3: Tự động Hoàn tất Quá hạn 24h (JOB-01) & Tính bất biến Giao dịch (BR-HND-07)** | | | | | | | | | |
| **STC-RCP-007** | JOB-01 tự động hoàn tất biên nhận AwaitingConfirmation sau 24h nếu Citizen không khiếu nại | 1. Có biên nhận ở trạng thái AwaitingConfirmation tạo quá 24h.<br>2. Kích hoạt JOB-01.<br>3. Kiểm tra lại trạng thái ở /user/transactions. | - Biên nhận chuyển sang Completed; TradeOrder đồng bộ Completed; cấp túi phân cho Cây ảo. | Biên nhận quá 24h không có Disputed. | Passed | Passed | Pending | N/A | JOB-01 Auto-Complete |
| **STC-RCP-008** | Xác nhận lại lần 2 hoặc chỉnh sửa biên nhận sau khi Completed đều bị chặn (BR-HND-07) | 1. Thử gọi lại nút xác nhận hoặc gửi request sửa biên nhận cho đơn đã Completed. | - Bị chặn hoàn toàn (BR-HND-07), bảo toàn tuyệt đối dữ liệu kế toán/đối soát. | Đơn đã Completed. | Passed | Passed | Pending | N/A | BR-HND-07 Immutability |
| **Scenario 7.4: Tra cứu Lịch sử Giao dịch Hai phía (/user/transactions & /recycler/transactions - U-12)** | | | | | | | | | |
| **STC-RCP-009** | Citizen tra cứu lịch sử giao dịch trên /user/transactions kèm bảng tổng hợp thống kê | 1. Đăng nhập citizen -> mở /user/transactions.<br>2. Quan sát thẻ thống kê tổng khối lượng tái chế (kg), số đơn hoàn tất và danh sách biên nhận phân trang. | - Hiển thị chính xác danh sách các đơn đã Completed và thẻ tổng kết. | Citizen có giao dịch Completed. | Failed | Passed | Pending | **INC-2026-TRD-06** | U-12 Citizen History \| Round 1 Failed (INC-2026-TRD-06): nhầm RecyclerProfile.UserId với Id trong HandoverTradeSyncHelper -> đã vá Round 2 Passed |
| **STC-RCP-010** | Lọc lịch sử giao dịch theo từ khóa tên vựa/mã biên nhận và khoảng thời gian | 1. Trên /user/transactions, nhập mã 'RCP-' hoặc chọn khoảng Từ ngày - Đến ngày rồi lọc. | - Kết quả trả về khớp đúng điều kiện lọc và phân trang lại chính xác. | Đang ở /user/transactions. | Passed | Passed | Pending | N/A | History Filtering |
