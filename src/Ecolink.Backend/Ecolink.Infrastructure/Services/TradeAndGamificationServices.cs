using System.Collections.Concurrent;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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
    private readonly ApplicationDbContext? _dbContext;
    private static readonly ConcurrentDictionary<Guid, TradeOrderDto> _orders = new();

    public TradeTransactionService(
        ILogger<TradeTransactionService> logger,
        IGamificationService gamificationService,
        ApplicationDbContext? dbContext = null)
    {
        _logger = logger;
        _gamificationService = gamificationService;
        _dbContext = dbContext;
    }

    public async Task<TradeOrderDto> CreateTradeOrderAsync(CreateTradeOrderRequest request, CancellationToken cancellationToken = default)
    {
        var orderId = Guid.NewGuid();
        var proposedSlot = !string.IsNullOrWhiteSpace(request.ProposedTimeSlot)
            ? request.ProposedTimeSlot.Trim()
            : request.ScheduledAt.ToString("yyyy-MM-dd HH:mm");

        var orderDto = new TradeOrderDto
        {
            Id = orderId,
            UserId = request.UserId,
            PartnerId = request.PartnerId,
            RecyclerUserId = request.PartnerId,
            Status = OrderStatus.Pending.ToString(),
            ScheduledAt = request.ScheduledAt,
            EstimatedWeightKg = request.EstimatedWeightKg,
            CategoryCode = request.CategoryCode,
            InitialPricePerKg = request.InitialPricePerKg,
            ProposedTimeSlot = proposedSlot,
            QrCodeValue = $"ECOLINK_ORDER_{orderId:N}"
        };

        if (_dbContext != null)
        {
            try
            {
                var recyclerProfile = await _dbContext.RecyclerProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.UserId == request.PartnerId, cancellationToken);

                var partnerEntity = await _dbContext.CollectionPartners
                    .FirstOrDefaultAsync(p => p.Id == request.PartnerId || p.UserId == request.PartnerId, cancellationToken);

                if (partnerEntity == null && recyclerProfile != null)
                {
                    partnerEntity = new CollectionPartner
                    {
                        Id = recyclerProfile.UserId,
                        UserId = recyclerProfile.UserId,
                        FacilityName = recyclerProfile.FacilityName,
                        Address = recyclerProfile.BusinessAddress,
                        Latitude = (double)recyclerProfile.Latitude,
                        Longitude = (double)recyclerProfile.Longitude,
                        OperatingHours = recyclerProfile.OperatingHours,
                        ContactPhone = recyclerProfile.ContactPhone,
                        IsVerified = recyclerProfile.VerificationStatus == VerificationStatus.Verified
                    };
                    _dbContext.CollectionPartners.Add(partnerEntity);
                }

                if (partnerEntity != null)
                {
                    var orderEntity = new TradeOrder
                    {
                        Id = orderId,
                        UserId = request.UserId,
                        PartnerId = partnerEntity.Id,
                        RecyclerUserId = recyclerProfile?.UserId ?? partnerEntity.UserId,
                        Status = OrderStatus.Pending,
                        ScheduledAt = request.ScheduledAt == default ? DateTime.UtcNow.AddHours(2) : request.ScheduledAt,
                        EstimatedWeightKg = request.EstimatedWeightKg,
                        Note = request.Note,
                        CategoryCode = request.CategoryCode,
                        InitialPricePerKg = request.InitialPricePerKg,
                        ProposedTimeSlot = proposedSlot,
                        PickupAddress = request.PickupAddress
                    };

                    _dbContext.TradeOrders.Add(orderEntity);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    orderDto.PartnerId = orderEntity.PartnerId;
                    orderDto.RecyclerUserId = orderEntity.RecyclerUserId;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể ghi đơn bàn giao {OrderId} vào PostgreSQL, sử dụng bộ nhớ đệm fallback.", orderId);
            }
        }

        _orders[orderId] = orderDto;
        _logger.LogInformation("Tạo đơn hẹn bàn giao rác thành công: {OrderId}, QR: {QR}", orderId, orderDto.QrCodeValue);

        return orderDto;
    }

    public async Task<TransactionReceiptDto> ConfirmReceiptByQrAsync(ConfirmQrReceiptRequest request, CancellationToken cancellationToken = default)
    {
        if (!_orders.TryGetValue(request.TradeOrderId, out var order))
        {
            if (_dbContext != null)
            {
                var dbOrder = await _dbContext.TradeOrders
                    .FirstOrDefaultAsync(o => o.Id == request.TradeOrderId, cancellationToken);

                if (dbOrder != null)
                {
                    dbOrder.Status = OrderStatus.Completed;
                    dbOrder.UpdatedAt = DateTime.UtcNow;

                    var unitPrice = request.UnitPrice > 0
                        ? request.UnitPrice
                        : dbOrder.AgreedPricePerKg ?? dbOrder.InitialPricePerKg ?? 0m;

                    var dbReceipt = new TransactionReceipt
                    {
                        Id = Guid.NewGuid(),
                        TradeOrderId = dbOrder.Id,
                        ActualWeightKg = request.ActualWeightKg,
                        UnitPrice = unitPrice,
                        TotalAmountCash = request.ActualWeightKg * unitPrice,
                        PointsAwarded = (int)Math.Round(request.ActualWeightKg * 10),
                        QrSecurityCode = string.IsNullOrWhiteSpace(request.QrSecurityCode)
                            ? Guid.NewGuid().ToString("N")
                            : request.QrSecurityCode,
                        CompletedAt = DateTime.UtcNow
                    };

                    _dbContext.TransactionReceipts.Add(dbReceipt);
                    await _dbContext.SaveChangesAsync(cancellationToken);

                    return new TransactionReceiptDto
                    {
                        ReceiptId = dbReceipt.Id,
                        TradeOrderId = dbOrder.Id,
                        ActualWeightKg = dbReceipt.ActualWeightKg,
                        TotalAmountCash = dbReceipt.TotalAmountCash,
                        PointsAwarded = dbReceipt.PointsAwarded,
                        CompletedAt = dbReceipt.CompletedAt
                    };
                }
            }

            throw new KeyNotFoundException($"Không tìm thấy đơn hẹn bàn giao {request.TradeOrderId}");
        }

        order.Status = OrderStatus.Completed.ToString();
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

    public async Task<IEnumerable<TradeOrderDto>> GetUserOrdersAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_dbContext != null)
        {
            try
            {
                var dbOrders = await _dbContext.TradeOrders
                    .AsNoTracking()
                    .Where(o => o.UserId == userId || o.RecyclerUserId == userId || o.PartnerId == userId)
                    .OrderByDescending(o => o.CreatedAt)
                    .Select(o => new TradeOrderDto
                    {
                        Id = o.Id,
                        UserId = o.UserId,
                        PartnerId = o.PartnerId,
                        RecyclerUserId = o.RecyclerUserId,
                        Status = o.Status.ToString(),
                        ScheduledAt = o.ScheduledAt,
                        EstimatedWeightKg = o.EstimatedWeightKg,
                        CategoryCode = o.CategoryCode,
                        InitialPricePerKg = o.InitialPricePerKg,
                        AgreedPricePerKg = o.AgreedPricePerKg,
                        ProposedTimeSlot = o.ProposedTimeSlot,
                        ConfirmedTimeSlot = o.ConfirmedTimeSlot,
                        QrCodeValue = $"ECOLINK_ORDER_{o.Id:N}"
                    })
                    .ToListAsync(cancellationToken);

                if (dbOrders.Count > 0)
                {
                    return dbOrders;
                }
            }
            catch
            {
                // Fallback sang bộ nhớ đệm nếu chưa kết nối PostgreSQL
            }
        }

        var userOrders = _orders.Values.Where(o => o.UserId == userId || o.PartnerId == userId).ToList();
        return userOrders;
    }
}
