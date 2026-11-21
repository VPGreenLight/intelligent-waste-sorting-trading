using Ecolink.Application.Events;
using Ecolink.Application.Interfaces;
using Ecolink.Infrastructure.Messaging;

namespace Ecolink.WebApi.Services;

/// <summary>
/// Worker chạy nền tiêu thụ sự kiện từ InMemoryChannelEventBus, bảo đảm Idempotency
/// theo EventId và cô lập lỗi (fault-isolated) để không làm hỏng luồng HTTP chính.
/// </summary>
public sealed class EventBackgroundWorker : BackgroundService
{
    private readonly InMemoryChannelEventBus _eventBus;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventBackgroundWorker> _logger;

    public EventBackgroundWorker(
        InMemoryChannelEventBus eventBus,
        IServiceScopeFactory scopeFactory,
        ILogger<EventBackgroundWorker> logger)
    {
        _eventBus = eventBus;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("EventBackgroundWorker started consuming SystemIntegrationEvents.");

        try
        {
            await foreach (var integrationEvent in _eventBus.Reader.ReadAllAsync(stoppingToken))
            {
                if (!_eventBus.TryMarkProcessed(integrationEvent.EventId))
                {
                    _logger.LogWarning(
                        "Duplicate integration event {EventType} ({EventId}) skipped for idempotency.",
                        integrationEvent.EventType,
                        integrationEvent.EventId);
                    continue;
                }

                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    switch (integrationEvent)
                    {
                        case SystemNotificationDispatchedEvent notifEvent:
                        {
                            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                            await notificationService.CreateAndPushAsync(
                                notifEvent.RecipientUserId,
                                notifEvent.Title,
                                notifEvent.Message,
                                notifEvent.NotificationType,
                                notifEvent.ReferenceType,
                                notifEvent.ReferenceId,
                                stoppingToken);
                            break;
                        }
                        default:
                            _logger.LogDebug(
                                "Unhandled integration event type {EventType} ({EventId}).",
                                integrationEvent.EventType,
                                integrationEvent.EventId);
                            break;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Fault-isolation (rule 03): Side-effect handler thất bại không được làm crash worker hay API chính.
                    _logger.LogError(
                        ex,
                        "Error while handling integration event {EventType} ({EventId}).",
                        integrationEvent.EventType,
                        integrationEvent.EventId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal graceful shutdown
        }
    }
}
