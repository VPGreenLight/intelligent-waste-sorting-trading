using System.Collections.Concurrent;
using System.Threading.Channels;
using Ecolink.Application.Common;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Messaging;

/// <summary>
/// Channel-based In-Memory Event Bus bảo đảm non-blocking PublishAsync (&lt;1ms)
/// và lưu dấu Idempotency theo EventId để EventBackgroundWorker không xử lý lặp.
/// </summary>
public sealed class InMemoryChannelEventBus : IEventBus
{
    private readonly Channel<IIntegrationEvent> _channel;
    private readonly ConcurrentDictionary<Guid, DateTime> _processedEvents = new();
    private readonly ILogger<InMemoryChannelEventBus> _logger;
    private long _publishCount;

    public InMemoryChannelEventBus(ILogger<InMemoryChannelEventBus> logger)
    {
        _logger = logger;
        _channel = Channel.CreateBounded<IIntegrationEvent>(new BoundedChannelOptions(2048)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ChannelReader<IIntegrationEvent> Reader => _channel.Reader;

    public async ValueTask PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken ct = default)
        where TEvent : class, IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _channel.Writer.WriteAsync(integrationEvent, ct);
        Interlocked.Increment(ref _publishCount);

        _logger.LogDebug(
            "EventBus published {EventType} ({EventId}) at {OccurredOnUtc}",
            integrationEvent.EventType,
            integrationEvent.EventId,
            integrationEvent.OccurredOnUtc);
    }

    public bool TryMarkProcessed(Guid eventId)
    {
        var now = DateTime.UtcNow;
        if (_processedEvents.Count > 10_000)
        {
            var cutoff = now.AddHours(-1);
            foreach (var kv in _processedEvents)
            {
                if (kv.Value < cutoff)
                {
                    _processedEvents.TryRemove(kv.Key, out _);
                }
            }
        }

        return _processedEvents.TryAdd(eventId, now);
    }
}
