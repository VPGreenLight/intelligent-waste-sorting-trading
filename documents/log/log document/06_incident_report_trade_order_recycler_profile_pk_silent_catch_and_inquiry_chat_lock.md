# BÁO CÁO SỰ CỐ KỸ THUẬT LUỒNG THƯƠNG LƯỢNG – ĐƠN HÀNG – BIÊN NHẬN (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-TRD-06`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Direct Trade Orders (`M-09`), Handover Request Management (`M-06`), Handover Completion & Receipt (`M-07`), In-App Real-Time Negotiation Chatbox (`M-08`), Frontend SPA (`ecolink-web`)  
**Các tệp ảnh hưởng chính:**
* Backend: `TradeAndGamificationServices.cs` (`TradeTransactionService`), `NegotiationChatService.cs`, `HandoverTradeSyncHelper.cs`, `HandoverReceiptService.cs`
* Frontend: `Requests.tsx`, `Handover.tsx`, `recycler/Home.tsx`, `user/Home.tsx`, `Transactions.tsx`, `user/Receipt.tsx`, `recycler/Receipt.tsx`, `Chat.tsx`, `Map.tsx`
**Thời điểm ghi nhận:** 17/11/2026 – 19/11/2026 (Live E2E Testing trên `ecolink_db`)  
**Mức độ nghiêm trọng:** Nghiêm trọng (Critical – Gây mất đồng bộ dữ liệu đơn bàn giao giữa RAM & PostgreSQL, nhầm lẫn giữa phiên Hỏi giá và Đơn chính thức, khóa nhầm luồng Chat từ bản đồ và chớp dữ liệu giả khi tải lại trang)

---

## 1. TỔNG HỢP 4 LỖI PHÁT HIỆN & NGUYÊN NHÂN GỐC RỄ

### 1.1. Lỗi 1: Vi phạm khóa ngoại `fk_trade_orders_recycler_profiles` bị khối `catch {}` nuốt im lặng (`TradeTransactionService.cs`)
* **Hiện tượng:** Khi Người dân tạo đơn từ Frontend hoặc qua luồng Chat, API `POST /api/v1/trade/orders` vẫn trả HTTP `200 OK` cho giao diện hiển thị bình thường, nhưng ngay khi F5 tải lại hoặc khởi động lại Backend, đơn bàn giao và lịch sử liên quan biến mất hoàn toàn khỏi PostgreSQL (`handover_requests`, `trade_orders`).
* **Nguyên nhân gốc rễ:** `TradeTransactionService.CreateTradeOrderAsync` gán `RecyclerUserId = partnerEntity.UserId` (hoặc `effectivePartnerId`) trong khi ràng buộc khóa ngoại `fk_trade_orders_recycler_profiles` chỉ chấp nhận giá trị đã tồn tại trong bảng `recycler_profiles` (`user_id` PK). Khi gặp đối tác chưa có `RecyclerProfile` tương ứng hoặc nhận nhầm `partnerId` thay vì `recyclerProfile.UserId`, PostgreSQL ném `DbUpdateException` (`23503`), nhưng khối `try { ... } catch { }` trống rỗng đã **nuốt ngoại lệ** và chỉ lưu vào `ConcurrentDictionary _orders` trên RAM!
* **Biện pháp khắc phục:**
  1. Chỉ gán `RecyclerUserId = recyclerProfile?.UserId` sau khi truy vấn/đối chiếu tồn tại bản ghi trong `_dbContext.RecyclerProfiles`; khi không tìm thấy `partnerEntity`, tự động đồng bộ tạo/ánh xạ đúng `CollectionPartner` theo `recyclerProfile`.
  2. Thay khối `catch` nuốt lỗi bằng `_logger.LogError(ex, ...)` và **`throw;` nếu `_dbContext.Database.IsRelational()`** để mọi lỗi toàn vẹn CSDL đều được phát hiện tức thời qua `GlobalExceptionMiddleware`.

### 1.2. Lỗi 2: `NegotiationChatService` nhầm lẫn bối cảnh đơn & gán nhãn nhầm "Đơn chốt" khi mới Hỏi giá (`[INQUIRY]`)
* **Hiện tượng:**
  1. Trên màn hình `/user/handover` và `/recycler/requests`, một cuộc trò chuyện chỉ mới nhắn hỏi giá trước khi tạo đơn lại tự động xuất hiện với nhãn giả mạo `"(đã chốt)"`, khối lượng mặc định `"10 kg"`, khung giờ `"10:00 - 12:00"` kèm mã đơn `RM-CEC000`.
  2. Khi mở chat từ một đối tác mới mà `HandoverRequestId == null`, hàm `ResolveThreadContextAsync` fallback về tìm đơn cũ gần nhất của Citizen thay vì tạo/tách đúng phiên trao đổi hỏi giá.
