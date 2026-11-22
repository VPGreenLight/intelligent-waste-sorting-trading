# Kế Hoạch Phân Tích Lỗi, Thiết Kế Giải Pháp & Clean Coding — 5 Trường Hợp Nghiệp Vụ & Kiến Trúc (EcoFact, Eco Point, Khiếu Nại 2 Chiều, Thống Kê Đa Chiều & Loại Bỏ Payout)

> **Phạm vi:** Toàn diện cả **Backend (.NET 10 — `intelligent-waste-sorting-trading`)** và **Frontend (`React 19 + TypeScript + Vite` — `ecolink-web`)**.  
> **Trạng thái:** Tài liệu phân tích & thiết kế chi tiết chờ duyệt trước khi triển khai code.

---

## 1. Tổng Quan & Bảng Tóm Tắt 5 Trường Hợp

| STT | Trường hợp nghiệp vụ / Bất cập | Nguyên nhân gốc rễ (Root Cause) | Giải pháp Clean Coding | Nhóm file tác động chính |
| :---: | :--- | :--- | :--- | :--- |
| **1** | **Trích dẫn nguồn EcoFact hỏng / không hiệu quả** | Một số bản ghi `eco_facts` có `source_url` trỏ tới link sâu 404 hoặc domain mẫu; `CleanHttpUrl` mới kiểm tra cú pháp URI, chưa chặn placeholder/link hỏng; FE hiển thị trực tiếp URL thô mà không có bộ lọc nguồn hợp lệ. | Bổ sung `EnsureValidEcoFactSourcesAsync` tự chuẩn hóa sang các cổng chính thống ổn định (`unep.org`, `epa.gov`, `vanban.chinhphu.vn`) hoặc đặt `null` nếu không khớp; FE tạo helper `resolveValidEcoFactSource()` tự ẩn nguồn lỗi và thêm nút `"Xoá nguồn"` cho Staff. | `EcoFactService.cs`, `VirtualTreeService.cs`, `GamificationCommon.cs`, `src/lib/ecoFactSource.ts` *(mới)*, `EcoFactsPage.tsx`, `user/Gamification.tsx`, `staff/EcoFacts.tsx`, `data/ops.ts` |
| **2** | **Hoàn thành giao dịch / xác nhận biên nhận không cộng điểm Eco Point** | (1) `PointsService.AwardHandoverPointsAsync` trả `0` khi dòng `HandoverRequestItems` chưa gắn `ActualWeightKg` (dù `receipt.TotalWeightKg > 0`); (2) `TradeTransactionService.ConfirmReceiptByQrAsync` nhánh PostgreSQL `dbOrder` bỏ sót gọi cộng điểm; (3) `AuthService` đọc điểm từ `UserEcoProfiles` không đồng bộ từ `UserPointWallets`; (4) FE `confirmHandoverReceipt` không gọi `fetchPointWallet()` / cập nhật `setSpendPoints()` và session sau khi xác nhận. | Fallback tính điểm theo `receipt.TotalWeightKg * DefaultPerKg` khi thiếu `ActualWeightKg` trên item; thống nhất **Single Source of Truth** từ `UserPointWallets` đồng bộ về `UserEcoProfiles` và `/api/v1/auth/me`; bổ sung cộng điểm ở `ConfirmReceiptByQrAsync`; FE đồng bộ ngay ví điểm & session khi xác nhận biên nhận. | `PointsService.cs`, `HandoverReceiptService.cs`, `HandoverTradeSyncHelper.cs`, `AuthService.cs`, `TradeAndGamificationServices.cs`, `recyclerWorkflowApi.ts`, `user/Receipt.tsx` |
| **3** | **Khiếu nại của User & Recycler (đặc biệt Recycler không gửi được sau khi User xác nhận biên nhận & gửi feedback)** | (1) Backend `ComplaintService.SubmitComplaintAsync` chưa fallback `EnsureHandoverFromTradeOrderAsync` khi đơn đi từ `TradeOrders` và kiểm tra chặt `handover.RecyclerId == callerUserId` gây `403` nếu lệch ID cơ sở/nhà tái chế; (2) Trên `/recycler/requests`, `actionError` render ở đỉnh trang nên bị khuất khi mở form inline dưới thẻ đơn, ô ảnh chỉ nhận text URL (không có `<input type="file">`) gây `400`; (3) Trên `/user/receipt` sau `Completed` chỉ ẩn nút dispute biên nhận mà thiếu form `POST /api/v1/complaints`; (4) Trên `/feedback`, `selectedOrderId` mặc định rỗng khiến form rơi về nhánh offline giả lập. | Mở rộng đối chiếu quyền Recycler (`RecyclerUserId` / `CollectionPartner.UserId`) và tự đồng bộ `HandoverRequest` từ `TradeOrder` trong `ComplaintService`; đưa thông báo lỗi/thành công + nút upload ảnh R2 vào trong form khiếu nại inline tại `recycler/Requests.tsx`; thêm luồng khiếu nại hậu giao dịch (`POST /api/v1/complaints`) & hiển thị review trên cả `user/Receipt.tsx` và `recycler/Requests.tsx`; tự động chọn đơn mặc định trên `Feedback.tsx`. | `ComplaintService.cs`, `ReviewService.cs`, `recycler/Requests.tsx`, `user/Receipt.tsx`, `Feedback.tsx`, `reviewComplaintApi.ts` |
| **4** | **Thiết kế lại phần thống kê với bộ lọc thời gian chi tiết** | `GET /api/v1/admin/analytics/kpi-summary` chỉ hỗ trợ `7d \| 30d \| 90d \| all` và `FromDate`/`ToDate`; `GET /api/v1/admin/analytics/charts` chỉ nhận `months = 6` cố định theo tháng, chưa lọc `WasteCategoryBreakdown` hay chia bucket động; FE `/admin` và `/admin/reports` thiếu bộ lọc Năm / Quý / Tháng / Tuần / Từ ngày - Đến ngày / Trong ngày. | Mở rộng `ExecutiveKpiQueryRequest` dùng chung cho KPI summary, biểu đồ và export báo cáo theo 6 chế độ (`day`, `week`, `month`, `quarter`, `year`, `range`); chia bucket biểu đồ xu hướng động theo giờ / ngày trong tuần / ngày trong tháng / tháng trong quý / tháng trong năm; tạo component `AnalyticsTimeFilterBar.tsx` tích hợp cho `admin/Home.tsx` và `admin/Reports.tsx`. | `GovernanceDtos.cs`, `IGovernanceServices.cs`, `AdminAnalyticsController.cs`, `AdminAnalyticsService.cs`, `governanceApi.ts`, `AnalyticsTimeFilterBar.tsx` *(mới)*, `admin/Home.tsx`, `admin/Reports.tsx` |
| **5** | **Loại bỏ Payout — Chỉ lưu Lịch sử đơn biên nhận & Lịch sử thanh toán dịch vụ (Pay-in)** | Hệ thống không chi hộ/giải ngân tiền bán phế liệu qua ngân hàng (`BR-HND-05`, `BR-PAY-01`), nhưng `data/cashflow.ts`, `user/Payout.tsx`, `user/Transactions.tsx`, `data/portal.ts` và một số màn hình vẫn chứa form nhập STK ngân hàng `"PayOS bắn tiền về tài khoản"`, dữ liệu `kind: "payout" \| "fee"` (`FEE_RATE = 0.08`) và dead-code `ExecutePayoutAsync` ở Backend. | Xoá toàn bộ dữ liệu/UI/dead-code mang tính Payout & trích phí 8%; giữ và tách bạch rõ **Lịch sử đơn biên nhận bàn giao** (đối chiếu khối lượng & Điểm Xanh) và **Lịch sử thanh toán gói dịch vụ VietQR (Pay-in)**; chuẩn hóa lại nhãn điều hướng, thống kê và bảng dữ liệu trên FE. | `data/cashflow.ts`, `data/ops.ts`, `data/portal.ts`, `user/Payout.tsx`, `user/Transactions.tsx`, `user/Receipt.tsx`, `user/Home.tsx`, `account/Profile.tsx`, `recycler/Home.tsx`, `admin/Home.tsx`, `admin/Reports.tsx`, `components/Assistant.tsx`, `check.tsx`, `check-routes.tsx`, `PayOsDtos.cs`, `ApplicationInterfaces.cs`, `PayOsService.cs` |

