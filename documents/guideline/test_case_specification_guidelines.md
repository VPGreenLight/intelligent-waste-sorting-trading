# QUY CHUẨN ĐẶC TẢ VÀ VIẾT TEST CASE (TEST CASE SPECIFICATION GUIDELINE)
**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Chuẩn tham chiếu:** Đồ án tốt nghiệp Capstone Project (SEP490 - FPT University) & Chuẩn kiểm thử phần mềm IEEE 829  
**Tài liệu đối chiếu:** [Quy chuẩn viết Use Case Description.md](file:///c:/Projects/Demo%20Waste%20Classification/documents/Quy%20chu%E1%BA%A9n%20vi%E1%BA%BFt%20Use%20Case%20Description.md) & [business_rule_&_system_message.md](file:///c:/Projects/Demo%20Waste%20Classification/documents/business_rule_&_system_message.md)  

---

## 1. MỤC ĐÍCH VÀ VỊ TRÍ TRONG BÁO CÁO CAPSTONE (CHAPTER IV / V)

* **Mục đích:** Chuẩn hóa toàn bộ cấu trúc, phương pháp thiết kế và ngôn ngữ trình bày kịch bản kiểm thử (Test Case) cho dự án EcoLink.
* **Tính liên kết cốt lõi (Traceability):** Trong đồ án Capstone, **mọi Test Case bắt buộc phải truy xuất ngược được về Use Case trong Chapter III**, gắn liền với các Business Rules (`BR-[MODULE]-ID`) và mã thông điệp hệ thống (`MSG-[MODULE]-ID`). Hội đồng chấm thi sẽ đánh giá rất cao khi thấy 100% Use Case và luồng rẽ nhánh (Alternate Flow / Exception Flow) đều có Test Case bao phủ.

---

## 2. QUY CHUẨN ĐỊNH DANH TEST CASE (TEST CASE ID CONVENTIONS)

Mã Test Case được xây dựng theo cấu trúc chuẩn:
$$\text{TC-}[\text{MODULE}]\text{-}[\text{UC\_NUM}]\text{-}[\text{STT}]\text{\_}[\text{TYPE}]$$

### 2.1. Bảng quy ước Phân hệ (MODULE)
* `AUTH`: Xác thực, Đăng ký, Đăng nhập, Quên mật khẩu
* `CLS`: Phân loại và Nhận diện rác bằng AI (Classification)
* `MTH`: Định vị GPS, Khớp nối bản đồ cơ sở thu gom (Matching)
* `NEG`: Thương lượng, Đặt lịch hẹn thu gom (Negotiation)
* `HND`: Bàn giao rác, Cân đo, Quét QR chốt biên nhận (Handover)
* `PAY`: Thanh toán gói dịch vụ nội bộ qua VietQR (Payment)
* `GAM`: Điểm thưởng, Cây ảo, Đua top xếp hạng, Đổi voucher (Gamification)
* `DSP`: Khiếu nại giao dịch (Dispute)
* `ADM`: Quản trị danh mục, người dùng, giá sàn phế liệu (Administration)
* `NTF`: Hệ thống thông báo Web Push & Email (Notification)

### 2.2. Quy ước Hậu tố Phân loại Kiểm thử (TYPE)
* `_P` (Positive Testing): Kiểm thử luồng thành công chính (Happy Path / Normal Flow).
* `_N` (Negative Testing): Kiểm thử luồng bắt lỗi, dữ liệu không hợp lệ, vi phạm Business Rule.
* `_B` (Boundary Testing): Kiểm thử giá trị biên (ví dụ: rác 0.01kg, 0kg, âm, vượt ngưỡng 1000kg).
* `_S` (Security / Permission): Kiểm thử quyền hạn, cố tình truy cập API trái phép.
* `_OOD` (Out-of-Distribution - Riêng cho AI): Kiểm thử ảnh không phải rác (màn hình, bàn phím, người).

*Ví dụ:*
* `TC-HND-01-01_P`: Test luồng quét QR bàn giao thành công.
* `TC-HND-01-02_N`: Test quét QR của đơn hàng đã hoàn tất trước đó (vi phạm `BR-HND-02`).
* `TC-CLS-01-03_OOD`: Test chụp ảnh bàn phím máy tính xem AI có phát hiện OOD không.

---

## 3. CẤU TRÚC BẢNG ĐẶC TẢ TEST CASE CHUẨN (STANDARD TEMPLATE)

Trong báo cáo đồ án, mỗi nhóm Use Case sẽ được trình bày dưới dạng bảng 9 cột chuẩn mực sau:

| Test Case ID | Use Case Ref | BR / MSG Ref | Test Objective (Mục tiêu kiểm thử) | Preconditions (Tiền điều kiện) | Test Steps (Các bước thực hiện) | Test Data (Dữ liệu thử nghiệm) | Expected Result (Kết quả kỳ vọng) | Priority |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :---: |
| `TC-xxx` | `UC-xxx` | `BR-xxx`<br>`MSG-xxx` | Mô tả ngắn gọn kiểm tra cái gì | Điều kiện hệ thống cần có trước khi test | 1. Bước 1<br>2. Bước 2<br>3. Bước 3 | Dữ liệu cụ thể nhập vào form / request | Kết quả trên màn hình & dữ liệu thay đổi trong DB | High / Med / Low |

---

## 4. KỸ THUẬT THIẾT KẾ TEST CASE BẮT BUỘC ÁP DỤNG CHO ECOLINK

### 4.1. Phân tích giá trị biên (Boundary Value Analysis - BVA)
Áp dụng cho các trường dữ liệu có giới hạn số học hoặc thời gian:
* **Khối lượng rác bàn giao (`ActualWeightKg`):**
  * Biên dưới: `0 kg` (Báo lỗi), `-1 kg` (Báo lỗi), `0.01 kg` (Chấp nhận).
  * Biên trên: `1000 kg` (Chấp nhận), `1000.01 kg` (Cảnh báo vượt hạn mức chuyến xe).
* **Bán kính quét trạm thu gom GPS (`RadiusKm`):**
  * `0 km` (Lỗi), `0.5 km` (Nhỏ nhất), `50 km` (Lớn nhất), `50.1 km` (Giới hạn tối đa).
* **Thời hạn hiệu lực mã QR / Đơn hẹn:**
  * Còn 1 giây trước khi hết hạn (Hợp lệ).
  * Vừa quá 1 giây (Báo lỗi `MSG-HND-03` hoặc `JOB-03` tự hủy).

### 4.2. Kiểm thử chuyển đổi trạng thái (State Transition Testing)
Áp dụng cho vòng đời của Đơn hẹn bàn giao (`TradeOrder`):
* `Pending` $\rightarrow$ `Accepted` $\rightarrow$ `Completed` (Hợp lệ - Pass).
* `Completed` $\rightarrow$ Thử gọi API xác nhận lần 2 $\rightarrow$ Bị chặn (Idempotent - Pass).
* `Cancelled` $\rightarrow$ Thử xác nhận bàn giao $\rightarrow$ Báo lỗi đơn đã hủy (Pass).

### 4.3. Kiểm thử đặc thù cho Phân hệ AI (AI Computer Vision Testing)
Không chỉ test ảnh rác đẹp, phải có kịch bản test độ bền vững của mô hình:
* **Ảnh mờ / Chụp trong tối:** Kiểm tra confidence score có giảm không.
* **Ảnh Out-Of-Distribution (OOD):** Đưa ảnh con chó, bàn phím, khuôn mặt $\rightarrow$ Hệ thống phải nhận diện OOD và trả về thông báo yêu cầu chụp lại rác (`BR-CLS-03`).
* **Ảnh có nền phức tạp:** Rác đặt trên bàn làm việc có màn hình laptop $\rightarrow$ Kiểm tra giải thuật Crop Bounding Box có loại bỏ được nền gây nhiễu không.

---

## 5. VÍ DỤ MINH HỌA THỰC CHIẾN CHO CÁC MODULE CỐT LÕI CỦA ECOLINK

### 5.1. Phân hệ Bàn giao phế liệu (Module Handover - UC-HND-01: Confirm Handover Receipt via QR)

| Test Case ID | UC Ref | BR / MSG Ref | Test Objective | Preconditions | Test Steps | Test Data | Expected Result | Priority |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :---: |
| **`TC-HND-01-01_P`** | `UC-HND-01` | `BR-HND-01`<br>`MSG-HND-01` | Xác nhận biên nhận hợp lệ khi cơ sở thu gom cân và quét QR | Đơn hàng `ORD-01` đang ở trạng thái `Pending`. Cơ sở thu gom đã đăng nhập. | 1. Mở màn hình Quét QR.<br>2. Quét mã QR của người dân.<br>3. Nhập trọng lượng và đơn giá.<br>4. Nhấn "Xác nhận biên nhận". | - `ActualWeightKg`: 5.2<br>- `UnitPrice`: 6000 | - Hệ thống hiển thị biên nhận thành công kèm `MSG-HND-01`.<br>- Tổng tiền hiển thị: 31,200đ.<br>- Trạng thái đơn chuyển sang `Completed`.<br>- Bắn event cộng điểm thưởng. | **Critical** |
| **`TC-HND-01-02_B`** | `UC-HND-01` | `BR-HND-01`<br>`MSG-GEN-01` | Báo lỗi khi nhập khối lượng rác bằng 0 hoặc số âm | Đơn hàng `ORD-02` trạng thái `Pending`. | 1. Quét mã QR.<br>2. Nhập trọng lượng bằng 0 hoặc số âm.<br>3. Bấm xác nhận. | - `ActualWeightKg`: 0<br>- `UnitPrice`: 6000 | - Hệ thống hiển thị lỗi validation `MSG-GEN-01`.<br>- Không tạo biên nhận, đơn hàng vẫn giữ nguyên trạng thái `Pending`. | **High** |
| **`TC-HND-01-03_N`** | `UC-HND-01` | `BR-HND-02`<br>`MSG-HND-03` | Báo lỗi khi quét lại mã QR của đơn hàng đã hoàn tất | Đơn hàng `ORD-01` đã ở trạng thái `Completed` trước đó. | 1. Quét lại mã QR của đơn `ORD-01`.<br>2. Bấm xác nhận biên nhận. | - `TradeOrderId`: ID của `ORD-01` | - Trả về mã lỗi HTTP 400 Bad Request kèm `MSG-HND-03`.<br>- Thông báo: "Đơn hẹn bàn giao đã hoàn tất hoặc không còn hiệu lực." | **Critical** |
| **`TC-HND-01-04_S`** | `UC-HND-01` | `BR-AUTH-03`<br>`MSG-AUTH-04` | Chặn tài khoản Người dân (User) cố tình gọi API xác nhận của Cơ sở thu gom | Đăng nhập tài khoản Role `Citizen` (User). | 1. Gửi request `POST /api/v1/trade-orders/{id}/confirm-receipt` kèm Token của User. | Token Role `Citizen` | - Trả về mã HTTP 403 Forbidden.<br>- Hệ thống từ chối thực hiện tác vụ. | **High** |

---

### 5.2. Phân hệ Nhận diện Rác AI (Module Classification - UC-CLS-01: Identify Waste via Photo)

| Test Case ID | UC Ref | BR / MSG Ref | Test Objective | Preconditions | Test Steps | Test Data | Expected Result | Priority |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :---: |
| **`TC-CLS-01-01_P`** | `UC-CLS-01` | `BR-CLS-01`<br>`MSG-CLS-01` | Nhận diện đúng chai nhựa với độ tin cậy cao | Camera / App hoạt động bình thường, kết nối AI Service ổn định. | 1. Chọn ảnh chai nhựa rõ nét.<br>2. Bấm "Phân loại ngay".<br>3. Chờ phản hồi. | File ảnh: `Chai_nhua.jpg` (Plastic bottle) | - Kết quả trả về: Danh mục `Plastic` (Nhựa).<br>- Độ tin cậy (Confidence) $\ge 85\%$.<br>- Thời gian phản hồi $< 1000ms$.<br>- Hiển thị hướng dẫn phân loại và tái chế. | **Critical** |
| **`TC-CLS-01-02_N`** | `UC-CLS-01` | `BR-CLS-08`<br>`MSG-CLS-04` | Báo lỗi khi tải lên tệp tin không phải định dạng ảnh hoặc quá 10MB | App đang ở màn hình chọn ảnh rác. | 1. Tải lên tệp tài liệu PDF hoặc ảnh dung lượng 15MB.<br>2. Bấm gửi phân tích. | File `TaiLieu.pdf` hoặc `Anh15MB.jpg` | - Hệ thống chặn ngay tại Client hoặc trả về HTTP 400 kèm `MSG-CLS-04`.<br>- Thông báo: "Định dạng tệp không được hỗ trợ hoặc vượt quá 10MB." | **Medium** |
| **`TC-CLS-01-03_OOD`** | `UC-CLS-01` | `BR-CLS-03`<br>`MSG-CLS-02` | Nhận diện trường hợp ảnh chụp không chứa vật thể rác (OOD) | AI Model đã bật ngưỡng tin cậy OOD Threshold = 0.50. | 1. Chụp ảnh bàn phím máy tính hoặc gương mặt người.<br>2. Gửi ảnh phân tích. | File ảnh: `BanPhim.jpg` (Không chứa rác) | - Trả về `isOutOfDistribution: true`.<br>- Hiển thị thông điệp `MSG-CLS-02`: "Không nhận diện được loại rác. Vui lòng chụp lại gần và rõ hơn." | **High** |

---

## 6. MA TRẬN TRUY XUẤT NGUỒN GỐC YÊU CẦU (REQUIREMENTS TRACEABILITY MATRIX - RTM)

Để bảo vệ đồ án tốt nghiệp, nhóm cần tổng hợp một bảng ma trận đối chiếu (RTM) để chứng minh tính toàn vẹn từ SRS đến Testing:

| Use Case ID | Use Case Name | Business Rules Liên quan | API Endpoint kiểm thử | Danh sách Test Case tương ứng | Tỷ lệ Pass (%) |
| :--- | :--- | :--- | :--- | :--- | :---: |
| **`UC-CLS-01`** | Phân loại rác qua ảnh | `BR-CLS-01`, `BR-CLS-02`, `BR-CLS-03` | `POST /api/v1/classify` | `TC-CLS-01-01_P`, `TC-CLS-01-02_N`, `TC-CLS-01-03_OOD` | 100% |
| **`UC-HND-01`** | Xác nhận biên nhận qua QR | `BR-HND-01`, `BR-HND-02`, `BR-HND-04` | `POST /api/v1/trade-orders/{id}/confirm-receipt` | `TC-HND-01-01_P`, `TC-HND-01-02_B`, `TC-HND-01-03_N`, `TC-HND-01-04_S` | 100% |
| **`UC-PAY-01`** | Thanh toán VietQR dịch vụ | `BR-PAY-01`, `BR-PAY-02` | `POST /api/v1/payments/service-packages` | `TC-PAY-01-01_P`, `TC-PAY-01-02_N`, `TC-PAY-01-03_B` | 100% |
| **`UC-GAM-01`** | Đổi điểm tích lũy lấy Voucher | `BR-GAM-01`, `BR-GAM-04`, `BR-GAM-05` | `POST /api/v1/gamification/vouchers/redeem` | `TC-GAM-01-01_P`, `TC-GAM-01-02_N` | 100% |

---

## 7. CHECKLIST KIỂM TRA CHẤT LƯỢNG TEST CASE TRƯỚC KHI ĐƯA VÀO BÁO CÁO

- [ ] **1. Tính duy nhất:** Mỗi Test Case ID là duy nhất và tuân theo cấu trúc `TC-[MODULE]-[UC]-[STT]_[TYPE]`.
- [ ] **2. Truy xuất hai chiều:** 100% Test Case đều có cột ánh xạ đến ít nhất 1 `Use Case ID` và 1 mã `BR/MSG`.
- [ ] **3. Phủ đủ các luồng:** Mỗi Use Case phải có ít nhất:
  - 1 Test Case Positive (`_P`) kiểm tra luồng chính.
  - Tối thiểu 1-2 Test Case Negative (`_N`) kiểm tra các điều kiện rẽ nhánh / bắt lỗi.
  - Test Case Boundary (`_B`) nếu Use Case có trường nhập số lượng / tiền tệ / khoảng cách.
- [ ] **4. Dữ liệu thử nghiệm cụ thể:** Cột `Test Data` không được ghi chung chung là "Nhập dữ liệu hợp lệ", mà phải ghi rõ giá trị cụ thể (ví dụ: `ActualWeightKg = 5.2`, `UnitPrice = 6000`).
- [ ] **5. Kết quả mong đợi rõ ràng:** Phải ghi rõ thông điệp hiển thị kèm mã `MSG-[MODULE]-ID` và sự thay đổi trạng thái trong CSDL.
