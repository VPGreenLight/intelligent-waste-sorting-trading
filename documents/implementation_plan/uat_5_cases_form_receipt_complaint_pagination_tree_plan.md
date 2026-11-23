# KẾ HOẠCH PHÂN TÍCH KỸ THUẬT & TRIỂN KHAI 5 TRƯỜNG HỢP UAT (`FORM`, `RECEIPT`, `RECYCLER COMPLAINT`, `PAGINATION` & `VIRTUAL TREE`)

**Dự án:** EcoLink – Intelligent Waste Sorting & Trading Platform (`SEP490_G141`)  
**Ngày lập:** 22/11/2026  
**Phạm vi:** Giao diện Web (`ecolink-web`) & Core Backend (`Ecolink.WebApi`, `Ecolink.Infrastructure`)  
**Mục tiêu:** Chẩn đoán chính xác nguyên nhân gốc rễ, liệt kê đầy đủ ràng buộc nghiệp vụ hiện hành và lập kế hoạch sửa đồng bộ 5 vấn đề phát hiện trong quá trình kiểm thử thực tế (UAT) trước khi tiến hành chỉnh sửa mã nguồn.

---

## TỔNG QUAN 5 TRƯỜNG HỢP & MA TRẬN HÀNH ĐỘNG

| STT | Trường hợp kiểm thử | Kết luận chẩn đoán (Root Cause) | Phạm vi tác động | Hướng triển khai đề xuất |
| :-: | :--- | :--- | :--- | :--- |
| **1** | Form lên đơn `Ngày hẹn thu gom` và `Khung giờ hẹn thu gom mong muốn` bị lệch | Modal `Chat.tsx` rộng `480px` chia 2 cột (`~204px`/cột); nhãn cột phải `"Khung giờ hẹn thu gom mong muốn: *"` quá dài nên rớt thành 2 dòng trong khi cột trái `"Ngày hẹn thu gom: *"` chỉ dài 1 dòng $\rightarrow$ `<select>` bị đẩy tụt xuống `~20px`. | FE ([Chat.tsx](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Chat.tsx), [Handover.tsx](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Handover.tsx)) | Rút ngắn nhãn cột phải thành `"Khung giờ thu gom:"` (kèm `whitespace-nowrap` cho `<RequiredMark />`) hoặc đồng bộ `min-h` vùng label để 2 ô nhập thẳng hàng ngang tuyệt đối. |
| **2** | Lỗi lập đơn biên nhận không nhập được cân nặng chính xác mà phải lệch `0.01` | Lỗi HTML5 constraint validation: `<input type="number" min={0.01} step={0.1} />` đặt `stepBase = 0.01`, nên trình duyệt chỉ chấp nhận chuỗi `0.01 + k × 0.1` (`9.91`, `10.01`...) và chặn số chẵn `10` hay `2.5` dù Backend `decimal.Round(w, 2) == w` chấp nhận bình thường. | FE ([recycler/Receipt.tsx](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Receipt.tsx), [user/Receipt.tsx](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Receipt.tsx)) | Đổi thuộc tính thành `min="0.01" step="0.01" inputMode="decimal"` ở cả Recycler & Citizen Receipt, giúp nhập chính xác mọi số nguyên và số lẻ tới `0.01 kg`. |
| **3** | Kiểm tra phần Khiếu nại của Recycler có hoạt động không & Ràng buộc | **Backend ĐÃ hoạt động đầy đủ 2 chiều** (`POST /api/v1/complaints`, `GET /api/v1/complaints/me`); tuy nhiên FE còn thiếu nút `Khiếu nại` ở trạng thái `collected` (`AwaitingConfirmation`), chưa lọc `Category` hợp lệ cho đơn `Cancelled`, và trang `/feedback` cho phép gửi nhầm luồng offline nếu bỏ trống mã đơn. | FE ([recycler/Requests.tsx](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Requests.tsx), [Feedback.tsx](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Feedback.tsx)); BE giữ nguyên rule | Giữ nguyên rule chuẩn ở Backend (`7 ngày`, chống trùng active, lọc loại cho `Cancelled`), bổ sung nút `Khiếu nại` cho đơn `collected`, lọc `Category` động theo trạng thái đơn và bắt buộc chọn đơn ở `/feedback`. |
| **4** | Cố định số thành phần trên trang là `10` và loại bỏ phần chọn/hiển thị số phần tử/trang | Component [PaginationBar.tsx](file:///c:/Projects/EcoLink/ecolink-web/src/components/PaginationBar.tsx) đang render khối `<select> Hiển thị {opt}/trang` và `extractPagedMeta` fallback `20`; 22 màn hình FE đang dùng lẫn lộn pageSize `4, 5, 6, 8, 10, 15, 20`. | FE ([PaginationBar.tsx](file:///c:/Projects/EcoLink/ecolink-web/src/components/PaginationBar.tsx), 22 page/component + `src/lib/*Api.ts`); có thể đồng bộ thêm default Backend | Định nghĩa `FIXED_PAGE_SIZE = 10` làm Single Source of Truth, gỡ bỏ dropdown `Hiển thị .../trang` trong `PaginationBar.tsx` (loại bỏ props `onPageSizeChange` / `pageSizeOptions`), và đồng bộ toàn bộ 22 trang về `10`. |
| **5** | Kiểm tra giới hạn tưới & bón phân cho cây ảo đã được reset theo ngày hay chưa | **Backend ĐÃ CÓ giới hạn (`DailyTreeCareLimit = 3`) & TỰ ĐỘNG RESET lúc `00:00 ICT (UTC+7)`** bằng `LastWaterDate == today ? DailyWaterCount : 0`; nhưng còn bất cập: `WaterAndUpgradeAsync` (tiêu điểm lên cấp) bị chặn chung vào hạn mức 3 lượt/ngày, `water`/`exp` chưa lưu bền vững khi F5, và user khác role `"user"` có thể rơi xuống nhánh mô phỏng `localStorage`. | BE ([VirtualTreeService.cs](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/VirtualTreeService.cs)); FE ([EcoFactsPage.tsx](file:///c:/Projects/EcoLink/ecolink-web/src/pages/EcoFactsPage.tsx)) | Giữ reset `00:00 ICT`, tách `WaterAndUpgradeAsync` khỏi giới hạn 3 lượt chăm sóc/ngày, hiển thị rõ bộ đếm + thời điểm reset trên UI và chặn fallback cộng xu giả cho mọi tài khoản đã đăng nhập. |

---

## 1. TRƯỜNG HỢP 1: FORM LÊN ĐƠN `NGÀY HẸN THU GOM` VÀ `KHUNG GIỜ HẸN THU GOM MONG MUỐN` BỊ LỆCH

### 1.1. Phân tích nguyên nhân gốc rễ
Kiểm tra trực tiếp Modal **`📋 Lên đơn & Đề xuất giá thu gom`** trong [`Chat.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Chat.tsx#L1567-L1695):
1. Khung modal dùng lớp `card w-full max-w-[480px] p-6` (`line 1568`), cho chiều rộng vùng nội dung bên trong khoảng `432px`.
2. Dòng chọn Ngày & Khung giờ dùng lưới 2 cột đều nhau `grid grid-cols-1 sm:grid-cols-2 gap-3` (`line 1645`), nên mỗi cột chỉ rộng xấp xỉ **`210px`**:
   - **Cột trái (`lines 1646–1670`):** `<label>` viết `"Ngày hẹn thu gom: *"` vừa khít trên **1 dòng** (`~20px`), ngay dưới là `<input type="date" className="input h-10 text-sm" />`, dưới cùng là dòng gợi ý `"Hôm nay (22/11/2026) đến +30 ngày"`.
   - **Cột phải (`lines 1672–1694`):** `<label>` viết `"Khung giờ hẹn thu gom mong muốn: *"` dài hơn bề ngang `210px` nên chữ `*` / từ cuối bị tràn xuống **dòng thứ 2** (`~40px`).
3. Do hai cột render độc lập theo chiều dọc (`<div>` chứa `<label>` + `<input>` / `<select>`), chiều cao nhãn bên phải cao gấp đôi nhãn bên trái khiến ô `<select>` `10:00 - 12:00` bị **đẩy tụt xuống lệch ~20px** so với ô `11/23/2026` bên trái (đúng như ảnh chụp màn hình UAT).
4. Kiểm tra chéo màn hình đặt lịch [`user/Handover.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Handover.tsx#L745-L796): trang này viết `"Ngày hẹn thu gom"` và `"Khung giờ muốn thu gom"` trên form rộng hơn (`max-w` của thẻ form chính), nhưng chưa đồng nhất chiều cao `h-10` tường minh giữa `<input type="date">` và `<select>`.

### 1.2. Phương án triển khai
1. **Tại [`Chat.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Chat.tsx#L1645-L1695):**
   - Chuẩn hóa nhãn cột phải ngắn gọn, cùng độ dài thị giác với cột trái: đổi `"Khung giờ hẹn thu gom mong muốn:"` thành **`"Khung giờ thu gom:"`** (hoặc giữ ý mong muốn qua placeholder/tooltip nếu cần), đồng thời bọc từ cuối + `<RequiredMark />` trong `whitespace-nowrap` để dấu `*` không bao giờ rớt đơn độc sang dòng mới.
   - Bổ sung căn thẳng hàng chắc chắn kể cả ở màn hình hẹp bằng cách đặt `flex flex-col` cho mỗi cột kết hợp đồng nhất `leading-5` / `min-h-[20px]` trên `<label>` (hoặc dùng `grid-cols-2` tách hàng label & hàng input khi `sm:`), đảm bảo mép trên và mép dưới của `<input type="date" className="input h-10 text-sm">` và `<select className="input h-10 text-sm">` thẳng hàng `100%` trên cùng một trục ngang.
2. **Tại [`user/Handover.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Handover.tsx#L745-L796):**
   - Thêm class `h-10` đồng nhất cho cả `<input type="date">` và `<select>`, giữ nhãn cột phải gọn (`"Khung giờ thu gom"`).

---

## 2. TRƯỜNG HỢP 2: LỖI LẬP ĐƠN BIÊN NHẬN KHÔNG NHẬP ĐƯỢC CÂN NẶNG CHÍNH XÁC MÀ PHẢI LỆCH `0.01`

### 2.1. Phân tích nguyên nhân gốc rễ (HTML5 Step-Base Validation Bug)
Kiểm tra trực tiếp ô nhập khối lượng cân thực tế tại [`recycler/Receipt.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Receipt.tsx#L389-L403):
```tsx
<input
  className="input h-9 w-28 tabular-nums"
  type="number"
  min={0.01}
  step={0.1}
  ...
  value={r.kg}
/>
```
- Theo chuẩn **HTML5 (`<input type="number">` Constraint Validation)** của trình duyệt (Chrome/Edge/Firefox):
  - Khi `<input type="number">` khai báo thuộc tính `min`, giá trị `min` trở thành **mốc gốc (`stepBase`)** cho bước nhảy `step` (thay vì lấy `0` làm gốc).
  - Tập các giá trị hợp lệ do trình duyệt tính là:
    $$\text{valid value} = \text{min} + k \times \text{step} = 0.01 + k \times 0.1 \quad (k \in \mathbb{N})$$
- **Hệ quả:** Trình duyệt **chỉ coi là hợp lệ** các số `0.01, 0.11, 0.21, ..., 1.01, ..., 9.91, 10.01`. Khi Recycler nhập số cân chính xác như `10`, `5`, `2.5`, hoặc `1.2` rồi bấm **Gửi biên nhận** (`<form onSubmit={submit}>`), HTML5 validation chặn ngay lập tức trước khi hàm `submit` chạy và hiện tooltip yêu cầu chọn 2 giá trị gần nhất bị **lệch `0.01`** (ví dụ `9.91` hoặc `10.01`).
- **Đối chiếu Backend ([`HandoverReceiptService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs#L488-L490)):**
  - Backend chỉ kiểm tra: `if (w <= 0 || decimal.Round(w, 2) != w)` và sau đó tính `Math.Round(Actual * Price, 0)`. Tức Backend hỗ trợ đầy đủ mọi số dương có tối đa **2 chữ số thập phân** (`10`, `10.5`, `10.25`, `0.05`...).
- **Kiểm tra chéo các form nhập cân khác:**
  - [`user/Receipt.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Receipt.tsx#L495-L501) dùng `min={0} step={0.1}` $\rightarrow$ không bị lệch `0.01`, nhưng vẫn sẽ báo lỗi HTML5 nếu Recycler gửi biên nhận có số lẻ hàng phần trăm (ví dụ `2.25 kg`) và User mở xem/đối chiếu lại.
  - `Chat.tsx:1637` (`min="0.5" step="0.5"`) là bội số của `0.5` nên không lệch `0.01`, nhưng nếu cần nhập lẻ `1.2 kg` hay `2.25 kg` lúc thương lượng cũng có thể cân nhắc chỉnh `step` nếu muốn linh hoạt hơn (hiện giữ nguyên `0.5` cho ước lượng ban đầu trừ khi cần đổi).

### 2.2. Phương án triển khai
1. **Tại [`recycler/Receipt.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Receipt.tsx#L389-L403):**
   - Sửa thuộc tính input thành `min="0.01" step="0.01" inputMode="decimal"` (`0.01 + k × 0.01` bao phủ **toàn bộ 100%** các số nguyên lẫn số thập phân đến 2 chữ số như `0.05`, `1`, `2.5`, `10`, `10.25`), khớp tuyệt đối với rule `decimal.Round(w, 2) == w` ở Backend.
   - Bổ sung chặn/chuẩn hóa UX nhập liệu trên dòng cân: dùng state chuỗi hoặc giữ an toàn giá trị khi người dùng xóa trắng ô để gõ lại số mới (tránh `Number("") -> 0` nhảy số giật cục khi đang nhập), và validate rõ bằng tiếng Việt nếu số chữ số thập phân `> 2` hoặc `<= 0`.
2. **Tại [`user/Receipt.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Receipt.tsx#L495-L501):**
   - Đồng bộ `min="0.01" step="0.01"` để khớp hoàn toàn dữ liệu có thể có 2 chữ số thập phân từ biên nhận Recycler gửi sang.

---

## 3. TRƯỜNG HỢP 3: KIỂM TRA PHẦN KHIẾU NẠI CỦA RECYCLER CÓ HOẠT ĐỘNG KHÔNG & CÁC RÀNG BUỘC

### 3.1. Kết luận kiểm tra hiện trạng hoạt động

#### A. Phía Backend ([`ComplaintsController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/ComplaintsController.cs) & [`ComplaintService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ComplaintService.cs))
**ĐANG HOẠT ĐỘNG ĐẦY ĐỦ 2 CHIỀU (`Citizen` $\leftrightarrow$ `Recycler`).**
- `POST /api/v1/complaints` chỉ gắn `[Authorize]` (không chặn vai trò Citizen). Trong [`ComplaintService.SubmitComplaintAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ComplaintService.cs#L85-L95):
  - Nếu `handover.UserId == callerUserId` $\rightarrow$ `ComplainantRole = "Citizen"`, `RespondentId = handover.RecyclerId`.
  - Nếu `handover.RecyclerId == callerUserId` $\rightarrow$ `ComplainantRole = "Recycler"`, `RespondentId = handover.UserId`.
- Phiếu do Recycler tạo được sinh mã `CMP-...`, lưu vào `transaction_complaints`, ghi vào nhật ký chat đối soát của `TradeOrder`, và đẩy lên hàng đợi trọng tài Staff tại `/staff/complaints`.
- `POST /api/v1/complaints/upload-evidence` và `GET /api/v1/complaints/me` đều hoạt động bình thường cho tài khoản Recycler.

#### B. Tổng hợp 6 Ràng buộc nghiệp vụ hiện hành trên Backend
Đối chiếu từ [`ComplaintService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ComplaintService.cs#L22-L186) và [`ReviewAndComplaintValidators.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/ReviewAndComplaintValidators.cs#L51-L72):

| STT | Ràng buộc | Điều kiện kỹ thuật chính xác trong Code | Mã lỗi & Phản hồi khi vi phạm |
| :-: | :--- | :--- | :--- |
| **1** | **Đúng chủ thể đơn (`BR-DSP-01`)** | `callerUserId` phải trùng `handover.UserId` hoặc `handover.RecyclerId`. | `403 Forbidden` (`MSG-ADM-04`) |
| **2** | **Trạng thái đơn & Nhóm nguyên nhân hợp lệ (`BR-DSP-01`)** | • Đơn `Accepted`, `Scheduled`, `AwaitingConfirmation`, `Disputed`, `Completed`: được chọn **cả 5 nhóm** (`ScaleDiscrepancy`, `PaymentIssue`, `NoShow`, `Misconduct`, `Other`).<br>• Đơn `Cancelled` (*Đã hủy*): **CHỈ** được chọn **`NoShow`** hoặc **`Misconduct`**.<br>• Đơn `Pending` hoặc `Negotiating`: **không được khiếu nại**. | `400 Bad Request` (`MSG-DSP-03`) |
| **3** | **Thời hạn `7 ngày` (`ComplaintWindowDays = 7`)** | Tính `DateTime.UtcNow - (receipt?.ConfirmedByUserAt ?? handover.ActualHandoverAt ?? handover.UpdatedAt) <= 7 days`. Quá 7 ngày hệ thống từ chối. | `400 Bad Request` (`MSG-DSP-03`) |
| **4** | **Mỗi đơn tối đa `1` khiếu nại đang xử lý** | Kiểm tra tồn tại `Status == Open \|\| Investigating` (kèm Unique Partial Index `uq_transaction_complaints_active_request` ở PostgreSQL). Chỉ khiếu nại tiếp được khi phiếu cũ đã đóng (`Resolved` / `Dismissed`). | `409 Conflict` (`MSG-DSP-04`) |
| **5** | **Định dạng mô tả & Bằng chứng (`BR-DSP-02`)** | • `Category`: 1 trong 5 giá trị `ScaleDiscrepancy`, `PaymentIssue`, `NoShow`, `Misconduct`, `Other`.<br>• `Description`: không rỗng, sau `Trim()` dài **`15` – `2000` ký tự**.<br>• `EvidencePhotoUrls`: tùy chọn (không bắt buộc ảnh), tối đa **`5` ảnh** (`.jpg/.jpeg/.png/.webp`, mỗi file upload tối đa `10 MB`). | `400 Bad Request` (`MSG-GEN-01`) |
| **6** | **Tự động chuyển `Disputed`** | Nếu đơn đang ở trạng thái `AwaitingConfirmation` (đã cân, chờ người dân xác nhận), việc gửi khiếu nại tự động đưa đơn sang `Disputed` để đóng băng job tự động hoàn tất (`JOB-01`). | Cập nhật tự động trong Transaction |

#### C. Các điểm hổng / bất cập trên Frontend khiến luồng Khiếu nại của Recycler dễ lỗi hoặc khó dùng
Rà soát [`recycler/Requests.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Requests.tsx#L394-L553) và [`Feedback.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Feedback.tsx#L60-L232):
1. **Thiếu nút Khiếu nại cho đơn ở trạng thái `"collected"` (`AwaitingConfirmation`):**
   - Tại [`recycler/Requests.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Requests.tsx#L394-L458), nút `Báo sự cố / Khiếu nại` chỉ hiện với `accepted`, còn nút `Khiếu nại giao dịch` chỉ hiện với `done` và `broken` (`Cancelled`). Khi Recycler đã lập biên nhận xong (đơn chuyển sang `AwaitingConfirmation` / `"collected"` trên FE) nhưng người dân không hợp tác xác nhận hoặc phát sinh tranh chấp, **cột Thao tác chỉ có mỗi link `Tạo biên nhận`**, Recycler không có nút mở khiếu nại ngay trên dòng đơn này (dù Backend cho phép).
   - Ngoài ra khi đơn bị chính User khác đang khiếu nại (`rawStatus === "Disputed"`), cũng chưa thể hiện rõ trạng thái tranh chấp tại bảng `Requests`.
2. **Dropdown Nhóm nguyên nhân không tự lọc cho đơn `Cancelled` (`status === "broken"`):**
   - Tại `recycler/Requests.tsx:511` và `Feedback.tsx:223`, khi chọn đơn đã hủy (`Cancelled`), UI vẫn cho chọn `PaymentIssue` / `ScaleDiscrepancy` / `Other`. Nếu Recycler chọn nhầm các loại này và gửi lên, Backend trả `400 MSG-DSP-03` dù đơn mới hủy tức thì trong ngày.
3. **Lỗ hổng tại trang `/feedback` ([`Feedback.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Feedback.tsx#L89-L120)):**
   - Trong menu Recycler ([`portal.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/portal.ts#L63)) có mục `"Khiếu nại và góp ý"` (`/feedback`).
   - Nhưng ở [`Feedback.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Feedback.tsx#L63-L65), `handoverOrders` lấy cả các đơn `Pending`/`Negotiating` hoặc quá 7 ngày vào dropdown; ngược lại nếu Recycler quên chọn đơn (`selectedOrderId === ""`) mà điền form rồi bấm Gửi, hàm `send` rơi xuống nhánh giả lập `makeRef()` ở cuối component mà **không hề gửi khiếu nại lên Server**.
4. **Chưa hiển thị danh sách Khiếu nại của Recycler ngay trên `/recycler/requests`:**
   - Recycler gửi xong từ `/recycler/requests` chỉ thấy dòng flash thông báo mã phiếu, muốn xem trạng thái Staff đã giải quyết hay chưa phải tự đi sang `/feedback`.

### 3.2. Phương án triển khai
1. **Tại [`recycler/Requests.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Requests.tsx):**
   - Mở nút **`Khiếu nại / Báo sự cố`** cho cả trạng thái `"collected"` (`AwaitingConfirmation`) kèm nhãn rõ ràng (`Xem biên nhận` thay cho `Tạo biên nhận` khi đã phát hành biên nhận).
   - Khi mở form khiếu nại trên một đơn `complaintOrder`, nếu đơn ở trạng thái `"broken"` (`Cancelled`), **chỉ hiển thị đúng 2 nhóm hợp lệ** (`NoShow`, `Misconduct`) kèm ghi chú quy định `BR-DSP-01`; với các đơn đủ điều kiện khác hiển thị đủ 5 nhóm.
   - Bổ sung khung tóm tắt **Khiếu nại giao dịch của tôi** (`fetchMyComplaints`) kèm thông báo rõ các ràng buộc (hạn 7 ngày, tối đa 1 phiếu đang xử lý/đơn, mô tả >= 15 ký tự).
2. **Tại [`Feedback.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Feedback.tsx):**
   - Lọc danh sách đơn trong dropdown `Đính kèm đơn bàn giao tranh chấp` theo đúng trạng thái hợp lệ (bỏ `Pending` / `Negotiating`).
   - Khi chọn đơn `Cancelled`/`broken`, tự động lọc nhóm nguyên nhân chỉ còn `NoShow` & `Misconduct` và tự động reset `backendCategory` nếu đang chọn giá trị không hợp lệ.
   - Nếu người dùng đã đăng nhập (`hasLiveBackendAuth()`) và chọn loại `"complaint"` có đơn khả dụng nhưng chưa chọn đơn, cảnh báo rõ sự khác biệt giữa *Khiếu nại đơn giao dịch (gửi Staff S-05)* vs *Góp ý vận hành chung*.

---

## 4. TRƯỜNG HỢP 4: CỐ ĐỊNH SỐ THÀNH PHẦN TRÊN TRANG LÀ `10` VÀ LOẠI BỎ PHẦN HIỂN THỊ SỐ LƯỢNG PHẦN TỬ/TRANG

### 4.1. Phân tích hiện trạng
1. **Trong [`PaginationBar.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/PaginationBar.tsx#L40-L103):**
   - `lines 84–103`: đang render cụm điều khiển chọn kích thước trang (`<span>Hiển thị</span>` + `<select aria-label="Số mục mỗi trang">{opt}/trang</select>`) mỗi khi component cha truyền prop `onPageSizeChange`.
   - `line 33`: hàm `extractPagedMeta` đang fallback mặc định `raw.pageSize ?? 20`.
2. **Kiểm kê toàn bộ 22 file sử dụng `PaginationBar` trong `ecolink-web/src` hiện dùng kích thước trang không đồng nhất:**
   - Dùng `4`: [`Partners.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Partners.tsx#L28) (`livePageSize = 4`)
   - Dùng `5`: [`user/Handover.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Handover.tsx#L95), [`recycler/Requests.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Requests.tsx#L76) (`pending`), [`staff/Applications.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/Applications.tsx#L36) (`pending`), [`admin/BlogReview.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/BlogReview.tsx#L51) (`pending`), [`staff/Complaints.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/Complaints.tsx#L58) (`offline`), [`Rewards.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Rewards.tsx#L154) (`redemptions`)
   - Dùng `6`: [`Blog.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Blog.tsx#L30), [`EcoFactsPage.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/EcoFactsPage.tsx#L138), [`Partners.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Partners.tsx#L26), [`Rewards.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Rewards.tsx#L149), [`user/Gamification.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Gamification.tsx#L52) (`facts`)
   - Dùng `8`: [`user/Gamification.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Gamification.tsx#L56) (`gifts`), [`staff/AiReview.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/AiReview.tsx#L83-L87)
   - Dùng `10`: [`recycler/Catalog.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Catalog.tsx#L58), [`recycler/Requests.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Requests.tsx#L78) (`active`), [`staff/Accounts.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/Accounts.tsx#L77), [`staff/Applications.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/Applications.tsx#L38) (`done`), [`staff/BlogEditor.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/BlogEditor.tsx#L50), [`staff/Complaints.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/Complaints.tsx#L48-L53), [`staff/EcoFacts.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/EcoFacts.tsx#L28), [`staff/Gifts.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/Gifts.tsx#L44-L52), [`admin/BlogReview.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/BlogReview.tsx#L53), [`admin/Leaderboard.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Leaderboard.tsx#L90), [`admin/StaffAccounts.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/StaffAccounts.tsx#L83), [`user/Transactions.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Transactions.tsx#L27)
   - Dùng `15`: [`admin/Reconciliation.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Reconciliation.tsx#L275), [`admin/Reports.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Reports.tsx#L213)
   - Dùng `20`: [`components/Leaderboard.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/Leaderboard.tsx#L61)
3. **Các hàm API client & Backend mặc định:** Phần lớn hàm `fetch*` trong `src/lib/*Api.ts` và các Controller Backend (`HandoverRequestController`, `RecyclerHandoverController`, `HandoverReceiptController`, `RecyclerReceiptController`, `PointsAndRewardsController`, `StaffBlogController`, `AdminBlogController`, `StaffGamificationController`, `WasteScanController`, `ServicePaymentController`) đang đặt mặc định `pageSize = 20` (hoặc `12` ở `PublicBlogController`).

### 4.2. Phương án triển khai chuẩn DRY
1. **Tại [`PaginationBar.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/PaginationBar.tsx):**
   - Khai báo hằng số trung tâm: **`export const FIXED_PAGE_SIZE = 10`**.
   - **Loại bỏ hoàn toàn** phần giao diện chọn/hiển thị số lượng phần tử trên trang (`<select> {opt}/trang`) ở `lines 84–103`, đồng thời xóa các prop `onPageSizeChange` và `pageSizeOptions` khỏi `PaginationBarProps` (làm TypeScript `tsc --noEmit` tự động phát hiện bất kỳ chỗ nào còn sót prop cũ).
   - Đặt giá trị mặc định `pageSize = FIXED_PAGE_SIZE` (`10`) trên `<PaginationBar />` và trong `extractPagedMeta`.
   - Phần thống kê bên trái (`lines 77–81`): rút gọn thành hiển thị tổng số mục dạng `Tổng N {itemLabel}` *(hoặc giữ `Trang {current}/{totalPages} · {totalCount} {itemLabel}`)* để loại bỏ triệt để việc hiển thị dải phần tử/trang như `"1–10"` nếu yêu cầu là không hiển thị số lượng phần tử/trang ở cả select lẫn text mô tả. *(Xác nhận điểm này với người dùng ở cuối kế hoạch trước khi sửa).*
2. **Đồng bộ toàn bộ 22 trang/component sử dụng `<PaginationBar />`:**
   - Nhập `FIXED_PAGE_SIZE` từ `@/components/PaginationBar`, thay thế tất cả `useState(4/5/6/8/15/20)` cho pageSize thành **`FIXED_PAGE_SIZE` (`10`)** và gỡ `onPageSizeChange` / `pageSizeOptions` khỏi mọi JSX `<PaginationBar />`.
   - Đối với các trang vừa phân trang server-side (`staff/Complaints.tsx`, `staff/EcoFacts.tsx`, `staff/Gifts.tsx`, `admin/Reconciliation.tsx`, `admin/Reports.tsx`, `Rewards.tsx`) vừa nạp dữ liệu danh mục: truyền thẳng `pageSize: FIXED_PAGE_SIZE` (`10`) khi gọi API phân trang.
   - *(Lưu ý ngoại lệ giữ nguyên)* Không ép `pageSize = 10` vào các lời gọi API mang tính **đếm tổng Dashboard** (`pageSize: 1` ở `staff/Home.tsx`, `admin/AccountProfile.tsx`, `admin/Gamification.tsx`), **ô xem nhanh widget** (`Scanner.tsx` lấy 5 lịch sử gần nhất, `Chat.tsx` nạp 50 tin nhắn hội thoại), hoặc **nạp danh mục cho trang lọc client-side** (`fetchStaffAccounts({ pageSize: 100 })` trước khi `paginateItems` 10 mục/trang).
3. **Đồng bộ API Client & Backend Controllers (tùy chọn tăng tính nhất quán):**
   - Đổi default parameter `pageSize = 10` trong các hàm phân trang danh sách của `src/lib/*Api.ts` và có thể đồng bộ `[FromQuery] int pageSize = 10` tại các Controller Backend nếu muốn API mặc định khớp `10` ngay cả khi gọi qua Swagger/Postman.

---

## 5. TRƯỜNG HỢP 5: KIỂM TRA GIỚI HẠN TƯỚI VÀ BÓN PHÂN CHO CÂY ẢO ĐÃ ĐƯỢC RESET THEO NGÀY HAY CHƯA

### 5.1. Kết quả kiểm tra chi tiết (Backend vs Frontend)

#### A. Phía Backend ([`VirtualTreeService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/VirtualTreeService.cs) & [`GamificationCommon.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GamificationCommon.cs))
**ĐÃ CÓ cơ chế giới hạn lượt/ngày và TỰ ĐỘNG RESET THEO NGÀY lúc `00:00` giờ Việt Nam (`ICT / UTC+7`)!**
- Hàm xác định ngày tại [`GamificationCommon.cs:7-13`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GamificationCommon.cs#L7-L13):
  ```csharp
  public static DateOnly Today() => DateOnly.FromDateTime(DateTime.UtcNow + Ict); // Ict = +7h
  ```
- Cơ chế reset lười (Lazy Daily Reset) dùng ngày thực hiện gần nhất `LastWaterDate`:
  - Tại [`VirtualTreeService.BuildAsync:197`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/VirtualTreeService.cs#L197):
    `var daily = t.LastWaterDate == today ? t.DailyWaterCount : 0;` $\rightarrow$ Ngay sau `00:00 ICT` sang ngày mới, API `GET /api/v1/gamification/tree/me` trả ngay `dailyWaterCount = 0` mà không cần background cronjob quét DB.
  - Tại [`VirtualTreeService.CareAsync:43-59`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/VirtualTreeService.cs#L43-L59) (endpoint `POST /api/v1/gamification/tree/care`, dùng cho cả `"water"` và `"fertilize"`):
    - Tính `dailyCount = tree.LastWaterDate == today ? tree.DailyWaterCount : 0`.
    - Kiểm tra `if (dailyCount >= settings.TreeLimit)` (cấu hình trong bảng `gamification_config.daily_tree_care_limit`, mặc định **`3`**, chỉnh được bởi Admin ở `/admin/gamification`). Nếu vượt giới hạn $\rightarrow$ ném `BR-GAM-06`: *"Bạn đã chăm cây tối đa 3 lần trong ngày hôm nay."*
    - Cập nhật DB có điều kiện đồng thời (`((t.LastWaterDate == today && t.DailyWaterCount == dailyCount) || t.LastWaterDate != today)`) rồi gán `DailyWaterCount = dailyCount + 1`, `LastWaterDate = today` $\rightarrow$ chống được lỗi bấm đúp hoặc gửi nhiều request cùng tích tắc vượt hạn mức `3/ngày`.

#### B. 3 Điểm bất cập còn tồn tại khi đối chiếu nghiệp vụ thực tế
1. **Bất cập 1 — Nút `Nâng cấp cây bằng Điểm Xanh` bị khóa chung vào giới hạn `3 lượt chăm sóc/ngày` ([`VirtualTreeService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/VirtualTreeService.cs#L86-L88)):**
   - Hiện tại `WaterAndUpgradeAsync` cũng kiểm tra `if (dailyCount >= settings.TreeLimit)` và `BuildAsync` (`line 217`) đặt `CanUpgrade = next != null && balance >= next.RequiredPoints && daily < s.TreeLimit`.
   - **Hệ quả:** Nếu User đã Tưới nước + Bón phân đủ `3/3` lượt trong ngày, sau đó giao rác kiếm đủ Điểm Xanh để nâng lên Cấp 2/Cấp 3 mở khóa EcoFact, họ **bị chặn nút Nâng cấp** đến tận hôm sau mới tiêu điểm nâng cấp được.
2. **Bất cập 2 — Tham số `actionType` (`"water"` vs `"fertilize"`) chưa phân biệt trên Backend, và độ ẩm/`EXP` chưa lưu DB:**
   - Trong `CareAsync(userId, actionType)`, biến `actionType` hiện **chưa được kiểm tra hay phân tách** — cả Tưới nước và Bón phân cùng cộng vào chung tổng `DailyWaterCount` (`3 lượt/ngày`), không đếm riêng 2 loại và không ghi lại chỉ số độ ẩm `water` hay `EXP`.
   - Do đó trên [`EcoFactsPage.tsx:171`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/EcoFactsPage.tsx#L171), mỗi khi F5 tải lại trang, độ ẩm cây (`water`) quay về `75%` mặc định trong React state.
3. **Bất cập 3 — Rò rỉ chế độ mô phỏng `localStorage` cho vai trò khác `"user"` & thiếu chỉ dẫn thời điểm reset trên UI:**
   - Tại [`EcoFactsPage.tsx:131`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/EcoFactsPage.tsx#L131): `const liveTreeUser = hasLiveBackendAuth() && session?.role === "user"`. Nếu tài khoản `Recycler`, `Staff`, hoặc `Admin` (đã đăng nhập) mở trang `/ecofacts`, `liveTreeUser` thành `false`, dẫn tới giao diện rơi về nhánh mô phỏng offline **không có giới hạn ngày** và cộng xu ảo qua `localStorage` (`triggerCoinReward`).
   - Tại `EcoFactsPage.tsx:774` hiện chỉ ghi `"Lượt hôm nay: 0/3"`, chưa nói rõ **`Reset lúc 00:00 hằng ngày`**, và khi đạt `3/3`, cả 2 nút đều bị `disabled` âm thầm nếu user không đọc dòng nhỏ phía trên.

### 5.2. Phương án triển khai đề xuất
1. **Ở Backend (`VirtualTreeService.cs`):**
   - **Giữ nguyên cơ chế reset lúc `00:00 ICT (UTC+7)`** của `DailyWaterCount`.
   - **Tách `WaterAndUpgradeAsync` khỏi rào cản `DailyWaterCount >= TreeLimit`:** nâng cấp cấp cây bằng Điểm Xanh (bấm nút *Nâng lên Cấp X*) không bị chặn dù người dùng đã dùng hết 3 lượt Tưới/Bón phân trong ngày (`CanUpgrade = next != null && balance >= next.RequiredPoints`).
   - *(Mở rộng tùy theo lựa chọn của bạn ở mục 6)*: giữ nguyên tổng **`3` lượt chăm sóc chung/ngày** (`water` + `fertilize`) theo `DailyTreeCareLimit` hoặc mở rộng entity lưu `DailyWaterCount` / `DailyFertilizeCount` + `HydrationPercent` nếu muốn tách giới hạn riêng từng nút.
2. **Ở Frontend ([`EcoFactsPage.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/EcoFactsPage.tsx) & [`user/Gamification.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Gamification.tsx)):**
   - Tại khu vực nút **Tưới nước** & **Bón phân**, hiển thị rõ nhãn: **`Lượt hôm nay: X/3 (Làm mới lúc 00:00)`** và hiện cảnh báo rõ ràng ngay dưới 2 nút khi đã đạt `3/3`.
   - Nếu `hasLiveBackendAuth()` là `true` nhưng `session?.role !== "user"`, **khóa chế độ mô phỏng cộng xu giả (`triggerCoinReward`)**, hiển thị banner giải thích đúng quy tắc `BR-GAM-08` (*Cây Ảo áp dụng cho tài khoản Người dân*).
   - Đồng bộ dependency `[liveTreeUser, isGuest]` cho timer mô phỏng ở `EcoFactsPage.tsx:269`.

---

## 6. ĐIỂM CẦN BẠN XÁC NHẬN TRƯỚC KHI TRIỂN KHAI CODE

1. **Ở Trường hợp 4 (Phân trang cố định `10`):**
   - Trong [`PaginationBar.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/PaginationBar.tsx#L77-L81), ngoài việc **xóa hoàn toàn ô chọn `{opt}/trang`**, ở dòng tóm tắt góc trái hiện đang ghi `"Hiển thị 1–10 trên 25 mục"`: bạn muốn **giữ nguyên** dòng tóm tắt phạm vi này, hay đổi thành **`"Tổng 25 mục · Trang 1/3"`** để không hiển thị số lượng phần tử/trang ở bất kỳ chỗ nào trên thanh phân trang?
2. **Ở Trường hợp 5 (Giới hạn chăm sóc cây ảo/ngày):**
   - Bạn muốn giữ nguyên tổng **`3 lượt/ngày` dùng chung** cho cả *Tưới nước + Bón phân* (như DB hiện tại `DailyTreeCareLimit = 3`, chỉ tách riêng nút **Nâng cấp cấp cây bằng Điểm Xanh** ra khỏi giới hạn này), hay muốn **tách riêng hạn mức mỗi loại** (ví dụ `3` lượt Tưới + `3` lượt Bón phân/ngày và lưu độ ẩm xuống DB qua 1 migration nhỏ)?

---

## 7. THỨ TỰ THỰC THI & KIỂM THỬ SAU KHI BẠN DUYỆT KẾ HOẠCH

1. **Pha 1 — Backend (`.NET 10`):** Chỉnh `VirtualTreeService.cs` (tách `WaterAndUpgradeAsync` khỏi `DailyWaterCount`), cập nhật Unit Test (`GamificationAndBlogUnitTests.cs`), chạy `dotnet build` & `dotnet test` trên `Ecolink.UnitTests` và `EcoLink.ApiTests`.
2. **Pha 2 — Frontend Shared (`PaginationBar.tsx` & `src/lib/*Api.ts`):** Đặt `FIXED_PAGE_SIZE = 10`, xóa control chọn/hiển thị số lượng/trang.
3. **Pha 3 — Frontend Pages:**
   - Sửa `Chat.tsx` + `user/Handover.tsx` (Trường hợp 1).
   - Sửa `recycler/Receipt.tsx` + `user/Receipt.tsx` (Trường hợp 2).
   - Sửa `recycler/Requests.tsx` + `Feedback.tsx` (Trường hợp 3).
   - Đồng bộ 22 trang dùng `PaginationBar` về `FIXED_PAGE_SIZE = 10` (Trường hợp 4).
   - Sửa `EcoFactsPage.tsx` + `user/Gamification.tsx` (Trường hợp 5).
4. **Pha 4 — Kiểm thử bắt buộc (`ecolink-web`):**
   - Chạy đủ 4 cổng: `npm run typecheck`, `npm run check`, `npm run check:routes`, `npm run build` và khôi phục `.check-routes/check-routes.js`.