---

## 2. Phân Tích Chi Tiết & Thiết Kế Giải Pháp Từng Trường Hợp

### Trường hợp 1: Sửa lại trích dẫn nguồn EcoFact (Xoá nguồn hỏng & Chuẩn hóa link chính thống hoạt động hiệu quả)

#### 2.1.1. Phân tích nguyên nhân gốc rễ
1. **Dữ liệu `SourceUrl` trong DB (`eco_facts`) thiếu kiểm định tính ổn định:**
   - Bảng `eco_facts` lưu cột `source_url` (có thể `null`) thông qua [`GamificationEntities.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/GamificationEntities.cs) và [`EcoFactConfiguration`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Data/Configurations/GamificationConfigurations.cs#L177-L197).
   - Một số dữ liệu seed/tạo cũ chứa URL sâu đã đổi đường dẫn (404) hoặc domain giả lập (`example.com`, `localhost`), dẫn đến người dùng bấm vào link `"Nguồn ↗"` gặp trang lỗi.
2. **Backend kiểm tra URL còn lỏng:**
   - Hàm [`GamErrors.CleanHttpUrl`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GamificationCommon.cs#L49-L57) chỉ kiểm tra `Uri.UriSchemeHttp` / `Uri.UriSchemeHttps`, chưa loại trừ các host giả lập (`example.com`, `example.org`, `localhost`, `127.0.0.1`, `test.local`).
   - [`EcoFactService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/EcoFactService.cs) và [`VirtualTreeService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/VirtualTreeService.cs) chưa có cơ chế tự rà soát/làm sạch `SourceUrl` cũ như mẫu `EnsureSeededGiftImagesAsync` trong [`GiftService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GiftService.cs#L566-L613).
3. **Frontend hiển thị chuỗi URL thô và thiếu đồng bộ:**
   - Trên [`EcoFactsPage.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/EcoFactsPage.tsx#L980-L1059), nút `"Nguồn ↗"` ở card và dòng `"Nguồn tham khảo: {selectedFact.sourceUrl} ↗"` trong modal hiển thị nguyên URL thô nếu `sourceUrl` khác rỗng, không kiểm tra xem URL có thuộc nguồn hợp lệ/chết hay không.
   - Trên [`user/Gamification.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Gamification.tsx#L286-L309) và dữ liệu mẫu [`data/ops.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/ops.ts#L290-L306) chưa thống nhất cách hiển thị hoặc ẩn nguồn.
   - Trên [`staff/EcoFacts.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/EcoFacts.tsx#L170-L177), Staff muốn gỡ nguồn nhanh phải bôi đen xoá tay trong input thay vì có thao tác 1-click.

#### 2.1.2. Giải pháp Clean Coding
1. **Backend (`.NET 10`):**
   - Trong [`GamificationCommon.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/GamificationCommon.cs):
     - Tách danh sách `InvalidPlaceholderHosts` (`example.com`, `example.org`, `example.net`, `localhost`, `127.0.0.1`, `0.0.0.0`, `test.local`).
     - Cập nhật `CleanHttpUrl`: từ chối các URL thuộc `InvalidPlaceholderHosts` (hoặc có host chỉ là placeholder), yêu cầu rõ ràng `http://` hoặc `https://` hợp lệ từ cơ quan/tổ chức uy tín.
   - Tạo helper nội bộ `EcoFactSourceCatalog` dùng chung cho `EcoFactService` và `VirtualTreeService`:
     - Ánh xạ chủ đề từ khóa trong `Title` / `Content` sang **URL chính thống cấp cổng / trang trụ cột lâu dài (permalink ổn định)** đã kiểm chứng:
       - **Nhựa / PET / Ô nhiễm nhựa:** `https://www.unep.org/interactives/beat-plastic-pollution/` *(UNEP)*
       - **3R / Phân loại rác tại nguồn / Thùng rác:** `https://www.epa.gov/recycle` *(US EPA)*
       - **Pin Li-ion / Rác thải điện tử:** `https://www.epa.gov/recycle/used-household-batteries` *(US EPA)*
       - **Giấy / Bìa Carton / Kim loại / Thủy tinh:** `https://www.epa.gov/recycle/how-do-i-recycle-common-recyclables` *(US EPA)*
       - **EPR / Chính sách môi trường Việt Nam:** `https://vanban.chinhphu.vn/?pageid=27160&docid=202613` *(Cổng TTĐT Chính phủ — Luật Bảo vệ môi trường)*
     - Bổ sung hàm idempotent `EnsureValidEcoFactSourcesAsync(ApplicationDbContext db, CancellationToken ct)` (chạy 1 lần với `SemaphoreSlim` giống `GiftService.EnsureSeededGiftImagesAsync`):
       - Nếu `f.SourceUrl` hiện tại là URL giả lập, link HTTP không bảo mật, hoặc trỏ vào đường dẫn chết không nằm trong allow-list domain uy tín (`unep.org`, `epa.gov`, `who.int`, `worldbank.org`, `vanban.chinhphu.vn`, `monre.gov.vn`), tự động thay bằng URL chính thống khớp chủ đề hoặc gán `null` (xoá nguồn) nếu là kiến thức tổng quát không gắn trích dẫn cụ thể.
2. **Frontend (`React + TypeScript`):**
   - Tạo module dùng chung [`src/lib/ecoFactSource.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/ecoFactSource.ts) *(mới)*:
     - `resolveValidEcoFactSource(rawUrl?: string | null): { url: string; label: string } | null`:
       - Parse bằng `new URL(...)`, chỉ chấp nhận `https:` (hoặc `http:` khi dev cần nhưng ưu tiên `https:`), loại bỏ các host placeholder / domain chết.
       - Nếu không hợp lệ $\rightarrow$ trả về `null` (giao diện ẩn hoàn toàn link nguồn).
       - Nếu hợp lệ $\rightarrow$ trả về `{ url, label }` với `label` là tên tổ chức/hostname gọn gàng (VD: `"UNEP (unep.org)"`, `"US EPA (epa.gov)"`, `"Cổng TTĐT Chính phủ (vanban.chinhphu.vn)"`, hoặc `urlObj.hostname.replace(/^www\./, "")`).
   - Cập nhật [`EcoFactsPage.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/EcoFactsPage.tsx#L980-L1059) và [`user/Gamification.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Gamification.tsx#L286-L309):
     - Dùng `resolveValidEcoFactSource(fact.sourceUrl)` trước khi render. Không hiển thị URL thô khó đọc hoặc đường dẫn hỏng.
   - Cập nhật [`staff/EcoFacts.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/staff/EcoFacts.tsx#L170-L177):
     - Thêm nút **"Xoá nguồn"** cạnh input `URL nguồn tham khảo (tuỳ chọn)` để Staff đặt `sourceUrl = ""` (gửi `null` lên API để xoá `source_url` trong DB) nhanh chóng, và thêm gợi ý nhanh các link chính thống.
   - Cập nhật [`data/ops.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/ops.ts#L290-L306): thêm trường tùy chọn `sourceUrl?: string` (hoặc giữ `undefined` cho dữ liệu không cần nguồn, gán đúng link chính thống cho các mục có số liệu pháp lý như `EF-01`, `EF-02`, `EF-03`, `EF-06`).

---

### Trường hợp 2: Hoàn thành giao dịch / xác nhận biên nhận thì không cộng điểm Eco Point

#### 2.2.1. Phân tích nguyên nhân gốc rễ (4 điểm đứt gãy thực tế trong code)
Qua kiểm tra toàn bộ luồng từ UI $\rightarrow$ API $\rightarrow$ DB:
1. **`PointsService.AwardHandoverPointsAsync` tính điểm bằng 0 khi dòng `HandoverRequestItems` thiếu `ActualWeightKg`:**
   - Tại [`PointsService.cs:158-166`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PointsService.cs#L158-L166):
     ```csharp
     var lines = await _db.HandoverRequestItems.AsNoTracking()
         .Where(i => i.RequestId == receipt.RequestId && i.ActualWeightKg != null)
         .Select(i => new { Code = i.WasteCategory!.Code, Kg = i.ActualWeightKg!.Value })
         .ToListAsync(ct);

     var raw = lines.Sum(l => l.Kg * settings.RateFor(l.Code));
     var points = (long)Math.Round(raw, 0, MidpointRounding.AwayFromZero);
     if (points <= 0) return 0;
     ```
   - Nếu `HandoverRequestItems` chưa có `ActualWeightKg` (hoặc tỷ lệ `RateFor` của một danh mục bị cấu hình `0`, hoặc `WasteCategory` bị null trong truy vấn join), `points <= 0` làm hàm thoát sớm $\rightarrow$ **không cộng vào `user_point_wallets`**, **không ghi `point_transactions`**, dù `receipt.TotalWeightKg > 0` và `HandoverReceiptService.CalculatePoints` đã hiển thị dự kiến cộng điểm!
2. **Hai hệ thống bảng điểm (`UserPointWallets` vs `UserEcoProfiles`) thiếu đồng bộ một chiều rõ ràng:**
   - Trong [`HandoverReceiptService.cs:375-389`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs#L375-L389): `CompleteAsync` tính `receipt.PointsAwarded = CalculatePoints(receipt.TotalWeightKg)` (luôn lấy cố định `10đ/kg`), sau đó gọi `_gamification.AwardPointsForRecyclingAsync(...)`, rồi `_points.AwardHandoverPointsAsync(...)`, rồi `SyncReceiptCompletionAsync(...)` (lại gọi `UpsertUserEcoProfilePointsAsync`!).
   - Hệ quả:
     - `UserEcoProfile.CurrentGreenPoints` có thể bị cộng sai lệch (thậm chí cộng kép hoặc lệch với `PointRateRules`), trong khi `UserPointWallets.CurrentBalance` (nguồn thật của Trang Cây Ảo, Đổi Quà, Check-in) lại dựa vào `PointsService.AwardHandoverPointsAsync` vốn rất dễ rơi về `0` hoặc nuốt exception trong khối `try/catch` dòng 384!
     - Tại [`AuthService.cs:769`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AuthService.cs#L769) (`MapToUserInfoDto` phục vụ `GET /api/v1/auth/me` & `POST /api/v1/auth/login`), hệ thống chỉ đọc `user.EcoProfile?.CurrentGreenPoints ?? 0` chứ **không đọc `UserPointWallets.CurrentBalance`**.
3. **Nhánh PostgreSQL của `TradeTransactionService.ConfirmReceiptByQrAsync` bỏ sót cộng điểm:**
   - Tại [`TradeAndGamificationServices.cs:528-563`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/TradeAndGamificationServices.cs#L528-L563), khi tìm thấy `dbOrder` trong DB, hàm tạo `TransactionReceipt` rồi `return` ở dòng 554 mà **không gọi `AwardPointsForRecyclingAsync` hay `CreditAsync`**.
4. **Frontend không đồng bộ lại Ví Điểm Xanh & Session ngay sau khi xác nhận biên nhận:**
   - Tại [`recyclerWorkflowApi.ts:1898-1942`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/recyclerWorkflowApi.ts#L1898-L1942), `confirmHandoverReceipt(requestId)` nhận về `ReceiptDto` xong trả thẳng cho UI mà **không gọi lại `fetchPointWallet()`** (để cập nhật `setSpendPoints(...)` và bắn event `"ecolink:points-change"`) và **không gọi lại `fetchCurrentUserProfile()`** (để cập nhật `session.currentGreenPoints`, `session.totalRecycledKg`).
   - Tại [`user/Receipt.tsx:311-363`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Receipt.tsx#L311-L363), sau khi User bấm xác nhận biên nhận xong, màn hình không hiện rõ thông báo đã cộng bao nhiêu Điểm Xanh vào ví.

#### 2.2.2. Giải pháp Clean Coding (Single Source of Truth)
1. **Backend (`PointsService.cs`, `HandoverReceiptService.cs`, `HandoverTradeSyncHelper.cs`, `TradeAndGamificationServices.cs`, `AuthService.cs`):**
   - **Thống nhất nguồn sự thật là `UserPointWallets` + `PointTransactions`:**
     - Trong [`PointsService.AwardHandoverPointsAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PointsService.cs#L148-L194):
       - Tính tổng điểm từ `HandoverRequestItems` có `ActualWeightKg != null`; **nếu `lines.Count == 0` hoặc tổng điểm `<= 0` trong khi `receipt.TotalWeightKg > 0`**, tự động fallback sang `receipt.TotalWeightKg * settings.DefaultPerKg` (làm tròn nguyên, tối thiểu `1` điểm khi `receipt.TotalWeightKg > 0`).
       - Đảm bảo `CreditAsync` chạy cho cả `receipt.UserId` và `receipt.RecyclerId` với `idempotencyKey = $"HANDOVER:{receipt.Id}:USER"` và `$"HANDOVER:{receipt.Id}:RECYCLER"` để chống cộng trùng 100%.
       - Ngay sau khi cập nhật `UserPointWallets`, đồng bộ bảng cache legacy `UserEcoProfiles` bằng **giá trị tuyệt đối từ ví**:
         - `eco.CurrentGreenPoints = (int)Math.Min(int.MaxValue, wallet.CurrentBalance)`
         - `eco.TotalLifetimePoints = (int)Math.Min(int.MaxValue, wallet.TotalEarned)`
         - (Thay vì cộng dồn mù `+= points` ở nhiều nơi gây cộng thiếu hoặc cộng đúp!)
     - Trong [`HandoverReceiptService.CompleteAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs#L336-L408):
       - Sử dụng kết quả điểm thực tế trả về từ `_points.AwardHandoverPointsAsync(receipt.Id, ct)` để gán `receipt.PointsAwarded` (nếu `_points != null`) trước khi chạy `SyncReceiptCompletionAsync` và gửi Notification (`"đã hoàn tất và cộng {points} điểm xanh"`). Không nuốt âm thầm lỗi cộng điểm mà ghi log `LogError` rõ ràng.
     - Trong [`HandoverTradeSyncHelper.SyncReceiptCompletionAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverTradeSyncHelper.cs#L625-L732):
       - Khi `UserPointWallets` đã tồn tại cho `userId`, chỉ đồng bộ `TotalRecycledKg` và ánh xạ `CurrentGreenPoints`/`TotalLifetimePoints` từ ví (không cộng dồn trùng điểm nữa).
     - Trong [`TradeAndGamificationServices.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/TradeAndGamificationServices.cs#L528-L563):
       - Nhánh `ConfirmReceiptByQrAsync` trên PostgreSQL gọi luôn cộng điểm vào ví (`IPointsService` / `IGamificationService`) cho cả Citizen và Recycler (nếu có) với idempotency key `QR:{dbReceipt.Id}`.
     - Trong [`AuthService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AuthService.cs#L650-L773):
       - Khi build `UserInfoDto` (`GetUserByIdAsync` và `GenerateAuthResponseAsync`), đọc thêm `UserPointWallets` (nếu có) để `dto.CurrentGreenPoints` luôn phản ánh đúng `wallet.CurrentBalance`.
2. **Frontend (`recyclerWorkflowApi.ts`, `user/Receipt.tsx`):**
   - Trong [`confirmHandoverReceipt`](file:///c:/Projects/EcoLink/ecolink-web/src/lib/recyclerWorkflowApi.ts#L1898-L1942):
     - Sau khi gọi thành công `POST /api/v1/handover-requests/{requestId}/receipt/confirm`:
       - Nếu đồng bộ Backend thành công: gọi `Promise.allSettled([fetchPointWallet(), fetchCurrentUserProfile()])` để cập nhật tức thời `setSpendPoints(wallet.balance)` và `session.currentGreenPoints` (giúp Navbar, Trang chủ User, Đổi thưởng, Cây Ảo, Hồ sơ tự cập nhật mà không cần F5).
       - Nếu ở chế độ Offline Demo: gọi `addSpendPoints(updated.points)`.
   - Trên [`user/Receipt.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Receipt.tsx#L311-L363):
     - Sau khi xác nhận thành công, đặt `confirmSuccessMsg` rõ ràng (VD: *"Đã xác nhận biên nhận #{receiptCode}! Bạn đã được cộng +{points} Điểm Xanh vào ví."*), kèm đường dẫn xem nhanh sang `/eco-facts` hoặc `/rewards`.

---

### Trường hợp 3: Kiểm tra & Khắc phục luồng gửi khiếu nại của User và Recycler (sau khi User xác nhận biên nhận và gửi feedback/review)

#### 2.3.1. Phân tích nguyên nhân gốc rễ
Kiểm tra chi tiết cả Backend và Frontend cho thấy vì sao luồng khiếu nại gặp lỗi khi test với Recycler (sau khi User xác nhận biên nhận + gửi feedback) và còn bất cập với User:
1. **Ở Backend ([`ComplaintService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/ComplaintService.cs#L66-L200)):**
   - `SubmitComplaintAsync` tìm đơn trực tiếp trong `_db.HandoverRequests` bằng `FirstOrDefaultAsync(h => h.Id == request.HandoverRequestId)`. Nếu đơn đi từ luồng `TradeOrder` mà chưa có bản ghi đồng bộ trong `HandoverRequests` (giống trường hợp từng xảy ra ở `HandoverReceiptService`), API ném `404 NotFoundException`.
   - Kiểm tra vai trò người gửi tại dòng 85–101 chỉ so sánh cứng `handover.UserId == callerUserId` hoặc `handover.RecyclerId == callerUserId`. Nếu ở dữ liệu thực tế hoặc phiên chuyển tài khoản, `TradeOrder.RecyclerUserId` / `CollectionPartner.UserId` khác `handover.RecyclerId` (ví dụ `PartnerId` từng trỏ về `CollectionPartner` ID khác `UserId`), Backend ném **`403 Forbidden` (`MSG-ADM-04`)** ngăn Recycler gửi khiếu nại.
   - Khi đơn đã ở trạng thái `Completed`, nếu User đã đánh giá 1–5 sao (`TransactionReview`), cần xác nhận lại quy tắc nghiệp vụ: `ComplaintService` KHÔNG chặn theo `TransactionReview` mà vẫn cho phép Recycler/User gửi khiếu nại trong hạn `ComplaintWindowDays = 7` ngày kể từ `receipt?.ConfirmedByUserAt ?? handover.ActualHandoverAt ?? handover.UpdatedAt`. Tuy nhiên ở phía UI lại đang làm người dùng hiểu lầm/không gửi được.
2. **Ở Frontend `/recycler/requests` ([`src/pages/recycler/Requests.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Requests.tsx#L161-L177,L443-L547)):**
   - **Lỗi hiển thị phản hồi form (người dùng bấm Gửi nhưng không thấy báo gì / tưởng không gửi được):**
     - State lỗi `actionError` chỉ hiển thị ở dòng 215 (đầu trang), hoàn toàn nằm ngoài vùng nhìn thấy khi người dùng đang cuộn xuống thẻ đơn và mở form `"Khiếu nại giao dịch"` ở dòng 480–546.
   - **Lỗi trường ảnh bằng chứng:**
     - Dòng 519–526 chỉ có `<input className="input h-9 text-xs" placeholder="https://..." />`. Trong khi đó [`SubmitComplaintRequestValidator`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/ReviewAndComplaintValidators.cs#L73-L86) yêu cầu mỗi phần tử trong `evidencePhotoUrls` bắt buộc phải là HTTP/HTTPS URL hợp lệ; nếu Recycler gõ mô tả hoặc chọn tên file không phải URL, Backend trả về **`400 Bad Request`**, và vì lỗi chỉ hiện ở đỉnh trang nên Recycler hoàn toàn không biết lý do thất bại! Ngoài ra, thiếu nút `<input type="file">` gọi `uploadComplaintEvidence` (`POST /api/v1/complaints/upload-evidence`).
   - **Thiếu luồng xử lý Review sau khi User đã gửi feedback:**
     - Sau khi User xác nhận biên nhận và gửi feedback/đánh giá sao (`CreateReviewAsync`), trên thẻ đơn `done` của Recycler trong `Requests.tsx` chưa hiển thị đánh giá đó, chưa có nút **"Báo cáo đánh giá sai sự thật / vi phạm"** (`reportAbusiveReview`) và chưa hiển thị lịch sử/trạng thái khiếu nại đã gửi cho đơn (`fetchMyComplaints`).
3. **Ở Frontend `/user/receipt` ([`src/pages/user/Receipt.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Receipt.tsx#L577-L635)) và `/feedback` ([`src/pages/Feedback.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Feedback.tsx#L78-L118)):**
   - Trên `user/Receipt.tsx`: sau khi User xác nhận biên nhận (`confirmed === true`), nút `"Khiếu nại biên nhận"` biến mất vì endpoint `/receipt/dispute` chỉ dành cho `AwaitingConfirmation`; User không có form để gửi khiếu nại hậu giao dịch (`POST /api/v1/complaints`) ngay tại trang chi tiết đơn đã hoàn tất (hoặc sau khi đã gửi đánh giá sao) nếu phát hiện sự cố cân thiếu/sai tiền.
   - Trên `Feedback.tsx`: mặc định `selectedOrderId` là `""`, nên nếu User hoặc Recycler mở `/feedback` gõ khiếu nại mà quên chọn dropdown `"Đính kèm đơn bàn giao"`, hàm `send()` rơi vào luồng offline giả lập `setRef(makeRef())` (tạo mã `FB-...`), không gọi Backend!

#### 2.3.2. Giải pháp Clean Coding
1. **Backend (`ComplaintService.cs`):**
   - Trong `SubmitComplaintAsync`:
     - Nếu `_db.HandoverRequests.FirstOrDefaultAsync(...)` là `null`, tìm `TradeOrders` tương ứng và gọi `HandoverTradeSyncHelper.EnsureHandoverFromTradeOrderAsync(_db, tradeOrder, ct)` trước khi báo `NotFoundException`.
     - Giải quyết đối chiếu danh tính Recycler an toàn: ngoài `handover.RecyclerId == callerUserId`, kiểm tra thêm nếu tồn tại `TradeOrder` có `Id == handover.Id` mà `(order.RecyclerUserId == callerUserId)` hoặc `CollectionPartner` có `UserId == callerUserId`, cho phép Recycler gửi khiếu nại hợp lệ.
     - Trả về đầy đủ thông tin phiếu khiếu nại (`ComplaintDetailDto`) kể cả khi đơn đã `Completed` và đã có `TransactionReview` (trong hạn 7 ngày theo `BR-DSP-01`).
2. **Frontend — Tái sử dụng component form khiếu nại chuẩn hoá (`src/components/ComplaintInlineForm.tsx` mới hoặc helper dùng chung):**
   - Đóng gói chung phần logic:
     - Chọn nhóm nguyên nhân (`ScaleDiscrepancy`, `PaymentIssue`, `NoShow`, `Misconduct`, `Other`) theo đúng vai trò (`User` vs `Recycler`).
     - Nhập mô tả chi tiết với bộ đếm ký tự tối thiểu `15` ký tự + validate tại client trước khi gọi API.
     - Hỗ trợ **Upload ảnh bằng chứng trực tiếp từ máy** qua `uploadComplaintEvidence` (`POST /api/v1/complaints/upload-evidence`, tối đa 5 ảnh) + tùy chọn dán URL HTTP/HTTPS hợp lệ (kiểm tra `https?://` rõ ràng trước khi submit).
     - Hiển thị **inline error alert** và **inline success banner (kèm mã phiếu `#CMP-...`)** ngay trong form để người dùng thấy phản hồi tức thì.
   - Tích hợp vào:
     - [`src/pages/recycler/Requests.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Requests.tsx): cho phép Recycler gửi khiếu nại cho mọi đơn `done` (kể cả đã có Review), `accepted` hoặc `broken` (trong 7 ngày); hiển thị badge trạng thái nếu đơn đã có phiếu khiếu nại đang `Open`/`Investigating` (`"Đang chờ Staff xử lý #{code}"`) hoặc kết quả đã phân xử; hiển thị đánh giá của User (nếu có) kèm nút **"Báo cáo đánh giá vi phạm"** (`reportAbusiveReview`).
     - [`src/pages/user/Receipt.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Receipt.tsx): giữ `disputeHandoverReceipt` khi đơn ở `AwaitingConfirmation`, đồng thời bổ sung nút & form **"Khiếu nại giao dịch (U-11)"** dùng `ComplaintInlineForm` khi đơn đã `Completed` (kể cả sau khi User đã gửi đánh giá 1–5 sao).
     - [`src/pages/Feedback.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/Feedback.tsx): khi đã đăng nhập và ở tab `"Khiếu nại"`, hiển thị danh sách khiếu nại của cả User & Recycler (`fetchMyComplaints`), nạp cả các đơn đã hoàn tất/đang mở để chọn dễ dàng, hiển thị rõ ràng chế độ `"Khiếu nại giao dịch chính thức (U-11)"` vs `"Góp ý chung về hệ thống"`, và không im lặng nuốt lỗi API.

---

### Trường hợp 4: Thiết kế lại phần thống kê với bộ lọc thời gian chi tiết hơn (Năm, Quý, Tháng, Tuần, Từ ngày - Đến ngày, Trong ngày)

#### 2.4.1. Phân tích nguyên nhân gốc rễ
1. **Backend (`GovernanceDtos.cs`, `AdminAnalyticsController.cs`, `AdminAnalyticsService.cs`):**
   - [`ExecutiveKpiQueryRequest`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/GovernanceDtos.cs#L7-L12) mới chỉ có `Period` (`7d | 30d | 90d | all`), `FromDate`, `ToDate`.
   - [`ResolveDateRange`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AdminAnalyticsService.cs#L600-L619) chỉ parse 4 preset `7d`, `30d`, `90d`, `all`.
   - [`GetAnalyticsChartsAsync`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/AdminAnalyticsService.cs#L149-L226) chỉ nhận `int months = 6`:
     - Phần 1 (`MonthlyHandoverTrend`) luôn chia cố định theo tháng (`T4..T9`).
     - Phần 2 (`WasteCategoryBreakdown`) và Phần 3 (`AiQualityByGroup`) **chưa hề lọc theo khoảng thời gian được chọn**, dẫn đến biểu đồ không khớp với số KPI khi đổi bộ lọc!
     - Lưu ý múi giờ: Các truy vấn nên quy đổi mốc ngày lịch Việt Nam **ICT (UTC+7)** sang khoảng UTC `[fromUtc, toExclusiveUtc)` nhất quán (VD: `"Trong ngày 21/11/2026"` = `2026-11-20T17:00:00Z` đến `< 2026-11-21T17:00:00Z`) để số liệu "Trong ngày" của Admin Việt Nam hoàn toàn chính xác.
2. **Frontend (`admin/Home.tsx`, `admin/Reports.tsx`, `governanceApi.ts`):**
   - [`admin/Home.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Home.tsx) mới chỉ có 3 nút `7d`, `30d`, `90d`.
   - [`admin/Reports.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Reports.tsx) chỉ hỗ trợ xuất theo khoảng ngày khi tải CSV/XLSX mà chưa cho xem biểu đồ & KPI tương ứng theo Năm / Quý / Tháng / Tuần / Từ-Đến / Trong ngày.

#### 2.4.2. Giải pháp Clean Coding (Kiến trúc Bộ lọc Thống kê Đa chiều)
1. **Backend (`GovernanceDtos.cs`, `IGovernanceServices.cs`, `AdminAnalyticsController.cs`, `AdminAnalyticsService.cs`):**
   - **Chuẩn hoá DTO truy vấn thống nhất `AnalyticsTimeFilterRequest` (hoặc mở rộng `ExecutiveKpiQueryRequest` giữ tương thích ngược):**
     - `Period`: `"day"` | `"week"` | `"month"` | `"quarter"` | `"year"` | `"range"` | `"7d"` | `"30d"` | `"90d"` | `"all"`
     - `Year`: `int?` (VD: `2025`, `2026`)
     - `Quarter`: `int?` (`1..4`)
     - `Month`: `int?` (`1..12`)
     - `Date`: `DateOnly?` (ngày được chọn cho chế độ `day`, hoặc ngày đại diện trong tuần cho chế độ `week`)
     - `FromDate`: `DateOnly?`
     - `ToDate`: `DateOnly?`
     - `Months`: `int?` (tương thích ngược cho caller cũ gọi `GET /api/v1/admin/analytics/charts?months=6`)
   - **Tách helper `ResolveTimeWindow(ExecutiveKpiQueryRequest query)` thuần khiết trong `AdminAnalyticsService.cs`:**
     - Sử dụng múi giờ ICT (`UTC+7`, hằng số dùng chung `GamTime.Ict` hoặc `TimeSpan.FromHours(7)`) khi tính "hôm nay" và chuyển biên ngày sang UTC:
       - `day`: `[Date 00:00 ICT, Date+1 00:00 ICT)` — nhãn `"Trong ngày {Date:dd/MM/yyyy}"`
       - `week`: từ Thứ Hai đến Chủ Nhật của tuần chứa `Date` — nhãn `"Tuần ({Mon:dd/MM/yyyy} - {Sun:dd/MM/yyyy})"`
       - `month`: từ ngày 1 đến ngày cuối của `Month/Year` — nhãn `"Tháng {Month:D2}/{Year}"`
       - `quarter`: Q1 (`T1–T3`), Q2 (`T4–T6`), Q3 (`T7–T9`), Q4 (`T10–T12`) của `Year` — nhãn `"Quý {Quarter}/{Year}"`
       - `year`: `01/01/{Year}` – `31/12/{Year}` — nhãn `"Năm {Year}"`
       - `range`: kiểm tra `FromDate <= ToDate` (nếu ngược ném `400 BusinessRuleException` rõ ràng), nhãn `"Từ {From:dd/MM/yyyy} đến {To:dd/MM/yyyy}"`
   - **Mở rộng `AnalyticsChartsDto` & `GetAnalyticsChartsAsync(ExecutiveKpiQueryRequest query, CancellationToken ct)`:**
     - Lọc cả `HandoverReceipts`, `HandoverRequestItems` (`WasteCategoryBreakdown`) và `AIFeedbackLogs` (`AiQualityByGroup`) trong cùng cửa sổ `[fromUtc, toExclusiveUtc)`.
     - Thêm `Granularity` (`"hour_bucket"` | `"day"` | `"month"`) & `PeriodLabel` vào `AnalyticsChartsDto`, giữ nguyên cấu trúc `MonthlyHandoverTrend` (`Month`, `Label`, `CompletedHandovers`, `RecycledWeightKg`, `GrossValueVnd`) để không phá vỡ contract cũ, chia cột động theo độ mịn:
       - `day`: 6 khung giờ 4 tiếng (`00-04h`, `04-08h`, `08-12h`, `12-16h`, `16-20h`, `20-24h`)
       - `week`: 7 ngày (`T2 dd/MM` .. `CN dd/MM`)
       - `month` hoặc `range <= 31 ngày`: từng ngày (`dd/MM`)
       - `quarter`: 3 tháng (`T1`, `T2`, `T3` ...)
       - `year` hoặc `range > 31 ngày`: từng tháng trong khoảng (`T1/26`, `T2/26`... hoặc `T1..T12`)
   - **Cập nhật `AdminAnalyticsController.cs`:**
     - `[HttpGet("analytics/kpi-summary")]` và `[HttpGet("analytics/charts")]` cùng nhận `[FromQuery] ExecutiveKpiQueryRequest query` (nhờ vậy `AdminAnalyticsService` và các endpoint export báo cáo dùng chung 1 hàm `ResolveTimeWindow` không lặp logic).
2. **Frontend (`src/lib/governanceApi.ts`, `src/components/admin/AnalyticsTimeFilterBar.tsx` *(mới)*, `src/pages/admin/Home.tsx`, `src/pages/admin/Reports.tsx`):**
   - Tạo kiểu `AnalyticsTimeFilter` trong `governanceApi.ts`:
     ```ts
     export type AnalyticsPeriodMode = "day" | "week" | "month" | "quarter" | "year" | "range"
     export interface AnalyticsTimeFilter {
       period: AnalyticsPeriodMode
       date?: string      // YYYY-MM-DD (dùng cho day / week)
       year?: number      // YYYY (dùng cho month / quarter / year)
       month?: number     // 1..12
       quarter?: number   // 1..4
       fromDate?: string  // YYYY-MM-DD (dùng cho range)
       toDate?: string    // YYYY-MM-DD (dùng cho range)
     }
     ```
   - Tạo component tái sử dụng [`src/components/admin/AnalyticsTimeFilterBar.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/admin/AnalyticsTimeFilterBar.tsx) *(mới)*:
     - Thanh chọn 6 tab chế độ thời gian gọn gàng (`Trong ngày`, `Theo tuần`, `Theo tháng`, `Theo quý`, `Theo năm`, `Từ ngày - Đến ngày`).
     - Render control đầu vào tương ứng cho từng chế độ:
       - `Trong ngày`: `<input type="date" />` + nút nhanh `"Hôm nay"`, `"Hôm qua"`.
       - `Theo tuần`: `<input type="date" />` + hiển thị badge khoảng `Thứ 2 – Chủ nhật` đã quy đổi + nút `"Tuần này"`, `"Tuần trước"`.
       - `Theo tháng`: Select Tháng (`T1..T12`) + Select Năm.
       - `Theo quý`: Select Quý (`Q1..Q4`) + Select Năm.
       - `Theo năm`: Select Năm.
       - `Từ ngày - Đến ngày`: 2 `<input type="date" />` (`Từ ngày`, `Đến ngày`) kèm validate `fromDate <= toDate` và nút `"7 ngày qua"`, `"30 ngày qua"`.
     - Hiển thị badge tổng kết kỳ đang xem (VD: `"Đang lọc: Quý 4/2026 (01/10/2026 - 31/12/2026)"`).
   - Tích hợp `AnalyticsTimeFilterBar` vào cả [`admin/Home.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Home.tsx) và [`admin/Reports.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Reports.tsx) để lọc đồng bộ KPI Cards, Biểu đồ xu hướng, Cơ cấu vật liệu và Xuất báo cáo CSV/XLSX.

---

### Trường hợp 5: Hệ thống không có khả năng Payout — Chỉ lưu lịch sử đơn biên nhận & lịch sử thanh toán dịch vụ (Pay-in) — Loại bỏ toàn bộ dữ liệu mang tính Payout

#### 2.5.1. Phân tích hiện trạng & Kiểm kê các vị trí còn dữ liệu Payout
Theo quy tắc [`BR-HND-05`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs#L19) (*"hệ thống chỉ ghi nhận, không xử lý tiền"* mua phế liệu) và [`BR-PAY-01`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/ServicePaymentController.cs#L41) (hệ thống chỉ thu tiền **Pay-in gói dịch vụ nội bộ qua VietQR**):
- **Hệ thống KHÔNG có tính năng Payout** (không chi tiền từ ví nền tảng sang tài khoản ngân hàng của User, không giữ hộ tiền bán rác hay trừ 8% phí hoa hồng giao dịch).
- **Những nơi đang chứa nhầm dữ liệu/UI mang tính Payout cần loại bỏ hoàn toàn:**
  1. [`src/data/cashflow.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/cashflow.ts):
     - Chứa comment mô tả `"pay-out về tài khoản người dân"`, `kind: "payin" | "fee" | "payout"`, `FEE_RATE = 0.08`, danh sách `BANKS`, `validAccount()`, `KIND_LABEL.payout = "Bắn ra về tài khoản"`, và mảng `FLOWS` có các dòng giả lập `"Giải ngân tự động qua PayOS sau khi Recycler xác nhận Pay-in"`, `"Phí nền tảng 8%"`.
  2. [`src/pages/user/Payout.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Payout.tsx):
     - Tiêu đề `"Tài khoản nhận tiền"`, phần 2 có form nhập `"Ngân hàng"`, `"Số tài khoản"`, `"Chủ tài khoản"` và mô tả `"PayOS bắn tiền về tài khoản"`.
  3. [`src/pages/user/Transactions.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Transactions.tsx):
     - Tiêu đề `"Lịch sử tiền vào tài khoản"`, ô thống kê `"Đã nhận"`, `"Phí nền tảng"`, nút xem `"Thiết lập tài khoản nhận tiền"`, footer mô tả `"Thu vào từ đối tác ... Bắn ra về tài khoản"`. Trong khi API thật `fetchUserTransactions` trả về **Lịch sử đơn biên nhận**!
  4. [`src/data/portal.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/portal.ts#L26): mục menu `{ to: "/user/payout", label: "Tài khoản nhận tiền" }`.
  5. [`src/pages/user/Receipt.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Receipt.tsx), [`src/pages/user/Home.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Home.tsx), [`src/pages/account/Profile.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/account/Profile.tsx), [`src/pages/recycler/Home.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Home.tsx), [`src/pages/admin/Home.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Home.tsx), [`src/pages/admin/Reports.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Reports.tsx), [`src/components/Assistant.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/Assistant.tsx), [`src/check.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/check.tsx), [`src/check-routes.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/check-routes.tsx), [`src/data/ops.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/ops.ts#L336,L404-L410) (chứa `fee30d`, `FEE.rate = FEE_RATE`).
  6. **Backend dead-code Payout:** [`PayOsDtos.cs:43,76`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/PayOsDtos.cs), [`ApplicationInterfaces.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Interfaces/ApplicationInterfaces.cs), [`PayOsService.cs:230`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PayOsService.cs) còn hàm `ExecutePayoutAsync` không hề được Controller hay Service nào gọi.

#### 2.5.2. Giải pháp Clean Coding (Loại bỏ triệt để Payout, tập trung 2 luồng lịch sử thật)
1. **Dọn sạch tầng dữ liệu & cấu hình điều hướng (`cashflow.ts`, `ops.ts`, `portal.ts`):**
   - Trong [`src/data/cashflow.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/cashflow.ts):
     - **Xoá bỏ** `FEE_RATE`, `BANKS`, `validAccount`, kiểu `"fee"` và `"payout"`, và toàn bộ bản ghi PayOS payout.
     - Chuyển `CashFlow` thành **`ServicePayInRecord`** (`kind: "payin"`) chỉ đại diện cho các giao dịch thanh toán gói dịch vụ qua VietQR (`Citizen VIP`, `Recycler Standard`, `Recycler Pro`).
   - Trong [`src/data/ops.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/ops.ts):
     - Loại bỏ `fee30d` (hoa hồng 8%) và `FEE`; thay bằng doanh thu gói dịch vụ Pay-in VietQR (`serviceRevenue30d`).
   - Trong [`src/data/portal.ts`](file:///c:/Projects/EcoLink/ecolink-web/src/data/portal.ts):
     - Đổi nhãn `/user/payout` $\rightarrow$ **`"Gói dịch vụ & VietQR"`**, `/user/transactions` $\rightarrow$ **`"Lịch sử biên nhận & Pay-in"`** (giữ nguyên URL `/user/payout` & `/user/transactions` hoặc thêm alias route để không làm gãy bookmark/link cũ như `/user/payout` trong `SponsoredAdBanner`).
2. **Tái cấu trúc [`src/pages/user/Payout.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Payout.tsx):**
   - Đổi `PageHead` thành: **`"Gói dịch vụ & Thanh toán VietQR"`** — *"Đăng ký gói thành viên Citizen VIP bằng thanh toán Pay-in qua mã VietQR động và theo dõi hóa đơn dịch vụ."*
   - **Xoá bỏ hoàn toàn:** Form `"Tài khoản nhận tiền (U-13)"`, các input `bank`/`account`/`owner`, nút `"Lưu tài khoản nhận tiền"`, và ghi chú `"PayOS bắn tiền về tài khoản"`.
   - Thêm khung ghi chú nghiệp vụ rõ ràng: *"Lưu ý: Hệ thống EcoLink không xử lý chi tiền hộ (Payout) về tài khoản ngân hàng. Tiền mua bán phế liệu được hai bên thanh toán trực tiếp khi cân đo bàn giao; hệ thống chỉ ghi nhận đơn biên nhận để cộng Điểm Xanh và xử lý thanh toán gói dịch vụ (Pay-in) qua VietQR."*
   - Giữ nguyên & trau chuốt khối mua/nâng cấp gói `Citizen VIP`, modal quét mã QR `VietQrCheckoutModal`, và bảng lịch sử đơn thanh toán dịch vụ (`GET /api/v1/service-payments/orders`).
3. **Tái cấu trúc [`src/pages/user/Transactions.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Transactions.tsx):**
   - Đổi `PageHead` thành **`"Lịch sử đơn biên nhận & Thanh toán dịch vụ"`**.
   - Chia thành 2 phần/tab dữ liệu thật (thay thế hoàn toàn các cột `"Phí nền tảng"` và `"Bắn ra về tài khoản"`):
     - **Tab 1 — Lịch sử đơn biên nhận bàn giao (`GET /api/v1/transactions`):**
       - Thẻ KPI: `Tổng số biên nhận hoàn tất`, `Tổng khối lượng đã giao (kg)`, `Giá trị biên nhận đã ghi nhận`, `Tổng Điểm Xanh đã nhận`.
       - Bảng chi tiết: `Mã biên nhận`, `Mã đơn`, `Cơ sở thu mua`, `Nhóm phế liệu`, `Khối lượng (kg)`, `Giá trị ghi nhận (VNĐ)`, `Điểm Xanh`, `Thời gian hoàn tất`, kèm liên kết xem chi tiết `/user/receipt?orderId=...`.
     - **Tab 2 — Lịch sử thanh toán gói dịch vụ (Pay-in VietQR) (`GET /api/v1/service-payments/orders`):**
       - Bảng chi tiết: `Mã đơn Pay-in`, `Gói dịch vụ`, `Số tiền thanh toán (VNĐ)`, `Trạng thái` (`Đã thanh toán`, `Chờ chuyển khoản`, `Đã huỷ`), `Thời gian tạo`.
4. **Cập nhật các trang liên quan để không còn dấu vết Payout:**
   - [`src/pages/user/Receipt.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Receipt.tsx): sửa link `"Xem tiền đã nhận"` $\rightarrow$ `"Xem lịch sử biên nhận"`.
   - [`src/pages/account/Profile.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/account/Profile.tsx): xoá import `FLOWS`, đổi `"Khoản đã nhận"` $\rightarrow$ `"Biên nhận đã xác nhận"`.
   - [`src/pages/user/Home.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/user/Home.tsx), [`src/pages/recycler/Home.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/recycler/Home.tsx), [`src/pages/admin/Home.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Home.tsx), [`src/pages/admin/Reports.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/pages/admin/Reports.tsx), [`src/components/Assistant.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/components/Assistant.tsx): gỡ bỏ mọi tham chiếu đến `payout`, `fee` 8% và thay bằng dữ liệu Pay-in gói dịch vụ / biên nhận bàn giao thật.
   - [`src/check.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/check.tsx) & [`src/check-routes.tsx`](file:///c:/Projects/EcoLink/ecolink-web/src/check-routes.tsx): cập nhật tiêu đề smoke test tương ứng (`"Gói dịch vụ & Thanh toán VietQR"`).
   - **Backend cleanup:** xoá `PayOsPayoutRequest`, `PayOsPayoutResponseDto`, `IPayOsService.ExecutePayoutAsync`, phần `Payouts` trong [`PayOsService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/PayOsService.cs) và cập nhật comment mô tả chỉ còn Pay-in VietQR trong [`DependencyInjection.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/DependencyInjection.cs#L94).

---

## 3. Lộ Trình Triển Khai Theo Giai Đoạn & Kiểm Thử

### Giai đoạn 1: Sửa EcoFact Source & Cộng/Đồng Bộ Điểm Xanh (Trường hợp 1 & Trường hợp 2)
1. Backend: cập nhật `GamificationCommon.cs`, `EcoFactService.cs`, `VirtualTreeService.cs`, `PointsService.cs`, `HandoverReceiptService.cs`, `HandoverTradeSyncHelper.cs`, `TradeAndGamificationServices.cs`, `AuthService.cs`.
2. Frontend: tạo `src/lib/ecoFactSource.ts`, cập nhật `EcoFactsPage.tsx`, `user/Gamification.tsx`, `staff/EcoFacts.tsx`, `data/ops.ts`, `recyclerWorkflowApi.ts`, `user/Receipt.tsx`.

### Giai đoạn 2: Khiếu Nại 2 Chiều User & Recycler (Trường hợp 3)
1. Backend: cập nhật `ComplaintService.cs` (đồng bộ từ `TradeOrders` khi thiếu `HandoverRequest` & mở rộng đối chiếu quyền `RecyclerUserId` / `CollectionPartner.UserId`).
2. Frontend: tạo component dùng chung `src/components/ComplaintInlineForm.tsx`, tích hợp vào `recycler/Requests.tsx`, `user/Receipt.tsx`, `Feedback.tsx`.

### Giai đoạn 3: Nâng Cấp Thống Kê Đa Chiều (Trường hợp 4)
1. Backend: mở rộng `GovernanceDtos.cs`, `IGovernanceServices.cs`, `AdminAnalyticsController.cs`, `AdminAnalyticsService.cs` cho 6 chế độ lọc (`day`, `week`, `month`, `quarter`, `year`, `range`) với múi giờ ICT (UTC+7).
2. Frontend: cập nhật `governanceApi.ts`, tạo `src/components/admin/AnalyticsTimeFilterBar.tsx`, tích hợp vào `admin/Home.tsx` và `admin/Reports.tsx`.

### Giai đoạn 4: Loại Bỏ Payout Toàn Hệ Thống (Trường hợp 5)
1. Backend: dọn `ExecutePayoutAsync` trong `PayOsDtos.cs`, `ApplicationInterfaces.cs`, `PayOsService.cs`, `DependencyInjection.cs`.
2. Frontend: cập nhật `data/cashflow.ts`, `data/ops.ts`, `data/portal.ts`, `user/Payout.tsx`, `user/Transactions.tsx`, `user/Receipt.tsx`, `user/Home.tsx`, `account/Profile.tsx`, `recycler/Home.tsx`, `admin/Home.tsx`, `admin/Reports.tsx`, `components/Assistant.tsx`, `check.tsx`, `check-routes.tsx`.

### Giai đoạn 5: Kiểm Thử Toàn Diện (Verification Plan)
1. **Backend:**
   - Chạy `dotnet build` và `dotnet test` tại `c:\Projects\EcoLink\intelligent-waste-sorting-trading\src\Ecolink.Backend` (bao gồm bổ sung test cho EcoFact source cleanup, PointsService fallback khi thiếu `ActualWeightKg` trên item, Recycler gửi khiếu nại sau khi User xác nhận biên nhận + tạo Review, và Admin Analytics lọc `day`/`week`/`month`/`quarter`/`year`/`range`).
2. **Frontend:**
   - Chạy lần lượt 4 lệnh kiểm tra chuẩn tại `c:\Projects\EcoLink\ecolink-web`:
     - `npm run typecheck`
     - `npm run check`
     - `npm run check:routes`
     - `npm run build`
3. **Kiểm tra thủ công trên luồng end-to-end:**
   - **EcoFact:** mở `/eco-facts` kiểm tra không còn link hỏng, link chính thống mở được; thử nút `"Xoá nguồn"` ở `/staff/eco-facts`.
   - **Eco Point:** Recycler lập biên nhận $\rightarrow$ User vào `/user/receipt` bấm xác nhận $\rightarrow$ kiểm tra ví điểm trên Navbar, `/user`, `/eco-facts`, `/rewards`, `/account/profile` tăng đúng ngay lập tức.
   - **Khiếu nại 2 chiều:** User xác nhận biên nhận và gửi đánh giá sao $\rightarrow$ đăng nhập Recycler vào `/recycler/requests` bấm `"Khiếu nại giao dịch"`, upload ảnh + nhập mô tả $\ge 15$ ký tự và gửi $\rightarrow$ xác minh tạo mã `#CMP-...` thành công và hiện ở hàng đợi `/staff/complaints`; kiểm tra lại cả luồng User gửi khiếu nại ở `/user/receipt` và `/feedback`.
   - **Thống kê:** vào `/admin` và `/admin/reports`, chuyển qua lại giữa 6 chế độ lọc (Trong ngày, Tuần, Tháng, Quý, Năm, Từ ngày - Đến ngày) và kiểm tra KPI + biểu đồ + export CSV/XLSX khớp nhau.
   - **Không Payout:** vào `/user/payout` và `/user/transactions` xác minh không còn form STK ngân hàng, không còn phí 8% hay chữ `PayOS bắn tiền`, chỉ hiển thị Gói dịch vụ Pay-in VietQR và Lịch sử đơn biên nhận / Thanh toán dịch vụ.
