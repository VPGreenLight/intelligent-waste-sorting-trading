# QUY CHUẨN THIẾT KẾ VÀ LẬP TRÌNH HƯỚNG SỰ KIỆN (EVENT-DRIVEN ARCHITECTURE GUIDELINES)
**Dự án:** EcoLink - Nền tảng Phân loại Rác Thông minh & Giao dịch Tái chế  
**Phạm vi áp dụng:** Toàn bộ Backend (.NET), AI Service (FastAPI) và các dịch vụ mở rộng sau này.

---

## 1. TRIẾT LÝ CỐT LÕI (CORE PRINCIPLES)

Mọi dòng code trong hệ thống EcoLink khi phát sinh tác vụ xử lý đều phải tuân theo 4 nguyên tắc bất di bất dịch:

1. **Phân biệt rạch ròi giữa Mệnh lệnh (Command) và Sự kiện (Event):**
   - **Command (Yêu cầu hành động):** Là ý muốn làm một việc gì đó ở hiện tại hoặc tương lai. Có thể bị từ chối nếu không hợp lệ. Ví dụ: `CreateTradeOrderCommand`, `ConfirmReceiptCommand`.
   - **Event (Sự kiện đã rồi):** Là sự thật hiển nhiên đã xảy ra trong quá khứ, **không thể thay đổi hoặc thu hồi**. Ví dụ: `TradeOrderCompletedEvent`, `PaymentReceivedEvent`.
2. **Nguyên tắc "Việc chính xong trước, Việc phụ làm sau" (Zero Leaking Side-effects):**
   - Luồng nghiệp vụ chính của API phải kết thúc trong vòng **< 100ms** để trả về giao diện cho người dùng.
   - Các tác vụ như: Gửi Push Notification (FCM), Gửi Email, Cộng điểm Gamification, Nâng cấp Cây ảo, Cập nhật Bảng xếp hạng, Lưu log huấn luyện AI... **tuyệt đối không được gọi tuần tự (blocking)** trong Controller hoặc Service chính. Tất cả phải chuyển thành Event đẩy vào hàng đợi ngầm.
3. **Producer không phụ thuộc Consumer (Loose Coupling):**
   - Service phát ra sự kiện (`Publisher / Producer`) **không được phép biết hoặc quan tâm** ai đang lắng nghe (`Subscriber / Consumer`) và họ làm gì với sự kiện đó.
4. **Độc lập và Cách ly lỗi (Fault Isolation):**
   - Nếu dịch vụ gửi thông báo Firebase FCM bị sập hoặc mất mạng, sự kiện cộng điểm Gamification vẫn phải chạy thành công 100%. Lỗi của một Consumer không được làm hỏng các Consumer khác.

---

## 2. QUY CHUẨN ĐẶT TÊN VÀ ĐỊNH NGHĨA SỰ KIỆN (NAMING CONVENTIONS)

### 2.1. Tên Sự kiện (Event Naming)
- **Quy tắc:** Bắt buộc đặt ở **thì quá khứ (Past Tense)** theo cấu trúc:  
  `[Tên Thực thể / Danh từ] + [Động từ chia Quá khứ] + Event`
- ❌ **Sai:** `SendReceiptNotificationEvent`, `AwardPointsEvent`, `UpdateLeaderboard` (Đây là mệnh lệnh, không phải sự kiện).
-  **Đúng:** 
  - `TradeOrderCreatedEvent`
  - `TradeOrderCompletedEvent`
  - `PaymentWebhookReceivedEvent`
  - `WasteImageScannedEvent`
  - `UserRegisteredEvent`

### 2.2. Tên Bộ xử lý (Handler Naming)
- **Quy tắc:** Đặt tên thể hiện rõ hành vi nghiệp vụ ứng với sự kiện:  
  `[Hành động] + On + [Tên Sự kiện] + Handler`
-  **Ví dụ:**
  - `AwardPointsOnTradeOrderCompletedHandler`
  - `SendPushNotificationOnTradeOrderCompletedHandler`
  - `CollectTrainingDataOnWasteImageScannedHandler`

---

