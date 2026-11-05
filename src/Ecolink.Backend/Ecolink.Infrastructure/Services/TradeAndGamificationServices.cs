using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
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
        var pointsToAdd = (int)Math.Round(weightKg * 10);
        var profile = _profiles.GetOrAdd(userId, id => new EcoProfileDto { UserId = id });

        profile.CurrentGreenPoints += pointsToAdd;
        profile.TotalLifetimePoints += pointsToAdd;
        profile.TotalRecycledKg += weightKg;
        profile.TreeExperience += pointsToAdd;

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
    private static readonly Regex GpsTagRegex = new(
        @"\[GPS:(?<lat>-?\d+(?:\.\d+)?),(?<lng>-?\d+(?:\.\d+)?)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
        var effectiveUserId = request.UserId == Guid.Empty
            ? Guid.Parse("11111111-1111-1111-1111-111111111111")
            : request.UserId;
        var effectivePartnerId = request.PartnerId == Guid.Empty
            ? Guid.Parse("22222222-2222-2222-2222-222222222222")
            : request.PartnerId;

        var proposedSlot = !string.IsNullOrWhiteSpace(request.ProposedTimeSlot)
            ? request.ProposedTimeSlot.Trim()
            : request.ScheduledAt.ToString("yyyy-MM-dd HH:mm");

        var noteWithGps = BuildStoredNote(request.Note, request.PickupLatitude, request.PickupLongitude);

        var orderDto = new TradeOrderDto
        {
            Id = orderId,
            UserId = effectiveUserId,
            PartnerId = effectivePartnerId,
            RecyclerUserId = effectivePartnerId,
            Status = OrderStatus.Pending.ToString(),
            ScheduledAt = request.ScheduledAt == default ? DateTime.UtcNow.AddHours(2) : request.ScheduledAt,
            EstimatedWeightKg = request.EstimatedWeightKg,
            CategoryCode = request.CategoryCode,
            InitialPricePerKg = request.InitialPricePerKg,
            ProposedTimeSlot = proposedSlot,
            PickupAddress = request.PickupAddress,
            PickupLatitude = request.PickupLatitude,
            PickupLongitude = request.PickupLongitude,
            Note = request.Note,
            QrCodeValue = $"ECOLINK_ORDER_{orderId:N}"
        };

        if (_dbContext != null)
        {
            try
            {
                var recyclerProfile = await _dbContext.RecyclerProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.UserId == effectivePartnerId, cancellationToken);

                var partnerEntity = await _dbContext.CollectionPartners
                    .FirstOrDefaultAsync(p => p.Id == effectivePartnerId || p.UserId == effectivePartnerId, cancellationToken);

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
                        UserId = effectiveUserId,
                        PartnerId = partnerEntity.Id,
                        RecyclerUserId = recyclerProfile?.UserId ?? partnerEntity.UserId,
                        Status = OrderStatus.Pending,
                        ScheduledAt = orderDto.ScheduledAt,
                        EstimatedWeightKg = request.EstimatedWeightKg,
                        Note = noteWithGps,
                        CategoryCode = request.CategoryCode,
                        InitialPricePerKg = request.InitialPricePerKg,
                        ProposedTimeSlot = proposedSlot,
                        PickupAddress = request.PickupAddress
                    };

                    _dbContext.TradeOrders.Add(orderEntity);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    orderDto.PartnerId = orderEntity.PartnerId;
                    orderDto.RecyclerUserId = orderEntity.RecyclerUserId;
                    orderDto.PartnerName = recyclerProfile?.FacilityName ?? partnerEntity.FacilityName;
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
                var rawOrders = await _dbContext.TradeOrders
                    .AsNoTracking()
                    .Include(o => o.Partner)
                    .Include(o => o.RecyclerProfile)
                    .Where(o => o.UserId == userId || o.RecyclerUserId == userId || o.PartnerId == userId)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync(cancellationToken);

                if (rawOrders.Count > 0)
                {
                    return rawOrders.Select(o =>
                    {
                        var (cleanNote, lat, lng) = ParseStoredNote(o.Note);
                        return new TradeOrderDto
                        {
                            Id = o.Id,
                            UserId = o.UserId,
                            PartnerId = o.PartnerId,
                            RecyclerUserId = o.RecyclerUserId,
                            PartnerName = o.RecyclerProfile?.FacilityName ?? o.Partner?.FacilityName,
                            Status = o.Status.ToString(),
                            ScheduledAt = o.ScheduledAt,
                            EstimatedWeightKg = o.EstimatedWeightKg,
                            CategoryCode = o.CategoryCode,
                            InitialPricePerKg = o.InitialPricePerKg,
                            AgreedPricePerKg = o.AgreedPricePerKg,
                            ProposedTimeSlot = o.ProposedTimeSlot,
                            ConfirmedTimeSlot = o.ConfirmedTimeSlot,
                            PickupAddress = o.PickupAddress,
                            PickupLatitude = lat,
                            PickupLongitude = lng,
                            Note = cleanNote,
                            QrCodeValue = $"ECOLINK_ORDER_{o.Id:N}"
                        };
                    }).ToList();
                }
            }
            catch
            {
                // Fallback sang bộ nhớ đệm nếu chưa kết nối PostgreSQL
            }
        }

        var userOrders = _orders.Values
            .Where(o => o.UserId == userId || o.PartnerId == userId || o.RecyclerUserId == userId)
            .OrderByDescending(o => o.ScheduledAt)
            .ToList();
        return userOrders;
    }

    private static string? BuildStoredNote(string? note, double? lat, double? lng)
    {
        var clean = string.IsNullOrWhiteSpace(note) ? string.Empty : note.Trim();
        if (!lat.HasValue || !lng.HasValue || (lat.Value == 0 && lng.Value == 0))
        {
            return string.IsNullOrEmpty(clean) ? null : clean;
        }

        var tag = string.Format(
            CultureInfo.InvariantCulture,
            "[GPS:{0:F6},{1:F6}]",
            lat.Value,
            lng.Value);

        return string.IsNullOrEmpty(clean) ? tag : $"{clean} {tag}";
    }

    private static (string? CleanNote, double? Lat, double? Lng) ParseStoredNote(string? storedNote)
    {
        if (string.IsNullOrWhiteSpace(storedNote)) return (null, null, null);

        var match = GpsTagRegex.Match(storedNote);
        if (!match.Success)
        {
            return (storedNote.Trim(), null, null);
        }

        double? lat = double.TryParse(match.Groups["lat"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLat)
            ? parsedLat
            : null;
        double? lng = double.TryParse(match.Groups["lng"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLng)
            ? parsedLng
            : null;

        var stripped = GpsTagRegex.Replace(storedNote, string.Empty).Trim();
        return (string.IsNullOrEmpty(stripped) ? null : stripped, lat, lng);
    }
}
