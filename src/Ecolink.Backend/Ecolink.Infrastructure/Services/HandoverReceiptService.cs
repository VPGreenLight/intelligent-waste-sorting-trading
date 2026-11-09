using System.Globalization;
using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Hiện thực Handover Completion &amp; Transaction Receipt (UC R-06, U-09, U-12, JOB-01).
/// Quy tắc: BR-HND-04 (Completed khi Recycler lập biên nhận có ảnh cân và người dân xác nhận),
/// BR-HND-05 (quyết toán theo khối lượng thực tế x đơn giá đã chốt; hệ thống chỉ ghi nhận, không xử lý tiền),
/// BR-HND-07 (sau Completed không sửa/xóa), BR-GAM-01 (cộng điểm cho cả hai bên, mỗi sự kiện một lần).
/// </summary>
public class HandoverReceiptService : IHandoverReceiptService
{
    // Các tham số {X}/ma trận quy đổi chưa chốt trong báo cáo -> hằng số tạm
    public const int DeviationThresholdPercent = 20;   // MSG-HND-04
    public const int PointsPerKg = 10;                 // khớp quy tắc 1 kg = 10 điểm của GamificationService hiện có

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private enum Viewer { Recycler, User }

    private sealed record Line(
        HandoverRequestItem? Item, Guid CategoryId, string CategoryCode, string CategoryName,
        decimal Estimated, decimal Actual, decimal Price, bool IsExtra)
    {
        public decimal SubTotal => Math.Round(Actual * Price, 0, MidpointRounding.AwayFromZero);
    }

    private sealed record Plan(List<Line> Lines, string PhotosJson, List<string> Photos, string? ReceiptImageUrl)
    {
        public decimal TotalWeight => Lines.Sum(l => l.Actual);
        public decimal TotalAmount => Lines.Sum(l => l.SubTotal);
    }

    private readonly ApplicationDbContext _db;
    private readonly IGamificationService _gamification;
    private readonly ILogger<HandoverReceiptService> _logger;

    public HandoverReceiptService(ApplicationDbContext db, IGamificationService gamification,
        ILogger<HandoverReceiptService> logger)
    {
        _db = db;
        _gamification = gamification;
        _logger = logger;
    }

    // ============================================================
    // Recycler: R-06
    // ============================================================
    public async Task<ReceiptDto> PreviewAsync(Guid recyclerId, Guid requestId, CreateReceiptRequest request, CancellationToken ct = default)
    {
        var (entity, plan) = await BuildPlanAsync(recyclerId, requestId, request, ct);
        return BuildDto(entity, null, plan.Lines, plan.Photos, plan.ReceiptImageUrl, Viewer.Recycler, null, null);
    }

    public async Task<ReceiptDto> CreateAsync(Guid recyclerId, Guid requestId, CreateReceiptRequest request, CancellationToken ct = default)
    {
        var (entity, plan) = await BuildPlanAsync(recyclerId, requestId, request, ct);
        var now = DateTime.UtcNow;

        foreach (var line in plan.Lines)
        {
            if (line.Item != null)
            {
                line.Item.ActualWeightKg = line.Actual;
                line.Item.AgreedPricePerKg = line.Price;   // khóa đơn giá cho giao dịch này (Transaction Price Locking)
            }
            else
            {
                _db.HandoverRequestItems.Add(new HandoverRequestItem
                {
                    RequestId = entity.Id,
                    WasteCategoryId = line.CategoryId,
                    EstimatedWeightKg = 0,
                    ActualWeightKg = line.Actual,
                    AgreedPricePerKg = line.Price,
                    ItemNote = "Phát sinh khi cân tại chỗ"
                });
            }
        }

        var receipt = new HandoverReceipt
        {
            RequestId = entity.Id,
            ReceiptCode = await GenerateCodeAsync(ct),
            UserId = entity.UserId,
            RecyclerId = entity.RecyclerId,
            TotalWeightKg = plan.TotalWeight,
            TotalAmountCash = plan.TotalAmount,
            PointsAwarded = 0,
            ScaleProofPhotoUrl = plan.PhotosJson,
            ReceiptImageUrl = plan.ReceiptImageUrl,
            CreatedAt = now
        };
        _db.HandoverReceipts.Add(receipt);

        entity.Status = HandoverStatus.AwaitingConfirmation;
        entity.ActualHandoverAt = now;
        entity.UpdatedAt = now;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Tạo biên nhận cho yêu cầu {RequestId} thất bại.", requestId);
            throw new BusinessRuleException("BR-HND-04", "MSG-GEN-01",
                "Không thể tạo biên nhận (có thể yêu cầu này vừa được lập biên nhận). Vui lòng tải lại và thử lại.");
        }

