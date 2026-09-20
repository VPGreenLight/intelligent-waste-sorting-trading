using System.Collections.Concurrent;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

public class GamificationService : IGamificationService
{
    private readonly ILogger<GamificationService> _logger;
    private static readonly ConcurrentDictionary<Guid, EcoProfileDto> _profiles = new();

    public GamificationService(ILogger<GamificationService> logger)
    {
        _logger = logger;
    }

    public Task<EcoProfileDto> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = _profiles.GetOrAdd(userId, id => new EcoProfileDto
        {
            UserId = id,
            CurrentGreenPoints = 120,
            TotalLifetimePoints = 350,
            TreeLevel = 2,
            TreeExperience = 50,
            ExperienceToNextLevel = 100,
            TotalWasteScanned = 15,
            TotalRecycledKg = 8.5m
        });

        return Task.FromResult(profile);
    }

    public Task<EcoProfileDto> AwardPointsForRecyclingAsync(Guid userId, decimal weightKg, CancellationToken cancellationToken = default)
    {
        var pointsToAdd = (int)Math.Round(weightKg * 10); // 1kg rác tái chế = 10 điểm xanh
        var profile = _profiles.GetOrAdd(userId, id => new EcoProfileDto { UserId = id });

        profile.CurrentGreenPoints += pointsToAdd;
        profile.TotalLifetimePoints += pointsToAdd;
        profile.TotalRecycledKg += weightKg;
        profile.TreeExperience += pointsToAdd;

        // Cơ chế nâng cấp Cây ảo: Mỗi 100 EXP lên 1 cấp
        while (profile.TreeExperience >= 100)
        {
            profile.TreeLevel += 1;
            profile.TreeExperience -= 100;
            _logger.LogInformation("Người dùng {UserId} đã nâng cấp Cây ảo lên Level {Level}!", userId, profile.TreeLevel);
        }

        profile.ExperienceToNextLevel = 100 - profile.TreeExperience;
        _logger.LogInformation("Cộng {Points} Điểm Xanh cho User {UserId} từ {Weight}kg rác tái chế", pointsToAdd, userId, weightKg);

        return Task.FromResult(profile);
    }

    public Task<IEnumerable<LeaderboardEntryDto>> GetLeaderboardAsync(int top = 10, CancellationToken cancellationToken = default)
    {
        var mockBoard = new List<LeaderboardEntryDto>
        {
            new() { Rank = 1, FullName = "Nguyễn Văn Xanh", TotalPoints = 1450, TreeLevel = 5 },
            new() { Rank = 2, FullName = "Trần Thị Tươi", TotalPoints = 1200, TreeLevel = 4 },
            new() { Rank = 3, FullName = "Lê Hoàng Môi Trường", TotalPoints = 980, TreeLevel = 3 },
            new() { Rank = 4, FullName = "Phạm Tái Chế", TotalPoints = 850, TreeLevel = 3 },
            new() { Rank = 5, FullName = "Hoàng Eco", TotalPoints = 720, TreeLevel = 2 }
        };

        return Task.FromResult<IEnumerable<LeaderboardEntryDto>>(mockBoard.Take(top));
    }
}

public class TradeTransactionService : ITradeTransactionService
{
    private readonly ILogger<TradeTransactionService> _logger;
    private readonly IGamificationService _gamificationService;
    private static readonly ConcurrentDictionary<Guid, TradeOrderDto> _orders = new();

    public TradeTransactionService(ILogger<TradeTransactionService> logger, IGamificationService gamificationService)
    {
        _logger = logger;
        _gamificationService = gamificationService;
    }

    public Task<TradeOrderDto> CreateTradeOrderAsync(CreateTradeOrderRequest request, CancellationToken cancellationToken = default)
    {
        var orderId = Guid.NewGuid();
        var order = new TradeOrderDto
        {
            Id = orderId,
            UserId = request.UserId,
            PartnerId = request.PartnerId,
            Status = "Pending",
            ScheduledAt = request.ScheduledAt,
            EstimatedWeightKg = request.EstimatedWeightKg,
            QrCodeValue = $"ECOLINK_ORDER_{orderId:N}"
        };

        _orders[orderId] = order;
        _logger.LogInformation("Tạo đơn hẹn bàn giao rác thành công: {OrderId}, QR: {QR}", orderId, order.QrCodeValue);

        return Task.FromResult(order);
    }

    public async Task<TransactionReceiptDto> ConfirmReceiptByQrAsync(ConfirmQrReceiptRequest request, CancellationToken cancellationToken = default)
    {
        if (!_orders.TryGetValue(request.TradeOrderId, out var order))
        {
            throw new KeyNotFoundException($"Không tìm thấy đơn hẹn bàn giao {request.TradeOrderId}");
        }

        order.Status = "Completed";
        var totalCash = request.ActualWeightKg * request.UnitPrice;
        var pointsAwarded = (int)Math.Round(request.ActualWeightKg * 10);

        // Kích hoạt thưởng điểm Gamification
        await _gamificationService.AwardPointsForRecyclingAsync(order.UserId, request.ActualWeightKg, cancellationToken);

        var receipt = new TransactionReceiptDto
        {
            ReceiptId = Guid.NewGuid(),
            TradeOrderId = order.Id,
            ActualWeightKg = request.ActualWeightKg,
            TotalAmountCash = totalCash,
            PointsAwarded = pointsAwarded,
            CompletedAt = DateTime.UtcNow
        };

        _logger.LogInformation("Xác nhận biên nhận QR hoàn tất. Đơn {OrderId}: {Weight}kg, {Cash}đ, +{Points} điểm",
            order.Id, request.ActualWeightKg, totalCash, pointsAwarded);

        return receipt;
    }

    public Task<IEnumerable<TradeOrderDto>> GetUserOrdersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var userOrders = _orders.Values.Where(o => o.UserId == userId).ToList();
        return Task.FromResult<IEnumerable<TradeOrderDto>>(userOrders);
    }
}
