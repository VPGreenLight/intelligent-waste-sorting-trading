# Sổ tay Trình diễn 15 Phút Bảo vệ Đồ án EcoLink Trước Hội đồng

Tài liệu hướng dẫn điều hành buổi Demo trực tiếp (Live End-to-End Defense Runbook) đồng bộ 100% với bộ dữ liệu chuẩn từ `DemoDataSeeder` (`--reset-and-seed-demo`) tại khu vực **Khu CNC Hòa Lạc – Thạch Thất, Hà Nội**.

---

## 1. Lệnh Chuẩn bị & Reset Sạch Hệ thống (Trước Giờ Bảo vệ)

### 1.1. Dọn sạch & Seed lại toàn bộ CSDL `ecolink_db` (1 lệnh duy nhất)
> Thực hiện **trước khi khởi chạy WebAPI** (hoặc dừng WebAPI rồi chạy lại để giải phóng kết nối):

```powershell
Set-Location "c:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend\Ecolink.WebApi"
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --no-build -- --reset-and-seed-demo
```

**Dấu hiệu thành công:** Console in ra `=== EcoLink Council Demo Seeder Report (Reset=True) ===` với `WasteCategories=10`, `SortingRules=10`, `Users=28`, `RecyclerProfiles=15`, `HandoverRequests=164`, `HandoverReceipts=159`, `TradeOrders=164`, `TransactionReviews=155`, `BlogPosts=6`, `EcoFacts=10`, `Gifts=6`, `ServiceOrders=5` (CSDL đồng bộ đầy đủ **`16` EF Core Migrations**).

### 1.2. Khởi chạy đồng bộ toàn bộ Dịch vụ
```powershell
Set-Location "c:\Projects\EcoLink\intelligent-waste-sorting-trading"
.\run_all_services.ps1
```
- **Frontend Web (Vite):** `http://localhost:5173`
- **Backend Core API + SignalR:** `http://localhost:5000` (`/swagger`, `/health`)
- **AI Vision Service (FastAPI + YOLO26 Two-Stage):** `http://localhost:8000` (`/docs`, `/health`)

---

## 2. Bảng Tài khoản Quick-Login (Mặt dưới Form Đăng nhập `/login`)

| STT | Nhãn nút Quick-Login | Username | Mật khẩu | Vai trò | Trạng thái Sẵn sàng Cho Demo |
|:---:|:---|:---|:---|:---|:---|
| 1 | **Người dân** | `citizen` | `citizen123` | `User` | `250` Điểm xanh, Cây ảo **Lv 2**, Streak `3` ngày (**chưa điểm danh hôm nay**), `2` đơn active (`HND-DEMO-0001` `Pending`, `HND-DEMO-0002` `AwaitingConfirmation`) -> còn slot tạo mới ở `U-06` |
| 2 | **Công dân VIP** | `citizen_vip` | `citizen123` | `User` | Top #1 BXH Hòa Lạc, đã kích hoạt gói **PREMIUM** (tắt quảng cáo), Cây ảo **Lv 4** (`HarvestReady`), đơn `HND-DEMO-0003` (`Accepted`) |
| 3 | **Vựa EcoGreen** | `recycler` | `recycler123` | `Recycler` | *Vựa phế liệu EcoGreen Hòa Lạc* (`21.0138, 105.5256`, `5` đánh giá `4.80★`). Dùng nhận đơn, lập biên nhận & chứng minh **bị khóa mua `PRO`** do chưa đủ `50` đánh giá (`BR-PAY-04`) |
| 4 | **Vựa đủ chuẩn PRO (52★)** | `recycler_pro` | `recycler123` | `Recycler` | *Cơ sở Tái chế Xanh Hòa Lạc Pro* (`21.0165, 105.5282`, **`52` đánh giá `4.81★`**), **chưa kích hoạt `PRO`** -> Dùng **demo mua & kích hoạt gói `PRO` (`1.000.000đ`) trực tiếp qua VietQR** |
| 5 | **Nhân viên (7 quyền)** | `staff` | `staff123` | `Staff` (`NV-001`) | Đủ `7` mã quyền RBAC; có `5` ngày công lịch sử (**chưa chấm công hôm nay**) |
| 6 | **Nhân viên (2 quyền)** | `staff_support` | `staff123` | `Staff` (`NV-002`) | Chỉ có `2` quyền (`HANDLE_COMPLAINT`, `MANAGE_ACCOUNTS`) -> Chứng minh RBAC động (`BR-ADM-02`): ẩn menu & trả **`403 Forbidden`** khi gọi màn hình không được cấp quyền |
| 7 | **Quản trị viên** | `admin` | `admin123` | `Admin` | Toàn quyền hệ thống (`A-01..A-11`) |