## 3. CẤU TRÚC PHÂN LỚP (CLEAN ARCHITECTURE PLACEMENT)

Mô hình Event-Driven được bố trí chuẩn trong cấu trúc thư mục của EcoLink như sau:

```text
Ecolink.Backend/
├── Ecolink.Domain/
│   └── Common/
│       └── IDomainEvent.cs              <-- Đánh dấu sự kiện nội bộ Entity
├── Ecolink.Application/
│   ├── Common/
│   │   ├── IEventBus.cs                 <-- Interface phát sự kiện (Plug-and-play)
│   │   └── IIntegrationEvent.cs         <-- Interface gốc cho mọi sự kiện
│   ├── Events/                          <-- Nơi chứa các lớp POCO Event
│   │   ├── TradeOrderCompletedEvent.cs
│   │   └── PaymentReceivedEvent.cs
│   └── EventHandlers/                   <-- Logic xử lý ngầm (Consumers)
│       ├── AwardPointsOnTradeCompletedHandler.cs
│       └── SendPushNotificationHandler.cs
└── Ecolink.Infrastructure/
    └── Messaging/                       <-- Nơi triển khai hạ tầng hàng đợi
        ├── InMemoryChannelEventBus.cs   <-- Chạy ngầm bằng .NET Channels (Giai đoạn 1)
        ├── RedisStreamEventBus.cs       <-- Đổi sang Redis khi scale (Giai đoạn 2)
        └── EventBackgroundWorker.cs     <-- BackgroundService lắng nghe và dispatch
```

---

## 4. QUY CHUẨN THIẾT KẾ PAYLOAD CỦA SỰ KIỆN (EVENT PAYLOAD)

### 4.1. Metadata bắt buộc (Enveloping)
Mọi Event khi truyền đi phải có 3 trường dữ liệu cơ sở:
1. `EventId (Guid)`: Định danh duy nhất của sự kiện, phục vụ chống trùng lặp (Idempotency).
2. `OccurredOnUtc (DateTime)`: Thời điểm sự kiện phát sinh theo chuẩn UTC.
3. `CorrelationId (string)`: Mã vết để liên kết log giữa các service từ lúc request bắt đầu đến khi background job hoàn tất.

### 4.2. Dữ liệu mang theo (Self-Contained Payload)
- **Đủ thông tin cần thiết:** Event nên chứa các trường thông tin cơ bản đủ để Consumer thực hiện nghiệp vụ mà không phải gọi ngược lại Database quá nhiều lần.
- ❌ **Không nhồi nhét dữ liệu nhị phân:** Tuyệt đối không nhét chuỗi Base64 ảnh, file nhị phân vào Event. Chỉ truyền URL ảnh, đường dẫn S3/R2 hoặc ID.

---

## 5. BA NGUYÊN TẮC BẮT BUỘC KHI VIẾT HANDLER / CONSUMER

### Quy tắc 1: Đảm bảo tính khả lặp (Idempotent Consumer)
Mạng chập chờn hoặc cơ chế retry có thể khiến một Consumer nhận cùng một `EventId` 2 lần.  
**Quy định:** Consumer phải kiểm tra trước khi ghi nhận dữ liệu nhạy cảm (cộng tiền, cộng điểm):
```csharp
// Kiểm tra xem giao dịch này đã được cộng điểm trước đó chưa
if (await _dbContext.PointHistories.AnyAsync(x => x.EventId == @event.EventId))
{
    _logger.LogWarning("Event {EventId} đã được xử lý trước đó. Bỏ qua để tránh duplicate.", @event.EventId);
    return;
}
```

### Quy tắc 2: Tự cô lập lỗi (Safe Exception Handling)
Không bao giờ để ngoại lệ unhandled trong Consumer làm crash toàn bộ tiến trình Worker nền. Mọi Consumer phải có `try-catch`, ghi log chi tiết và có chiến lược Dead-Letter hoặc Retry hợp lý.

### Quy tắc 3: Không chặn luồng (Non-blocking I/O)
100% logic trong Consumer phải sử dụng hàm bất đồng bộ (`async / await`) và tôn trọng `CancellationToken`.

---

