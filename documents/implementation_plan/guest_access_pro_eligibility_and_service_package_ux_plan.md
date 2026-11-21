# Kế Hoạch Phân Tích & Triển Khai: Giới Hạn Quyền Truy Cập Guest, Chặn Hạ Gói / Cộng Dồn PRO, Nâng Chuẩn Điều Kiện Lên PRO & Chuẩn Hóa Luồng Mua Gói Premium

> **Phạm vi:** Toàn diện cả **Backend (`.NET 10` — `intelligent-waste-sorting-trading`)** và **Frontend (`React 19 + TypeScript + Vite` — `ecolink-web`)**.  
> **Chuẩn tuân thủ:** Tuân thủ Clean Architecture 4 tầng, *Thin Controllers*, `BusinessRuleException` / `ConflictException` / `ForbiddenAccessException` theo [`clean_architecture_and_coding_guidelines.md`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/guideline/clean_architecture_and_coding_guidelines.md) và quy tắc `BR-AUTH-08`, `BR-PAY-01`..`BR-PAY-06`.  
> **Trạng thái:** Tài liệu phân tích kỹ thuật & kế hoạch triển khai chi tiết chờ duyệt trước khi sửa mã nguồn.

---

## 1. Tổng Quan & Bảng Tóm Tắt 4 Trường Hợp

