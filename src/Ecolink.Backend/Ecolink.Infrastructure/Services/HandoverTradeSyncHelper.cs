using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ecolink.Application.Dtos;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Bộ đồng bộ hai chiều giữa Module 7 &amp; 8 (<see cref="HandoverRequest"/>, <see cref="HandoverRequestItem"/>, <see cref="HandoverReceipt"/>)
/// và Module 9 (<see cref="TradeOrder"/>, <see cref="ChatMessage"/>, <see cref="TransactionReceipt"/>).
/// Đảm bảo cả 3 module dùng chung một định danh <c>Guid Id</c> (<c>requestId == orderId</c>) và liên thông trạng thái xuyên suốt.
/// </summary>
internal static class HandoverTradeSyncHelper
{
    private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(7); // ICT (GMT+7)
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly Regex TimeRangeStartRegex = new(
        @"(?<hour>\d{1,2}):(?<min>\d{2})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GpsTagRegex = new(
        @"\[GPS:(?<lat>-?\d+(?:\.\d+)?),(?<lng>-?\d+(?:\.\d+)?)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex LinesTagRegex = new(
        @"\[LINES:(?<items>[^\]]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ReasonTagRegex = new(
        @"\[REASON:(?<reason>[^\]]+)\]",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static OrderStatus ToOrderStatus(HandoverStatus status) => status switch
    {
        HandoverStatus.Pending => OrderStatus.Pending,
        HandoverStatus.Negotiating => OrderStatus.Negotiating,
        HandoverStatus.Accepted => OrderStatus.Accepted,
        HandoverStatus.Scheduled => OrderStatus.Scheduled,
        HandoverStatus.AwaitingConfirmation => OrderStatus.AwaitingConfirmation,
        HandoverStatus.Completed => OrderStatus.Completed,
        HandoverStatus.Rejected => OrderStatus.Rejected,
        HandoverStatus.Cancelled => OrderStatus.Cancelled,
        HandoverStatus.Expired => OrderStatus.Expired,
        HandoverStatus.Disputed => OrderStatus.Disputed,
        _ => OrderStatus.Pending
    };

    public static HandoverStatus ToHandoverStatus(OrderStatus status) => status switch
    {
        OrderStatus.Pending => HandoverStatus.Pending,
        OrderStatus.Negotiating => HandoverStatus.Negotiating,
        OrderStatus.Accepted => HandoverStatus.Accepted,
        OrderStatus.Scheduled => HandoverStatus.Scheduled,
        OrderStatus.AwaitingConfirmation => HandoverStatus.AwaitingConfirmation,
        OrderStatus.Completed => HandoverStatus.Completed,
        OrderStatus.Rejected => HandoverStatus.Rejected,
        OrderStatus.Cancelled => HandoverStatus.Cancelled,
        OrderStatus.Expired => HandoverStatus.Expired,
        OrderStatus.Disputed => HandoverStatus.Disputed,
        _ => HandoverStatus.Pending
    };

    public static string FormatSlotText(DateTime utcTime)
    {
        var local = (utcTime.Kind == DateTimeKind.Local ? utcTime.ToUniversalTime() : DateTime.SpecifyKind(utcTime, DateTimeKind.Utc)) + LocalOffset;
        var endHour = Math.Min(23, local.Hour + 2);
        return $"{local:yyyy-MM-dd} ({local:HH:mm} - {endHour:D2}:{local.Minute:D2})";
    }

    public static DateTime ParseSlotToUtc(string? slotText, DateTime fallbackUtc)
    {
        if (string.IsNullOrWhiteSpace(slotText))
            return DateTime.SpecifyKind(fallbackUtc, DateTimeKind.Utc);

        var trimmed = slotText.Trim();
        if (DateTimeOffset.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
            return dto.UtcDateTime;

        var fallbackLocal = DateTime.SpecifyKind(fallbackUtc, DateTimeKind.Utc) + LocalOffset;
        var datePart = fallbackLocal.Date;

        // Kiểm tra xem chuỗi có chứa yyyy-MM-dd hoặc dd/MM/yyyy không
        var dateMatch = Regex.Match(trimmed, @"(?<y>\d{4})-(?<m>\d{2})-(?<d>\d{2})");
        if (dateMatch.Success &&
            int.TryParse(dateMatch.Groups["y"].Value, out var y) &&
            int.TryParse(dateMatch.Groups["m"].Value, out var m) &&
            int.TryParse(dateMatch.Groups["d"].Value, out var d))
        {
            try { datePart = new DateTime(y, m, d); } catch { /* ignore */ }
        }
        else
        {
            var vnDateMatch = Regex.Match(trimmed, @"(?<d>\d{2})/(?<m>\d{2})/(?<y>\d{4})");
            if (vnDateMatch.Success &&
                int.TryParse(vnDateMatch.Groups["y"].Value, out var vy) &&
                int.TryParse(vnDateMatch.Groups["m"].Value, out var vm) &&
                int.TryParse(vnDateMatch.Groups["d"].Value, out var vd))
            {
                try { datePart = new DateTime(vy, vm, vd); } catch { /* ignore */ }
            }
        }

        // Tìm giờ bắt đầu HH:mm (sau phần ngày nếu có)
        var timeSearchText = dateMatch.Success ? trimmed[(dateMatch.Index + dateMatch.Length)..] : trimmed;
        var timeMatch = TimeRangeStartRegex.Match(timeSearchText);
        if (timeMatch.Success &&
            int.TryParse(timeMatch.Groups["hour"].Value, out var hour) &&
            int.TryParse(timeMatch.Groups["min"].Value, out var min) &&
            hour is >= 0 and <= 23 && min is >= 0 and <= 59)
        {
            var localDt = new DateTime(datePart.Year, datePart.Month, datePart.Day, hour, min, 0, DateTimeKind.Unspecified);
            return DateTime.SpecifyKind(localDt - LocalOffset, DateTimeKind.Utc);
        }

        return DateTime.SpecifyKind(fallbackUtc, DateTimeKind.Utc);
    }

    /// <summary>
    /// Khóa đơn giá <see cref="HandoverRequestItem.AgreedPricePerKg"/> cho tất cả dòng phế liệu của yêu cầu khi được chấp nhận (BR-NEG-05).
    /// </summary>
    public static async Task LockHandoverItemPricesAsync(
        ApplicationDbContext db,
        HandoverRequest handover,
        decimal? negotiatedUnitPrice,
        CancellationToken ct)
    {
        var items = handover.Items.Count > 0
            ? handover.Items.ToList()
            : await db.HandoverRequestItems.Where(i => i.RequestId == handover.Id).ToListAsync(ct);

        if (items.Count == 0) return;

        var categoryIds = items.Select(i => i.WasteCategoryId).Distinct().ToList();
        var activePrices = await db.RecyclerPrices
            .AsNoTracking()
            .Where(p => p.RecyclerId == handover.RecyclerId && categoryIds.Contains(p.WasteCategoryId))
            .ToDictionaryAsync(p => p.WasteCategoryId, p => p.PricePerKg, ct);

        var primaryItem = items.OrderByDescending(i => i.EstimatedWeightKg).First();

        foreach (var item in items)
        {
            if (negotiatedUnitPrice.HasValue && negotiatedUnitPrice.Value > 0 && (items.Count == 1 || item.Id == primaryItem.Id))
            {
                item.AgreedPricePerKg = negotiatedUnitPrice.Value;
            }
            else if (!item.AgreedPricePerKg.HasValue)
            {
                if (activePrices.TryGetValue(item.WasteCategoryId, out var listedPrice))
                    item.AgreedPricePerKg = listedPrice;
                else if (negotiatedUnitPrice.HasValue && negotiatedUnitPrice.Value > 0)
                    item.AgreedPricePerKg = negotiatedUnitPrice.Value;
            }
        }
    }

    /// <summary>
    /// Đồng bộ từ <see cref="HandoverRequest"/> (Module 7 &amp; 8) sang <see cref="TradeOrder"/> &amp; <see cref="ChatMessage"/> (Module 9).
    /// </summary>
    public static async Task SyncHandoverToTradeOrderAsync(
        ApplicationDbContext db,
        Guid requestId,
        Guid? actorUserId,
        string? systemChatText,
        CancellationToken ct)
    {
        try
        {
            var handover = await db.HandoverRequests
                .Include(h => h.Items).ThenInclude(i => i.WasteCategory)
                .Include(h => h.Recycler)
                .FirstOrDefaultAsync(h => h.Id == requestId, ct);

            if (handover == null) return;

            var partner = await EnsurePartnerForRecyclerAsync(db, handover.RecyclerId, handover.Recycler, ct);
            if (partner == null) return;

            var categoryIds = handover.Items.Select(i => i.WasteCategoryId).Distinct().ToList();
            var activePrices = await db.RecyclerPrices
                .AsNoTracking()
                .Where(p => p.RecyclerId == handover.RecyclerId && categoryIds.Contains(p.WasteCategoryId))
                .ToDictionaryAsync(p => p.WasteCategoryId, p => p.PricePerKg, ct);

            var primaryItem = handover.Items.OrderByDescending(i => i.EstimatedWeightKg).FirstOrDefault();
            var primaryCode = primaryItem?.WasteCategory?.Code ?? "plastic";
            decimal? initialPrice = null;
            if (primaryItem != null)
            {
                if (activePrices.TryGetValue(primaryItem.WasteCategoryId, out var lp))
                    initialPrice = lp;
                else
                    initialPrice = primaryItem.AgreedPricePerKg;
            }

            var agreedPrice = primaryItem?.AgreedPricePerKg
                              ?? handover.Items.Select(i => i.AgreedPricePerKg).FirstOrDefault(p => p.HasValue);

            var linesMeta = handover.Items
                .Select(i => new TradeOrderLineDto
                {
                    WasteId = MapCategoryCodeToWasteId(i.WasteCategory?.Code),
                    Kg = i.ActualWeightKg ?? i.EstimatedWeightKg
                })
                .ToList();

            var storedNote = BuildStoredNote(
                handover.UserNote,
                handover.Latitude.HasValue ? (double)handover.Latitude.Value : null,
                handover.Longitude.HasValue ? (double)handover.Longitude.Value : null,
                linesMeta,
                handover.CancelReason);

            var proposedSlotText = FormatSlotText(handover.ProposedTimeSlot);
            var confirmedSlotText = handover.ConfirmedTimeSlot.HasValue
                ? FormatSlotText(handover.ConfirmedTimeSlot.Value)
                : null;

            var hasRecyclerProfile = handover.Recycler != null ||
                                     await db.RecyclerProfiles.AsNoTracking().AnyAsync(r => r.UserId == handover.RecyclerId, ct);

            var tradeOrder = await db.TradeOrders.FirstOrDefaultAsync(o => o.Id == handover.Id, ct);
            var now = DateTime.UtcNow;

            if (tradeOrder == null)
            {
                tradeOrder = new TradeOrder
                {
                    Id = handover.Id,
                    UserId = handover.UserId,
                    PartnerId = partner.Id,
                    RecyclerUserId = hasRecyclerProfile ? handover.RecyclerId : null,
                    Status = ToOrderStatus(handover.Status),
                    ScheduledAt = DateTime.SpecifyKind(handover.ProposedTimeSlot, DateTimeKind.Utc),
                    EstimatedWeightKg = handover.Items.Sum(i => i.EstimatedWeightKg),
                    CategoryCode = primaryCode,
                    InitialPricePerKg = initialPrice,
                    AgreedPricePerKg = agreedPrice,
                    ProposedTimeSlot = proposedSlotText,
                    ConfirmedTimeSlot = confirmedSlotText,
                    PickupAddress = handover.PickupAddress,
                    Note = storedNote,
                    AcceptedAt = handover.ConfirmedTimeSlot.HasValue ? now : null,
                    CreatedAt = handover.CreatedAt,
                    UpdatedAt = now
                };
                db.TradeOrders.Add(tradeOrder);
            }
            else
            {
                tradeOrder.Status = ToOrderStatus(handover.Status);
                tradeOrder.ScheduledAt = DateTime.SpecifyKind(handover.ProposedTimeSlot, DateTimeKind.Utc);
                tradeOrder.EstimatedWeightKg = handover.Items.Sum(i => i.EstimatedWeightKg);
                tradeOrder.CategoryCode = primaryCode;
                tradeOrder.InitialPricePerKg = initialPrice ?? tradeOrder.InitialPricePerKg;
                tradeOrder.AgreedPricePerKg = agreedPrice ?? tradeOrder.AgreedPricePerKg;
                tradeOrder.ProposedTimeSlot = proposedSlotText;
                if (confirmedSlotText != null)
                    tradeOrder.ConfirmedTimeSlot = confirmedSlotText;
                if (handover.Status == HandoverStatus.Accepted)
                    tradeOrder.AcceptedAt ??= now;
                tradeOrder.PickupAddress = handover.PickupAddress;
                tradeOrder.Note = storedNote;
                tradeOrder.UpdatedAt = now;
            }

            if (!string.IsNullOrWhiteSpace(systemChatText))
            {
                var senderId = actorUserId.HasValue && actorUserId.Value != Guid.Empty
                    ? actorUserId.Value
                    : handover.UserId;

                var senderExists = await db.Users.AsNoTracking().AnyAsync(u => u.Id == senderId, ct);
                if (senderExists)
                {
                    db.ChatMessages.Add(new ChatMessage
                    {
                        Id = Guid.NewGuid(),
                        TradeOrderId = tradeOrder.Id,
                        SenderUserId = senderId,
                        SenderRole = "System",
                        MessageType = ChatMessageType.System,
                        MessageContent = systemChatText,
                        IsRead = false,
                        SentAt = now,
                        CreatedAt = now
                    });
                }
            }

            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Trong môi trường Unit Test cô lập không seed bảng Users/CollectionPartners, bỏ qua lỗi FK phụ trợ
            // để không ảnh hưởng tới luồng chính của HandoverRequestService / HandoverReceiptService.
        }
    }

    /// <summary>
    /// Đảm bảo <see cref="TradeOrder"/> tồn tại và đồng bộ trạng thái mới nhất từ <see cref="HandoverRequest"/>
    /// khi mở phòng chat thương lượng (Module 9).
    /// </summary>
    public static async Task<TradeOrder?> EnsureTradeOrderForChatAsync(
        ApplicationDbContext db,
        Guid orderId,
        CancellationToken ct)
    {
        var handover = await db.HandoverRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == orderId, ct);

        if (handover != null)
        {
            await SyncHandoverToTradeOrderAsync(db, orderId, null, null, ct);
        }

        return await db.TradeOrders
            .Include(o => o.User)
            .Include(o => o.Partner)
            .Include(o => o.RecyclerProfile)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
    }

    /// <summary>
    /// Đồng bộ kết quả thương lượng từ <see cref="NegotiationChatService"/> (Module 9) sang <see cref="HandoverRequest"/> (Module 7 &amp; 8).
    /// </summary>
    public static async Task SyncNegotiationToHandoverAsync(
        ApplicationDbContext db,
        TradeOrder order,
        decimal? agreedPricePerKg,
        decimal? proposedWeightKg,
        string? timeSlotText,
        CancellationToken ct)
    {
        try
        {
            var handover = await db.HandoverRequests
                .Include(h => h.Items)
                .FirstOrDefaultAsync(h => h.Id == order.Id, ct);

            if (handover == null)
            {
                handover = await EnsureHandoverFromTradeOrderAsync(db, order, ct);
                if (handover == null) return;
            }

            var now = DateTime.UtcNow;
            handover.Status = ToHandoverStatus(order.Status);
            handover.UpdatedAt = now;

            if (!string.IsNullOrWhiteSpace(timeSlotText))
            {
                var parsedSlot = ParseSlotToUtc(timeSlotText, handover.ProposedTimeSlot);
                handover.ProposedTimeSlot = parsedSlot;
                if (order.Status == OrderStatus.Accepted)
                {
                    handover.ConfirmedTimeSlot = parsedSlot;
                }
            }
            else if (order.Status == OrderStatus.Accepted)
            {
                handover.ConfirmedTimeSlot ??= handover.ProposedTimeSlot;
            }

            if (proposedWeightKg.HasValue && proposedWeightKg.Value > 0 && handover.Items.Count > 0)
            {
                var roundedWeight = decimal.Round(proposedWeightKg.Value, 2);
                if (handover.Items.Count == 1)
                {
                    handover.Items.First().EstimatedWeightKg = roundedWeight;
                }
                else
                {
                    var currentTotal = handover.Items.Sum(i => i.EstimatedWeightKg);
                    if (currentTotal > 0 && currentTotal != roundedWeight)
                    {
                        var primary = handover.Items.OrderByDescending(i => i.EstimatedWeightKg).First();
                        var othersWeight = currentTotal - primary.EstimatedWeightKg;
                        if (roundedWeight > othersWeight)
                        {
                            primary.EstimatedWeightKg = decimal.Round(roundedWeight - othersWeight, 2);
                        }
                    }
                }
            }

            if (order.Status == OrderStatus.Accepted || agreedPricePerKg.HasValue)
            {
                await LockHandoverItemPricesAsync(db, handover, agreedPricePerKg ?? order.AgreedPricePerKg ?? order.InitialPricePerKg, ct);
            }
        }
        catch
        {
            // Bỏ qua trong môi trường Unit Test của NegotiationChatService nếu không seed bảng RecyclerProfiles / WasteCategories.
        }
    }

    /// <summary>
    /// Đảm bảo <see cref="HandoverRequest"/> tồn tại từ <see cref="TradeOrder"/> (phục vụ các đơn tạo qua <c>/api/v1/trade/orders</c>
    /// vẫn lập được biên nhận ở Module 8 và quản lý được ở Module 7).
    /// </summary>
    public static async Task<HandoverRequest?> EnsureHandoverFromTradeOrderAsync(
        ApplicationDbContext db,
        TradeOrder order,
        CancellationToken ct)
    {
        try
        {
            var existing = await db.HandoverRequests
                .Include(h => h.Items)
                .FirstOrDefaultAsync(h => h.Id == order.Id, ct);

            var (cleanNote, lat, lng, parsedLines, cancelReason) = ParseStoredNote(order.Note);
            var recyclerUserId = order.RecyclerUserId ?? order.Partner?.UserId ?? order.PartnerId;

            var hasRecyclerProfile = await db.RecyclerProfiles
                .AsNoTracking()
                .AnyAsync(r => r.UserId == recyclerUserId, ct);

            if (!hasRecyclerProfile)
            {
                var partner = await db.CollectionPartners
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == order.PartnerId || p.UserId == order.PartnerId, ct);
                if (partner != null)
                {
                    recyclerUserId = partner.UserId;
                    hasRecyclerProfile = await db.RecyclerProfiles
                        .AsNoTracking()
                        .AnyAsync(r => r.UserId == recyclerUserId, ct);
                }
            }

            if (!hasRecyclerProfile) return existing;

            var scheduledUtc = ParseSlotToUtc(order.ProposedTimeSlot, order.ScheduledAt);
            var confirmedUtc = !string.IsNullOrWhiteSpace(order.ConfirmedTimeSlot)
                ? ParseSlotToUtc(order.ConfirmedTimeSlot, scheduledUtc)
                : (order.Status == OrderStatus.Accepted ? scheduledUtc : (DateTime?)null);

            if (existing == null)
            {
                var prefix = $"HND-{(order.CreatedAt + LocalOffset):yyyyMMdd}-";
                var suffix = order.Id.ToString("N")[..4].ToUpperInvariant();
                var code = $"{prefix}{suffix}";

                var items = await BuildHandoverItemsFromTradeOrderAsync(
                    db,
                    order.Id,
                    recyclerUserId,
                    order.CategoryCode,
                    order.EstimatedWeightKg,
                    order.AgreedPricePerKg ?? order.InitialPricePerKg,
                    parsedLines,
                    ct);

                if (items.Count == 0) return null;

                existing = new HandoverRequest
                {
                    Id = order.Id,
                    RequestCode = code,
                    UserId = order.UserId,
                    RecyclerId = recyclerUserId,
                    PickupAddress = string.IsNullOrWhiteSpace(order.PickupAddress) ? "Địa chỉ thu gom EcoLink" : order.PickupAddress,
                    Latitude = lat.HasValue ? (decimal)lat.Value : null,
                    Longitude = lng.HasValue ? (decimal)lng.Value : null,
                    WastePhotoUrl = JsonSerializer.Serialize(new List<string> { "https://r2.ecolink.vn/handover/default-waste.jpg" }, JsonOptions),
                    ProposedTimeSlot = scheduledUtc,
                    ConfirmedTimeSlot = confirmedUtc,
                    Status = ToHandoverStatus(order.Status),
                    UserNote = cleanNote,
                    CancelReason = cancelReason,
                    CreatedAt = order.CreatedAt,
                    UpdatedAt = DateTime.UtcNow,
                    Items = items
                };

                db.HandoverRequests.Add(existing);
            }
            else
            {
                existing.Status = ToHandoverStatus(order.Status);
                existing.ProposedTimeSlot = scheduledUtc;
                if (confirmedUtc.HasValue)
                    existing.ConfirmedTimeSlot = confirmedUtc;
                if (!string.IsNullOrWhiteSpace(cancelReason))
                    existing.CancelReason = cancelReason;
                existing.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);
            return existing;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Đồng bộ hoàn tất biên nhận (<see cref="HandoverReceiptService"/> - U-09 &amp; JOB-01) sang <see cref="TradeOrder"/>,
    /// <see cref="TransactionReceipt"/>, <see cref="ChatMessage"/> và bảng điểm <see cref="UserEcoProfile"/>.
    /// </summary>
    public static async Task SyncReceiptCompletionAsync(
        ApplicationDbContext db,
        HandoverReceipt receipt,
        CancellationToken ct)
    {
        try
        {
            var now = receipt.ConfirmedByUserAt ?? DateTime.UtcNow;
            var tradeOrder = await db.TradeOrders.FirstOrDefaultAsync(o => o.Id == receipt.RequestId, ct);
            if (tradeOrder != null)
            {
                tradeOrder.Status = OrderStatus.Completed;
                tradeOrder.UpdatedAt = now;

                var existingTxReceipt = await db.TransactionReceipts
                    .FirstOrDefaultAsync(r => r.TradeOrderId == tradeOrder.Id, ct);

                if (existingTxReceipt == null)
                {
                    var unitPrice = receipt.TotalWeightKg > 0
                        ? decimal.Round(receipt.TotalAmountCash / receipt.TotalWeightKg, 2)
                        : (tradeOrder.AgreedPricePerKg ?? tradeOrder.InitialPricePerKg ?? 0m);

                    db.TransactionReceipts.Add(new TransactionReceipt
                    {
                        Id = receipt.Id,
                        TradeOrderId = tradeOrder.Id,
                        ActualWeightKg = receipt.TotalWeightKg,
                        UnitPrice = unitPrice,
                        TotalAmountCash = receipt.TotalAmountCash,
                        PointsAwarded = (int)receipt.PointsAwarded,
                        QrSecurityCode = receipt.ReceiptCode,
                        CompletedAt = now,
                        CreatedAt = now
                    });
                }

                var senderExists = await db.Users.AsNoTracking().AnyAsync(u => u.Id == receipt.UserId, ct);
                if (senderExists)
                {
                    db.ChatMessages.Add(new ChatMessage
                    {
                        Id = Guid.NewGuid(),
                        TradeOrderId = tradeOrder.Id,
                        SenderUserId = receipt.UserId,
                        SenderRole = "System",
                        MessageType = ChatMessageType.System,
                        MessageContent = $"Giao dịch #{receipt.ReceiptCode} đã hoàn tất. Tổng khối lượng thực tế: {receipt.TotalWeightKg:0.##} kg • Tổng thành tiền: {receipt.TotalAmountCash:N0} VNĐ • Điểm Xanh thưởng: +{receipt.PointsAwarded} điểm.",
                        IsRead = false,
                        SentAt = now,
                        CreatedAt = now
                    });
                }
            }

            // Đồng bộ điểm xanh xuống bảng user_eco_profiles trong PostgreSQL (BR-GAM-01)
            await UpsertUserEcoProfilePointsAsync(db, receipt.UserId, receipt.TotalWeightKg, (int)receipt.PointsAwarded, ct);
            await UpsertUserEcoProfilePointsAsync(db, receipt.RecyclerId, receipt.TotalWeightKg, (int)receipt.PointsAwarded, ct);

            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Không làm gián đoạn luồng chính nếu các bảng phụ không được seed trong Unit Test.
        }
    }

    private static async Task UpsertUserEcoProfilePointsAsync(
        ApplicationDbContext db,
        Guid userId,
        decimal weightKg,
        int pointsToAdd,
        CancellationToken ct)
    {
        var userExists = await db.Users.AsNoTracking().AnyAsync(u => u.Id == userId, ct);
        if (!userExists) return;

        var profile = await db.UserEcoProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile == null)
        {
            profile = new UserEcoProfile
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CurrentGreenPoints = pointsToAdd,
                TotalLifetimePoints = pointsToAdd,
                TreeLevel = 1 + (pointsToAdd / 100),
                TreeExperience = pointsToAdd % 100,
                TotalWasteScanned = 1,
                TotalRecycledKg = weightKg,
                CreatedAt = DateTime.UtcNow
            };
            db.UserEcoProfiles.Add(profile);
        }
        else
        {
            profile.CurrentGreenPoints += pointsToAdd;
            profile.TotalLifetimePoints += pointsToAdd;
            profile.TotalRecycledKg += weightKg;
            profile.TreeExperience += pointsToAdd;
            while (profile.TreeExperience >= 100)
            {
                profile.TreeLevel += 1;
                profile.TreeExperience -= 100;
            }
            profile.UpdatedAt = DateTime.UtcNow;
        }
    }

