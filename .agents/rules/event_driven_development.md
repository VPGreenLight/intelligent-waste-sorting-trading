---
description: Quy chuẩn thiết kế và lập trình Hướng sự kiện (Event-Driven Development) cho EcoLink
globs: ["src/Ecolink.Backend/**/*", "src/Ecolink.AiService/**/*"]
---

# Quy tắc Lập trình Hướng Sự Kiện (Event-Driven Guidelines) cho EcoLink

Khi tạo mới hoặc sửa đổi code trong hệ thống EcoLink, hãy tuân thủ nghiêm ngặt các nguyên tắc sau:

1. **Phân tách Rõ Ràng Command & Event:**
   - Luồng nghiệp vụ chính (Request-Response) chỉ xử lý trạng thái cốt lõi và hoàn tất trong < 100ms.
   - Các tác vụ phụ (Side-effects): Gamification (cộng điểm, cây ảo, rank), Thông báo (FCM Push, Email), Lưu log/dataset AI... **BẮT BUỘC** phải phát sinh Event (`IIntegrationEvent`) và đẩy vào hàng đợi ngầm qua `IEventBus`.
   
2. **Quy chuẩn đặt tên:**
   - Tên Event luôn ở thì quá khứ: `[Entity][ActionInPastTense]Event` (ví dụ: `TradeOrderCompletedEvent`, `PaymentWebhookReceivedEvent`).
   - Tên Handler: `[Action]On[EventName]Handler` (ví dụ: `AwardPointsOnTradeCompletedHandler`).

3. **Tính chất của Event & Handler:**
   - Mọi Event phải có `EventId` (Guid) và `OccurredOnUtc` (DateTime).
   - Payload gọn nhẹ, tự thân (Self-contained), không truyền file nhị phân/base64 trong Event.
   - Consumer/Handler phải đảm bảo **Idempotency** (chống lặp khi nhận trùng message) và bắt ngoại lệ an toàn, không làm gián đoạn hệ thống.

Chi tiết tham khảo tài liệu chuẩn tại: `documents/event_driven_architecture_guidelines.md`.