| STT | Vấn đề người dùng nêu | Hiện trạng & Lỗ hổng trong Code | Quy chuẩn & Giải pháp Clean Coding | File tác động chính |
| :---: | :--- | :--- | :--- | :--- |
| **1** | **Giới hạn thao tác của Guest vào các chức năng chính** | Đối chiếu [`UC List`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/T%C3%A0i%20li%E1%BB%87u%20UC%20List%20ch%C6%B0a%20%C4%91%C6%B0%E1%BB%A3c%20ho%C3%A0n%20thi%E1%BB%87n.md#L3-L13) & `BR-AUTH-08`: Guest chỉ có `G-01..G-04`, nhưng hiện tại Guest vẫn mở được `/account/*`, bấm Điểm danh / Đổi quà giả ở `/rewards`, Tưới/Bón/Nâng cấp Cây ảo giả ở `/ecofacts`, Quét ảnh thật & Gửi báo cáo AI sai ở `/scanner` (`AllowAnonymous`), xem SĐT đầy đủ & báo cáo đánh giá ở `/map`, và Gửi khiếu nại giả ở `/feedback`. | Bọc `/account/*` trong `<RequireRole>`; giữ chế độ **chỉ xem công khai (`G-01`) + Quét thử ảnh mẫu** cho Guest, mọi thao tác nghiệp vụ (`Điểm danh`, `Đổi quà`, `Chăm cây`, `Tải/Chụp ảnh AI thật`, `Báo cáo AI sai`, `Đặt lịch`, `Chat`, `Xem SĐT`, `Báo cáo đánh giá`, `Gửi khiếu nại`) đều mở hộp thoại Đăng nhập `openAuthPopup("login")`; siết API Backend tương ứng. | **BE:** `WasteScanController.cs` <br> **FE:** `App.tsx`, `Rewards.tsx`, `CheckIn.tsx`, `EcoFactsPage.tsx`, `Scanner.tsx`, `Guidance.tsx`, `MapPage.tsx`, `Feedback.tsx` |
| **2** | **Khi mua gói cao hơn thì ẩn nút đăng ký gói thấp hơn & Không gia hạn cộng dồn cho gói PRO** | Khi Recycler đang dùng `PRO`, thẻ `PREMIUM` vẫn hiện nút `"Mua gói PREMIUM qua VietQR"`, thẻ `PRO` vẫn hiện `"Gia hạn cộng dồn (+365 ngày)"`; Backend chưa chặn mua `PREMIUM` khi đang có `PRO` hiệu lực và vẫn cộng dồn ngày cho `PRO` (`BR-PAY-04`). | Chuẩn hóa thứ bậc `BASIC (0) < PREMIUM (1) < PRO (2)`. Thêm `isIncludedInHigherPlan` & `canRenew` vào `ServicePackageCatalogDto`: nếu đang ở `PRO` thì **ẩn nút đăng ký** ở `BASIC`/`PREMIUM` (hiện badge *"Đã bao gồm trong gói Pro hiện tại"*) và **ẩn nút gia hạn** ở `PRO`. Backend chặn mua gói thấp hơn hoặc gia hạn `PRO` khi `PRO` chưa hết hạn. | **BE:** `ServicePackageDtos.cs`, `ServicePackageService.cs`, `PaymentService.cs`, `ServicePackageHelpers.cs` <br> **FE:** `servicePackageApi.ts`, `recycler/Home.tsx`, `user/Payout.tsx` |
| **3** | **Điều kiện xác thực khắt khe hơn khi lên PRO hoặc đăng ký lên PRO lại** | `EvaluateEligibility` ở [`ServicePackageService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ServicePackageService.cs#L425-L459) và [`PaymentService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PaymentService.cs#L151-L164) chỉ kiểm tra `VerificationStatus == Verified`, bỏ qua số lần bị cảnh cáo (`WarningCount`), điểm đánh giá sao (`AverageRating`, `TotalReviews`) và quy trình xác thực lại khi đăng ký lại PRO. | Xây dựng bộ kiểm định 4 tiêu chí dùng chung `ServicePackageHelpers.EvaluateProEligibilityAsync` trả về `ProEligibilityReportDto` (1. KYB & Hồ sơ kho bãi hợp lệ + kiểm tra xác thực lại khi tái đăng ký nếu có cảnh cáo kỳ trước; 2. Ngưỡng cảnh cáo an toàn `WarningCount <= 5` — bỏ qua điều kiện khiếu nại; 3. Đánh giá tích cực `AverageRating >= 4.0`, nếu tái đăng ký bắt buộc `TotalReviews >= 1`; 4. Không trùng gói `PRO` đang hiệu lực) + hiển thị Checklist trực quan ở `recycler/Home.tsx`. | **BE:** `ServicePackageDtos.cs`, `ServicePackageHelpers.cs`, `ServicePackageService.cs`, `PaymentService.cs`, `ServicePackagePaymentTests.cs` <br> **FE:** `servicePackageApi.ts`, `recycler/Home.tsx` |
| **4** | **Làm rõ & Chuẩn hóa luồng User mua gói Premium tắt quảng cáo** | Hiện User muốn mua `PREMIUM` (59.000đ) phải vào mục **"Tài khoản nhận tiền" (`/user/payout`)** — tên gọi gây hiểu lầm, trộn chung form Payout cũ và hiển thị lẫn cả thẻ gói `PRO` (1.000.000đ) của Recycler; Menu Avatar & Hồ sơ cá nhân thiếu lối tắt nâng cấp. | Tách lọc Backend chỉ trả gói phù hợp cho `User` (`BASIC` & `PREMIUM`, ẩn hoàn toàn gói `PRO` của Recycler); đổi nhãn điều hướng/tiêu đề sang **"Gói dịch vụ & Tắt quảng cáo"** (bổ sung alias `/user/subscription`, giữ `/user/payout` tương thích route); gắn lối vào nhanh trên Menu Avatar (`Navbar.tsx`), Hồ sơ (`Profile.tsx`) và `SponsoredAdBanner.tsx`. | **BE:** `ServicePackageService.cs` <br> **FE:** `portal.ts`, `App.tsx`, `user/Payout.tsx`, `Navbar.tsx`, `account/Profile.tsx`, `SponsoredAdBanner.tsx` |

---

## 2. Trường Hợp 1: Phân Tích Khả Năng Truy Cập Của Guest & Giới Hạn Thao Tác

### 2.1. Đối chiếu tài liệu nghiệp vụ & Rà soát lỗ hổng hiện tại

Theo [`Tài liệu UC List`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/T%C3%A0i%20li%E1%BB%87u%20UC%20List%20ch%C6%B0a%20%C4%91%C6%B0%E1%BB%A3c%20ho%C3%A0n%20thi%E1%BB%87n.md#L3-L49) và quy tắc [`BR-AUTH-08`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/documents/business_rule_%26_system_message.md#L35):
- **Phân hệ Guest** chỉ bao gồm 4 Use Cases:
  - `G-01`: Xem trang chủ và giới thiệu công khai.
  - `G-02`: Đăng ký tài khoản User.
  - `G-03`: Đăng ký tài khoản Thu mua (Recycler).
  - `G-04`: Đăng nhập hệ thống.
- Mọi chức năng tương tác có lưu trạng thái, gửi yêu cầu, tải tệp, nhận điểm, thương lượng, khiếu nại, đánh giá (`SU-01..SU-04`, `U-01..U-15`) đều yêu cầu **người dùng đã đăng nhập đúng vai trò**.

**6 lỗ hổng thực tế phát hiện trong mã nguồn hiện tại:**

1. **Route `/account/*` chưa được bảo vệ bởi `<RequireRole>` ([`App.tsx:71-75`](file:///c:/Projects/EcoLink/ecolink-web/src/App.tsx#L71-L75)):**
   - Guest truy cập trực tiếp `/account/profile`, `/account/notifications`, `/account/password` vẫn xem được khung giao diện hồ sơ cá nhân với số liệu giả lập và bấm nút lưu.
2. **Guest thao tác được Điểm danh & Đổi thưởng giả trên [`Rewards.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Rewards.tsx) và [`CheckIn.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/CheckIn.tsx#L50-L121):**
   - `CheckIn.tsx` chỉ kiểm tra `session?.role === "user" || session?.role === "recycler"`; nếu là Guest (`!session`), UI tự chạy nhánh `localStorage` cho phép Guest bấm **"Điểm danh"** nhận điểm giả và ở [`Rewards.tsx:630-640`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Rewards.tsx#L630-L640) cho phép Guest bấm **"Đổi ngay"** trừ điểm giả.
3. **Guest chăm sóc & nâng cấp Cây ảo giả trên [`EcoFactsPage.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/EcoFactsPage.tsx#L126-L180):**
   - Khi chưa đăng nhập, Guest có thể bấm Tưới nước, Bón phân, Thu hoạch và lưu tiến độ cây vào `localStorage["ecolink:virtual-tree-v1"]`.
4. **Guest gọi API quét ảnh AI thật & gửi báo cáo AI nhận diện sai ẩn danh ([`Scanner.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Scanner.tsx#L1001-L1050) & [`WasteScanController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/WasteScanController.cs#L44-L52,L285-L290)):**
   - `POST /api/v1/waste-scan/classify` và `POST /api/v1/waste-scan/feedback` đều đang mở `[AllowAnonymous]`, cho phép người lạ chưa đăng nhập upload ảnh tùy ý lên AI Service + R2 Storage và gửi báo cáo sai vào hàng đợi duyệt của Staff mà không có `UserId` (`BR-CLS-05`, `BR-CLS-08`).
5. **Lộ thông tin liên hệ & cho Guest thao tác trên [`MapPage.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/MapPage.tsx#L1180-L1280):**
   - Trong `PartnerDetail`, dòng 1180–1189 render trực tiếp số điện thoại thật (`tel:{partner.contactPhone}`) cho cả Guest (trong khi `BR-HND-08` quy định hạn chế hiển thị thông tin liên hệ đầy đủ); nút `"Đặt lịch bàn giao"`, `"Nhắn thương lượng"`, `"Báo cáo vi phạm"` chưa chủ động yêu cầu đăng nhập ngay tại UI.
6. **Guest gửi form Khiếu nại giả trên [`Feedback.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Feedback.tsx#L114-L117):**
   - Guest điền form khiếu nại và bấm Gửi sẽ nhận màn hình thành công giả với mã `makeRef()` dù chưa đăng nhập và không có giao dịch nào.

### 2.2. Giải pháp Clean Coding (Frontend + Backend)

#### A. Bảng ma trận quyền hạn mới của Guest (`G-01..G-04`)

| Chức năng / Đường dẫn | Guest được xem (Read-only) | Guest bị giới hạn & Hành động khi bấm |
| :--- | :--- | :--- |
| `/`, `/blog`, `/guidance`, `/waste`, `/terms`, `/leaderboard` | Xem đầy đủ nội dung giới thiệu, hướng dẫn phân loại, điều khoản, bảng xếp hạng ẩn danh (`BR-GAM-07`). | Không phát sinh thay đổi dữ liệu cá nhân. |
| `/account/*` (`/profile`, `/notifications`, `/password`) | **Bị chặn truy cập trực tiếp** (`<RequireRole>`). | Chuyển về `/` và tự mở hộp thoại đăng nhập nhỏ `CompactAuthModal`. |
| `/scanner` | Xem hướng dẫn 3 bước, quét thử **Ảnh mẫu Demo có sẵn** (`Dùng luôn ảnh mẫu để quét thử AI`). | Bấm **Chụp ảnh**, **Tải ảnh lên**, **Đổi ảnh**, hoặc **Báo cáo AI nhận diện sai** $\rightarrow$ mở `openAuthPopup("login")` yêu cầu đăng nhập. |
| `/map` | Tìm cơ sở thu mua quanh vị trí công khai, xem bảng giá, điểm sao trung bình. | Che số điện thoại thành dạng mờ (`0968 *** 233 · Đăng nhập để liên hệ`); bấm **Xem SĐT**, **Đặt lịch bàn giao**, **Nhắn thương lượng**, **Tạo yêu cầu thu gom** hoặc **Báo cáo vi phạm** $\rightarrow$ mở `openAuthPopup("login")`. |
| `/rewards` | Xem danh sách quà tặng / E-Voucher hiện có để tham khảo (ẩn số dư điểm giả `1.250` của mock data, hiển thị trạng thái *"Đăng nhập để tích điểm & đổi quà"*). | Bấm **Điểm danh** hoặc **Đổi quà** $\rightarrow$ mở `openAuthPopup("login")`. |
| `/ecofacts` | Xem minh họa Cây ảo cấp 1 và mục Eco Fact giới thiệu. | Nút **Tưới nước**, **Bón phân**, **Nâng cấp**, **Mở khóa Eco Fact** bị chuyển thành lời mời **"Đăng nhập để nuôi Cây ảo & mở Eco Fact"** $\rightarrow$ mở `openAuthPopup("login")`. |
| `/feedback` | Xem hướng dẫn hỗ trợ, FAQ & đường dây nóng/email trung tâm hỗ trợ. | Form gửi Khiếu nại/Góp ý khóa thao tác gửi (chặn `makeRef()` giả) với nút **"Đăng nhập để gửi khiếu nại / góp ý"** $\rightarrow$ mở `openAuthPopup("login")`. |

#### B. Chi tiết triển khai

1. **Backend ([`WasteScanController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/WasteScanController.cs)):**
   - Gắn `[Authorize]` cho `ClassifyImage` (`POST /api/v1/waste-scan/classify`) và `SubmitFeedback` (`POST /api/v1/waste-scan/feedback`) để bảo vệ tài nguyên AI Vision, R2 Storage và buộc gắn `UserId` vào `AiScanHistory` / `AIFeedbackLog` theo đúng `BR-CLS-05` và `BR-CLS-08`.
   - Giữ `[AllowAnonymous]` cho `GET /api/v1/waste-scan/models` và `GET /api/v1/waste-scan/rules` để Guest vẫn tra cứu được quy tắc phân loại công khai trên `/guidance`, `/waste`.
2. **Frontend (`ecolink-web`):**
   - Tạo helper dùng chung trong [`src/lib/session.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/session.ts) hoặc [`src/lib/popupAuth.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/popupAuth.ts):
     - `requireAuthenticatedAction(): boolean`: kiểm tra nếu `getSession()` tồn tại $\rightarrow$ trả `true`; ngược lại gọi `openAuthPopup("login")` và trả `false`.
     - `maskPhoneNumber(phone?: string | null): string`: hiển thị dạng `0968***233` cho Guest.
   - **[`src/App.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/App.tsx#L70-L76):** Bọc 3 route `/account/profile`, `/account/notifications`, `/account/password` trong `<RequireRole roles={["user", "recycler", "staff", "admin"]}><Layout /></RequireRole>` (hoặc `<RequireRole roles={["user", "recycler", "staff", "admin"]} />` ngay bên trong `<Layout>`).
   - **[`src/pages/Rewards.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Rewards.tsx) & [`src/components/CheckIn.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/CheckIn.tsx):**
     - Phân biệt rõ 3 ngữ cảnh: SSR (`isSsrSmokeTest()`), Live Logged-in User (`liveRewardsUser`), và Browser Guest (`!getSession() && !isSsrSmokeTest()`).
     - Với Browser Guest: không khởi tạo điểm giả từ `RANK_POINTS`/`localStorage`; khối số dư hiển thị `"—"` kèm CTA `"Đăng nhập để xem số dư & tích điểm"`; `CheckIn` & nút `Đổi ngay` gọi `requireAuthenticatedAction()`. (Nếu Backend đang chạy công khai `GET /api/v1/gamification/gifts`, ưu tiên fetch danh mục quà thật read-only cho Guest xem nếu endpoint cho phép, fallback `REWARDS` tĩnh ở chế độ đọc).
   - **[`src/pages/EcoFactsPage.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/EcoFactsPage.tsx):**
     - Với Browser Guest (`!session`): hiển thị banner mời đăng nhập tài khoản Người dân ở khối Cây ảo; chặn các nút *Tưới nước*, *Bón phân*, *Thu hoạch*, *Nâng cấp* bằng `requireAuthenticatedAction()` để Guest không tương tác giả vào `localStorage`.
   - **[`src/pages/Scanner.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Scanner.tsx) & [`src/pages/Guidance.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Guidance.tsx):**
     - Trong `Scanner.tsx`: tách rõ **"Dùng thử với ảnh mẫu Demo"** (chạy mô phỏng kết quả mẫu `r` không gọi upload R2 / AI API tốn tài nguyên khi chưa đăng nhập) và các nút thật (**Chụp ảnh**, **Tải ảnh lên**, **Gửi phản hồi AI nhận diện sai**) gọi `requireAuthenticatedAction()` trước khi mở file picker / camera / submit form.
     - Trong `Guidance.tsx` (section U-04 cuối trang): nếu Guest bấm gửi báo cáo AI sai, gọi `openAuthPopup("login")`.
   - **[`src/pages/MapPage.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/MapPage.tsx#L1180-L1293):**
     - Trong `PartnerDetail`: nếu `!session`, che số điện thoại (`maskPhoneNumber(partner.contactPhone)`) và bấm vào nút SĐT / **Đặt lịch bàn giao** / **Nhắn thương lượng** / **Báo cáo vi phạm** $\rightarrow$ gọi `openAuthPopup("login")` thay vì chuyển trang rồi mới bật popup. (Tương tự nút `"Tạo yêu cầu thu gom trước"` trong `FallbackPanel`).
   - **[`src/pages/Feedback.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Feedback.tsx):**
     - Khi `!session` (trên trình duyệt thật): hiển thị banner nhắc đăng nhập ngay trên form khiếu nại và đổi nút submit thành `"Đăng nhập để gửi khiếu nại / góp ý"` gọi `openAuthPopup("login")`, không chạy `makeRef()` giả cho Guest.

> [!NOTE]
> Mọi thay đổi trên đều giữ nguyên khả năng render cho SSR smoke tests ([`src/check.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/check.tsx) & [`src/check-routes.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/check-routes.tsx)) bằng cách kiểm tra `isSsrSmokeTest()` / giữ nguyên tiêu đề & text bắt buộc.

---

## 3. Trường Hợp 2: Ẩn Nút Đăng Ký Gói Thấp Hơn Khi Đã Mua Gói Cao Hơn & Không Gia Hạn Cộng Dồn Gói PRO

### 3.1. Phân tích hiện trạng

1. Ở [`recycler/Home.tsx:558-661`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Home.tsx#L558-L661):
   - Khi Recycler đang dùng **gói `PRO`** (`currentTier === "Pro"`), thẻ **`PREMIUM`** (gói thấp hơn) vẫn hiển thị nút `"Mua gói PREMIUM qua VietQR"`. Nếu bấm mua, người dùng phải trả thêm 59.000đ cho quyền lợi (tắt quảng cáo) vốn **đã nằm trọn bên trong gói PRO**.
   - Trên thẻ **`PRO`**, khi `isCurrent === true`, nút vẫn ghi `"Gia hạn cộng dồn (+365 ngày)"` — điều này cho phép Recycler tự cộng dồn nhiều năm liên tiếp trước khi qua vòng xác thực lại điều kiện uy tín (mâu thuẫn với Trường hợp 3).
2. Ở Backend ([`ServicePackageService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ServicePackageService.cs#L68-L102), [`PaymentService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PaymentService.cs#L151-L165), [`ServicePackageHelpers.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ServicePackageHelpers.cs#L194-L207)):
   - `ServicePackageHelpers.ActivateSubscriptionForOrderAsync` đang cộng dồn ngày hết hạn cho mọi gói cùng `PackageCode` (bao gồm cả `PRO`).
   - `CreatePayInOrderAsync` chưa chặn mua `PREMIUM` khi người dùng đang có gói `PRO` còn hiệu lực, và chưa chặn mua mới `PRO` khi kỳ `PRO` hiện tại vẫn đang hoạt động.

### 3.2. Giải pháp Clean Coding (Tách cấp bậc chuẩn & Cấm cộng dồn PRO)

1. **Định nghĩa chuẩn thứ bậc gói (`GetPackageTierRank` trong [`ServicePackageHelpers.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ServicePackageHelpers.cs)):**
   - `BASIC` (hoặc `IsDefaultFree`) $\rightarrow$ `rank = 0` (`"Basic"`)
   - `PREMIUM` (hoặc không phải Free & không có VerifiedBadge) $\rightarrow$ `rank = 1` (`"Premium"`)
   - `PRO` (hoặc `RequiresVerifiedKyb == true` / có `VerifiedBlueBadge`) $\rightarrow$ `rank = 2` (`"Pro"`)
2. **Mở rộng `ServicePackageCatalogDto` (`Ecolink.Application/Dtos/ServicePackageDtos.cs`):**
   - Thêm 2 thuộc tính boolean rõ nghĩa để Frontend không phải tự suy đoán:
     - `bool IsIncludedInHigherPlan`: `true` khi tài khoản đang có gói cao hơn gói này còn hiệu lực (VD: đang ở `PRO` $\rightarrow$ `BASIC` và `PREMIUM` đều `true`; đang ở `PREMIUM` $\rightarrow$ `BASIC` là `true`).
     - `bool CanRenew`: `false` đối với gói miễn phí `BASIC`, các gói có `IsIncludedInHigherPlan == true`, **và gói `PRO` đang còn hiệu lực** (chỉ cho phép `CanRenew = true` đối với `PREMIUM` khi người dùng đang ở `PREMIUM`).
3. **Siết nghiệp vụ trong Backend [`PaymentService.CreatePayInOrderAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PaymentService.cs#L124-L165) & [`ServicePackageHelpers.ActivateSubscriptionForOrderAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ServicePackageHelpers.cs#L178-L225):**
   - Trước khi tạo đơn `ServiceOrder`, truy vấn các `UserSubscription` còn hạn (`Status == Active && ExpiresAt > utcNow`) của `currentUserId`:
     - Nếu người dùng đã có gói ở bậc cao hơn gói đang định mua (VD: đang có `PRO` còn hạn mà tạo đơn `PREMIUM`): ném `BusinessRuleException("BR-PAY-04", "MSG-PAY-03", "Tài khoản của bạn đang sử dụng gói cao hơn (Pro) đã bao gồm đầy đủ quyền lợi này, không cần đăng ký gói thấp hơn.")`.
     - Nếu gói đang mua là **`PRO`** và tài khoản **đang có thuê bao `PRO` còn hiệu lực**: ném `BusinessRuleException("BR-PAY-04", "MSG-PAY-06", "Gói Pro Tích xanh không hỗ trợ gia hạn cộng dồn trước hạn. Vui lòng chờ kỳ hiện tại kết thúc và thực hiện xác thực lại điều kiện.")`.
   - Trong `ActivateSubscriptionForOrderAsync`:
     - Chỉ áp dụng cộng dồn từ `latestSamePackageExpiry` nếu **không phải gói `PRO`** (`!IsProTier(order.PackageCodeSnapshot)`). Gói `PRO` luôn có `startsAt = utcNow`.
4. **Cập nhật UI tại [`recycler/Home.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Home.tsx) & [`user/Payout.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Payout.tsx):**
   - Khi `pkg.isIncludedInHigherPlan === true` (hoặc suy ra `rank < currentRank`): **ẩn hoàn toàn nút đăng ký/thanh toán** và chỉ hiển thị nhãn gọn *"Đã bao gồm trong gói {entitlements.currentTier} hiện tại"*.
   - Khi `isPro && isCurrent`: **ẩn nút `"Gia hạn cộng dồn (+365 ngày)"`**, thay bằng khung thông tin thời hạn (*"Đang hiệu lực đến {dd/MM/yyyy} · Sẽ xét duyệt lại điều kiện khi hết kỳ"*).

---

## 4. Trường Hợp 3: Điều Kiện Xác Thực Khắt Khe Hơn Khi Lên PRO Hoặc Đăng Ký Lên PRO Lại

### 4.1. Phân tích hiện trạng

Hiện tại:
- [`ServicePackageService.EvaluateEligibility`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ServicePackageService.cs#L445-L456) và [`PaymentService.CreatePayInOrderAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PaymentService.cs#L151-L164) chỉ kiểm tra duy nhất `recyclerProfile.VerificationStatus == VerificationStatus.Verified`.
- Trong khi đó CSDL đã có đầy đủ các chỉ số chất lượng & uy tín nhưng **chưa được dùng để gác cổng gói Tích xanh PRO**:
  - [`User.cs:20,27`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/User.cs#L20-L30): `Status` (`UserStatus`), `WarningCount` (số lần bị cảnh cáo vi phạm), `ModeratedAt`.
  - [`AuthEntities.cs:30-45`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/AuthEntities.cs#L30-L45): `RecyclerProfile.VerificationStatus`, `ActiveStatus`, `AverageRating`, `TotalReviews`, `FacilityName`, `BusinessAddress`, `UpdatedAt`.

### 4.2. Giải pháp Clean Coding — Bộ Tiêu Chí Thẩm Định Tích Xanh PRO (`ProEligibilityReportDto`)

Để tránh trùng lặp logic giữa hiển thị bảng giá (`ServicePackageService`) và khởi tạo đơn thanh toán (`PaymentService`), gom toàn bộ quy tắc đánh giá điều kiện PRO vào một hàm bất đồng bộ dùng chung: `ServicePackageHelpers.EvaluateProEligibilityAsync(db, userId, currentRole, utcNow, ct)`.

**Cấu trúc 4 tiêu chí đánh giá (`ProEligibilityReportDto`):**

| Tiêu chí | Ngưỡng quy định (Strict Rule) | Ghi chú nghiệp vụ & Xác thực lại khi đăng ký lại (`isReRegistration`) |
| :--- | :--- | :--- |
| **1. Hồ sơ pháp lý KYB & Cập nhật hồ sơ** (`isKybAndProfileValid`) | • `User.Status == UserStatus.Active` <br> • `RecyclerProfile.VerificationStatus == VerificationStatus.Verified` <br> • Có đầy đủ `FacilityName`, `BusinessAddress`, `ContactPhone`. | • Nếu là **đăng ký lại PRO** (từng có ít nhất 1 `UserSubscription` gói `PRO` đã hết hạn hoặc bị thu hồi): nếu trong thời gian sau khi gói PRO cũ bắt đầu có phát sinh cảnh cáo (`User.ModeratedAt > previousProStart`) mà `RecyclerProfile.UpdatedAt <= User.ModeratedAt`, hệ thống yêu cầu **cập nhật & xác thực lại hồ sơ** trước khi đăng ký lại PRO. |
| **2. Số lần bị cảnh cáo trong giới hạn** (`isWarningLimitOk`) | • **`User.WarningCount <= 5`** (tối đa 5 lần cảnh cáo; từ lần cảnh cáo thứ 6 trở lên `WarningCount > 5` sẽ khóa quyền đăng ký / tái đăng ký PRO). <br> • **Bỏ qua điều kiện khiếu nại** (không chặn lên PRO theo phiếu khiếu nại). | • Cho phép đối tác vẫn có thể hoạt động bình thường nếu chỉ có nhắc nhở dưới ngưỡng `5` lần hoặc đang trong quá trình đối chiếu giao dịch, chỉ chặn khi vượt ngưỡng cảnh cáo cho phép. |
| **3. Mức đánh giá (Feedback) tích cực** (`isPositiveFeedbackMet`) | • Nếu đã có đánh giá (`TotalReviews > 0`): **`AverageRating >= 4.0m`** (tối thiểu $4.0 / 5.0$ sao). <br> • Nếu là **đăng ký lại gói PRO (`isReRegistration == true`)**: bắt buộc **`TotalReviews >= 1`** và **`AverageRating >= 4.0m`**. | • **Cơ chế an toàn cho cả đối tác mới lẫn tái đăng ký:** Cơ sở mới duyệt KYB chưa có giao dịch/đánh giá (`TotalReviews == 0`) vẫn có thể đăng ký PRO lần đầu nếu `WarningCount <= 5`; nhưng nếu **đăng ký lại PRO ở chu kỳ tiếp theo**, bắt buộc phải chứng minh được trải nghiệm thực tế tích cực ($\ge 1$ review và $\ge 4.0$ sao). |
| **4. Chu kỳ gói PRO** (`isNotCurrentlyActivePro`) | • Không có gói `PRO` nào đang ở trạng thái `Active` và `ExpiresAt > utcNow`. | • Gắn chặt với Trường hợp 2: không gia hạn cộng dồn trước hạn. |

> [!TIP]
> **Tính tương thích ngược Unit Test (`UTC001`–`UTC022`):**  
> Ở các test hiện tại (`UTC003`, `UTC004`, `UTC006`, `UTC009`, `UTC018`, `UTC020`), `RecyclerProfile` vừa được tạo với `VerificationStatus = Verified`, `WarningCount = 0`, `TotalReviews = 0`, chưa từng hết hạn PRO nên thỏa mãn đồng thời cả 4 tiêu chí. Chỉ khi có `WarningCount > 5`, `AverageRating < 4.0` (khi đã có review), hoặc tái đăng ký sau kỳ PRO mà không đủ review $\ge 4.0$ / chưa cập nhật xác thực lại hồ sơ sau cảnh cáo thì mới bị chặn.

**Hiển thị trên Frontend ([`recycler/Home.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Home.tsx)):**
- API `GET /api/v1/service-packages` trả kèm `proEligibility: ProEligibilityReportDto` trên thẻ `PRO`.
- Ngay trên thẻ **PRO** trong `recycler/Home.tsx`, hiển thị bảng checklist 4 dòng gọn gàng (`✓` màu xanh / `✕` màu cảnh báo kèm số liệu thật: *KYB Verified*, *Feedback: X.X ★ (Y lượt)*, *Cảnh cáo: N/5*, *Trạng thái kỳ PRO*) để Recycler biết chính xác mình đang vướng điều kiện nào nếu nút bị khóa, kèm nút dẫn tới `/account/profile` hoặc nút mở chỉnh sửa vị trí/hồ sơ cơ sở.

---

## 5. Trường Hợp 4: Làm Rõ & Chuẩn Hóa Luồng User Mua Gói Premium Tắt Quảng Cáo

### 5.1. Đánh giá hiện trạng — User hiện tại mua bằng cách nào và ở đâu?

1. **Cách User mua gói Premium hiện tại trong codebase:**
   - Gói `PREMIUM` (`59.000đ / 30 ngày`, quyền lợi `AdFree = true`, `EcoPlusBadge = true`, `DailyPointMultiplier = 1.2`) hiện đang được gắn bên trong trang **[`src/pages/user/Payout.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Payout.tsx#L268-L467)** ở route **`/user/payout`**.
   - Các điểm dẫn User vào mua gói hiện nay gồm:
     - Sidebar cổng Người dân ([`src/data/portal.ts:26`](file:///c:/Projects/EcoLink/ecolink-web/src/data/portal.ts#L26)): mục tên là **"Tài khoản nhận tiền"** (`/user/payout`).
     - Trang tổng quan Người dân ([`src/pages/user/Home.tsx:143,153`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Home.tsx#L143-L153)): chip `"Gói Basic · Nâng cấp Premium"` và banner [`SponsoredAdBanner.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/SponsoredAdBanner.tsx#L55-L61) nút `"Tắt quảng cáo"` $\rightarrow$ chuyển tới `/user/payout`.
2. **Tại sao luồng này hiện chưa hợp lý với người dùng?**
   - **Đặt sai tên & sai chỗ:** User nhìn menu `"Tài khoản nhận tiền"` sẽ không biết vào đó để mua gói **Premium tắt quảng cáo** (chưa kể trong cùng trang `Payout.tsx` vẫn còn khối form `"Tài khoản ngân hàng nhận tiền bán phế liệu"`).
   - **Hiển thị thừa gói PRO của Recycler cho User:** Trên [`Payout.tsx:310-413`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Payout.tsx#L310-L413), giao diện đang render cả 3 thẻ `BASIC`, `PREMIUM`, `PRO` (vì API `GET /api/v1/service-packages?targetRole=User` ở Backend [`ServicePackageService.cs:44-47`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ServicePackageService.cs#L42-L47) chưa lọc theo `currentRole` nếu truyền/không truyền query hoặc UI fallback `CANONICAL_FALLBACK_PACKAGES` gồm cả 3 gói). Dù thực tế User chỉ mua được duy nhất `PREMIUM`, việc hiện thẻ `PRO (1.000.000đ)` bị khóa dễ gây nhầm lẫn.
   - **Thiếu lối tắt từ thanh điều hướng chung:** User khi đang ở `/scanner`, `/map`, `/rewards`, `/blog`, `/account/profile` mở Menu Avatar góc trên bên phải ([`Navbar.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/Navbar.tsx#L205-L248)) không thấy hạng gói hiện tại hoặc mục `"Gói dịch vụ & Tắt quảng cáo"`.

### 5.2. Giải pháp Clean Coding — Chuẩn hóa điểm truy cập & trải nghiệm mua Premium cho User

1. **Lọc danh mục gói đúng vai trò ở Backend ([`ServicePackageService.cs:30-103`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ServicePackageService.cs#L30-L103)):**
   - Khi `currentRole == UserRole.User` (người dân phổ thông), luôn lọc danh mục chỉ còn các gói có `TargetRole == ServiceTargetRole.All || TargetRole == ServiceTargetRole.User` (`BASIC` và `PREMIUM`), **không trả gói `PRO` dành riêng cho Recycler**.
   - Đánh dấu `BASIC` là `IsIncludedInHigherPlan = true` khi User đang sử dụng `PREMIUM` (ẩn nút ở gói `BASIC`, hiện rõ đã nâng cấp).
2. **Chuẩn hóa Route, Sidebar & Tiêu đề trang cho User:**
   - Trong [`src/data/portal.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/portal.ts#L22-L28): đổi nhãn mục `/user/payout` ở Sidebar User thành **"Gói dịch vụ & Tắt quảng cáo"** (đồng thời khai báo thêm route alias `/user/subscription` trong [`App.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/App.tsx#L94) trỏ chung vào component này; giữ nguyên route `/user/payout` và text tương thích nếu [`check-routes.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/check-routes.tsx#L37) / [`check.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/check.tsx#L114) đang kiểm tra).
   - Trong [`src/pages/user/Payout.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Payout.tsx):
     - Chỉ render các gói thuộc `User`/`All` (`BASIC` và `PREMIUM` trên lưới 2 cột cân đối `lg:grid-cols-2`, lọc bỏ hoàn toàn thẻ `PRO` của Recycler khỏi UI User).
     - Đặt khu vực **"U-16 · Gói Premium (Tắt quảng cáo) & VietQR"** làm trọng tâm hàng đầu; cập nhật tiêu đề/eyebrow rõ ràng hơn để User dễ nhận biết ngay khi vừa mở trang.
     - Cập nhật [`check.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/check.tsx#L114) & [`check-routes.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/check-routes.tsx#L37) đồng bộ nếu đổi tiêu đề `<PageHead>`.
3. **Bổ sung lối tắt nâng cấp rõ ràng ở mọi điểm chạm của User:**
   - **[`src/components/Navbar.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/Navbar.tsx#L205-L248):** Trong dropdown Avatar khi đã đăng nhập, với vai trò `user` hiển thị badge hạng gói hiện tại (`useEntitlements()`) và một liên kết nổi bật **"Gói dịch vụ & Tắt quảng cáo"** $\rightarrow$ `/user/payout#citizen-packages` (còn với `recycler` là **"Gói Tích xanh Pro"** $\rightarrow$ `/recycler#recycler-packages`).
   - **[`src/pages/account/Profile.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/account/Profile.tsx#L199-L227):** Trong card `"Tài khoản của bạn"`, hiển thị dòng `"Gói dịch vụ"` (`Basic` / `Premium` / `Pro`) kèm nút nâng cấp nhanh tới đúng trang của vai trò.
   - **[`src/components/SponsoredAdBanner.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/SponsoredAdBanner.tsx):** Gắn nhãn rõ trên nút CTA là `"Tắt quảng cáo (Gói Premium)"` dẫn tới `/user/payout#citizen-packages` (hoặc `/recycler#recycler-packages` với Recycler), và chỉ hiển thị banner khi đã đăng nhập gói `Basic` (hoặc ở cổng User/Recycler).

---

## 6. Lộ Trình Triển Khai & Kế Hoạch Kiểm Thử

### Giai đoạn 1: Backend (`.NET 10`) — Auth Endpoint, Non-Stacking PRO & 4-Criteria PRO Eligibility
1. Cập nhật [`WasteScanController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/WasteScanController.cs): yêu cầu `[Authorize]` cho `ClassifyImage` và `SubmitFeedback` (`U-01`, `U-04`), giữ `[AllowAnonymous]` cho `models` và `rules`.
2. Mở rộng [`ServicePackageDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/ServicePackageDtos.cs) thêm `IsIncludedInHigherPlan`, `CanRenew`, và `ProEligibilityReportDto`.
3. Triển khai [`ServicePackageHelpers.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ServicePackageHelpers.cs), [`ServicePackageService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ServicePackageService.cs) và [`PaymentService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PaymentService.cs):
   - Tách hàm `EvaluateProEligibilityAsync` kiểm tra 4 tiêu chí nghiêm ngặt cho lần đầu và đăng ký lại PRO.
   - Chặn tạo đơn `PREMIUM` khi đang có `PRO`; chặn gia hạn cộng dồn cho `PRO` khi chưa hết hạn; lọc ẩn gói `PRO` cho tài khoản `User`.
4. Bổ sung các Unit Tests mới (`UTC023`..`UTC027`) trong [`ServicePackagePaymentTests.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.UnitTests/Module12_ServicePackages/ServicePackagePaymentTests.cs) để kiểm chứng:
   - Chặn mua gói `PREMIUM` khi đang có `PRO` hiệu lực & ẩn nút (`IsIncludedInHigherPlan = true`).
   - Chặn gia hạn cộng dồn `PRO` khi đang trong kỳ hạn (`CanRenew = false`).
   - Chặn lên `PRO` khi `WarningCount > 5`, khi `AverageRating < 4.0`, hoặc khi tái đăng ký `PRO` chưa đủ đánh giá / chưa xác thực lại sau cảnh cáo; đảm bảo vẫn cho phép lên `PRO` khi `WarningCount <= 5` kể cả khi có khiếu nại (không xét điều kiện khiếu nại).
   - Cho phép lên `PRO` khi thỏa mãn đầy đủ 4 tiêu chí.

### Giai đoạn 2: Frontend (`React + TypeScript`) — Guest Guards, PRO Checklist & User Premium Discovery
1. Nâng cấp [`src/lib/popupAuth.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/popupAuth.ts), [`src/lib/session.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/session.ts) và [`src/App.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/App.tsx) (bọc `/account/*` bằng `<RequireRole>` và thêm helper `requireAuthenticatedAction`, `maskPhoneNumber`).
2. Áp dụng giới hạn Guest nhất quán tại `Rewards.tsx`, `CheckIn.tsx`, `EcoFactsPage.tsx`, `Scanner.tsx`, `Guidance.tsx`, `MapPage.tsx`, `Feedback.tsx`.
3. Cập nhật [`src/lib/servicePackageApi.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/servicePackageApi.ts), [`src/pages/recycler/Home.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Home.tsx) (ẩn nút gói thấp hơn, bỏ cộng dồn PRO, hiển thị Checklist 4 điều kiện PRO).
4. Chuẩn hóa luồng mua Premium của User trên `portal.ts`, `user/Payout.tsx`, `Navbar.tsx`, `account/Profile.tsx`, `SponsoredAdBanner.tsx`, `check.tsx`, `check-routes.tsx`.

### Giai đoạn 3: Kiểm Thử Toàn Diện
1. **Backend:** Chạy `dotnet build` (`Ecolink.WebApi.csproj`) và toàn bộ `dotnet test` (`Ecolink.UnitTests.csproj`) đảm bảo `0` lỗi / `0` fail.
2. **Frontend (`c:\Projects\EcoLink\ecolink-web`):** Chạy `npm run typecheck`, `npm run check`, `npm run check:routes`, và `npm run build` đảm bảo tất cả các trang & route render chuẩn ở cả trạng thái đã đăng nhập và chưa đăng nhập.