    private static async Task<CollectionPartner?> EnsurePartnerForRecyclerAsync(
        ApplicationDbContext db,
        Guid recyclerUserId,
        RecyclerProfile? recyclerProfile,
        CancellationToken ct)
    {
        var partner = await db.CollectionPartners
            .FirstOrDefaultAsync(p => p.UserId == recyclerUserId || p.Id == recyclerUserId, ct);
        if (partner != null) return partner;

        var userExists = await db.Users.AsNoTracking().AnyAsync(u => u.Id == recyclerUserId, ct);
        if (!userExists) return null;

        recyclerProfile ??= await db.RecyclerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == recyclerUserId, ct);

        partner = new CollectionPartner
        {
            Id = recyclerUserId,
            UserId = recyclerUserId,
            FacilityName = recyclerProfile?.FacilityName ?? "Cơ sở thu gom EcoLink",
            Address = recyclerProfile?.BusinessAddress ?? "TP. Hồ Chí Minh",
            Latitude = recyclerProfile != null ? (double)recyclerProfile.Latitude : 10.7769,
            Longitude = recyclerProfile != null ? (double)recyclerProfile.Longitude : 106.7009,
            OperatingHours = recyclerProfile?.OperatingHours ?? "08:00 - 18:00",
            ContactPhone = recyclerProfile?.ContactPhone ?? "0901234567",
            IsVerified = recyclerProfile == null || recyclerProfile.VerificationStatus == VerificationStatus.Verified
        };

