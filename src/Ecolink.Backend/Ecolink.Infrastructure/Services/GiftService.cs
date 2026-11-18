using System.Security.Cryptography;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Cửa hàng quà + kho quà (UC U-14, S-10). Đổi quà trừ điểm và trừ tồn kho trong MỘT transaction (BR-GAM-03, BR-GAM-04):
/// tồn kho giảm bằng UPDATE có điều kiện (còn hàng + đang ACTIVE) và điểm trừ bằng UPDATE có điều kiện (đủ số dư),
/// nên hai người cùng đổi món cuối cùng chỉ một người thành công. Voucher dùng một lần, gắn với tài khoản, có hạn dùng (BR-GAM-05).
/// </summary>
public class GiftService : IGiftService
{
    private const string Rule = "BR-GAM-04";
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";   // bỏ ký tự dễ nhầm: 0 O 1 I

    private readonly ApplicationDbContext _db;
    private readonly IPointsService _points;
    private readonly IGamificationConfigService _config;

    public GiftService(ApplicationDbContext db, IPointsService points, IGamificationConfigService config)
    {
        _db = db;
        _points = points;
        _config = config;
    }

    // ============================================================ Cửa hàng
    public async Task<List<GiftDto>> ListStoreAsync(string? type, string? sort, string? search, CancellationToken ct = default)
    {
        var q = _db.Gifts.AsNoTracking().Where(g => g.Status == "ACTIVE");
        if (!string.IsNullOrWhiteSpace(type)) { var t = ParseType(type); q = q.Where(g => g.Type == t); }
        if (!string.IsNullOrWhiteSpace(search)) { var p = GamErrors.Like(search); q = q.Where(g => EF.Functions.ILike(g.Name, p)); }

        q = (sort ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "points_asc" or "pointsasc" => q.OrderBy(g => g.PointsRequired).ThenBy(g => g.Id),
            "points_desc" or "pointsdesc" => q.OrderByDescending(g => g.PointsRequired).ThenBy(g => g.Id),
            _ => q.OrderByDescending(g => g.CreatedAt).ThenBy(g => g.Id)
        };
        return (await q.ToListAsync(ct)).Select(g => ToDto(g)).ToList();
    }

