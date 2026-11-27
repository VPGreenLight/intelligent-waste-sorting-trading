# REPORT 5.3 – SYSTEM TEST: MODULE 02: AI WASTE CLASSIFICATION (YOLO26 TWO-STAGE), SORTING RULE ENGINE & WASTE CATALOG (M-02/M-03)

## 1. Workflow Summary Header (Theo mẫu Sheet `M02_WasteScan` – `SEP490_G141`)

| Workflow | `Module 02: AI Waste Classification (YOLO26 Two-Stage), Sorting Rule Engine & Waste Catalog (M-02/M-03)` | | | |
| :--- | :--- | :---: | :---: | :---: |
| **Test requirement** | Kiểm thử End-to-End từ giao diện Quét rác AI (/scanner) qua Backend Proxy tới AI Vision Service FastAPI (YOLO26 Two-Stage 10 danh mục), xử lý OOD < 60%, phân tách 5 nhóm thu mua thương mại vs 5 nhóm hướng dẫn an toàn (BR-SRT-04), tra cứu /guidance, và Admin quản trị Danh mục rác + Rule Engine. | | | |
| **Number of TCs** | **10** | | | |
| **Testing Round** | **Passed** | **Failed** | **Pending** | **N/A** |
| *Round 1* | 10 | 0 | 0 | 0 |
| *Round 2* | 10 | 0 | 0 | 0 |
| *Round 3* | 0 | 0 | 10 | 0 |

---

## 2. System Test Case Specification (`STC-SCN-001` $\rightarrow$ `STC-SCN-010`)

