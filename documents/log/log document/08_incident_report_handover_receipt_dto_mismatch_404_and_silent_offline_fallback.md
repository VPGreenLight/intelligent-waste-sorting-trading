# BÁO CÁO SỰ CỐ KỸ THUẬT LUỒNG LẬP & XÁC NHẬN BIÊN NHẬN BÀN GIAO (INCIDENT BUG REPORT)
**Mã sự cố:** `INC-2026-RCP-08`  
**Hệ thống:** EcoLink – Intelligent Waste Sorting & Trading Platform  
**Phân hệ:** Handover Completion & Transaction Receipt (`M-07` / `UC R-06`, `UC U-09`), Frontend SPA (`ecolink-web`), Direct Trade Sync (`M-09`)  
**Các tệp ảnh hưởng chính:**
* **Frontend (`ecolink-web`):** [`src/pages/recycler/Receipt.tsx`](file:///d:/Project/ecolink-web/src/pages/recycler/Receipt.tsx), [`src/pages/user/Receipt.tsx`](file:///d:/Project/ecolink-web/src/pages/user/Receipt.tsx), [`src/lib/recyclerWorkflowApi.ts`](file:///d:/Project/ecolink-web/src/lib/recyclerWorkflowApi.ts), [`src/data/portal.ts`](file:///d:/Project/ecolink-web/src/data/portal.ts)
* **Backend (`intelligent-waste-sorting-trading`):** [`HandoverReceiptDtos.cs`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/HandoverReceiptDtos.cs), [`TradeAndGamificationDtos.cs`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/TradeAndGamificationDtos.cs), [`HandoverReceiptService.cs`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs), [`RecyclerReceiptController.cs`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/RecyclerReceiptController.cs), [`HandoverReceiptController.cs`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/HandoverReceiptController.cs)  
**Thời điểm ghi nhận:** 19/11/2026 (Live E2E Testing qua Cloudflare Tunnel giữa tài khoản Recycler và Citizen)  
**Mức độ nghiêm trọng:** Nghiêm trọng (Critical – Biên nhận chỉ hiển thị giả trên trình duyệt của Cơ sở thu gom, hoàn toàn không lưu vào PostgreSQL khiến Người dùng không nhận được biên nhận để xác nhận hoàn tất giao dịch)

---

## 1. MÔ TẢ HIỆN TƯỢNG (INCIDENT SYNOPSIS)

Trong quá trình kiểm thử đầu-cuối (End-to-End) luồng thu gom thực tế giữa **Cơ sở thu gom (Recycler)** và **Người dân (Citizen)** trên hai cửa sổ trình duyệt độc lập:

1. **Phía Cơ sở thu gom (`/recycler/receipt?orderId=7e797c00-98f8-43d6-ad8d-2d6d33912b1d`, cửa sổ `[InPrivate]`):**
   * Giao diện hiển thị đơn `BH-7E797C` (*Cổng phụ Khu CNC Hòa Lạc*, `10 kg`, `Chai nhựa PET`, `55.000 đ`).
   * Nút hành động bị khóa ở trạng thái **`Đã gửi biên nhận`** kèm thông báo:
     > *"Đơn này đã có biên nhận #REC-187209 (trạng thái: AwaitingConfirmation)."*
   * Tuy nhiên, tại tab **Network** của DevTools ngay thời điểm tải trang, request gọi `GET .../receipt` (từ `api.ts:124`) trả về **`404 Not Found`** (`252 B`).
   * Khu vực *"Ảnh bàn giao & bằng chứng cân"* vẫn đang ở trạng thái trống (*"Chọn ảnh rác đã cân hoặc biên nhận cân, tối đa 4 ảnh"*).

2. **Phía Người dân (`/user/receipt`, cửa sổ thường):**
   * Khi người dân mở trang *Xác nhận biên nhận*, giao diện hoàn toàn không hiển thị biên nhận nào mà báo trạng thái rỗng:
     > *"Chưa có biên nhận thu gom nào đang chờ xác nhận. Khi cơ sở thu gom đến cân thực tế và phát hành biên nhận, bạn có thể bấm vào nút 'Xác nhận biên nhận' từ trang Yêu cầu thu gom hoặc khung Chat."*
   * Tại tab **Network** của DevTools phía người dân, không có bất kỳ request `receipt` nào được gửi lên Backend.

---

## 2. CHUỖI TRUY VẾT LỖI E2E (END-TO-END TRACE & EVIDENCE)

Quá trình đứt gãy dữ liệu diễn ra theo chuỗi nhân quả sau:

```text
[1] Recycler mở /recycler/receipt?orderId=7e797c00-...
      │
      ▼ Gọi GET /api/v1/trade/orders -> TradeOrderDto chỉ có Lines: [{ wasteId: "pet", kg: 10 }]
      │ (KHÔNG có HandoverRequestItem.Id và KHÔNG có WasteCategoryId dạng Guid)
      ▼
[2] Recycler bấm "Gửi biên nhận"
      │ Frontend gửi POST /api/v1/recycler/handover-requests/{id}/receipt
      │ với payload: items: [{ wasteCategoryId: "pet", actualWeightKg: 10 }] (thiếu itemId, sai kiểu Guid)
      ▼
[3] Backend ASP.NET Core từ chối request (400 Bad Request / BR-HND-05)
      │ -> Bảng handover_receipts KHÔNG có bản ghi; handover_requests KHÔNG đổi sang AwaitingConfirmation
      ▼
[4] Hàm createHandoverReceipt() trong recyclerWorkflowApi.ts nuốt lỗi trong catch {}
      │ -> Tự sinh biên nhận giả #REC-187209 và ghi vào localStorage ("ecolink:workflow-sync:v1")
      │ -> Recycler thấy thông báo giả đã gửi (trong cửa sổ [InPrivate])
      │ -> Khi reload, GET .../receipt trả 404 Not Found, nhưng fetchRecyclerReceipt() tiếp tục đọc localStorage!
      ▼
[5] Citizen mở /user/receipt ở cửa sổ trình duyệt khác (không có ?orderId=...)
      │ -> localStorage cô lập giữa InPrivate và cửa sổ thường (không chia sẻ được #REC-187209)
      │ -> Gọi GET /api/v1/trade/orders tìm đơn status === "collected" ("AwaitingConfirmation") -> undefined
      │ -> Frontend không gọi GET /api/v1/handover-requests/{id}/receipt -> Hiển thị trạng thái rỗng
```

---

## 3. PHÂN TÍCH CHI TIẾT CÁC NGUYÊN NHÂN GỐC RỄ (ROOT CAUSE ANALYSIS)

### 3.1. Lỗi 1 (Critical): Sai lệch hợp đồng DTO tạo biên nhận giữa Frontend và Backend (`400 Bad Request` / `BR-HND-05`)

1. **Frontend nạp đơn từ nguồn dữ liệu thiếu khóa định danh của Handover Item:**
   * Tại [`ecolink-web/src/pages/recycler/Receipt.tsx#L64`](file:///d:/Project/ecolink-web/src/pages/recycler/Receipt.tsx#L64), màn hình lập biên nhận gọi `fetchHandoverOrdersResult("recycler")` $\rightarrow$ `GET /api/v1/trade/orders` ([`TradeController.cs#L52-L58`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/TradeController.cs#L52-L58)).
   * DTO trả về là `TradeOrderDto` ([`TradeAndGamificationDtos.cs#L69-L73`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/TradeAndGamificationDtos.cs#L69-L73)), trong đó `Lines` chỉ là `TradeOrderLineDto`:
     ```csharp
     public class TradeOrderLineDto
     {
         public string WasteId { get; set; } = string.Empty; // vd: "pet", "paper", "iron"
         public decimal Kg { get; set; }
     }
     ```
   * Cấu trúc này **không chứa `HandoverRequestItem.Id` (`itemId: Guid`) và `WasteCategoryId` (`Guid`)**, dù API chuyên trách của module bàn giao (`GET /api/v1/recycler/handover-requests/{id}`) có trả đầy đủ `HandoverItemDto` ([`HandoverDtos.cs#L94-L104`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/HandoverDtos.cs#L94-L104)).

2. **Payload gửi lên Backend sai kiểu dữ liệu `Guid?` và thiếu `itemId`:**
   * Tại [`ecolink-web/src/pages/recycler/Receipt.tsx#L132-L136`](file:///d:/Project/ecolink-web/src/pages/recycler/Receipt.tsx#L132-L136), Frontend lắp payload gửi `POST /api/v1/recycler/handover-requests/{requestId}/receipt`:
     ```ts
     const payload: CreateReceiptRequest = {
       items: rows.map((r) => ({
         wasteCategoryId: r.wasteId, // "pet" | "plastic" — chuỗi ngắn, KHÔNG PHẢI GUID!
         actualWeightKg: r.kg,
       })),
       ...
     }
     ```
   * Đối chiếu với hợp đồng Backend tại [`HandoverReceiptDtos.cs#L8-L19`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/HandoverReceiptDtos.cs#L8-L19):
     ```csharp
     public class ReceiptItemRequest
     {
         public Guid? ItemId { get; set; }
         public Guid? WasteCategoryId { get; set; }
         [Required]
         public decimal? ActualWeightKg { get; set; }
     }
     ```
   * **Hệ quả tại Backend:**
     * Vì `wasteCategoryId` là chuỗi `"pet"` chứ không phải `Guid`, bộ **JSON Model Binding của ASP.NET Core lập tức từ chối request bằng `400 Bad Request`** ngay trước khi vào được Controller/Service.
     * Hơn nữa, ngay cả khi truyền `WasteCategoryId` đúng kiểu `Guid`, việc bỏ trống `ItemId` vẫn khiến [`HandoverReceiptService.BuildPlanAsync`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs#L426-L449) coi đó là *dòng phát sinh* (`IsExtra`) và từ chối theo quy tắc `BR-HND-05`:
       * *"Loại phế liệu này đã có trong yêu cầu, hãy nhập theo dòng có sẵn."*
       * *"Vui lòng nhập khối lượng thực tế cho tất cả loại phế liệu trong yêu cầu."* (`seenItems.Count != request.Items.Count`).

---

### 3.2. Lỗi 2 (Critical): Hàm `createHandoverReceipt` và `fetchRecyclerReceipt` nuốt lỗi (`catch {}`) và tự tạo dữ liệu giả trên `localStorage`

1. **Nuốt mọi lỗi HTTP của Backend tại tầng API client Frontend:**
   * Trong [`ecolink-web/src/lib/recyclerWorkflowApi.ts#L1467-L1535`](file:///d:/Project/ecolink-web/src/lib/recyclerWorkflowApi.ts#L1467-L1535):
     ```ts
     try {
       const res = await authorizedRequest<ReceiptDto>(
         "recycler",
         `/api/v1/recycler/handover-requests/${requestId}/receipt`,
         { method: "POST", body: JSON.stringify(req) },
       )
       ...
     } catch {
       // Bỏ qua lỗi kết nối và rơi vào lưu đệm ngoại tuyến
     }
     ```
   * Khối `catch {}` không phân biệt **lỗi mất kết nối mạng (`err.isNetworkError`)** với **lỗi từ chối dữ liệu / nghiệp vụ của Backend (`400 Bad Request`, `404 Not Found`, `403 Forbidden`)**.
   * Khi Backend trả về `400`, hàm không báo lỗi ra UI mà rơi thẳng xuống nhánh dự phòng ngoại tuyến, tự sinh đối tượng giả:
     ```ts
     const localReceipt: ReceiptDto = {
       receiptId: `REC-${Date.now()}`,
       receiptCode: `REC-${String(Date.now()).slice(-6)}`, // Sinh mã #REC-187209 đúng như ảnh chụp!
       requestStatus: "AwaitingConfirmation",
       ...
     }
     ```
     và ghi vào `localStorage` (`OFFLINE_SYNC_KEY = "ecolink:workflow-sync:v1"`). Trong khi đó, mã biên nhận thật do Backend sinh tại [`HandoverReceiptService.GenerateCodeAsync`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs#L566-L571) luôn có tiền tố **`RCP-yyyy-NNNN`** (ví dụ `RCP-2026-0001`).

2. **Đọc đè cache giả sau khi API trả `404` và cô lập giữa 2 cửa sổ trình duyệt:**
   * Khi tải lại trang `/recycler/receipt?orderId=...`, hàm [`fetchRecyclerReceipt`](file:///d:/Project/ecolink-web/src/lib/recyclerWorkflowApi.ts#L1538-L1555) gọi `GET /api/v1/recycler/handover-requests/{id}/receipt`. Do PostgreSQL chưa có bản ghi, [`HandoverReceiptService.GetForRecyclerAsync`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverReceiptService.cs#L137-L143) ném `NotFoundException("Không tìm thấy biên nhận.")` $\rightarrow$ [`GlobalExceptionMiddleware`](file:///d:/Project/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Middlewares/GlobalExceptionMiddleware.cs#L44-L67) trả **`404 Not Found`** (`252 B`, `"MSG-NOT-FOUND"`), khớp hoàn toàn với log Network ở ảnh chụp.
   * Tuy nhiên `fetchRecyclerReceipt` tiếp tục nuốt lỗi `404` trong `catch {}` rồi trả về `state.customReceipts?.[requestId]` (`#REC-187209`), khiến giao diện Recycler hiển thị sai sự thật rằng đơn đã có biên nhận.
   * Vì Recycler thao tác trên cửa sổ **`[InPrivate]`** và Citizen mở ở cửa sổ thường, hai cửa sổ không dùng chung `localStorage`. Dữ liệu giả ở cửa sổ Recycler hoàn toàn vô hình đối với Citizen.

---

### 3.3. Lỗi 3 (Phía User): Không tự định vị được đơn chờ xác nhận tại `/user/receipt`

1. **Truy cập từ Sidebar không có `orderId`:**
   * Liên kết menu của người dân trong [`ecolink-web/src/data/portal.ts#L25`](file:///d:/Project/ecolink-web/src/data/portal.ts#L25) trỏ trực tiếp tới `/user/receipt` (không có tham số `?orderId=...`).
2. **Phụ thuộc trạng thái `collected` từ `/api/v1/trade/orders`:**
   * Tại [`ecolink-web/src/pages/user/Receipt.tsx#L80-L87`](file:///d:/Project/ecolink-web/src/pages/user/Receipt.tsx#L80-L87):
     ```ts
     const ordersRes = await fetchHandoverOrdersResult("user")
     const found = orderId
       ? ordersRes.orders.find((o) => o.id === orderId || o.displayCode === orderId)
       : ordersRes.orders.find((o) => o.status === "collected" || o.status === "done")

     const targetId = found?.id || orderId
     ```
   * [`mapBackendOrderStatus`](file:///d:/Project/intelligent-waste-sorting-trading/ecolink-web/src/lib/recyclerWorkflowApi.ts#L945-L955) ánh xạ `AwaitingConfirmation` $\rightarrow$ `"collected"` và `Completed` $\rightarrow$ `"done"`.
   * Vì lỗi (1) & (2) khiến PostgreSQL chưa từng tạo biên nhận và chưa cập nhật `HandoverRequest.Status = AwaitingConfirmation`, mọi đơn của người dân trên server vẫn ở `Accepted` / `Scheduled` (`"accepted"`). Kết quả: `found` là `undefined`, `targetId` là `undefined`, trình duyệt không gọi `GET /api/v1/handover-requests/{id}/receipt`, và màn hình rơi vào nhánh `!h` (*"Chưa có biên nhận thu gom nào đang chờ xác nhận."*).
   * Ngay cả trong trường hợp có nhiều đơn, việc chỉ lấy đơn `.find(...)` đầu tiên mà không liệt kê danh sách biên nhận chờ xác nhận cũng làm người dùng dễ bỏ sót đơn khi mở trực tiếp từ Sidebar.

---

### 3.4. Lỗi 4 (Phụ trợ): Upload ảnh bằng chứng cân (`BR-HND-04`) chưa upload tệp thật lên Storage

* Trong [`ecolink-web/src/pages/recycler/Receipt.tsx#L137-L141`](file:///d:/Project/ecolink-web/src/pages/recycler/Receipt.tsx#L137-L141):
  ```ts
  scaleProofPhotoUrls:
    files.length > 0
      ? files.map((f) => `https://images.ecolink.vn/proofs/${encodeURIComponent(f.name)}`)
      : ["https://images.ecolink.vn/proofs/scale-verified.jpg"],
  receiptImageUrl: "https://images.ecolink.vn/proofs/receipt-standard.jpg",
  ```
* Dù người thu gom chọn file ảnh từ máy (hoặc bỏ trống như trong ảnh chụp), Frontend chưa thực hiện upload tệp ảnh thực lên Cloudflare R2 trước khi gửi URL, và cũng không chặn submit khi chưa có ảnh cân theo quy tắc `BR-HND-04`.

---

## 4. ĐÁNH GIÁ TÁC ĐỘNG HỆ THỐNG (SYSTEM IMPACT)

| Thành phần / Tác nhân | Mức độ ảnh hưởng | Mô tả tác động thực tế |
| :--- | :--- | :--- |
| **Luồng lập biên nhận (`R-06`)** | Cao | Giao dịch kết thúc giả trên máy Recycler (`#REC-xxxxxx` trong `localStorage`) dù Backend đã từ chối `400 Bad Request`. |
| **Luồng xác nhận (`U-09`)** | Cao | Citizen hoàn toàn không thấy biên nhận chờ xác nhận trên cả `/user/receipt` lẫn `/user/handover` (nút *"Xác nhận biên nhận"* chỉ xuất hiện khi `status === "collected"`). |
| **CSDL PostgreSQL & Gamification (`BR-GAM-01`)** | Cao | Không phát sinh bản ghi trong `handover_receipts`, không kích hoạt `JOB-01` tự hoàn tất sau 48h, không cộng Điểm Xanh và không ghi nhận lịch sử giao dịch (`U-12`). |

---

## 5. GIẢI PHÁP KHẮC PHỤC ĐỀ XUẤT (ACTIONABLE REMEDIATION)

### 5.1. Đọc & gửi đúng `ItemId` / `WasteCategoryId` (Guid) khi lập biên nhận (`R-06`)
1. **Cách khuyến nghị (Sử dụng API chuẩn của Module Handover):**
   * Khi tải trang `/recycler/receipt?orderId={requestId}`, sau khi có `targetId`, gọi `GET /api/v1/recycler/handover-requests/{requestId}` (đã trả `HandoverRequestDto` gồm danh sách `Items` với đầy đủ `id: Guid`, `wasteCategoryId: Guid`, `wasteCategoryCode`, `wasteCategoryName`, `estimatedWeightKg`, `agreedPricePerKg`).
   * Lưu `itemId` (`i.id`) và `wasteCategoryId` (`i.wasteCategoryId`) trên từng dòng của state bảng cân đo.
   * Khi submit `POST /api/v1/recycler/handover-requests/{requestId}/receipt`, truyền chính xác:
     ```ts
     items: rows.map((r) => ({
       itemId: r.itemId ?? null,
       wasteCategoryId: r.itemId ? null : r.wasteCategoryId, // Chỉ gửi wasteCategoryId (Guid) nếu là dòng phát sinh (AF-01)
       actualWeightKg: r.kg,
     }))
     ```
2. **Bổ trợ phía Backend (nếu muốn đồng bộ thêm từ `TradeOrderDto`):**
   * Đảm bảo mọi đơn từ `TradeOrder` đã được đồng bộ sang `HandoverRequest` thông qua `HandoverTradeSyncHelper.EnsureHandoverFromTradeOrderAsync` (Backend đã có cơ chế tự đồng bộ trong `HandoverReceiptService.BuildPlanAsync` dòng 380–392), do đó chỉ cần gọi `GET /api/v1/recycler/handover-requests/{id}` (hoặc bổ sung `ItemId` vào `TradeOrderLineDto`) là Frontend sẽ có đủ `Guid` hợp lệ.

### 5.2. Ngừng nuốt lỗi `4xx/5xx` trong `recyclerWorkflowApi.ts`
* Trong [`createHandoverReceipt`](file:///d:/Project/ecolink-web/src/lib/recyclerWorkflowApi.ts#L1467-L1484) (và `confirmHandoverReceipt`, `disputeHandoverReceipt`):
  * Chỉ fallback sang `localStorage` khi **`err instanceof ApiError && err.isNetworkError`** (thực sự mất kết nối hoặc backend offline) và `hasLiveBackendAuth() === false`.
  * Nếu lỗi đến từ Backend (`status === 400`, `403`, `404`, `409`, `500`), **phải ném lỗi (`throw err`)** để `Receipt.tsx` hiển thị chính xác thông báo `err.message` cho người thu gom (ví dụ: *"Vui lòng tải lên ít nhất 1 ảnh cân"*, *"Chưa xác định được đơn giá"*, v.v.) và **tuyệt đối không tự sinh mã `#REC-...` giả**.
* Tương tự trong `fetchRecyclerReceipt` / `fetchUserReceipt`: không lấy cache `state.customReceipts` để che đè lên phản hồi `404 Not Found` từ Backend thật khi `hasLiveBackendAuth() === true`.

### 5.3. Cải thiện điều hướng & tải danh sách chờ xác nhận phía User (`/user/receipt`)
* Tại [`ecolink-web/src/pages/user/Receipt.tsx`](file:///d:/Project/ecolink-web/src/pages/user/Receipt.tsx):
  * Khi vào `/user/receipt` không có `?orderId=...`, nên ưu tiên gọi `GET /api/v1/handover-requests?status=AwaitingConfirmation` (nơi mỗi phần tử có sẵn cờ `hasReceipt: true` từ `HandoverRequestDto`) bên cạnh danh sách `trade/orders`.
  * Nếu có nhiều đơn đang chờ xác nhận, hiển thị danh sách cho người dân chọn đơn muốn đối chiếu thay vì chỉ lấy phần tử `.find(...)` đầu tiên.
* Cập nhật luồng upload ảnh bằng chứng cân sử dụng dịch vụ upload lên R2 (hoặc yêu cầu chọn tối thiểu 1 ảnh cân thực tế theo `BR-HND-04`) trước khi gửi `createHandoverReceipt`.

---

## 6. BÀI HỌC VÀ BIỆN PHÁP PHÒNG NGỪA (LESSONS LEARNED & PREVENTIONS)

1. **Nguyên tắc phân biệt lỗi mạng vs. lỗi nghiệp vụ (`Fail Loud on 4xx/5xx`):**
   * Cơ chế offline cache chỉ được phép kích hoạt ở chế độ Demo không đăng nhập hoặc khi `isNetworkError === true`. Mọi phản hồi `4xx`/`5xx` từ WebApi đều là quy tắc nghiệp vụ/hợp đồng dữ liệu bị vi phạm và **phải hiển thị trực tiếp lên UI**, tuyệt đối không dùng `catch {}` nuốt lỗi để tạo dữ liệu giả.
2. **Kiểm tra hợp đồng TypeScript khớp 1-1 với DTO Backend (`Guid` vs `string`):**
   * Không dùng trường `string` mã ngắn hiển thị UI (`"pet"`, `"plastic"`) để truyền vào tham số định danh `Guid?` (`WasteCategoryId` / `ItemId`) của Backend. Cần tách rõ `displayCode` và `guidId` ngay trong lớp mapping API Frontend.
3. **Kiểm thử E2E bắt buộc trên hai trình duyệt/profile cô lập:**
   * Các luồng tương tác hai chiều (Citizen $\leftrightarrow$ Recycler) bắt buộc phải kiểm thử trên hai profile/cửa sổ tách biệt (`Normal` vs `InPrivate`) kèm kiểm tra tab **Network** và truy vấn trực tiếp bảng `handover_receipts` / `handover_requests` trong PostgreSQL để phát hiện sớm hiện tượng lưu giả cục bộ trên `localStorage`.
