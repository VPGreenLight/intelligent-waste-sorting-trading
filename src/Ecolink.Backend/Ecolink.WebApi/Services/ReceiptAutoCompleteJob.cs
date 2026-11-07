using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ecolink.Application.Interfaces;

namespace Ecolink.WebApi.Services;

/// <summary>JOB-01: mỗi 60 phút tự hoàn tất các biên nhận chờ người dân xác nhận quá 48 giờ (BR-HND-07, BR-GAM-01).</summary>
public class ReceiptAutoCompleteJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(60);
    private static readonly TimeSpan ConfirmationTimeout = TimeSpan.FromHours(48);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<ReceiptAutoCompleteJob> _logger;

    public ReceiptAutoCompleteJob(IServiceScopeFactory scopes, ILogger<ReceiptAutoCompleteJob> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IHandoverReceiptService>();
                await service.AutoCompleteExpiredAsync(ConfirmationTimeout, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "JOB-01 thất bại, sẽ thử lại ở lượt sau.");
            }

            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
