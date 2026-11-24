# TÀI LIỆU ĐẶC TẢ API & SIGNALR HUB – MODULE 8 & 9: CHATBOX THƯƠNG LƯỢNG & ĐƠN GIAO DỊCH TRỰC TIẾP (IN-APP NEGOTIATION CHATBOX & TRADE ORDERS)
**Mã phân hệ:** `M-08` (`NEG` – `Use Cases U-08`, `R-05`) & `M-09` (`TRD` – `Use Cases U-07`, `R-03`, `R-04`, `U-09`)  
**Controllers & Hubs:** [`NegotiationChatController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/NegotiationChatController.cs) (`7` REST + `1` WS = `8` APIs), [`NegotiationChatHub.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Hubs/NegotiationChatHub.cs), [`TradeController.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.WebApi/Controllers/TradeController.cs) (`5` APIs thuộc `M-09`)  
**Services thực thi:** [`NegotiationChatService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/NegotiationChatService.cs), [`TradeTransactionService`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/TradeAndGamificationServices.cs), [`HandoverTradeSyncHelper.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/HandoverTradeSyncHelper.cs), [`CloudflareR2StorageService.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Infrastructure/Services/CloudflareR2StorageService.cs)  
**Validators:** [`NegotiationChatValidators.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Validators/NegotiationChatValidators.cs)  
**Entities:** [`TradeOrder`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/TradeEntities.cs#L36-L68), [`ChatMessage`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Domain/Entities/TradeEntities.cs#L73-L97)  
**DTOs:** [`NegotiationChatDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/NegotiationChatDtos.cs), [`TradeAndGamificationDtos.cs`](file:///c:/Projects/EcoLink/intelligent-waste-sorting-trading/src/Ecolink.Backend/Ecolink.Application/Dtos/TradeAndGamificationDtos.cs)  
**Base REST Routes:** `/api/v1/trade/orders` & `/api/v1/trade/orders/{orderId:guid}` & `/api/v1/trade/receipts`  
**SignalR WebSocket Endpoint:** `/hubs/negotiation-chat`

---

## 1. TỔNG QUAN DANH SÁCH ENDPOINTS (`M-08`: 8 APIs & `M-09`: 5 APIs = `13` APIs)

> Tham chiếu Inventory: [`00_api_inventory_and_endpoint_statistics.md`](./00_api_inventory_and_endpoint_statistics.md) (`STT 064` $\rightarrow$ `076`).

### 1.1. Danh mục RESTful API Endpoints (`NegotiationChatController` + `TradeController`)

| STT | Inventory ID | Phân hệ | Phương thức | Endpoint URI | Quyền truy cập | Use Case | Chức năng nghiệp vụ | Mã thông báo / Quy tắc |
|:---:|:---:|:---:|:---:|:---|:---:|:---:|:---|:---|
| 1 | `064` | `M-08` | `GET` | `/api/v1/trade/orders/{orderId:guid}/messages` | `[Authorize]` (`Citizen`/`Recycler`, hoặc `Staff`/`Admin` khi `Disputed`) | `U-08`, `R-05` | Lấy tổng quan phòng chat (`OrderChatThreadDto` gồm `isProposalLocked`, `isInquiryOnly`, `pickupAddress`, `activeProposal`, `unreadCount`) và danh sách tin nhắn có phân trang. | `BR-NEG-01`, `BR-NEG-02`, `BR-NEG-03`, `BR-DSP-06`, `MSG-NEG-THREAD-OK` |
| 2 | `065` | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/messages` | `[Authorize]` (`Citizen` / `Recycler` của đơn) | `U-08`, `R-05` | Gửi tin nhắn `Text` hoặc `Image`. Tự động phát SignalR `ReceiveChatMessage` vào phòng và `OrderChatUpdated` tới group `user:{uid}`. | `BR-NEG-01`, `BR-NEG-02`, `MSG-NEG-02` |
| 3 | `066` | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/messages/attachments` | `[Authorize]` (`Citizen` / `Recycler` của đơn) | `U-08`, `R-05` | Tải ảnh chụp thực tế phế liệu hoặc điểm hẹn (`jpg`, `png`, `webp`, tối đa `5 MB`) lên **Cloudflare R2** và nhận `attachmentUrl`. | `BR-NEG-01`, `MSG-NEG-UPLOAD-OK` |
| 4 | `067` | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/proposals` | `[Authorize]` (`Citizen` / `Recycler` của đơn) | `U-08`, `R-05` | Gửi đề xuất (`Proposal`) gồm đơn giá, khối lượng, khung giờ thu gom (ràng buộc `[hôm nay .. +30 ngày]`), `categoryCode`, `pickupAddress`, `pickupLatitude/Longitude`. | `BR-NEG-02`, `BR-NEG-03`, `BR-NEG-04`, `MSG-NEG-01` |
| 5 | `068` | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/proposals/{messageId:guid}/respond` | Đối phương của người tạo `Proposal` | `U-08`, `R-05` | Chấp nhận (`isAccepted = true`) hoặc từ chối (`false`) đề xuất `Pending` mới nhất; khi chấp nhận tự nâng cấp đơn `isInquiryDraft` sang yêu cầu chính thức. | `BR-NEG-02`, `BR-NEG-03`, `BR-NEG-05`, `MSG-NEG-03` |
| 6 | `069` | `M-08` | `POST` | `/api/v1/trade/orders/{orderId:guid}/accept-terms` | `Recycler` (Nhận đơn) | `R-05` | `Recycler` chốt trực tiếp điều khoản & khung giờ đề xuất ban đầu của đơn mà không cần gửi phản đề xuất. | `BR-NEG-02`, `BR-NEG-05`, `MSG-NEG-03` |
| 7 | `070` | `M-08` | `PUT` | `/api/v1/trade/orders/{orderId:guid}/messages/read` | `[Authorize]` (`Citizen` / `Recycler` của đơn) | `U-08`, `R-05` | Đánh dấu toàn bộ tin nhắn chưa đọc của đối phương là đã đọc (`isRead = true`, `readAt = UtcNow`). | `BR-NEG-01`, `MSG-NEG-READ-OK` |
| 8 | `072` | `M-09` | `GET` | `/api/v1/trade/orders` | Authenticated (`userId?` query fallback) | `U-07`, `R-03`, `U-08` | Lấy danh sách `TradeOrderDto` của tài khoản (gồm cả đơn nháp `isInquiryOnly`), làm giàu `lastMessagePreview` (plain-text không emoji), `lastMessageSenderId`, `lastMessageSenderRole`, `lastMessageAt`, `unreadCount`. | `200 OK` |
| 9 | `073` | `M-09` | `POST` | `/api/v1/trade/orders` | Authenticated | `U-06`, `U-08` | Tạo đơn bàn giao (`CreateTradeOrderRequest`) hoặc phiên chat hỏi trước (`isInquiryDraft = true` tái sử dụng phiên `Pending/Negotiating` đang mở giữa cùng hai bên); phát SignalR `OrderChatUpdated`. | `200 OK` |
| 10 | `074` | `M-09` | `PATCH` | `/api/v1/trade/orders/{orderId:guid}/status` | `[Authorize]` / Participant | `U-07`, `R-03`, `R-04` | Cập nhật trạng thái đơn (`Accepted`, `Rejected`, `Cancelled`, `Scheduled`, `Completed`) kèm `reason`, đồng bộ `HandoverRequest` & phát SignalR `OrderChatUpdated`. | `200 OK` |
| 11 | `075` | `M-09` | `PATCH` | `/api/v1/trade/orders/{orderId:guid}/time-slot` | `[Authorize]` / Participant | `U-08` | Đổi khung giờ thu gom đề xuất (`proposedTimeSlot`) khi đơn còn mở thương lượng và phát SignalR `OrderChatUpdated`. | `200 OK` |
| 12 | `076` | `M-09` | `POST` | `/api/v1/trade/receipts/confirm-qr` | Authenticated | `R-06`, `U-09` | Quét mã `qrSecurityCode` chốt `ActualWeightKg`, `UnitPrice`, tạo `TransactionReceiptDto` và cộng Điểm Xanh. | `200 OK` / `404 Not Found` |

### 1.2. Kênh Sự kiện Thời gian thực SignalR (`/hubs/negotiation-chat` – `STT 071`, `M-08`)

* **Xác thực kết nối WebSocket:** Truyền JWT Access Token qua HTTP Header `Authorization: Bearer <token>`, HttpOnly Cookie `accessToken` hoặc Query Parameter `/hubs/negotiation-chat?access_token=<token>`. Khi kết nối, Hub tự đưa client vào user group `"user:{userId}"` để nhận tín hiệu cập nhật danh sách chat ngay cả khi chưa mở phòng chat cụ thể.

| Chiều giao tiếp | Tên Hub Method / Client Event | Payload / Tham số | Mô tả |
|:---:|:---|:---|:---|
| **Client $\rightarrow$ Server** | `JoinOrderChatRoom` | `(Guid orderId)` | Kiểm tra quyền tham gia phòng chat (`BR-NEG-01`) và đưa kết nối vào SignalR Group `"order:{orderId}"`. |
| **Client $\rightarrow$ Server** | `LeaveOrderChatRoom` | `(Guid orderId)` | Rời khỏi SignalR Group `"order:{orderId}"`. |
| **Client $\rightarrow$ Server** | `SendTypingIndicator` | `(Guid orderId, bool isTyping)` | Phát trạng thái đang gõ phím tới đối phương trong phòng chat. |
| **Server $\rightarrow$ Client** | `ReceiveChatMessage` | `{ orderId, message: ChatMessageDto, notification }` | Phát tức thời khi có tin nhắn `Text` hoặc `Image` mới kèm thông báo `MSG-NEG-02`. |
| **Server $\rightarrow$ Client** | `ProposalCreated` | `{ orderId, proposalMessage: ChatMessageDto, supersededMessageIds: Guid[], notification }` | Phát khi có đề xuất `Proposal` mới; giao diện tự động làm mờ các `supersededMessageIds` (`BR-NEG-03`). |
| **Server $\rightarrow$ Client** | `ProposalResponded` | `{ orderId, result: ProposalAcceptanceResultDto, notification }` | Phát khi đề xuất được chấp nhận/từ chối hoặc khi `Recycler` chốt điều khoản (`MSG-NEG-03`, `BR-NEG-05`). |
| **Server $\rightarrow$ Client** | `MessagesMarkedAsRead` | `{ orderId, readerUserId, markedCount, readAtUtc }` | Cập nhật trạng thái "Đã xem" trên giao diện người gửi. |
| **Server $\rightarrow$ Client** | `OrderChatUpdated` | `{ orderId, eventType, updatedAt }` | Đẩy tới SignalR Group `"user:{userId}"` của các bên liên quan khi tạo đơn, đổi trạng thái/khung giờ hoặc phát sinh tin nhắn/đề xuất mới để danh sách hội thoại tự động làm mới. |

---

## 2. CHI TIẾT TỪNG REST API ENDPOINT

---

### 2.1. Xem Phòng Chat & Lịch sử Tin nhắn Bất biến (`GET /api/v1/trade/orders/{orderId:guid}/messages`)

* **Mô tả:** Trả về điều khoản hiện tại của đơn bàn giao, trạng thái khóa thương lượng (`isProposalLocked` theo `BR-NEG-02`), bản đề xuất `Pending` mới nhất (`activeProposal` theo `BR-NEG-03`) và danh sách tin nhắn phân trang theo thứ tự thời gian. Khi đơn ở trạng thái `Disputed`, nhân viên `Staff`/`Admin` có quyền gọi API này để đối soát bằng chứng (`BR-DSP-06`).

#### a. Path & Query Parameters

| Tham số | Vị trí | Kiểu dữ liệu | Bắt buộc | Mặc định | Mô tả |
|:---|:---:|:---:|:---:|:---:|:---|
| `orderId` | Path | `Guid` | Có | — | Định danh đơn bàn giao (`trade_orders.id`). |
| `page` | Query | `int` | Không | `1` | Trang cần lấy (`>= 1`). |
| `pageSize` | Query | `int` | Không | `50` | Số lượng tin nhắn mỗi trang (`1..100`). |

#### b. Ví dụ Response (`200 OK` – `ApiResponse<OrderChatThreadDto>`):
```json
{
  "success": true,
  "messageCode": "MSG-NEG-THREAD-OK",
  "message": "Tải lịch sử thương lượng và điều khoản đơn bàn giao thành công.",
  "data": {
    "orderId": "8f2b1c40-4a12-4e89-9b32-7c901234abcd",
    "citizenUserId": "11111111-1111-1111-1111-111111111111",
    "citizenName": "Nguyễn Văn An",
    "recyclerUserId": "22222222-2222-2222-2222-222222222222",
    "facilityName": "Vựa phế liệu EcoGreen",
    "orderStatus": "Negotiating",
    "categoryCode": "plastic",
    "estimatedWeightKg": 12.5,
    "initialPricePerKg": 7000,
    "agreedPricePerKg": null,
    "proposedTimeSlot": "2026-11-05 14:00 - 16:00",
    "confirmedTimeSlot": null,
    "scheduledAt": "2026-11-05T07:00:00Z",
    "acceptedAt": null,
    "isProposalLocked": false,
    "isChatOpen": true,
    "unreadCount": 1,
    "activeProposal": {
      "id": "b1920a11-3c44-4d88-8e11-901234567890",
      "orderId": "8f2b1c40-4a12-4e89-9b32-7c901234abcd",
      "senderUserId": "22222222-2222-2222-2222-222222222222",
      "senderName": "Vựa phế liệu EcoGreen",
      "senderRole": "Recycler",
      "messageType": "Proposal",
      "messageContent": "Chai PET sạch không lẫn nắp mình thu 7.500đ/kg chiều nay nhé.",
      "attachmentUrl": null,
      "proposal": {
        "proposedPricePerKg": 7500,
        "proposedWeightKg": 12.5,
        "proposedTimeSlot": "2026-11-05 14:00 - 16:00",
        "note": "Chai PET sạch không lẫn nắp mình thu 7.500đ/kg chiều nay nhé.",
        "proposalStatus": "Pending",
        "respondedByUserId": null,
        "respondedAt": null,
        "responseNote": null
      },
      "isRead": false,
      "sentAt": "2026-11-03T04:30:00Z",
      "readAt": null
    },
    "messages": {
      "items": [],
      "pageNumber": 1,
      "pageSize": 50,
      "totalRecords": 3,
      "totalPages": 1,
      "hasPreviousPage": false,
      "hasNextPage": false
    }
  },
  "errors": null,
  "timestamp": "2026-11-03T05:00:00Z"
}
```

---

### 2.2. Gửi Tin nhắn Văn bản hoặc Hình ảnh (`POST /api/v1/trade/orders/{orderId:guid}/messages`)

* **Mô tả:** Gửi tin nhắn thông thường (`Text` hoặc `Image`) giữa Người dân (`U-08`) và Cơ sở thu gom (`R-05`). Theo **`BR-NEG-02`**, endpoint này vẫn hoạt động ngay cả khi đơn đã chuyển sang `Accepted`, `Scheduled` hoặc `Completed`.

#### a. Request Body (`SendChatMessageRequest`)

| Trường | Kiểu dữ liệu | Bắt buộc | Ràng buộc (`SendChatMessageRequestValidator`) | Mô tả |
|:---|:---:|:---:|:---|:---|
| `messageType` | `string` | Có | `"Text"` hoặc `"Image"` | Loại tin nhắn thông thường. |
| `messageContent` | `string?` | Bắt buộc nếu `Text` | `1..2000` ký tự (`Text`) hoặc `<= 500` ký tự (`Image`) | Nội dung văn bản hoặc chú thích ảnh. |
| `attachmentUrl` | `string?` | Bắt buộc nếu `Image` | `<= 1000` ký tự | URL ảnh trả về từ API upload Cloudflare R2. |

#### b. Ví dụ Request (`Text`):
```json
{
  "messageType": "Text",
  "messageContent": "Mình đã ép dẹp toàn bộ vỏ chai PET và buộc sẵn thành 2 bao tải rồi nhé.",
  "attachmentUrl": null
}
```

---

### 2.3. Tải Ảnh Đính kèm Phòng Chat lên Cloudflare R2 (`POST /api/v1/trade/orders/{orderId:guid}/messages/attachments`)

* **Content-Type:** `multipart/form-data`
* **Form Field:** `file` (`IFormFile` — chấp nhận `image/jpeg`, `image/png`, `image/webp`, dung lượng tối đa `5 MB`).
* **Response (`200 OK` – `ApiResponse<ChatAttachmentUploadResponseDto>`):** Trả về `attachmentUrl` công khai trên Cloudflare R2 để truyền vào `POST .../messages` (`messageType = "Image"`).

---

### 2.4. Gửi Đề xuất / Phản đề xuất Giá & Khung giờ (`POST /api/v1/trade/orders/{orderId:guid}/proposals`)

* **Mô tả:** Cho phép `Citizen` (`U-08`) hoặc `Recycler` (`R-05`) gửi bản đề xuất (`Proposal`) điều chỉnh đơn giá, khối lượng ước tính và khung giờ thu gom (`BR-NEG-04`).
  * Nếu đơn đang `Pending`, tự động chuyển sang `Negotiating` (`BR-HND-02`).
  * Mọi đề xuất `Pending` trước đó của đơn tự động chuyển sang `Superseded` (`BR-NEG-03`).
  * Nếu đơn đã `Accepted`, `Scheduled`, `Completed`, `Rejected`, `Cancelled` $\rightarrow$ trả về `400 Bad Request` (`MSG-NEG-PROPOSAL-LOCKED` – `BR-NEG-02`).

#### a. Request Body (`SendProposalRequest`)

| Trường | Kiểu dữ liệu | Bắt buộc | Ràng buộc (`SendProposalRequestValidator` & `BR-NEG-04`) | Mô tả |
|:---|:---:|:---:|:---|:---|
| `proposedPricePerKg` | `decimal` | Có | `(0, 10_000_000]` | Đơn giá thu mua đề xuất (`VNĐ/kg`). |
| `proposedWeightKg` | `decimal` | Có | `(0, 100_000]` | Khối lượng ước tính đề xuất (`kg`). |
| `proposedTimeSlot` | `string` | Có | Không rỗng, `<= 150` ký tự; ngày hẹn phải nằm trong `[hôm nay .. +30 ngày]` (`MaxAppointmentAheadDays = 30`, giờ ICT) | Khung giờ thu gom đề xuất (VD: `"2026-11-05 14:00 - 16:00"`). |
| `categoryCode` | `string?` | Không | Mã danh mục tái chế hợp lệ (VD: `"plastic"`, `"paper"`) | Cập nhật nhóm phế liệu đang thương lượng (nếu đổi từ nháp chat). |
| `pickupAddress` | `string?` | Không | Địa chỉ điểm hẹn lấy hàng | Cập nhật địa chỉ thu gom trước khi chốt đơn. |
| `pickupLatitude` / `pickupLongitude` | `double?` | Không | Tọa độ GPS điểm hẹn | Cập nhật tọa độ điểm hẹn (ẩn khỏi API phía Recycler khi chưa `Accepted`). |
| `note` | `string?` | Không | `<= 1000` ký tự | Ghi chú giải thích kèm theo đề xuất. |

#### b. Ví dụ Request:
```json
{
  "proposedPricePerKg": 7500,
  "proposedWeightKg": 12.5,
  "proposedTimeSlot": "2026-11-05 14:00 - 16:00",
  "categoryCode": "plastic",
  "pickupAddress": "45 Lê Duẩn, Bến Nghé, Quận 1, TP.HCM",
  "note": "Buổi sáng xe tải kho mình bận tuyến Quận 9, hẹn bạn đầu giờ chiều mình ghé cân luôn giá 7.500đ/kg nhé!"
}
```

---

### 2.5. Phản hồi Chấp nhận / Từ chối Đề xuất (`POST /api/v1/trade/orders/{orderId:guid}/proposals/{messageId:guid}/respond`)

* **Mô tả:** Đối phương của người gửi đề xuất bấm **Chấp nhận** (`isAccepted = true`) hoặc **Từ chối** (`isAccepted = false`) trên bản đề xuất mới nhất đang `Pending`:
  * **Chặn vi phạm `BR-NEG-03`:** Trả về `400 Bad Request` nếu người gọi tự chấp nhận đề xuất của chính mình (`MSG-NEG-SELF-RESPONSE`) hoặc bấm vào đề xuất cũ đã bị ghi đè (`MSG-NEG-SUPERSEDED`).
  * **Khi `isAccepted = true` (`BR-NEG-03`, `BR-NEG-05`):** Lưu cứng `agreedPricePerKg`, `estimatedWeightKg`, `confirmedTimeSlot` vào `TradeOrder`, chuyển trạng thái đơn sang `Accepted`, bật cờ `isProposalLocked = true` (`BR-NEG-02`), phát sinh tin nhắn `System` và thông báo `MSG-NEG-03`.

#### a. Ví dụ Request:
```json
{
  "isAccepted": true,
  "responseNote": "Đồng ý khung giờ 14:00 - 16:00 chiều nay nhé!"
}
```

#### b. Ví dụ Response (`200 OK` – `MSG-NEG-03`):
```json
{
  "success": true,
  "messageCode": "MSG-NEG-03",
  "message": "Nguyễn Văn An đã chấp nhận đề nghị của bạn.",
  "data": {
    "orderId": "8f2b1c40-4a12-4e89-9b32-7c901234abcd",
    "proposalMessageId": "b1920a11-3c44-4d88-8e11-901234567890",
    "proposalStatus": "Accepted",
    "orderStatus": "Accepted",
    "agreedPricePerKg": 7500,
    "confirmedWeightKg": 12.5,
    "confirmedTimeSlot": "2026-11-05 14:00 - 16:00",
    "acceptedAt": "2026-11-03T04:35:10Z",
    "isProposalLocked": true,
    "systemMessage": {
      "id": "c7812e99-1122-4333-9444-556677889900",
      "orderId": "8f2b1c40-4a12-4e89-9b32-7c901234abcd",
      "senderUserId": "11111111-1111-1111-1111-111111111111",
      "senderName": "Hệ thống EcoLink",
      "senderRole": "System",
      "messageType": "System",
      "messageContent": "Nguyễn Văn An đã chấp nhận đề nghị của bạn. Điều khoản đã khóa: 7,500 VNĐ/kg • 12.5 kg • Khung giờ hẹn: 2026-11-05 14:00 - 16:00.",
      "attachmentUrl": null,
      "proposal": null,
      "isRead": false,
      "sentAt": "2026-11-03T04:35:10Z",
      "readAt": null
    }
  },
  "errors": null,
  "timestamp": "2026-11-03T04:35:10Z"
}
```

---

### 2.6. Recycler Chốt Trực tiếp Khung giờ Ban đầu của Đơn (`POST /api/v1/trade/orders/{orderId:guid}/accept-terms`)

* **Mô tả (`R-05` / `BR-NEG-05`):** Khi khung giờ thu gom mong muốn ban đầu (`proposedTimeSlot`) và đơn giá niêm yết của đơn bàn giao đã hoàn toàn phù hợp, `Recycler` bấm nút **"Chấp nhận yêu cầu"** để khóa ngay `confirmedTimeSlot = proposedTimeSlot` và chuyển đơn sang `Accepted` mà không cần gửi phản đề xuất trung gian.

---

### 2.7. Đánh dấu Đã đọc Tin nhắn (`PUT /api/v1/trade/orders/{orderId:guid}/messages/read`)

* **Mô tả:** Cập nhật `isRead = true` và `readAt = UtcNow` cho toàn bộ tin nhắn do đối phương gửi trong `orderId` mà người dùng hiện tại chưa đọc, đồng thời phát sự kiện SignalR `MessagesMarkedAsRead`.

---

### 2.8. Quản lý Đơn Giao dịch Trực tiếp & Phiên Chat Hỏi trước (`TradeController` – `M-09`, `STT 072–076`)

1. **`GET /api/v1/trade/orders` (`STT 072`):**
   * **Query Parameters:** `[FromQuery] Guid? userId` *(tuy nhiên khi đã đăng nhập qua JWT/Cookie, controller ưu tiên dùng `CurrentUser.UserId`)*. Hiện tại endpoint trả về toàn bộ `TradeOrderDto` mà người dùng tham gia (bao gồm cả đơn nháp khởi tạo từ chat `isInquiryOnly = true`), trong khi `GET /api/v1/handover-requests` lọc bỏ đơn nháp `isInquiryOnly == true` cho đến khi hai bên chốt thành yêu cầu chính thức.
   * **Các trường làm giàu phòng chat trong `TradeOrderDto`:**
     * `isInquiryOnly` (`bool`): `true` nếu phiên được tạo chỉ để hỏi/thương lượng trước qua chat (`isInquiryDraft = true`).
     * `lastMessagePreview` (`string?`): Định dạng plain-text **không dùng emoji** qua `FormatChatMessagePreview` (`"Đã gửi ảnh phế liệu"` nếu `Image`, `"Đề xuất: {price:N0} đ/kg · {weight:0.##} kg"` hoặc `"Bản đề xuất thương lượng"` nếu `Proposal`, tối đa `90` ký tự cắt gọn `"…"` với `Text`/`System`).
     * `lastMessageSenderId` (`Guid?`), `lastMessageSenderRole` (`string?`), `lastMessageAt` (`DateTime?`), `unreadCount` (`int`).
2. **`POST /api/v1/trade/orders` (`STT 073`):** Nhận `CreateTradeOrderRequest` (gồm `partnerId`, `scheduledAt`, `estimatedWeightKg`, `categoryCode?`, `initialPricePerKg?`, `proposedTimeSlot?`, `pickupAddress?`, `pickupLatitude/Longitude?`, `lines?`, `isInquiryDraft`). Kiểm tra khung giờ `[hôm nay .. +30 ngày]` (`HandoverTradeSyncHelper.ValidateAppointmentWindowUtc`); nếu `isInquiryDraft = true` sẽ tái sử dụng phiên chat `Pending`/`Negotiating` đang mở giữa cùng hai bên và **không** sinh `HandoverRequest` chính thức cho tới khi có đề xuất được chấp nhận. Trả `{ success, data, order, message }` và phát SignalR `OrderChatUpdated`.
3. **`PATCH /api/v1/trade/orders/{orderId:guid}/status` (`STT 074`):** Nhận `UpdateTradeOrderStatusRequest { status, reason? }`, cập nhật trạng thái đơn và phát `OrderChatUpdated` (`OrderStatusUpdated`).
4. **`PATCH /api/v1/trade/orders/{orderId:guid}/time-slot` (`STT 075`):** Nhận `UpdateTradeOrderTimeSlotRequest { proposedTimeSlot }`, kiểm tra cửa sổ `[hôm nay .. +30 ngày]` và phát `OrderChatUpdated` (`OrderTimeSlotUpdated`).
5. **`POST /api/v1/trade/receipts/confirm-qr` (`STT 076`):** Nhận `ConfirmQrReceiptRequest { tradeOrderId, qrSecurityCode, actualWeightKg, unitPrice }`, đối chiếu `qrSecurityCode` để hoàn tất giao dịch và trả về `TransactionReceiptDto`.

---

## 3. BẢNG MÃ LỖI & MÃ THÔNG BÁO TRONG MODULE 8 & 9

| HTTP Status | `messageCode` | Quy tắc | Trường hợp xảy ra |
|:---:|:---|:---:|:---|
| `200` / `201` | `MSG-NEG-01` | `BR-NEG-04` | Yêu cầu trao đổi / đề xuất của người dân đã gửi đến cơ sở thu gom. |
| `201` | `MSG-NEG-02` | `BR-NEG-01` | Gửi tin nhắn mới thành công và đẩy thông báo cho đối phương. |
| `200` | `MSG-NEG-03` | `BR-NEG-03`, `BR-NEG-05` | Chấp nhận đề nghị thành công và khóa điều khoản + khung giờ thu gom chính thức. |
| `400` | `MSG-GEN-01` | `BR-NEG-04` | `"Không thể chọn ngày hoặc khung giờ hẹn trong quá khứ."` / `"Chỉ được chọn ngày hẹn từ hôm nay đến tối đa 30 ngày tới."` |
| `400` | `MSG-NEG-PROPOSAL-LOCKED` | `BR-NEG-02` | Cố tình gửi hoặc phản hồi `Proposal` khi đơn đã chuyển sang `Accepted`, `Scheduled`, `Completed`, `Rejected` hoặc `Cancelled`. |
| `400` | `MSG-NEG-SUPERSEDED` | `BR-NEG-03` | Bấm chấp nhận một đề xuất cũ đã bị ghi đè bởi đề xuất mới hơn (`ProposalStatus == Superseded`). |
| `400` | `MSG-NEG-SELF-RESPONSE` | `BR-NEG-03` | Người gửi đề xuất tự bấm chấp nhận hoặc từ chối đề xuất của chính mình. |
| `403` | `MSG-NEG-FORBIDDEN` | `BR-NEG-01` | Người dùng thứ ba không thuộc đơn bàn giao cố truy cập lịch sử chat hoặc gửi tin nhắn. |
| `404` | `MSG-NOT-FOUND` | — | Không tìm thấy `orderId` hoặc `messageId` của bản đề xuất. |

