using Ecolink.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecolink.WebApi.Services;

/// <summary>JOB-03: mỗi 30 phút chuyển các yêu cầu Pending quá hạn phản hồi (mặc định 24 giờ) sang Expired.</summary>
public class HandoverExpirationJob : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan ResponseTimeout = TimeSpan.FromHours(24);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<HandoverExpirationJob> _logger;

    public HandoverExpirationJob(IServiceScopeFactory scopes, ILogger<HandoverExpirationJob> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chờ ứng dụng khởi động xong rồi mới quét lần đầu
        try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IHandoverRequestService>();
                await service.ExpirePendingAsync(ResponseTimeout, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "JOB-03 thất bại, sẽ thử lại ở lượt sau.");
            }

            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }
}