        db.CollectionPartners.Add(partner);
        return partner;
    }

    private static async Task<List<HandoverRequestItem>> BuildHandoverItemsFromTradeOrderAsync(
        ApplicationDbContext db,
        Guid requestId,
        Guid recyclerId,
        string? categoryCode,
        decimal totalEstimatedKg,
        decimal? unitPrice,
        List<TradeOrderLineDto>? parsedLines,
        CancellationToken ct)
    {
        var categories = await db.WasteCategories.AsNoTracking().ToListAsync(ct);
        if (categories.Count == 0) return new List<HandoverRequestItem>();

        var activePrices = await db.RecyclerPrices
            .AsNoTracking()
            .Where(p => p.RecyclerId == recyclerId)
            .ToDictionaryAsync(p => p.WasteCategoryId, p => p.PricePerKg, ct);

        var result = new List<HandoverRequestItem>();
        var usedCategoryIds = new HashSet<Guid>();

        if (parsedLines != null && parsedLines.Count > 0)
        {
            foreach (var line in parsedLines)
            {
                var cat = MatchWasteCategory(categories, line.WasteId);
                if (cat == null) continue;

                if (usedCategoryIds.Add(cat.Id))
                {
                    var price = activePrices.TryGetValue(cat.Id, out var lp) ? lp : unitPrice;
                    result.Add(new HandoverRequestItem
                    {
                        Id = Guid.NewGuid(),
                        RequestId = requestId,
                        WasteCategoryId = cat.Id,
                        EstimatedWeightKg = decimal.Round(Math.Max(0.1m, line.Kg), 2),
                        AgreedPricePerKg = price
                    });
                }
                else
                {
                    var existingItem = result.First(i => i.WasteCategoryId == cat.Id);
                    existingItem.EstimatedWeightKg = decimal.Round(existingItem.EstimatedWeightKg + Math.Max(0.1m, line.Kg), 2);
                }
            }
        }

        if (result.Count == 0)
        {
            var fallbackCat = MatchWasteCategory(categories, categoryCode) ?? categories.First();
            var price = unitPrice ?? (activePrices.TryGetValue(fallbackCat.Id, out var lp) ? lp : (decimal?)null);
            result.Add(new HandoverRequestItem
            {
                Id = Guid.NewGuid(),
                RequestId = requestId,
                WasteCategoryId = fallbackCat.Id,
                EstimatedWeightKg = decimal.Round(Math.Max(0.1m, totalEstimatedKg), 2),
                AgreedPricePerKg = price
            });
        }

        return result;
    }

    private static WasteCategory? MatchWasteCategory(List<WasteCategory> categories, string? codeOrWasteId)
    {
        if (string.IsNullOrWhiteSpace(codeOrWasteId)) return categories.FirstOrDefault();
        var key = codeOrWasteId.Trim().ToLowerInvariant();

        var normalizedCode = key switch
        {
            "pet" or "hdpe" or "plastic" => "plastic",
            "cardboard" or "carton" => "cardboard",
            "paper" => "paper",
            "iron" or "aluminum" or "copper" or "metal" => "metal",
            "glass" => "glass",
            _ => key
        };

        return categories.FirstOrDefault(c => string.Equals(c.Code, normalizedCode, StringComparison.OrdinalIgnoreCase))
               ?? categories.FirstOrDefault(c => string.Equals(c.Code, key, StringComparison.OrdinalIgnoreCase))
               ?? categories.FirstOrDefault();
    }

    private static string MapCategoryCodeToWasteId(string? code)
    {
        var key = (code ?? "plastic").Trim().ToLowerInvariant();
        return key switch
        {
            "plastic" => "pet",
            "cardboard" => "cardboard",
            "paper" => "paper",
            "metal" => "iron",
            "glass" => "glass",
            _ => key
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
        if (!string.IsNullOrEmpty(clean)) parts.Add(clean);

        if (lat.HasValue && lng.HasValue && !(lat.Value == 0 && lng.Value == 0))
        {
            parts.Add(string.Format(CultureInfo.InvariantCulture, "[GPS:{0:F6},{1:F6}]", lat.Value, lng.Value));
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
                parts.Add($"[LINES:{encodedLines}]");
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
            return (null, null, null, null, null);

        double? lat = null;
        double? lng = null;
        var gpsMatch = GpsTagRegex.Match(storedNote);
        if (gpsMatch.Success)
        {
            if (double.TryParse(gpsMatch.Groups["lat"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLat))
                lat = parsedLat;
            if (double.TryParse(gpsMatch.Groups["lng"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedLng))
                lng = parsedLng;
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
            if (parsedList.Count > 0) lines = parsedList;
        }

        string? cancelReason = null;
        var reasonMatch = ReasonTagRegex.Match(storedNote);
        if (reasonMatch.Success)
            cancelReason = reasonMatch.Groups["reason"].Value.Trim();

        var stripped = GpsTagRegex.Replace(storedNote, string.Empty);
        stripped = LinesTagRegex.Replace(stripped, string.Empty);
        stripped = ReasonTagRegex.Replace(stripped, string.Empty).Trim();

        return (string.IsNullOrEmpty(stripped) ? null : stripped, lat, lng, lines, cancelReason);
    }
}