**3 Hồ sơ Ứng viên KYB chờ Staff duyệt tại `/staff/applications` (`Mật khẩu: citizen123`):**
`applicant_hoalacxanh` (*Vựa Phế Liệu Hòa Lạc Xanh*), `applicant_minhkhai` (*Điểm Thu Gom Phế Liệu Minh Khai*), `applicant_anhtuan` (*Cơ Sở Thu Mua Tái Chế Anh Tuấn* — đều tọa lạc quanh Hòa Lạc / Thạch Thất).

---

## 3. Kịch bản Trình diễn 15 Phút Từng Bước (`Phút 0` $\rightarrow$ `Phút 15`)

### Chặng 1 (Phút 0–3): `Guest` & `Citizen` — Tin tức, Quét AI & Danh mục Rác Động
- **Màn hình:** `/blog` $\rightarrow$ `/guidance` $\rightarrow$ `/scanner`
- **Thao tác trực tiếp:**
  1. Mở `/blog`: Xem **`4` bài viết công khai** đã duyệt từ PostgreSQL (`BlogPosts` `Approved`).
  2. Đăng nhập nhanh **`citizen`** $\rightarrow$ Mở `/scanner` (`U-01`): Tải lên ảnh **chai nhựa PET / vỏ lon**, AI trả về kết quả phân loại + hộp quy tắc sơ chế 4 bước + cảnh báo an toàn từ `WasteRuleEngine`.
  3. Nhấn nút **Báo sai AI** (`U-04`) nếu cần tạo thêm ticket gửi sang hàng đợi Staff.
  4. Thử tải 1 ảnh **ngoài phân phối (OOD)** hoặc vật thể ngoài danh mục để minh họa cơ chế gán cờ `IsOutOfDistribution` và không ghép nhầm cơ sở thu mua cho nhóm `IsRecyclable = false` (`battery`, `biological`, `trash`).

### Chặng 2 (Phút 3–6): `Citizen` — Bản đồ PostGIS Hòa Lạc, Chat Thương lượng SignalR & Đặt lịch Thu gom
- **Màn hình:** `/map` $\rightarrow$ `/user/handover` $\rightarrow$ `/chat`
- **Thao tác trực tiếp:**
  1. Mở `/map` quanh **Khu CNC Hòa Lạc (`21.0124, 105.5253`)**: Chọn lọc vật liệu `plastic` / `metal` $\rightarrow$ Thấy **Vựa phế liệu EcoGreen Hòa Lạc** (`22222222-...`), **EcoPro Thạch Thất** (`22222223-...`) và các vựa `recycler_hl1..4` sắp xếp chuẩn theo khoảng cách GPS chính xác (`PostGIS ST_DWithin` / Haversine trực tiếp, không làm mờ vòng `~500m`). Khi mở rộng bán kính `> 28km` hoặc tìm tại khu vực Hà Nội sẽ thấy **Trạm Tái Chế Xanh Cầu Giấy** đạt **`4.87★` (`54` đánh giá)** gắn huy hiệu **Đối tác Pro**, cùng **Vựa Phế Liệu Sạch Đống Đa** (`4.71★ / 7`) và **Điểm Thu Mua Tái Chế Bách Khoa** (`4.63★ / 8`). Đối tác nhóm `Individual` hiển thị huy hiệu **Thu mua lưu động**.
  2. Vào `/user/handover` (`U-06`): Kiểm tra 2 đơn sẵn có (`HND-DEMO-0001` `Pending`, `HND-DEMO-0002` `AwaitingConfirmation`) và tạo **1 đơn thu gom mới** gửi tới *EcoGreen Hòa Lạc* (chứng minh **hạn mức tối đa 3 đơn active**, đồng thời **che SĐT trước khi duyệt** theo `BR-HND-08`).
  3. Mở `/chat` (`U-08`): Gửi tin nhắn & xem lịch sử thương lượng real-time với `EcoGreen` (`HND-DEMO-0001`).