## 6. MẪU CODE CHUẨN (STANDARD CODE BOILERPLATE)

### 6.1. Định nghĩa Base Event và Interface Bus
```csharp
// File: Ecolink.Application/Common/IIntegrationEvent.cs
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
    string? CorrelationId { get; }
}

public abstract record BaseIntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public string? CorrelationId { get; init; }
}

// File: Ecolink.Application/Common/IEventBus.cs
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) 
        where TEvent : IIntegrationEvent;
}
```

### 6.2. Tạo một Event cụ thể
```csharp
// File: Ecolink.Application/Events/TradeOrderCompletedEvent.cs
public record TradeOrderCompletedEvent : BaseIntegrationEvent
{
    public required Guid OrderId { get; init; }
    public required Guid UserId { get; init; }
    public required Guid PartnerId { get; init; }
    public required decimal ActualWeightKg { get; init; }
    public required decimal TotalAmountCash { get; init; }
}
```

### 6.3. Bắn sự kiện từ Service (Producer)
```csharp
// File: Ecolink.Infrastructure/Services/TradeTransactionService.cs
public async Task<TransactionReceiptDto> ConfirmReceiptByQrAsync(ConfirmQrReceiptRequest request)
{
    // 1. Thực hiện nghiệp vụ chính: Đổi trạng thái đơn hàng
    order.Status = "Completed";
    await _dbContext.SaveChangesAsync();

    // 2. Phát sự kiện (Chỉ mất < 5ms để đưa vào hàng đợi)
    await _eventBus.PublishAsync(new TradeOrderCompletedEvent
    {
        OrderId = order.Id,
        UserId = order.UserId,
        PartnerId = order.PartnerId,
        ActualWeightKg = request.ActualWeightKg,
        TotalAmountCash = totalCash
    });

    // 3. Trả về ngay lập tức cho màn hình ứng dụng
    return receipt;
}
```

### 6.4. Xử lý sự kiện ngầm (Consumer / Handler)
```csharp
// File: Ecolink.Application/EventHandlers/AwardPointsOnTradeCompletedHandler.cs
public class AwardPointsOnTradeCompletedHandler : IIntegrationEventHandler<TradeOrderCompletedEvent>
{
    private readonly IGamificationService _gamificationService;
    private readonly ILogger<AwardPointsOnTradeCompletedHandler> _logger;

    public AwardPointsOnTradeCompletedHandler(IGamificationService gamificationService, ILogger<AwardPointsOnTradeCompletedHandler> logger)
    {
        _gamificationService = gamificationService;
        _logger = logger;
    }

    public async Task HandleAsync(TradeOrderCompletedEvent @event, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Bắt đầu cộng điểm thưởng cho User {UserId} từ đơn {OrderId}", @event.UserId, @event.OrderId);
        await _gamificationService.AwardPointsForRecyclingAsync(@event.UserId, @event.ActualWeightKg, cancellationToken);
    }
}
```

---

## 7. BẢNG CHECKLIST KIỂM TRA CODE REVIEW (EVENT-DRIVEN CHECKLIST)

Trước khi merge bất kỳ tính năng mới nào, lập trình viên và reviewer phải tự kiểm tra 5 câu hỏi sau:

- [ ] **Câu 1:** Hàm API này có tác vụ nào thuộc dạng "việc phụ" (gửi mail, thông báo, tích điểm, ghi log nặng) đang chạy đồng bộ (`await`) làm người dùng phải chờ không? *(Nếu có: Phải tách thành Event)*.
- [ ] **Câu 2:** Tên lớp Event đã đặt ở **thì quá khứ** (`-edEvent`) chưa?
- [ ] **Câu 3:** Event có kế thừa từ `BaseIntegrationEvent` để có sẵn `EventId` và `OccurredOnUtc` chưa?
- [ ] **Câu 4:** Consumer xử lý Event có đảm bảo **Idempotent** (không bị lỗi hoặc nhân đôi dữ liệu nếu nhận 2 lần) chưa?
- [ ] **Câu 5:** Lỗi trong Consumer có được bắt và ghi log riêng biệt mà không ảnh hưởng đến luồng chính không?