        _logger.LogInformation("Recycler {RecyclerId} lập biên nhận {Code} cho yêu cầu {RequestId}.", recyclerId, receipt.ReceiptCode, requestId);
        return await LoadDtoAsync(requestId, Viewer.Recycler, ct);
    }

    public async Task<ReceiptDto> GetForRecyclerAsync(Guid recyclerId, Guid requestId, CancellationToken ct = default)
    {
        var exists = await _db.HandoverReceipts.AsNoTracking()
            .AnyAsync(r => r.RequestId == requestId && r.RecyclerId == recyclerId, ct);
        if (!exists) throw new NotFoundException("Không tìm thấy biên nhận.");
        return await LoadDtoAsync(requestId, Viewer.Recycler, ct);
    }

    public Task<TransactionHistoryDto> GetRecyclerHistoryAsync(Guid recyclerId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default) =>
        HistoryAsync(recyclerId, Viewer.Recycler, from, to, page, pageSize, ct);

    // ============================================================
    // Người dân: U-09, U-12
    // ============================================================
    public async Task<ReceiptDto> GetForUserAsync(Guid userId, Guid requestId, CancellationToken ct = default)
    {
        var exists = await _db.HandoverReceipts.AsNoTracking()
            .AnyAsync(r => r.RequestId == requestId && r.UserId == userId, ct);
        if (!exists) throw new NotFoundException("Không tìm thấy biên nhận.");
        return await LoadDtoAsync(requestId, Viewer.User, ct);
    }

    public async Task<ReceiptDto> ConfirmAsync(Guid userId, Guid requestId, CancellationToken ct = default)
    {
        var request = await _db.HandoverRequests.AsNoTracking()
                          .Where(h => h.Id == requestId && h.UserId == userId)
                          .Select(h => new { h.Status })
                          .FirstOrDefaultAsync(ct)
                      ?? throw new NotFoundException("Không tìm thấy yêu cầu bàn giao.");

        if (request.Status != HandoverStatus.AwaitingConfirmation)
        {
            var msg = request.Status == HandoverStatus.Completed
                ? "Giao dịch đã hoàn tất và không thể thay đổi."
                : "Yêu cầu chưa có biên nhận chờ xác nhận.";
            throw new BusinessRuleException("BR-HND-04", "MSG-GEN-01", msg);
        }

        var done = await CompleteAsync(requestId, ct);
        if (done == null)
            throw new BusinessRuleException("BR-HND-04", "MSG-GEN-01", "Biên nhận đã được xử lý trước đó.");

        return await LoadDtoAsync(requestId, Viewer.User, ct);
    }

    public Task<TransactionHistoryDto> GetUserHistoryAsync(Guid userId, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct = default) =>
        HistoryAsync(userId, Viewer.User, from, to, page, pageSize, ct);

    // ============================================================
    // JOB-01
    // ============================================================
    public async Task<int> AutoCompleteExpiredAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - timeout;
        var ids = await (from r in _db.HandoverReceipts.AsNoTracking()
                         join h in _db.HandoverRequests.AsNoTracking() on r.RequestId equals h.Id
                         where h.Status == HandoverStatus.AwaitingConfirmation && r.CreatedAt < cutoff
                         select r.RequestId).ToListAsync(ct);

        var count = 0;
        foreach (var id in ids)
        {
            try
            {
                if (await CompleteAsync(id, ct) != null) count++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "JOB-01: tự động hoàn tất yêu cầu {RequestId} thất bại.", id);
            }
            finally
            {
                _db.ChangeTracker.Clear();
            }
        }
        if (count > 0) _logger.LogInformation("JOB-01: đã tự động hoàn tất {Count} giao dịch.", count);
        return count;
    }

    // ============================================================
    // Hoàn tất giao dịch (dùng chung cho người dân xác nhận và JOB-01)
    // ============================================================
    private async Task<HandoverReceipt?> CompleteAsync(Guid requestId, CancellationToken ct)
    {
        var receipt = await _db.HandoverReceipts.FirstOrDefaultAsync(r => r.RequestId == requestId, ct);
        if (receipt == null) return null;

        var now = DateTime.UtcNow;
        var points = CalculatePoints(receipt.TotalWeightKg);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Cập nhật nguyên tử có điều kiện: nếu người dân và job cùng chạy, chỉ một bên thắng -> điểm chỉ cộng một lần
        var changed = await _db.HandoverRequests
            .Where(h => h.Id == requestId && h.Status == HandoverStatus.AwaitingConfirmation)
            .ExecuteUpdateAsync(s => s
                .SetProperty(h => h.Status, HandoverStatus.Completed)
                .SetProperty(h => h.UpdatedAt, now), ct);
        if (changed == 0) return null;

        receipt.ConfirmedByUserAt = now;
        receipt.PointsAwarded = points;
        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        // BR-GAM-01: cộng điểm cho cả người dân và Recycler. Điểm gốc đã lưu ở points_awarded;
        // GamificationService hiện là bộ nhớ tạm nên lỗi ở đây không được làm hỏng giao dịch.
        try
        {
            await _gamification.AwardPointsForRecyclingAsync(receipt.UserId, receipt.TotalWeightKg, ct);
            await _gamification.AwardPointsForRecyclingAsync(receipt.RecyclerId, receipt.TotalWeightKg, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không cộng được điểm vào hồ sơ xanh cho giao dịch {Code}.", receipt.ReceiptCode);
        }

        return receipt;
    }

    private static long CalculatePoints(decimal weightKg) =>
        (long)Math.Round(weightKg * PointsPerKg, 0, MidpointRounding.AwayFromZero);

    // ============================================================
    // Dựng kế hoạch biên nhận + kiểm tra hợp lệ
    // ============================================================
    private async Task<(HandoverRequest Request, Plan Plan)> BuildPlanAsync(
        Guid recyclerId, Guid requestId, CreateReceiptRequest req, CancellationToken ct)
    {
        var request = await _db.HandoverRequests
                          .Include(h => h.Items).ThenInclude(i => i.WasteCategory)
                          .FirstOrDefaultAsync(h => h.Id == requestId && h.RecyclerId == recyclerId, ct)
                      ?? throw new NotFoundException("Không tìm thấy yêu cầu bàn giao.");   // BR-HND-03

        if (request.Status is not (HandoverStatus.Accepted or HandoverStatus.Scheduled))
        {
            var msg = request.Status switch
            {
                HandoverStatus.AwaitingConfirmation => "Yêu cầu đã có biên nhận đang chờ người dân xác nhận.",
                HandoverStatus.Completed => "Giao dịch đã hoàn tất và không thể thay đổi.",
                _ => $"Chỉ lập biên nhận cho yêu cầu đã được chấp nhận (hiện tại: {request.Status})."
            };
            throw new BusinessRuleException("BR-HND-02", "MSG-GEN-01", msg);
        }

        if (await _db.HandoverReceipts.AnyAsync(r => r.RequestId == requestId, ct))
            throw new BusinessRuleException("BR-HND-04", "MSG-GEN-01", "Yêu cầu này đã có biên nhận.");

        var photos = ValidatePhotos(req.ScaleProofPhotoUrls);
        var receiptImage = ValidateOptionalUrl(req.ReceiptImageUrl);

        var existingById = request.Items.ToDictionary(i => i.Id);
        var requestCategories = request.Items.Select(i => i.WasteCategoryId).ToHashSet();
        var seenItems = new HashSet<Guid>();
        var seenExtras = new HashSet<Guid>();
        var raw = new List<(HandoverRequestItem? Item, Guid Category, decimal Weight)>();

        foreach (var it in req.Items)
        {
            var w = it.ActualWeightKg!.Value;
            if (w <= 0 || decimal.Round(w, 2) != w)
                throw new BusinessRuleException("BR-HND-05", "MSG-AUTH-02", "Khối lượng thực tế phải lớn hơn 0 và tối đa 2 chữ số thập phân.");

            if (it.ItemId.HasValue)
            {
                if (!existingById.TryGetValue(it.ItemId.Value, out var item))
                    throw new BusinessRuleException("BR-HND-05", "MSG-GEN-01", "Có dòng phế liệu không thuộc yêu cầu này.");
                if (!seenItems.Add(item.Id))
                    throw new BusinessRuleException("BR-HND-05", "MSG-GEN-01", "Mỗi dòng phế liệu chỉ được nhập một lần.");
                raw.Add((item, item.WasteCategoryId, w));
            }
            else
            {
                if (!it.WasteCategoryId.HasValue)
                    throw new BusinessRuleException("BR-HND-05", "MSG-AUTH-02", "Dòng phát sinh cần chọn loại phế liệu.");
                var cat = it.WasteCategoryId.Value;
                if (requestCategories.Contains(cat))
                    throw new BusinessRuleException("BR-HND-05", "MSG-GEN-01", "Loại phế liệu này đã có trong yêu cầu, hãy nhập theo dòng có sẵn.");
                if (!seenExtras.Add(cat))
                    throw new BusinessRuleException("BR-HND-05", "MSG-GEN-01", "Mỗi loại phế liệu phát sinh chỉ được nhập một lần.");
                raw.Add((null, cat, w));
            }
        }

        // EF-02: thiếu khối lượng của một dòng trong yêu cầu
        if (seenItems.Count != request.Items.Count)
            throw new BusinessRuleException("BR-HND-05", "MSG-AUTH-02", "Vui lòng nhập khối lượng thực tế cho tất cả loại phế liệu trong yêu cầu.");

        var categoryIds = raw.Select(r => r.Category).Distinct().ToList();
        var prices = await _db.RecyclerPrices.AsNoTracking()
            .Where(p => p.RecyclerId == recyclerId && categoryIds.Contains(p.WasteCategoryId))
            .ToDictionaryAsync(p => p.WasteCategoryId, ct);
        var extraCategories = await _db.WasteCategories.AsNoTracking()
            .Where(c => seenExtras.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);

        var lines = new List<Line>();
        foreach (var (item, categoryId, weight) in raw)
        {
            if (item != null)
            {
                var name = item.WasteCategory?.Name ?? string.Empty;
                decimal price;
                if (item.AgreedPricePerKg.HasValue) price = item.AgreedPricePerKg.Value;
                else if (prices.TryGetValue(categoryId, out var p)) price = p.PricePerKg;
                else throw new BusinessRuleException("BR-HND-05", "MSG-GEN-01", $"Chưa xác định được đơn giá cho loại \"{name}\". Vui lòng cập nhật bảng giá.");

                lines.Add(new Line(item, categoryId, item.WasteCategory?.Code ?? string.Empty, name,
                    item.EstimatedWeightKg, weight, price, false));
            }
            else
            {
                if (!extraCategories.TryGetValue(categoryId, out var cat))
                    throw new NotFoundException("Loại phế liệu phát sinh không tồn tại.");
                if (!prices.TryGetValue(categoryId, out var p) || p.Status != PriceStatus.Active)
                    throw new BusinessRuleException("BR-HND-05", "MSG-GEN-01", $"Loại \"{cat.Name}\" không nằm trong danh mục đang thu mua của cơ sở.");

                lines.Add(new Line(null, categoryId, cat.Code, cat.Name, 0, weight, p.PricePerKg, true));
            }
        }

        var plan = new Plan(lines, JsonSerializer.Serialize(photos, JsonOptions), photos, receiptImage);
        return (request, plan);
    }

    // ============================================================
    // Dựng DTO
    // ============================================================
    private async Task<ReceiptDto> LoadDtoAsync(Guid requestId, Viewer viewer, CancellationToken ct)
    {
        var entity = await _db.HandoverRequests.AsNoTracking()
            .Include(h => h.Items).ThenInclude(i => i.WasteCategory)
            .Include(h => h.Recycler)
            .FirstAsync(h => h.Id == requestId, ct);
        var receipt = await _db.HandoverReceipts.AsNoTracking().FirstAsync(r => r.RequestId == requestId, ct);
        var user = await _db.Users.AsNoTracking().Where(u => u.Id == entity.UserId)
            .Select(u => new { u.FullName, u.PhoneNumber }).FirstOrDefaultAsync(ct);

        var lines = entity.Items
            .Where(i => i.ActualWeightKg.HasValue)
            .OrderBy(i => i.CreatedAt)
            .Select(i => new Line(i, i.WasteCategoryId, i.WasteCategory?.Code ?? string.Empty,
                i.WasteCategory?.Name ?? string.Empty, i.EstimatedWeightKg, i.ActualWeightKg!.Value,
                i.AgreedPricePerKg ?? 0, i.EstimatedWeightKg == 0))
            .ToList();

        var photos = ParseList(receipt.ScaleProofPhotoUrl);
        return BuildDto(entity, receipt, lines, photos, receipt.ReceiptImageUrl, viewer, user?.FullName, user?.PhoneNumber);
    }

    private static ReceiptDto BuildDto(HandoverRequest entity, HandoverReceipt? receipt, List<Line> lines,
        List<string> photos, string? receiptImageUrl, Viewer viewer, string? userName, string? userPhone)
    {
        var totalWeight = lines.Sum(l => l.Actual);
        var totalAmount = lines.Sum(l => l.SubTotal);

        // Độ lệch chỉ tính trên các dòng có trong yêu cầu ban đầu (có khối lượng ước tính)
        var listed = lines.Where(l => !l.IsExtra && l.Estimated > 0).ToList();
        var estimated = listed.Sum(l => l.Estimated);
        var deviation = estimated > 0
            ? Math.Round((listed.Sum(l => l.Actual) - estimated) / estimated * 100, 2)
            : 0m;
        var warn = Math.Abs(deviation) > DeviationThresholdPercent;

        var confirmed = receipt?.ConfirmedByUserAt != null;
        var dto = new ReceiptDto
        {
            ReceiptId = receipt?.Id,
            ReceiptCode = receipt?.ReceiptCode,
            RequestId = entity.Id,
            RequestCode = entity.RequestCode,
            RequestStatus = entity.Status.ToString(),
            Lines = lines.Select(l => new ReceiptLineDto
            {
                ItemId = l.Item?.Id,
                WasteCategoryId = l.CategoryId,
                WasteCategoryCode = l.CategoryCode,
                WasteCategoryName = l.CategoryName,
                EstimatedWeightKg = l.Estimated,
                ActualWeightKg = l.Actual,
                UnitPricePerKg = l.Price,
                SubTotal = l.SubTotal,
                IsExtra = l.IsExtra
            }).ToList(),
            TotalWeightKg = totalWeight,
            TotalAmountCash = totalAmount,
            EstimatedWeightKg = estimated,
            DeviationPercent = deviation,
            HasDeviationWarning = warn,
            DeviationMessage = warn
                ? $"Khối lượng xác nhận chênh lệch so với thỏa thuận hơn {DeviationThresholdPercent}%. Vui lòng kiểm tra trước khi xác nhận."
                : null,
            Points = confirmed ? receipt!.PointsAwarded : CalculatePoints(totalWeight),
            PointsCredited = confirmed,
            ScaleProofPhotoUrls = photos,
            ReceiptImageUrl = receiptImageUrl,
            CreatedAt = receipt == null ? null : DateTime.SpecifyKind(receipt.CreatedAt, DateTimeKind.Utc),
            ConfirmedByUserAt = receipt?.ConfirmedByUserAt == null ? null : DateTime.SpecifyKind(receipt.ConfirmedByUserAt.Value, DateTimeKind.Utc),
            IsLocked = entity.Status == HandoverStatus.Completed,
            CanConfirm = viewer == Viewer.User && entity.Status == HandoverStatus.AwaitingConfirmation
        };

        if (receipt != null)
        {
            dto.Recycler = new HandoverPartyDto
            {
                Id = entity.RecyclerId,
                Name = entity.Recycler?.FacilityName ?? string.Empty,
                Address = entity.Recycler?.BusinessAddress,
                Phone = entity.Recycler?.ContactPhone
            };
            dto.Requester = new HandoverPartyDto
            {
                Id = entity.UserId,
                Name = userName ?? string.Empty,
                Phone = userPhone,
                Address = entity.PickupAddress
            };
        }
        return dto;
    }

    // ============================================================
    // Lịch sử giao dịch (U-12)
    // ============================================================
    private async Task<TransactionHistoryDto> HistoryAsync(
        Guid ownerId, Viewer viewer, DateTime? from, DateTime? to, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _db.HandoverReceipts.AsNoTracking().Where(r => r.ConfirmedByUserAt != null);
        query = viewer == Viewer.User
            ? query.Where(r => r.UserId == ownerId)
            : query.Where(r => r.RecyclerId == ownerId);

        if (from.HasValue)
        {
            var f = ToUtc(from.Value);
            query = query.Where(r => r.ConfirmedByUserAt >= f);
        }
        if (to.HasValue)
        {
            // "to" chỉ có ngày (00:00) -> gồm cả ngày đó
            var t = ToUtc(to.Value.TimeOfDay == TimeSpan.Zero ? to.Value.AddDays(1) : to.Value);
            query = query.Where(r => r.ConfirmedByUserAt < t);
        }

        var total = await query.CountAsync(ct);
        var sumWeight = await query.SumAsync(r => (decimal?)r.TotalWeightKg, ct) ?? 0m;
        var sumAmount = await query.SumAsync(r => (decimal?)r.TotalAmountCash, ct) ?? 0m;
        var sumPoints = await query.SumAsync(r => (long?)r.PointsAwarded, ct) ?? 0L;

        var rows = await query.OrderByDescending(r => r.ConfirmedByUserAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        var requestIds = rows.Select(r => r.RequestId).ToList();
        var codes = await _db.HandoverRequests.AsNoTracking().Where(h => requestIds.Contains(h.Id))
            .ToDictionaryAsync(h => h.Id, h => h.RequestCode, ct);
        var categoryRows = await _db.HandoverRequestItems.AsNoTracking()
            .Where(i => requestIds.Contains(i.RequestId) && i.ActualWeightKg != null)
            .Select(i => new { i.RequestId, Name = i.WasteCategory!.Name })
            .ToListAsync(ct);

        Dictionary<Guid, string> names;
        if (viewer == Viewer.User)
        {
            var ids = rows.Select(r => r.RecyclerId).Distinct().ToList();
            names = await _db.RecyclerProfiles.AsNoTracking().Where(p => ids.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId, p => p.FacilityName, ct);
        }
        else
        {
            var ids = rows.Select(r => r.UserId).Distinct().ToList();
            names = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        }

        var items = rows.Select(r =>
        {
            var counterpartId = viewer == Viewer.User ? r.RecyclerId : r.UserId;
            return new TransactionHistoryItemDto
            {
                ReceiptId = r.Id,
                ReceiptCode = r.ReceiptCode,
                RequestId = r.RequestId,
                RequestCode = codes.TryGetValue(r.RequestId, out var c) ? c : string.Empty,
                CompletedAt = DateTime.SpecifyKind(r.ConfirmedByUserAt!.Value, DateTimeKind.Utc),
                CounterpartName = names.TryGetValue(counterpartId, out var n) ? n : string.Empty,
                Categories = categoryRows.Where(x => x.RequestId == r.RequestId).Select(x => x.Name).Distinct().ToList(),
                TotalWeightKg = r.TotalWeightKg,
                TotalAmountCash = r.TotalAmountCash,
                PointsAwarded = r.PointsAwarded
            };
        }).ToList();

        return new TransactionHistoryDto
        {
            Items = items, Page = page, PageSize = pageSize, TotalCount = total,
            TotalWeightKg = sumWeight, TotalAmountCash = sumAmount, TotalPoints = sumPoints
        };
    }

    // ============================================================
    // Helpers
    // ============================================================
    private static DateTime ToUtc(DateTime d) =>
        d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();

    private static List<string> ValidatePhotos(List<string> urls)
    {
        var cleaned = urls.Select(u => u?.Trim() ?? string.Empty).ToList();
        if (cleaned.Count < 1)
            throw new BusinessRuleException("BR-HND-04", "MSG-AUTH-02", "Vui lòng tải lên ít nhất 1 ảnh cân (BR-HND-04).");
        if (cleaned.Count > 5)
            throw new BusinessRuleException("BR-HND-04", "MSG-GEN-01", "Tối đa 5 ảnh cân.");
        foreach (var u in cleaned) EnsureHttpUrl(u);
        return cleaned;
    }

    private static string? ValidateOptionalUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var u = url.Trim();
        EnsureHttpUrl(u);
        return u;
    }

    private static void EnsureHttpUrl(string u)
    {
        if (u.Length > 1000 || !Uri.TryCreate(u, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            throw new BusinessRuleException("BR-HND-04", "MSG-CLS-01", "URL ảnh không hợp lệ.");
    }

    private static List<string> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>(); }
        catch (JsonException) { return new List<string> { json }; }
    }

    /// <summary>
    /// Mã biên nhận lấy số từ sequence PostgreSQL (nextval là nguyên tử) nên hai yêu cầu đồng thời
    /// không bao giờ nhận cùng một số, và việc xóa bản ghi cũng không làm số bị dùng lại.
    /// </summary>
    private async Task<string> GenerateCodeAsync(CancellationToken ct)
    {
        var n = await _db.Database
            .SqlQuery<long>($"SELECT nextval('seq_handover_receipt_code') AS \"Value\"")
            .SingleAsync(ct);
        var year = DateTime.UtcNow.AddHours(7).ToString("yyyy", CultureInfo.InvariantCulture);
        return $"RCP-{year}-{n:D6}";
    }
}