### Chặng 3 (Phút 6–9): `Recycler` — Tiếp nhận Đơn, Cân Đo Lập Biên nhận & Nâng cấp Gói `PRO` Tích xanh
- **Màn hình:** `/recycler` $\rightarrow$ `/recycler/requests` $\rightarrow$ `/recycler/catalog` $\rightarrow$ `/user/payout` (hoặc mục Gói dịch vụ)
- **Thao tác trực tiếp:**
  1. Đổi sang tài khoản **`recycler`** (*Vựa EcoGreen*):
     - Mở `/recycler/requests` $\rightarrow$ Duyệt nhận đơn `HND-DEMO-0001` (`Pending` $\rightarrow$ `Accepted`, lúc này mới mở khóa SĐT người dân theo `BR-HND-08`).
     - Mở trang Gói dịch vụ: Chỉ ra **nút mua gói `PRO` bị khóa kèm thông báo lý do rõ ràng** do mới có `5/50` đánh giá (`BR-PAY-04`).
  2. Đổi sang tài khoản **`recycler_pro`** (*Tái chế Xanh Hòa Lạc Pro* — có sẵn **`52` đánh giá `4.81★`**):
     - Mở trang Gói dịch vụ $\rightarrow$ Tất cả 4 tiêu chí `PRO` đều đạt Xanh $\rightarrow$ Bấm **Nâng cấp `PRO` (`1.000.000 đ`)** $\rightarrow$ Hiện QR VietQR động $\rightarrow$ Bấm **Mô phỏng Thanh toán Thành công (`Simulated Exact`)** $\rightarrow$ Tự động kích hoạt Huy hiệu **Tích xanh** & **Ưu tiên ghim nổi bật bản đồ**.
  3. Quay lại **`recycler`** $\rightarrow$ Mở `/recycler/receipt`: Cân thực tế cho đơn `Accepted` vừa nhận (tải ảnh đồng hồ cân, chuyển sang `AwaitingConfirmation`).