    public async Task<GiftDto> GetStoreGiftAsync(Guid giftId, CancellationToken ct = default)
    {
        var g = await _db.Gifts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == giftId && x.Status == "ACTIVE", ct)
                ?? throw new NotFoundException("Không tìm thấy quà tặng.");
        return ToDto(g);
    }

    public async Task<RedeemResultDto> RedeemAsync(Guid userId, Guid giftId, RedeemGiftRequest request, CancellationToken ct = default)
    {
        var gift = await _db.Gifts.AsNoTracking().FirstOrDefaultAsync(g => g.Id == giftId, ct)
                   ?? throw new NotFoundException("Không tìm thấy quà tặng.");
        if (gift.Status != "ACTIVE")
            throw GamErrors.Rule(Rule, "MSG-GAM-04", "Quà này hiện không còn khả dụng.");

        var qty = request.Quantity <= 0 ? 1 : request.Quantity;
        string? address = null;
        if (gift.Type == GiftType.Voucher)
        {
            if (qty != 1)
                throw GamErrors.Rule("BR-GAM-05", "MSG-GEN-01", "Voucher chỉ đổi được một mã mỗi lượt.");
        }
        else
        {
            address = request.DeliveryAddress?.Trim();
            if (string.IsNullOrEmpty(address))
                throw GamErrors.Rule(Rule, "MSG-AUTH-02", "Vui lòng nhập địa chỉ giao hàng cho quà hiện vật.");
        }

        var settings = await _config.GetSettingsAsync(ct);
        var cost = checked(gift.PointsRequired * qty);
        var now = DateTime.UtcNow;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // 1) Giảm tồn kho nguyên tử: chỉ khi quà còn ACTIVE và đủ hàng (BR-GAM-04)
        var stockOk = await _db.Gifts
            .Where(g => g.Id == giftId && g.Status == "ACTIVE" && g.StockQuantity >= qty)
            .ExecuteUpdateAsync(s => s
                .SetProperty(g => g.StockQuantity, g => g.StockQuantity - qty)
                .SetProperty(g => g.UpdatedAt, now), ct);
        if (stockOk == 0)
            throw GamErrors.Rule(Rule, "MSG-GAM-04", "Quà này đã hết hàng hoặc không còn khả dụng.");

        var redemption = new GiftRedemption
        {
            UserId = userId,
            GiftId = giftId,
            PointsSpent = cost,
            Quantity = qty,
            RedemptionCode = await NewRedemptionCodeAsync(ct),
            DeliveryAddress = address,
            RedeemedAt = now
        };
        if (gift.Type == GiftType.Voucher)
        {
            redemption.VoucherCode = await NewVoucherCodeAsync(ct);
            redemption.ExpiresAt = now.AddDays(settings.VoucherDays);
            redemption.Status = RedemptionStatus.Redeemed;
        }
        else
        {
            redemption.Status = RedemptionStatus.Pending;
        }
        _db.GiftRedemptions.Add(redemption);
        await _db.SaveChangesAsync(ct);

        // 2) Trừ điểm nguyên tử: không đủ điểm -> rollback cả tồn kho (BR-GAM-03)
        var paid = await _points.TryDebitAsync(userId, cost, PointActionType.RedeemGift, "GiftRedemption",
            redemption.Id.ToString(), $"REDEEM:{redemption.Id}", $"Đổi quà: {gift.Name} x{qty}", ct);
        if (!paid)
            throw GamErrors.Rule("BR-GAM-03", "MSG-GAM-03", "Bạn không đủ điểm để đổi quà này.");

        await tx.CommitAsync(ct);

        var saved = await LoadRedemptionAsync(redemption.Id, ct);
        return new RedeemResultDto { Redemption = saved, WalletBalance = (await _points.GetWalletAsync(userId, ct)).Balance };
    }

    public async Task<GamPagedResult<RedemptionDto>> ListMyRedemptionsAsync(Guid userId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.GiftRedemptions.AsNoTracking().Include(r => r.Gift).Where(r => r.UserId == userId);
        if (!string.IsNullOrWhiteSpace(status)) { var st = ParseStatus(status); q = q.Where(r => r.Status == st); }
        return await PageAsync(q.OrderByDescending(r => r.RedeemedAt).ThenBy(r => r.Id), page, pageSize, r => ToDto(r, null), ct);
    }

    public async Task<RedemptionDto> GetMyRedemptionAsync(Guid userId, Guid redemptionId, CancellationToken ct = default)
    {
        var r = await _db.GiftRedemptions.AsNoTracking().Include(x => x.Gift)
                    .FirstOrDefaultAsync(x => x.Id == redemptionId && x.UserId == userId, ct)
                ?? throw new NotFoundException("Không tìm thấy lượt đổi quà.");
        return ToDto(r, null);
    }

    // ============================================================ Staff (S-10)
    public async Task<GamPagedResult<GiftDto>> ListInventoryAsync(string? type, string? status, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.Gifts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(type)) { var t = ParseType(type); q = q.Where(g => g.Type == t); }
        if (!string.IsNullOrWhiteSpace(status)) { var st = ParseGiftStatus(status); q = q.Where(g => g.Status == st); }
        if (!string.IsNullOrWhiteSpace(search)) { var p = GamErrors.Like(search); q = q.Where(g => EF.Functions.ILike(g.Name, p)); }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(g => g.CreatedAt).ThenBy(g => g.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new GamPagedResult<GiftDto> { Items = rows.Select(g => ToDto(g)).ToList(), Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<GiftDto> GetInventoryGiftAsync(Guid giftId, CancellationToken ct = default) =>
        ToDto(await _db.Gifts.AsNoTracking().FirstOrDefaultAsync(g => g.Id == giftId, ct)
              ?? throw new NotFoundException("Không tìm thấy quà tặng."));

    public async Task<GiftDto> CreateAsync(SaveGiftRequest r, CancellationToken ct = default)
    {
        var (name, type, desc, image) = Validate(r);
        var gift = new Gift
        {
            Name = name, Type = type, Description = desc, ImageUrl = image, PointsRequired = r.PointsRequired,
            StockQuantity = r.StockQuantity, Status = "ACTIVE"
        };
        _db.Gifts.Add(gift);
        await _db.SaveChangesAsync(ct);
        return ToDto(gift);
    }

    public async Task<GiftDto> UpdateAsync(Guid giftId, SaveGiftRequest r, CancellationToken ct = default)
    {
        var (name, type, desc, image) = Validate(r);
        var gift = await _db.Gifts.FirstOrDefaultAsync(g => g.Id == giftId, ct) ?? throw new NotFoundException("Không tìm thấy quà tặng.");
        gift.Name = name; gift.Type = type; gift.Description = desc; gift.ImageUrl = image;
        gift.PointsRequired = r.PointsRequired;          // tồn kho đổi riêng bằng restock, để không ghi đè số đang bị đổi
        gift.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(gift);
    }

    public async Task<GiftDto> RestockAsync(Guid giftId, int quantity, CancellationToken ct = default)
    {
        if (quantity <= 0) throw GamErrors.Rule(Rule, "MSG-AUTH-02", "Số lượng nhập thêm phải lớn hơn 0.");
        var now = DateTime.UtcNow;
        var n = await _db.Gifts.Where(g => g.Id == giftId).ExecuteUpdateAsync(s => s
            .SetProperty(g => g.StockQuantity, g => g.StockQuantity + quantity)
            .SetProperty(g => g.UpdatedAt, now), ct);
        if (n == 0) throw new NotFoundException("Không tìm thấy quà tặng.");
        return await GetInventoryGiftAsync(giftId, ct);
    }

    public async Task<ImportVouchersResultDto> ImportVouchersAsync(Guid giftId, ImportVouchersRequest request, CancellationToken ct = default)
    {
        var gift = await _db.Gifts.AsNoTracking().FirstOrDefaultAsync(g => g.Id == giftId, ct)
                   ?? throw new NotFoundException("Không tìm thấy quà tặng.");
        if (gift.Type != GiftType.Voucher)
            throw GamErrors.Rule("BR-GAM-05", "MSG-GEN-01", "Chỉ vật phẩm loại Voucher mới hỗ trợ nhập mã voucher.");

        int importCount;
        var rawCodes = request.Codes?.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToList();
        if (rawCodes is { Count: > 0 })
        {
            if (rawCodes.Any(c => c.Length > 80))
                throw GamErrors.Rule(Rule, "MSG-NTF-03", "Mã voucher tối đa 80 ký tự.");
            importCount = rawCodes.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        }
        else
        {
            importCount = request.Quantity ?? 0;
        }

        if (importCount <= 0 || importCount > 100000)
            throw GamErrors.Rule(Rule, "MSG-AUTH-02", "Vui lòng cung cấp danh sách mã voucher hoặc số lượng hợp lệ (1–100000).");

        var now = DateTime.UtcNow;
        await _db.Gifts.Where(g => g.Id == giftId).ExecuteUpdateAsync(s => s
            .SetProperty(g => g.StockQuantity, g => g.StockQuantity + importCount)
            .SetProperty(g => g.UpdatedAt, now), ct);

        var updated = await GetInventoryGiftAsync(giftId, ct);
        return new ImportVouchersResultDto
        {
            GiftId = giftId,
            ImportedCount = importCount,
            NewStockQuantity = updated.StockQuantity
        };
    }

    public async Task<GiftDto> SetStatusAsync(Guid giftId, string status, CancellationToken ct = default)
    {
        var st = ParseGiftStatus(status);
        var now = DateTime.UtcNow;
        var n = await _db.Gifts.Where(g => g.Id == giftId).ExecuteUpdateAsync(s => s
            .SetProperty(g => g.Status, st).SetProperty(g => g.UpdatedAt, now), ct);
        if (n == 0) throw new NotFoundException("Không tìm thấy quà tặng.");
        return await GetInventoryGiftAsync(giftId, ct);
    }

    public async Task<GamPagedResult<RedemptionDto>> ListGiftRedemptionsAsync(Guid giftId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        if (!await _db.Gifts.AnyAsync(g => g.Id == giftId, ct)) throw new NotFoundException("Không tìm thấy quà tặng.");
        var q = _db.GiftRedemptions.AsNoTracking().Include(r => r.Gift).Where(r => r.GiftId == giftId);
        if (!string.IsNullOrWhiteSpace(status)) { var st = ParseStatus(status); q = q.Where(r => r.Status == st); }

        return await PageStaffRedemptionsAsync(q, page, pageSize, ct);
    }

    public async Task<GamPagedResult<RedemptionDto>> ListAllRedemptionsAsync(string? status, string? type, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.GiftRedemptions.AsNoTracking().Include(r => r.Gift).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) { var st = ParseStatus(status); q = q.Where(r => r.Status == st); }
        if (!string.IsNullOrWhiteSpace(type)) { var t = ParseType(type); q = q.Where(r => r.Gift != null && r.Gift.Type == t); }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var p = GamErrors.Like(search);
            q = q.Where(r => EF.Functions.ILike(r.RedemptionCode, p)
                             || (r.Gift != null && EF.Functions.ILike(r.Gift.Name, p)));
        }

        return await PageStaffRedemptionsAsync(q, page, pageSize, ct);
    }

    private async Task<GamPagedResult<RedemptionDto>> PageStaffRedemptionsAsync(IQueryable<GiftRedemption> q, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(r => r.RedeemedAt).ThenBy(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var ids = rows.Select(r => r.UserId).Distinct().ToList();
        var names = await _db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.FullName, ct);
        return new GamPagedResult<RedemptionDto>
        {
            Page = page, PageSize = pageSize, TotalCount = total,
            Items = rows.Select(r => ToDto(r, names.GetValueOrDefault(r.UserId))).ToList()
        };
    }

    public Task<RedemptionDto> FulfillAsync(Guid redemptionId, CancellationToken ct = default) =>
        FulfillAsync(redemptionId, null, ct);

    /// <summary>Staff xác nhận đã giao quà hiện vật / phát voucher (Fulfill) hoặc hủy đơn kèm hoàn điểm + tồn kho (Cancel).</summary>
    public async Task<RedemptionDto> FulfillAsync(Guid redemptionId, FulfillRedemptionRequest? request, CancellationToken ct = default)
    {
        var action = request?.Action?.Trim();
        var isCancel = string.Equals(action, "Cancel", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(action, "Cancelled", StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrEmpty(action)
            && !isCancel
            && !string.Equals(action, "Fulfill", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(action, "Fulfilled", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(action, "Delivered", StringComparison.OrdinalIgnoreCase))
        {
            throw GamErrors.Rule("BR-GAM-05", "MSG-AUTH-02", "Thao tác xử lý không hợp lệ (Fulfill hoặc Cancel).");
        }

        var now = DateTime.UtcNow;
        if (isCancel)
        {
            var existing = await _db.GiftRedemptions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == redemptionId, ct)
                           ?? throw new NotFoundException("Không tìm thấy lượt đổi quà.");

            await using var tx = await _db.Database.BeginTransactionAsync(ct);
            var cancelled = await _db.GiftRedemptions
                .Where(r => r.Id == redemptionId
                            && (r.Status == RedemptionStatus.Pending || r.Status == RedemptionStatus.Redeemed))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(r => r.Status, RedemptionStatus.Cancelled)
                    .SetProperty(r => r.FulfilledAt, (DateTime?)now), ct);
            if (cancelled == 0)
                throw GamErrors.Rule("BR-GAM-05", "MSG-GEN-01", "Lượt đổi quà này đã được xử lý, đã hết hạn hoặc đã bị hủy.");

            await _db.Gifts
                .Where(g => g.Id == existing.GiftId)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(g => g.StockQuantity, g => g.StockQuantity + existing.Quantity)
                    .SetProperty(g => g.UpdatedAt, now), ct);

            if (existing.PointsSpent > 0)
            {
                var note = string.IsNullOrWhiteSpace(request?.Reason)
                    ? $"Hoàn điểm hủy đơn đổi quà {existing.RedemptionCode}"
                    : $"Hoàn điểm hủy đơn đổi quà {existing.RedemptionCode}: {request.Reason.Trim()}";
                await _points.CreditAsync(
                    existing.UserId,
                    existing.PointsSpent,
                    PointActionType.Adjustment,
                    "GiftRedemptionRefund",
                    existing.Id.ToString(),
                    $"REFUND:{existing.Id}",
                    note,
                    ct);
            }

            await tx.CommitAsync(ct);
            return await LoadRedemptionAsync(redemptionId, ct);
        }

        var n = await _db.GiftRedemptions
            .Where(r => r.Id == redemptionId
                        && (r.Status == RedemptionStatus.Pending
                            || (r.Status == RedemptionStatus.Redeemed && (r.ExpiresAt == null || r.ExpiresAt > now))))
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RedemptionStatus.Delivered)
                .SetProperty(r => r.FulfilledAt, (DateTime?)now), ct);
        if (n == 0)
        {
            if (!await _db.GiftRedemptions.AnyAsync(r => r.Id == redemptionId, ct))
                throw new NotFoundException("Không tìm thấy lượt đổi quà.");
            throw GamErrors.Rule("BR-GAM-05", "MSG-GEN-01", "Lượt đổi quà này đã được xử lý, đã hết hạn hoặc đã bị hủy.");
        }
        return await LoadRedemptionAsync(redemptionId, ct);
    }

    /// <summary>JOB-12: voucher đã phát nhưng quá hạn -> Expired để không thể dùng tại điểm đối tác (BR-GAM-05).</summary>
    public async Task<int> ExpireVouchersAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.GiftRedemptions
            .Where(r => r.Status == RedemptionStatus.Redeemed && r.ExpiresAt != null && r.ExpiresAt < now)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RedemptionStatus.Expired), ct);
    }

    // ============================================================ Hỗ trợ
    private static (string Name, GiftType Type, string? Desc, string? Image) Validate(SaveGiftRequest r)
    {
        var name = (r.Name ?? string.Empty).Trim();
        if (name.Length == 0) throw GamErrors.Rule("BR-GAM-04", "MSG-AUTH-02", "Vui lòng nhập tên quà.");
        if (name.Length > 150) throw GamErrors.Rule("BR-GAM-04", "MSG-NTF-03", "Tên quà tối đa 150 ký tự.");
        if (string.IsNullOrWhiteSpace(r.Type)) throw GamErrors.Rule("BR-GAM-04", "MSG-AUTH-02", "Vui lòng chọn loại quà.");
        var type = ParseType(r.Type);
        var desc = string.IsNullOrWhiteSpace(r.Description) ? null : r.Description.Trim();
        if (desc is { Length: > 5000 }) throw GamErrors.Rule("BR-GAM-04", "MSG-NTF-03", "Mô tả quá dài.");
        if (r.PointsRequired < 0) throw GamErrors.Rule("BR-GAM-04", "MSG-AUTH-02", "Điểm quy đổi phải là số không âm.");
        if (r.StockQuantity < 0) throw GamErrors.Rule("BR-GAM-04", "MSG-AUTH-02", "Số lượng tồn kho phải là số không âm.");
        return (name, type, desc, GamErrors.CleanImageUrl(r.ImageUrl, "BR-GAM-04"));
    }

    private static GiftType ParseType(string s) =>
        Enum.TryParse<GiftType>(s.Trim(), true, out var t) && Enum.IsDefined(t)
            ? t : throw GamErrors.Rule(Rule, "MSG-GEN-01", "Loại quà không hợp lệ (Physical hoặc Voucher).");

    private static RedemptionStatus ParseStatus(string s) =>
        Enum.TryParse<RedemptionStatus>(s.Trim(), true, out var t) && Enum.IsDefined(t)
            ? t : throw GamErrors.Rule(Rule, "MSG-GEN-01", "Trạng thái lọc không hợp lệ.");

    private static string ParseGiftStatus(string? s)
    {
        var v = (s ?? string.Empty).Trim().ToUpperInvariant();
        return v is "ACTIVE" or "INACTIVE" ? v : throw GamErrors.Rule(Rule, "MSG-AUTH-02", "Trạng thái phải là ACTIVE hoặc INACTIVE.");
    }

    private async Task<RedemptionDto> LoadRedemptionAsync(Guid id, CancellationToken ct) =>
        ToDto(await _db.GiftRedemptions.AsNoTracking().Include(r => r.Gift).FirstAsync(r => r.Id == id, ct), null);

    private static async Task<GamPagedResult<RedemptionDto>> PageAsync(IQueryable<GiftRedemption> ordered, int page, int pageSize,
        Func<GiftRedemption, RedemptionDto> map, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var total = await ordered.CountAsync(ct);
        var rows = await ordered.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new GamPagedResult<RedemptionDto> { Items = rows.Select(map).ToList(), Page = page, PageSize = pageSize, TotalCount = total };
    }

    private async Task<string> NewRedemptionCodeAsync(CancellationToken ct)
    {
        var date = (DateTime.UtcNow + GamTime.Ict).ToString("yyyyMMdd");
        for (var i = 0; i < 5; i++)
        {
            var code = $"RDM-{date}-{RandomCode(6)}";
            if (!await _db.GiftRedemptions.AnyAsync(r => r.RedemptionCode == code, ct)) return code;
        }
        return $"RDM-{date}-{Guid.NewGuid():N}"[..24].ToUpperInvariant();
    }

    private async Task<string> NewVoucherCodeAsync(CancellationToken ct)
    {
        for (var i = 0; i < 5; i++)
        {
            var code = $"ECO-{RandomCode(4)}-{RandomCode(4)}-{RandomCode(4)}";
            if (!await _db.GiftRedemptions.AnyAsync(r => r.VoucherCode == code, ct)) return code;
        }
        return $"ECO-{Guid.NewGuid():N}"[..20].ToUpperInvariant();
    }

    private static string RandomCode(int length)
    {
        var chars = new char[length];
        for (var i = 0; i < length; i++) chars[i] = CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)];
        return new string(chars);
    }

    private static GiftDto ToDto(Gift g) => new()
    {
        Id = g.Id, Name = g.Name, Type = g.Type.ToString(), Description = g.Description, ImageUrl = g.ImageUrl,
        PointsRequired = g.PointsRequired, StockQuantity = g.StockQuantity, InStock = g.StockQuantity > 0 && g.Status == "ACTIVE",
        Status = g.Status, CreatedAt = g.CreatedAt, UpdatedAt = g.UpdatedAt
    };

    private static RedemptionDto ToDto(GiftRedemption r, string? userName) => new()
    {
        Id = r.Id, RedemptionCode = r.RedemptionCode, PointsSpent = r.PointsSpent, Quantity = r.Quantity,
        VoucherCode = r.VoucherCode, ExpiresAt = r.ExpiresAt, Status = r.Status.ToString(), DeliveryAddress = r.DeliveryAddress,
        RedeemedAt = r.RedeemedAt, FulfilledAt = r.FulfilledAt, UserName = userName,
        Gift = new RedemptionGiftInfoDto
        {
            Id = r.GiftId, Name = r.Gift?.Name ?? string.Empty, Type = r.Gift?.Type.ToString() ?? string.Empty, ImageUrl = r.Gift?.ImageUrl
        }
    };
}
