using Ecolink.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ecolink.WebApi.Services;

/// <summary>JOB-12: mỗi ngày lúc 00:05 ICT đánh dấu Expired các voucher đã phát nhưng quá hạn (BR-GAM-05).</summary>
public class VoucherExpirationJob : BackgroundService
{
    private static readonly TimeSpan Ict = TimeSpan.FromHours(7);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<VoucherExpirationJob> _logger;

    public VoucherExpirationJob(IServiceScopeFactory scopes, ILogger<VoucherExpirationJob> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Chạy một lượt khi khởi động để dọn các voucher quá hạn trong lúc ứng dụng tắt
        try { await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken); } catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var gifts = scope.ServiceProvider.GetRequiredService<IGiftService>();
                var n = await gifts.ExpireVouchersAsync(stoppingToken);
                if (n > 0) _logger.LogInformation("JOB-12: đã đánh dấu {Count} voucher hết hạn.", n);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "JOB-12 thất bại, sẽ thử lại vào lượt sau.");
            }

            try { await Task.Delay(UntilNextRun(), stoppingToken); } catch (OperationCanceledException) { break; }
        }
    }

    /// <summary>Thời gian chờ tới 00:05 ICT kế tiếp.</summary>
    internal static TimeSpan UntilNextRun()
    {
        var nowIct = DateTime.UtcNow + Ict;
        var next = nowIct.Date.AddMinutes(5);
        if (next <= nowIct) next = next.AddDays(1);
        return next - nowIct;
    }
}