### Chặng 4 (Phút 9–11): `Citizen` — Xác nhận Biên nhận Cộng Điểm, Chăm Cây Ảo, Eco Facts, Đổi Quà & Đánh giá
- **Màn hình:** `/user/receipt` $\rightarrow$ `/user/transactions` $\rightarrow$ `/rewards` $\rightarrow$ `/ecofacts` $\rightarrow$ `/leaderboard`
- **Thao tác trực tiếp:**
  1. Đăng nhập lại **`citizen`** $\rightarrow$ Mở `/user/receipt` cho đơn **`HND-DEMO-0002`** (**`RCP-DEMO-0002`**, `6.5 kg` kim loại): Bấm **Xác nhận Biên nhận** (`U-09`) $\rightarrow$ Điểm xanh tự động cộng vào ví (`UserPointWallet` & `UserEcoProfile`)!
  2. Đánh giá **`5★`** ngay cho đơn vừa hoàn tất (`U-10` — `BR-RVW-01`), và mở tab khiếu nại giới thiệu mẫu khiếu nại **`CMP-DEMO-0001`**.
  3. Mở mục **`Eco Fact`** (`/user/gamification`) $\rightarrow$ `/rewards` & `/ecofacts`:
     - Trên mục **`Eco Fact`** (`/user/gamification`): Trình diễn giao diện đã tinh gọn tập trung vào thẻ **Cây ảo WebP** + bộ lọc động **Cấp 1..4** đồng bộ thiết kế trực tiếp với `/ecofacts` (tách riêng chức năng Điểm danh / Đổi quà sang `/rewards`).
     - Tại `/rewards` & `/ecofacts`: Bấm **Điểm danh hàng ngày** (`Streak` chuyển `3` $\rightarrow$ **`4` ngày**, nhận thưởng cộng dồn ICT); tưới nước / bón phân cho **Cây ảo Lv 2** (`3` túi phân có sẵn) $\rightarrow$ Mở khóa thêm thẻ kiến thức tại `/ecofacts` (`10` facts cấp `1..4`); đổi **1 mã E-Voucher** từ `Gift` kho quà (hoặc kiểm tra voucher đã đổi **`ECOHL-GIFT10-8899`** ở `RDM-DEMO-0002`).
     - Mở `/leaderboard`: Xem Top BXH Hòa Lạc tháng hiện tại (`citizen_vip` Top #1).

### Chặng 5 (Phút 11–13): `Staff` — Chấm công, Duyệt KYB, Gán nhãn AI, Trọng tài Khiếu nại & Kiểm chứng RBAC `403`
- **Màn hình:** `/staff/checkin` $\rightarrow$ `/staff/applications` $\rightarrow$ `/staff/ai-review` $\rightarrow$ `/staff/dataset` $\rightarrow$ `/staff/complaints`
- **Thao tác trực tiếp:**
  1. Đăng nhập **`staff`** (`NV-001` — đủ `7` quyền):
     - `/staff/checkin` (`S-11`): Bấm **Check-in Ca làm việc hôm nay** (kế thừa chuỗi `5` ngày công lịch sử).
     - `/staff/applications` (`S-04`): Xem **`3` hồ sơ KYB đang chờ** (`applicant_hoalacxanh`, `applicant_minhkhai`, `applicant_anhtuan`) — minh họa mô hình **KYB 2 phân nhóm theo Điều 3 Nghị định 39/2007/NĐ-CP** (cơ sở cố định `Facility`: GPKD + đủ `5/5` ảnh kho bãi mới cho gửi; cá nhân thu mua nhỏ lẻ `Individual`: miễn GPKD, xác minh qua CCCD 2 mặt lưu nội bộ) $\rightarrow$ Thẩm định & Phê duyệt 1 hồ sơ.
     - `/staff/ai-review` (`S-01`) & `/staff/dataset` (`S-02`/`S-03`): Duyệt **`2` phản hồi AI `Pending`**, gán nhãn từ **`3` ảnh `Unlabeled`**, và xem bộ dữ liệu chuẩn **`v2.0.0`** (Train/Val/Test `70/20/10` chống rò rỉ `BR-CLS-07`).
     - `/staff/complaints` (`S-05`): Mở Hồ sơ Trọng tài 360° cho khiếu nại **`CMP-DEMO-0001`** (`Open`, sai lệch cân `14kg` vs `11.5kg`).
  2. Đăng nhập **`staff_support`** (`NV-002` — chỉ `2` quyền):
     - Vào được `/staff/complaints` (`200 OK`), nhưng **menu duyệt KYB / AI bị ẩn** và nếu truy cập thẳng `/staff/applications` hoặc API bị **chặn `403 Forbidden`** (`BR-ADM-02`).

### Chặng 6 (Phút 13–15): `Admin` — Phân quyền, Duyệt Blog, Đối soát VietQR, Báo cáo & Audit Log
- **Màn hình:** `/admin/permissions` $\rightarrow$ `/admin/blog` $\rightarrow$ `/admin/reconciliation` $\rightarrow$ `/admin` & `/admin/reports`
- **Thao tác trực tiếp:**
  1. Đăng nhập **`admin`**:
     - `/admin/permissions` (`A-04`): Xem ma trận quyền của `NV-001` (`7` quyền) & `NV-002` (`2` quyền); bật/tắt thử 1 quyền để minh họa hiệu lực tức thời không cần đăng nhập lại (`AuthStateCacheInvalidator`).
     - `/admin/blog` (`A-09`): Duyệt bài viết đang nằm ở trạng thái **`Pending_Approval`** để xuất bản lên `/blog`.
     - `/admin/reconciliation` (`A-11`): Xử lý giao dịch bất thường **`ORD-SEED-PRO-ANOM`** (*Underpaid*: chuyển `500.000đ` / `1.000.000đ`) và webhook mồ côi `ECOLINK_UNKNOWN_999`.
     - `/admin` & `/admin/reports` (`A-01`, `A-02`): Xem KPI toàn hệ thống, tải **báo cáo CSV đã che mờ PII** (`BR-ADM-06`) và kiểm tra nhật ký bất biến **`AuditLog`** vừa phát sinh (`BR-ADM-05`).

---

## 4. Checklist Quy tắc Nghiệp vụ Trọng tâm Cần Nhấn mạnh Trước Hội đồng

- [x] **`BR-SRT-04` (Soft Deactivate & Zero Match for Non-Recyclable):** Vật phẩm `battery` / `trash` / `biological` trả về hướng dẫn an toàn, không ghép đơn thu mua thương mại; danh mục ngừng sử dụng giữ nguyên lịch sử giao dịch.
- [x] **`BR-HND-08` (Privacy Before Acceptance):** Ẩn số điện thoại công dân cho tới khi Recycler bấm Chấp nhận đơn (`Accepted`).
- [x] **`BR-HND-07` & `BR-GAM-03` (Immutable Receipt & Atomic Wallet/Inventory):** Biên nhận sau khi Citizen xác nhận được khóa vĩnh viễn và cộng điểm nguyên tử; đổi quà trừ kho không thể âm.
- [x] **`BR-PAY-04` (PRO Eligibility Gate):** Tự động khóa nút mua `PRO` khi `< 50` đánh giá (`recycler`) và mở khóa đầy đủ khi đạt chuẩn (`recycler_pro`: `52` lượt, `4.81★`).
- [x] **`BR-CLS-07` (Sticky Stratified Split 70/20/10):** Phiên bản Dataset `v2.0.0` đóng băng split cố định từng `ImageSha256`, chống rò rỉ dữ liệu Train/Test giữa các phiên bản.
- [x] **`BR-MTH-01` (2-Tier KYB Onboarding per NĐ 39/2007/NĐ-CP):** Phân tách `Facility` (GPKD + đủ `5/5` ảnh kho bãi) và `Individual` (miễn GPKD, bắt buộc CCCD 2 mặt lưu nội bộ, gắn nhãn *"Thu mua lưu động"* trên `/map`).
- [x] **`BR-ADM-02`, `BR-ADM-05`, `BR-ADM-06` (Dynamic Fine-Grained RBAC, Append-Only Audit Log & PII Masking):** Đã kiểm chứng tự động qua `OnboardingTests`, `GovernanceTests`, `AuditLogImmutabilityInterceptor` và xuất CSV báo cáo (`1,135 / 1,135` Unit Tests Passed).