* **Nguyên nhân gốc rễ:** Backend chưa phân biệt giữa **Phiên trao đổi hỏi giá nháp (`IsInquiryDraft = true`, tag `[INQUIRY]`, `OrderStatus.Negotiating`)** và **Đơn bàn giao chính thức (`OrderStatus.Pending` / `HandoverRequest`)**. Đồng thời `HandoverTradeSyncHelper` và `NegotiationChatService` tự động khóa `AgreedPricePerKg` ngay khi mới gửi tin nhắn hỏi giá trước khi có đề xuất giá được `Accepted`.
* **Biện pháp khắc phục:**
  1. Bổ sung cờ `IsInquiryDraft` (`Note` gắn `[INQUIRY]`, `OrderStatus.Negotiating`, `EstimatedWeightKg = 0`, `CategoryCode = null`, `AgreedPricePerKg = null`). Chỉ khi người dùng chính thức bấm **"Tạo đơn bàn giao"**, `TradeTransactionService` nâng cấp chính `TradeOrder` `[INQUIRY]` đó sang `OrderStatus.Pending` kèm `HandoverTradeSyncHelper.EnsureHandoverFromTradeOrderAsync` (giữ nguyên toàn bộ lịch sử tin nhắn đã trao đổi).
  2. Xóa các bản ghi dữ liệu mồ côi (`RM-CEC000`) trong `ecolink_db` và đồng bộ đơn giá thực tế của đối tác lên `/user/handover` và `/recycler/requests`.

### 1.3. Lỗi 3: Khóa nhầm ô nhập chat & nút Gửi đề xuất khi đi từ `/map` sang `/chat`
* **Hiện tượng:** Khi Citizen bấm nút "Nhắn tin / Thương lượng" từ trang Bản đồ cơ sở (`/map`) sang `/chat`, ô nhập tin nhắn và modal Gửi đề xuất giá bị vô hiệu hóa (`disabled`) vì Frontend yêu cầu phải có `orderId` tồn tại trước mới cho chat.
* **Nguyên nhân gốc rễ:** Luồng chuyển từ `/map` sang `/chat` chưa tự động khởi tạo phiên thương lượng nháp (`IsInquiryDraft = true`, `OrderStatus.Negotiating` tuân thủ `BR-NEG-01`), và Modal Gửi đề xuất còn thiếu trường chọn `Loại phế liệu (Category)` + `Điểm hẹn`. SignalR Hub cũng chưa đẩy đồng bộ thời gian thực theo kênh `user:{userId}` cho các cập nhật tạo đơn tức thời.
* **Biện pháp khắc phục:**
  1. Mở khóa luồng hai chiều **Chat thương lượng $\leftrightarrow$ Tạo đơn $\leftrightarrow$ Chốt đơn**: tự động khởi tạo/khôi phục thread `Negotiating` khi vào `/chat?partnerId=...`.
  2. Bổ sung selector `Loại phế liệu` & `Điểm hẹn` trong Modal Đề xuất; bổ sung push SignalR theo group `user:{userId}` để Citizen & Recycler cập nhật tức thì trạng thái đơn và giá chốt.

### 1.4. Lỗi 4: Chớp dữ liệu mẫu TP.HCM (`Mock Flash`) khi F5 tải lại trang trên tài khoản thật
* **Hiện tượng:** Khi đăng nhập bằng tài khoản thật (`collector`, `citizen`, v.v.) và nhấn `F5` ở `/recycler/requests`, `/handover`, `/recycler/home`, `/user/home`, `/transactions`, `/user/receipt`, giao diện chớp hiện danh sách đơn/cơ sở mẫu ở TP.HCM trong tích tắc trước khi biến mất hoặc ghi đè bởi dữ liệu thật từ Hòa Lạc (`ecolink_db`).
* **Nguyên nhân gốc rễ:** Các component React khởi tạo state mặc định từ các mảng hằng số mock (`sampleHandovers`, `samplePartners`, TP.HCM GPS) trước khi kiểm tra xem người dùng hiện tại có đang ở phiên **Backend thật** hay chế độ **Demo Offline**.
* **Biện pháp khắc phục:** Áp dụng đồng nhất helper `hasLiveBackendAuth()` trên toàn bộ 7 trang giao dịch (`Requests.tsx`, `Handover.tsx`, `recycler/Home.tsx`, `user/Home.tsx`, `Transactions.tsx`, `user/Receipt.tsx`, `recycler/Receipt.tsx`) để khởi tạo state rỗng + loading spinner thay vì render dữ liệu mock khi đã đăng nhập Backend thật, đồng thời chuẩn hóa tọa độ mặc định sang **Hòa Lạc** trên `/user/handover`.
