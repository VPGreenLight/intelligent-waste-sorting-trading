namespace Ecolink.Application.Common;

/// <summary>
/// Hợp đồng chung cho mọi Integration Event chạy bất đồng bộ qua In-Memory Channel EventBus.
/// Mọi Event bắt buộc mang EventId và OccurredOnUtc để đảm bảo Idempotency và Audit (rule 03).
/// </summary>
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime OccurredOnUtc { get; }
    string EventType { get; }
}

/// <summary>
/// Lớp cơ sở cho Integration Event với EventId + timestamp UTC tự sinh.
/// Tên Event đặt theo thì quá khứ: [Entity][ActionInPastTense]Event.
/// </summary>
public abstract record BaseIntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
    public virtual string EventType => GetType().Name;
}

/// <summary>
/// Interface trung chuyển sự kiện nội bộ phi đồng bộ (Channel-backed EventBus).
/// Giúp giữ Main Business Flow dưới 100ms bằng cách tách side-effect thông báo / audit.
/// </summary>
public interface IEventBus
{
    ValueTask PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : class, IIntegrationEvent;
}