| Test Case ID | Test Case Description | Test Case Procedure | Expected Results | Pre-conditions | Round 1 | Round 2 | Round 3 | Defect IDs | Note |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: | :---: | :---: | :--- |
| **Scenario 2.1: Quét & Nhận diện Rác bằng AI trên /scanner (U-01, U-02)** | | | | | | | | | |
| **STC-SCN-001** | Quét ảnh chai nhựa PET / lon nhôm trên /scanner với mô hình mặc định YOLO26 Two-Stage | 1. Mở trang /scanner.<br>2. Tải lên ảnh chai nhựa rõ nét.<br>3. Nhấn nút 'Phân loại ngay'. | - AI Service trả về nhãn 'plastic', Confidence >= 0.60, IsOutOfDistribution = false.<br>- UI hiển thị hộp Quy tắc sơ chế 4 bước (StandardBinColor = 'Cam'), đồng thời mở nút 'Tìm cơ sở thu mua quanh bạn' dẫn sang /map?category=plastic. | AI Service và WebApi hoạt động bình thường. | Passed | Passed | Pending | N/A | U-01 Happy Path (YOLO26) |
| **STC-SCN-002** | Quét ảnh nhóm rác không thu mua thương mại (Pin cũ 'battery' / Rác hữu cơ 'biological') | 1. Mở /scanner.<br>2. Tải lên ảnh pin đã qua sử dụng hoặc vỏ trái cây.<br>3. Nhấn 'Phân loại ngay'. | - AI nhận diện đúng nhãn 'battery' / 'biological' (IsRecyclable = false).<br>- Hiển thị hộp Cảnh báo an toàn / hướng dẫn thùng rác công cộng, KHÔNG hiển thị nút gửi đơn thu mua tới vựa phế liệu. | Hệ thống đủ 10 danh mục YOLO26. | Passed | Passed | Pending | N/A | BR-SRT-04 Zero Match for Non-Recyclable |
| **STC-SCN-003** | Phát hiện ảnh ngoài phân phối (OOD) hoặc độ tin cậy thấp < 0.60 (BR-AI-01) | 1. Mở /scanner.<br>2. Tải lên ảnh bàn phím máy tính hoặc ảnh bị mờ tối.<br>3. Nhấn 'Phân loại ngay'. | - Hệ thống gắn cờ IsOutOfDistribution = true.<br>- UI hiển thị cảnh báo yêu cầu chụp rõ/gần hơn (MSG-CLS-02) và gợi ý công cụ Báo sai AI. | Ngưỡng OOD threshold mặc định = 0.60. | Passed | Passed | Pending | N/A | OOD Gate Test |
| **Scenario 2.2: Lịch sử Quét Cá nhân & Điều chỉnh Nhãn Trước Khi Tìm Điểm Thu Gom** | | | | | | | | | |
| **STC-SCN-004** | Lưu tự động kết quả quét vào Lịch sử quét cá nhân (/scanner) | 1. Đăng nhập tài khoản citizen.<br>2. Thực hiện 1 lượt quét ảnh thành công trên /scanner.<br>3. Mở tab/khối Lịch sử quét gần đây và F5 trang. | - Bản ghi AiScanHistory (kèm ModelUsed='yolo26_detect', ImageUrl, SHA-256, SortingGuide) xuất hiện và lưu bền vững sau F5. | Đã đăng nhập citizen. | Passed | Passed | Pending | N/A | BR-CLS-08 |
| **STC-SCN-005** | Người dùng tự đổi sang loại rác khác trước khi bấm tìm điểm thu gom | 1. Sau khi AI quét ra 'paper', người dùng chọn lại thẻ 'cardboard' trên UI.<br>2. Nhấn nút tìm điểm thu gom. | - Quy tắc sơ chế cập nhật tương ứng sang Bìa Carton.<br>- Chuyển hướng sang /map mang đúng bộ lọc category=cardboard. | Đang ở màn hình kết quả /scanner. | Passed | Passed | Pending | N/A | Manual Override |
| **Scenario 2.3: Tra cứu Hướng dẫn Phân loại & Thư viện Loại Rác (/guidance, /waste-lookup)** | | | | | | | | | |
| **STC-SCN-006** | Xem danh sách hướng dẫn 10 nhóm rác trên /guidance và chi tiết từng quy tắc | 1. Mở trang /guidance từ thanh điều hướng.<br>2. Nhấn chọn xem từng nhóm: Nhựa, Giấy, Kim loại, Pin/Rác nguy hại. | - Hiển thị đủ 4 bước chuẩn bị, hướng dẫn bỏ đúng màu thùng (Cam, Xanh Dương, Xám, Đỏ) và cảnh báo an toàn. | Có ít nhất 10 SortingRules trạng thái ACTIVE. | Passed | Passed | Pending | N/A | BR-SRT-01 |
| **STC-SCN-007** | Tìm kiếm nhanh từ khóa trên trang Tra cứu loại rác (/waste-lookup) | 1. Mở /waste-lookup.<br>2. Gõ từ khóa 'pin' hoặc 'carton' vào ô tìm kiếm. | - Lọc tức thời đúng danh mục khớp từ khóa; hiển thị rõ nhãn 'Có thể bán phế liệu' vs 'Chỉ xử lý theo hướng dẫn'. | Trang /waste-lookup đang hoạt động. | Passed | Passed | Pending | N/A | Rule Engine Search |
| **Scenario 2.4: Admin Quản trị Danh mục Rác, Rule Engine Publish & Xóa mềm (A-05, BR-SRT-04)** | | | | | | | | | |
| **STC-SCN-008** | Admin tạo danh mục rác mới và kiểm tra chặn trùng mã danh mục (BR-SRT-02) | 1. Đăng nhập Admin -> mở /admin/categories.<br>2. Tạo danh mục mới 'vo_hop_sua' (Hộp giấy Tetrapak).<br>3. Thử tạo lại một danh mục khác trùng code 'vo_hop_sua'. | - Lần 1: Tạo thành công ở trạng thái Active và tự sinh Rule nháp (DRAFT), ghi AuditLog.<br>- Lần 2: Trả lỗi ConflictException (MSG-SRT-02). | Đã đăng nhập Admin. | Passed | Passed | Pending | N/A | BR-SRT-02 |
| **STC-SCN-009** | Admin cập nhật & Phát hành SortingRule (DRAFT -> ACTIVE) và đồng bộ cache | 1. Mở /admin/rules.<br>2. Chỉnh sửa đầy đủ bước sơ chế cho rule nháp 'vo_hop_sua'.<br>3. Nhấn nút 'Phát hành (Publish)'. | - Rule chuyển sang ACTIVE; bộ nhớ đệm WasteRuleEngineService làm mới ngay lập tức; người dân mở /guidance thấy ngay quy tắc mới. | Có Rule trạng thái DRAFT đủ trường bắt buộc. | Passed | Passed | Pending | N/A | BR-SRT-03 |
| **STC-SCN-010** | Chặn phát hành Rule khi thiếu bước sơ chế hoặc danh mục đang Inactive (BR-SRT-03, BR-SRT-04) | 1. Thử bấm Publish khi bỏ trống nội dung chuẩn bị.<br>2. Thử bấm Publish khi danh mục cha đã tắt Inactive. | - Hệ thống chặn cả 2 trường hợp kèm mã lỗi BR-SRT-03 / BR-SRT-04 rõ ràng. | Có Rule DRAFT vi phạm điều kiện. | Passed | Passed | Pending | N/A | Guard Validation |
