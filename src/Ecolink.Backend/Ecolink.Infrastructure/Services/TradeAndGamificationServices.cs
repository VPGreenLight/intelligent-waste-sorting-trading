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

    private static readonly Regex LinesTagRegex = new(
        @"\[LINES:(?<items>[^\]]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ReasonTagRegex = new(
        @"\[REASON:(?<reason>[^\]]+)\]",
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

        var sanitizedLines = request.Lines?
            .Where(l => !string.IsNullOrWhiteSpace(l.WasteId) && l.Kg > 0)
            .Select(l => new TradeOrderLineDto { WasteId = l.WasteId.Trim(), Kg = l.Kg })
            .ToList();

        var noteWithMeta = BuildStoredNote(
            request.Note,
            request.PickupLatitude,
            request.PickupLongitude,
            sanitizedLines,
            null);

        var scheduledUtc = NormalizeUtc(
            request.ScheduledAt == default ? DateTime.UtcNow.AddHours(2) : request.ScheduledAt);

        var orderDto = new TradeOrderDto
        {
            Id = orderId,
            UserId = effectiveUserId,
            PartnerId = effectivePartnerId,
            RecyclerUserId = effectivePartnerId,
            Status = OrderStatus.Pending.ToString(),
            ScheduledAt = scheduledUtc,
            EstimatedWeightKg = request.EstimatedWeightKg,
            CategoryCode = request.CategoryCode,
            InitialPricePerKg = request.InitialPricePerKg,
            ProposedTimeSlot = proposedSlot,
            PickupAddress = request.PickupAddress,
            PickupLatitude = request.PickupLatitude,
            PickupLongitude = request.PickupLongitude,
            Note = request.Note,
            Lines = sanitizedLines,
            QrCodeValue = $"ECOLINK_ORDER_{orderId:N}"
        };

        if (_dbContext != null)
        {
            try
            {
                var citizenUser = await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Id == effectiveUserId, cancellationToken)
                    ?? await _dbContext.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Role == UserRole.Citizen || u.Role == UserRole.User, cancellationToken);

                if (citizenUser != null)
                {
                    effectiveUserId = citizenUser.Id;
                    orderDto.UserId = citizenUser.Id;
                    orderDto.CitizenName = !string.IsNullOrWhiteSpace(citizenUser.FullName)
                        ? citizenUser.FullName
                        : citizenUser.Username;
                }

                var partnerEntity = await _dbContext.CollectionPartners
                    .FirstOrDefaultAsync(p => p.Id == effectivePartnerId || p.UserId == effectivePartnerId, cancellationToken);

                var targetRecyclerUserId = partnerEntity?.UserId ?? effectivePartnerId;
                var recyclerProfile = await _dbContext.RecyclerProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.UserId == targetRecyclerUserId || r.Id == effectivePartnerId, cancellationToken);

                if (partnerEntity == null && recyclerProfile != null)
                {
                    partnerEntity = await _dbContext.CollectionPartners
                        .FirstOrDefaultAsync(p => p.UserId == recyclerProfile.UserId, cancellationToken);

                    if (partnerEntity == null)
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
                }

                if (partnerEntity == null)
                {
                    var recyclerUser = await _dbContext.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == effectivePartnerId, cancellationToken);

                    if (recyclerUser != null)
                    {
                        partnerEntity = new CollectionPartner
                        {
                            Id = recyclerUser.Id,
                            UserId = recyclerUser.Id,
                            FacilityName = !string.IsNullOrWhiteSpace(recyclerUser.FullName)
                                ? recyclerUser.FullName
                                : recyclerUser.Username,
                            Address = request.PickupAddress ?? "Cơ sở thu mua EcoLink",
                            Latitude = request.PickupLatitude ?? 10.7769,
                            Longitude = request.PickupLongitude ?? 106.7009,
                            OperatingHours = "08:00 - 18:00",
                            ContactPhone = recyclerUser.PhoneNumber ?? "0901234567",
                            IsVerified = true
                        };
                        _dbContext.CollectionPartners.Add(partnerEntity);
                    }
                    else
                    {
                        partnerEntity = await _dbContext.CollectionPartners
                            .FirstOrDefaultAsync(cancellationToken);

                        if (partnerEntity != null && recyclerProfile == null)
                        {
                            recyclerProfile = await _dbContext.RecyclerProfiles
                                .AsNoTracking()
                                .FirstOrDefaultAsync(r => r.UserId == partnerEntity.UserId, cancellationToken);
                        }
                    }
                }

                if (partnerEntity != null && citizenUser != null)
                {
                    var orderEntity = new TradeOrder
                    {
                        Id = orderId,
                        UserId = citizenUser.Id,
                        PartnerId = partnerEntity.Id,
                        // Chỉ gắn RecyclerUserId khi bản ghi tồn tại trong recycler_profiles (tuân thủ FK fk_trade_orders_recycler_profiles)
                        RecyclerUserId = recyclerProfile?.UserId,
                        Status = OrderStatus.Pending,
                        ScheduledAt = scheduledUtc,
                        EstimatedWeightKg = request.EstimatedWeightKg,
                        Note = noteWithMeta,
                        CategoryCode = request.CategoryCode,
                        InitialPricePerKg = request.InitialPricePerKg,
                        ProposedTimeSlot = proposedSlot,
                        PickupAddress = request.PickupAddress
                    };

                    _dbContext.TradeOrders.Add(orderEntity);
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    await HandoverTradeSyncHelper.EnsureHandoverFromTradeOrderAsync(_dbContext, orderEntity, cancellationToken);

                    orderDto.PartnerId = orderEntity.PartnerId;
                    orderDto.RecyclerUserId = recyclerProfile?.UserId ?? partnerEntity.UserId;
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
                var unsyncedHandoverIds = await _dbContext.HandoverRequests
                    .AsNoTracking()
                    .Where(h => (h.UserId == userId || h.RecyclerId == userId) &&
                                !_dbContext.TradeOrders.Any(o => o.Id == h.Id))
                    .Select(h => h.Id)
                    .ToListAsync(cancellationToken);

                foreach (var hid in unsyncedHandoverIds)
                {
                    await HandoverTradeSyncHelper.SyncHandoverToTradeOrderAsync(_dbContext, hid, null, null, cancellationToken);
                }

                var rawOrders = await _dbContext.TradeOrders
                    .AsNoTracking()
                    .Include(o => o.User)
                    .Include(o => o.Partner)
                    .Include(o => o.RecyclerProfile)
                    .Where(o =>
                        o.UserId == userId ||
                        o.RecyclerUserId == userId ||
                        o.PartnerId == userId ||
                        (o.Partner != null && o.Partner.UserId == userId))
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync(cancellationToken);

                return rawOrders.Select(MapEntityToDto).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Không thể truy vấn đơn bàn giao từ PostgreSQL cho User {UserId}, dùng bộ đệm tạm.", userId);
            }
        }

        var userOrders = _orders.Values
            .Where(o => o.UserId == userId || o.PartnerId == userId || o.RecyclerUserId == userId)
            .OrderByDescending(o => o.ScheduledAt)
            .ToList();
        return userOrders;
    }

    public async Task<TradeOrderDto> UpdateOrderStatusAsync(
        Guid orderId,
        Guid callerUserId,
        UpdateTradeOrderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalized = (request.Status ?? string.Empty).Trim().ToLowerInvariant();
        var nextStatus = normalized switch
        {
            "accepted" => OrderStatus.Accepted,
            "rejected" or "declined" => OrderStatus.Rejected,
            "cancelled" or "canceled" or "broken" => OrderStatus.Cancelled,
            "completed" or "done" => OrderStatus.Completed,
            "scheduled" => OrderStatus.Scheduled,
            _ => throw new ArgumentException($"Trạng thái đơn '{request.Status}' không hợp lệ.")
        };

        var trimmedReason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

        if (_dbContext != null)
        {
            var dbOrder = await _dbContext.TradeOrders
                .Include(o => o.User)
                .Include(o => o.Partner)
                .Include(o => o.RecyclerProfile)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (dbOrder != null)
            {
                var (cleanNote, lat, lng, lines, existingReason) = ParseStoredNote(dbOrder.Note);
                var now = DateTime.UtcNow;

                dbOrder.Status = nextStatus;
                dbOrder.UpdatedAt = now;

                if (nextStatus == OrderStatus.Accepted)
                {
                    dbOrder.AcceptedAt ??= now;
                    dbOrder.AgreedPricePerKg ??= dbOrder.InitialPricePerKg;
                    dbOrder.ConfirmedTimeSlot ??= dbOrder.ProposedTimeSlot;
                }

                dbOrder.Note = BuildStoredNote(
                    cleanNote,
                    lat,
                    lng,
                    lines,
                    trimmedReason ?? existingReason);

                var effectiveActorId = callerUserId != Guid.Empty ? callerUserId : dbOrder.UserId;
                var sysText = nextStatus switch
                {
                    OrderStatus.Accepted => $"Đơn bàn giao đã được xác nhận nhận thu gom. Khung giờ chốt: {dbOrder.ConfirmedTimeSlot ?? dbOrder.ProposedTimeSlot}.",
                    OrderStatus.Rejected => string.IsNullOrEmpty(trimmedReason)
                        ? "Cơ sở thu gom đã từ chối yêu cầu bàn giao này."
                        : $"Cơ sở thu gom đã từ chối yêu cầu. Lý do: {trimmedReason}",
                    OrderStatus.Cancelled => string.IsNullOrEmpty(trimmedReason)
                        ? "Lịch hẹn bàn giao đã được huỷ."
                        : $"Lịch hẹn bàn giao đã huỷ. Lý do: {trimmedReason}",
                    _ => $"Trạng thái đơn bàn giao chuyển sang {nextStatus}."
                };

                _dbContext.ChatMessages.Add(new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    TradeOrderId = dbOrder.Id,
                    SenderUserId = effectiveActorId,
                    SenderRole = "System",
                    MessageType = ChatMessageType.System,
                    MessageContent = sysText,
                    IsRead = false,
                    SentAt = now,
                    CreatedAt = now
                });

                await _dbContext.SaveChangesAsync(cancellationToken);
                await HandoverTradeSyncHelper.EnsureHandoverFromTradeOrderAsync(_dbContext, dbOrder, cancellationToken);

                var updatedDto = MapEntityToDto(dbOrder);
                _orders[orderId] = updatedDto;
                return updatedDto;
            }
        }

        if (_orders.TryGetValue(orderId, out var cached))
        {
            cached.Status = nextStatus.ToString();
            if (nextStatus == OrderStatus.Accepted)
            {
                cached.ConfirmedTimeSlot ??= cached.ProposedTimeSlot;
                cached.AgreedPricePerKg ??= cached.InitialPricePerKg;
            }
            if (!string.IsNullOrEmpty(trimmedReason))
            {
                cached.CancelReason = trimmedReason;
            }
            return cached;
        }

        throw new KeyNotFoundException($"Không tìm thấy đơn bàn giao {orderId}");
    }

    public async Task<TradeOrderDto> UpdateOrderTimeSlotAsync(
        Guid orderId,
        Guid callerUserId,
        string proposedTimeSlot,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(proposedTimeSlot))
        {
            throw new ArgumentException("Khung giờ thu gom không được để trống.");
        }

        var slot = proposedTimeSlot.Trim();

        if (_dbContext != null)
        {
            var dbOrder = await _dbContext.TradeOrders
                .Include(o => o.User)
                .Include(o => o.Partner)
                .Include(o => o.RecyclerProfile)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (dbOrder != null)
            {
                var now = DateTime.UtcNow;
                dbOrder.ProposedTimeSlot = slot;
                dbOrder.UpdatedAt = now;

                var effectiveActorId = callerUserId != Guid.Empty ? callerUserId : dbOrder.UserId;
                _dbContext.ChatMessages.Add(new ChatMessage
                {
                    Id = Guid.NewGuid(),
                    TradeOrderId = dbOrder.Id,
                    SenderUserId = effectiveActorId,
                    SenderRole = "System",
                    MessageType = ChatMessageType.System,
                    MessageContent = $"Khung giờ thu gom đề xuất đã được cập nhật sang: {slot}.",
                    IsRead = false,
                    SentAt = now,
                    CreatedAt = now
                });

                await _dbContext.SaveChangesAsync(cancellationToken);
                await HandoverTradeSyncHelper.EnsureHandoverFromTradeOrderAsync(_dbContext, dbOrder, cancellationToken);

                var updatedDto = MapEntityToDto(dbOrder);
                _orders[orderId] = updatedDto;
                return updatedDto;
            }
        }

        if (_orders.TryGetValue(orderId, out var cached))
        {
            cached.ProposedTimeSlot = slot;
            return cached;
        }

        throw new KeyNotFoundException($"Không tìm thấy đơn bàn giao {orderId}");
    }

    private static DateTime NormalizeUtc(DateTime dt) =>
        dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };

    private static TradeOrderDto MapEntityToDto(TradeOrder o)
    {
        var (cleanNote, lat, lng, lines, cancelReason) = ParseStoredNote(o.Note);
        var citizenName = !string.IsNullOrWhiteSpace(o.User?.FullName)
            ? o.User.FullName
            : o.User?.Username;
        var resolvedRecyclerUserId = o.RecyclerUserId ?? o.Partner?.UserId;

        return new TradeOrderDto
        {
            Id = o.Id,
            UserId = o.UserId,
            CitizenName = citizenName,
            PartnerId = o.PartnerId,
            RecyclerUserId = resolvedRecyclerUserId,
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
            CancelReason = cancelReason,
            Lines = lines,
            QrCodeValue = $"ECOLINK_ORDER_{o.Id:N}"
        };
    }

    private static string? BuildStoredNote(
        string? note,
        double? lat,
        double? lng,
        IReadOnlyCollection<TradeOrderLineDto>? lines,
        string? cancelReason)
    {
        var parts = new List<string>();
        var clean = string.IsNullOrWhiteSpace(note) ? string.Empty : note.Trim();
        if (!string.IsNullOrEmpty(clean))
        {
            parts.Add(clean);
        }

        if (lat.HasValue && lng.HasValue && !(lat.Value == 0 && lng.Value == 0))
        {
            parts.Add(string.Format(
                CultureInfo.InvariantCulture,
                "[GPS:{0:F6},{1:F6}]",
                lat.Value,
                lng.Value));
        }

        if (lines != null && lines.Count > 0)
        {
            var encodedLines = string.Join(
                ",",
                lines.Select(l => string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}:{1:0.##}",
                    l.WasteId.Replace(":", string.Empty).Replace(",", string.Empty).Replace("]", string.Empty),
                    l.Kg)));
            if (!string.IsNullOrEmpty(encodedLines))
            {
                parts.Add($"[LINES:{encodedLines}]");
            }
        }

        if (!string.IsNullOrWhiteSpace(cancelReason))
        {
            var safeReason = cancelReason.Trim().Replace("]", ")");
            parts.Add($"[REASON:{safeReason}]");
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    private static (
        string? CleanNote,
        double? Lat,
        double? Lng,
        List<TradeOrderLineDto>? Lines,
        string? CancelReason) ParseStoredNote(string? storedNote)
    {
        if (string.IsNullOrWhiteSpace(storedNote))
        {
            return (null, null, null, null, null);
        }

        double? lat = null;
        double? lng = null;
        var gpsMatch = GpsTagRegex.Match(storedNote);
        if (gpsMatch.Success)
        {
            if (double.TryParse(gpsMatch.Groups["lat"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLat))
            {
                lat = parsedLat;
            }
            if (double.TryParse(gpsMatch.Groups["lng"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLng))
            {
                lng = parsedLng;
            }
        }

        List<TradeOrderLineDto>? lines = null;
        var linesMatch = LinesTagRegex.Match(storedNote);
        if (linesMatch.Success)
        {
            var parsedList = new List<TradeOrderLineDto>();
            var pairs = linesMatch.Groups["items"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var pair in pairs)
            {
                var kv = pair.Split(':', 2);
                if (kv.Length == 2 &&
                    !string.IsNullOrWhiteSpace(kv[0]) &&
                    decimal.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var kg) &&
                    kg > 0)
                {
                    parsedList.Add(new TradeOrderLineDto { WasteId = kv[0].Trim(), Kg = kg });
                }
            }
            if (parsedList.Count > 0)
            {
                lines = parsedList;
            }
        }

        string? cancelReason = null;
        var reasonMatch = ReasonTagRegex.Match(storedNote);
        if (reasonMatch.Success)
        {
            cancelReason = reasonMatch.Groups["reason"].Value.Trim();
        }

        var stripped = GpsTagRegex.Replace(storedNote, string.Empty);
        stripped = LinesTagRegex.Replace(stripped, string.Empty);
        stripped = ReasonTagRegex.Replace(stripped, string.Empty).Trim();

        return (string.IsNullOrEmpty(stripped) ? null : stripped, lat, lng, lines, cancelReason);
    }
}
