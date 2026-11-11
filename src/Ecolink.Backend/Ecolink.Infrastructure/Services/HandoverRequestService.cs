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
/// Hiện thực Handover Request Management (UC U-06, U-07, R-03, R-04).
/// Quy tắc: BR-HND-01 (chỉ gửi tới Recycler đã duyệt, Online, nhận loại phế liệu đó),
/// BR-HND-02 (trạng thái một chiều), BR-HND-03 (Recycler chỉ thấy yêu cầu của mình),
/// BR-HND-06 (hủy trong {X} giờ = hủy muộn), BR-HND-08 (lộ liên hệ sau khi Accepted).
/// </summary>
public class HandoverRequestService : IHandoverRequestService
{
    // Các tham số {X} trong báo cáo chưa chốt giá trị -> hằng số tạm, đổi tại đây.
    public const int MaxActiveRequestsPerUser = 3;
    public const int LateCancelWindowHours = 4;      // BR-HND-06 / MSG-HND-06 (phải lớn hơn MinLeadHours để hủy muộn xảy ra được)
    private const int MinLeadHours = 2;              // SCR-USR-06: tối thiểu 2 giờ
    private const int MaxAheadDays = 7;               // SCR-USR-06: tối đa 7 ngày
    private static readonly TimeSpan LocalOffset = TimeSpan.FromHours(7); // ICT (GMT+7)

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static readonly HandoverStatus[] ActiveStatuses =
        { HandoverStatus.Pending, HandoverStatus.Negotiating, HandoverStatus.Accepted, HandoverStatus.Scheduled,
          HandoverStatus.AwaitingConfirmation };

    private static readonly HandoverStatus[] UserCancellable =
        { HandoverStatus.Pending, HandoverStatus.Negotiating, HandoverStatus.Accepted, HandoverStatus.Scheduled };

    private static readonly HandoverStatus[] ContactVisible =
        { HandoverStatus.Accepted, HandoverStatus.Scheduled, HandoverStatus.AwaitingConfirmation,
          HandoverStatus.Completed, HandoverStatus.Disputed };

    private readonly ApplicationDbContext _db;
    private readonly ILogger<HandoverRequestService> _logger;

    public HandoverRequestService(ApplicationDbContext db, ILogger<HandoverRequestService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ============================================================
    // Người dân
    // ============================================================
    public async Task<HandoverRequestDto> CreateAsync(Guid userId, CreateHandoverRequest request, CancellationToken ct = default)
    {
        var recyclerId = request.RecyclerId!.Value;
        if (recyclerId == userId)
            throw new BusinessRuleException("BR-HND-01", "MSG-GEN-01", "Bạn không thể gửi yêu cầu bàn giao cho chính cơ sở của mình.");

        var active = await _db.HandoverRequests.CountAsync(h => h.UserId == userId && ActiveStatuses.Contains(h.Status), ct);
        if (active >= MaxActiveRequestsPerUser)
            throw new BusinessRuleException("BR-HND-LIMIT", "MSG-GEN-01",
                $"Bạn chỉ có thể có tối đa {MaxActiveRequestsPerUser} yêu cầu đang xử lý cùng lúc.");

        var recycler = await _db.RecyclerProfiles.AsNoTracking().FirstOrDefaultAsync(r => r.UserId == recyclerId, ct)
                       ?? throw new NotFoundException("Không tìm thấy cơ sở thu gom.");
        EnsureRecyclerAvailable(recycler);

        var items = await ValidateAndBuildItemsAsync(recyclerId, request.Items, ct);
        var slot = ValidateTimeSlot(request.ProposedTimeSlot!.Value, recycler);
        var photos = ValidatePhotos(request.PhotoUrls);
        var address = ValidateAddress(request.PickupAddress);

        var entity = new HandoverRequest
        {
            RequestCode = await GenerateCodeAsync(ct),
            UserId = userId,
            RecyclerId = recyclerId,
            PickupAddress = address,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            WastePhotoUrl = JsonSerializer.Serialize(photos, JsonOptions),
            ProposedTimeSlot = slot,
            UserNote = Normalize(request.UserNote),
            Status = HandoverStatus.Pending,
            Items = items
        };
        _db.HandoverRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        await HandoverTradeSyncHelper.SyncHandoverToTradeOrderAsync(_db, entity.Id, userId, null, ct);

        _logger.LogInformation("Người dùng {UserId} tạo yêu cầu bàn giao {Code} tới {RecyclerId}.", userId, entity.RequestCode, recyclerId);
        return await ToDtoAsync(entity.Id, viewerIsRecycler: false, ct);
    }

    public async Task<HandoverRequestDto> UpdateAsync(Guid userId, Guid requestId, UpdateHandoverRequest request, CancellationToken ct = default)
    {
        var entity = await _db.HandoverRequests.Include(h => h.Items).Include(h => h.Recycler)
                         .FirstOrDefaultAsync(h => h.Id == requestId && h.UserId == userId, ct)
                     ?? throw new NotFoundException("Không tìm thấy yêu cầu bàn giao.");

        if (entity.Status != HandoverStatus.Pending)
            throw new BusinessRuleException("BR-HND-02", "MSG-GEN-01", "Chỉ có thể chỉnh sửa yêu cầu đang ở trạng thái Pending.");

        var items = await ValidateAndBuildItemsAsync(entity.RecyclerId, request.Items, ct);
        var slot = ValidateTimeSlot(request.ProposedTimeSlot!.Value, entity.Recycler!);
        var photos = ValidatePhotos(request.PhotoUrls);
        var address = ValidateAddress(request.PickupAddress);

        // Xóa dòng cũ và thêm dòng mới tường minh (item mới đã có Guid nên không được gán qua navigation, EF sẽ coi là Modified)
        _db.HandoverRequestItems.RemoveRange(entity.Items);
        foreach (var item in items) item.RequestId = entity.Id;
        _db.HandoverRequestItems.AddRange(items);
        entity.PickupAddress = address;
        entity.Latitude = request.Latitude;
        entity.Longitude = request.Longitude;
        entity.WastePhotoUrl = JsonSerializer.Serialize(photos, JsonOptions);
        entity.ProposedTimeSlot = slot;
        entity.UserNote = Normalize(request.UserNote);
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await HandoverTradeSyncHelper.SyncHandoverToTradeOrderAsync(
            _db, entity.Id, userId, "Người dân đã cập nhật thông tin yêu cầu bàn giao.", ct);

        return await ToDtoAsync(entity.Id, false, ct);
    }

    public async Task<HandoverRequestDto> CancelByUserAsync(Guid userId, Guid requestId, string reason, CancellationToken ct = default)
    {
        var entity = await _db.HandoverRequests
                         .FirstOrDefaultAsync(h => h.Id == requestId && h.UserId == userId, ct)
                     ?? throw new NotFoundException("Không tìm thấy yêu cầu bàn giao.");
        ApplyCancel(entity, userId, reason, UserCancellable);
        await _db.SaveChangesAsync(ct);

        await HandoverTradeSyncHelper.SyncHandoverToTradeOrderAsync(
            _db, entity.Id, userId, $"Người dân đã hủy yêu cầu bàn giao. Lý do: {entity.CancelReason}", ct);

        return await ToDtoAsync(entity.Id, false, ct);
    }

    public async Task<HandoverPagedResult<HandoverRequestDto>> GetUserRequestsAsync(
        Guid userId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        await SyncUnsyncedTradeOrdersForUserAsync(userId, isRecycler: false, ct);
        var query = _db.HandoverRequests.AsNoTracking().Where(h => h.UserId == userId);
        return await PageAsync(query, status, page, pageSize, viewerIsRecycler: false, ct);
    }

    public Task<HandoverRequestDto> GetUserRequestAsync(Guid userId, Guid requestId, CancellationToken ct = default) =>
        GetOwnedAsync(h => h.Id == requestId && h.UserId == userId, false, ct);

    // ============================================================
    // Recycler
    // ============================================================
    public async Task<HandoverPagedResult<HandoverRequestDto>> GetRecyclerRequestsAsync(
        Guid recyclerId, string? status, int page, int pageSize, CancellationToken ct = default)
    {
        await SyncUnsyncedTradeOrdersForUserAsync(recyclerId, isRecycler: true, ct);
        var query = _db.HandoverRequests.AsNoTracking().Where(h => h.RecyclerId == recyclerId); // BR-HND-03
        return await PageAsync(query, status, page, pageSize, viewerIsRecycler: true, ct);
    }

    private async Task SyncUnsyncedTradeOrdersForUserAsync(Guid userId, bool isRecycler, CancellationToken ct)
    {
        try
        {
            var ordersQuery = isRecycler
                ? _db.TradeOrders.Include(o => o.Partner).Where(o =>
                    (o.RecyclerUserId == userId || o.PartnerId == userId || (o.Partner != null && o.Partner.UserId == userId)) &&
                    !_db.HandoverRequests.Any(h => h.Id == o.Id))
                : _db.TradeOrders.Include(o => o.Partner).Where(o =>
                    o.UserId == userId && !_db.HandoverRequests.Any(h => h.Id == o.Id));

            var unsynced = await ordersQuery.ToListAsync(ct);
            foreach (var order in unsynced)
            {
                await HandoverTradeSyncHelper.EnsureHandoverFromTradeOrderAsync(_db, order, ct);
            }
        }
        catch { /* ignore in isolated tests */ }
    }

    public Task<HandoverRequestDto> GetRecyclerRequestAsync(Guid recyclerId, Guid requestId, CancellationToken ct = default) =>
        GetOwnedAsync(h => h.Id == requestId && h.RecyclerId == recyclerId, true, ct);

    public async Task<HandoverRequestDto> AcceptAsync(Guid recyclerId, Guid requestId, string? note, CancellationToken ct = default)
    {
        var entity = await FindForRecyclerAsync(recyclerId, requestId, ct);
        if (entity.Status is not (HandoverStatus.Pending or HandoverStatus.Negotiating))
            throw new BusinessRuleException("BR-HND-02", "MSG-GEN-01", $"Không thể chấp nhận yêu cầu đang ở trạng thái {entity.Status}.");

        if (entity.ProposedTimeSlot <= DateTime.UtcNow)
            throw new BusinessRuleException("BR-NEG-04", "MSG-GEN-01", "Khung giờ đề xuất đã qua, không thể chấp nhận. Vui lòng từ chối hoặc trao đổi lại với người dân.");

        entity.Status = HandoverStatus.Accepted;               // BR-NEG-05: chốt khung giờ đề xuất
        entity.ConfirmedTimeSlot = entity.ProposedTimeSlot;
        entity.RecyclerNote = Normalize(note);
        entity.UpdatedAt = DateTime.UtcNow;

        await HandoverTradeSyncHelper.LockHandoverItemPricesAsync(_db, entity, null, ct);
        await _db.SaveChangesAsync(ct);

        await HandoverTradeSyncHelper.SyncHandoverToTradeOrderAsync(
            _db,
            entity.Id,
            recyclerId,
            $"Cơ sở thu gom đã chấp nhận yêu cầu bàn giao. Khung giờ chốt: {HandoverTradeSyncHelper.FormatSlotText(entity.ConfirmedTimeSlot.Value)}.",
            ct);

        return await ToDtoAsync(entity.Id, true, ct);
    }

    public async Task<HandoverRequestDto> RejectAsync(Guid recyclerId, Guid requestId, string reason, CancellationToken ct = default)
    {
        var entity = await FindForRecyclerAsync(recyclerId, requestId, ct);
        if (entity.Status is not (HandoverStatus.Pending or HandoverStatus.Negotiating))
            throw new BusinessRuleException("BR-HND-02", "MSG-GEN-01", $"Không thể từ chối yêu cầu đang ở trạng thái {entity.Status}.");

        var cleanedReason = RequireReason(reason);
        entity.Status = HandoverStatus.Rejected;
        entity.CancelReason = cleanedReason;
        entity.CancelledBy = recyclerId;
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await HandoverTradeSyncHelper.SyncHandoverToTradeOrderAsync(
            _db, entity.Id, recyclerId, $"Cơ sở thu gom đã từ chối yêu cầu bàn giao. Lý do: {entity.CancelReason}", ct);

        return await ToDtoAsync(entity.Id, true, ct);
    }

    public async Task<HandoverRequestDto> CancelByRecyclerAsync(Guid recyclerId, Guid requestId, string reason, CancellationToken ct = default)
    {
        var entity = await FindForRecyclerAsync(recyclerId, requestId, ct);
        ApplyCancel(entity, recyclerId, reason, new[] { HandoverStatus.Accepted, HandoverStatus.Scheduled });
        await _db.SaveChangesAsync(ct);

        await HandoverTradeSyncHelper.SyncHandoverToTradeOrderAsync(
            _db, entity.Id, recyclerId, $"Cơ sở thu gom đã hủy lịch hẹn. Lý do: {entity.CancelReason}", ct);

        return await ToDtoAsync(entity.Id, true, ct);
    }

    // ============================================================
    // JOB-03
    // ============================================================
    public async Task<int> ExpirePendingAsync(TimeSpan timeout, CancellationToken ct = default)
    {
        var cutoff = DateTime.UtcNow - timeout;
        var now = DateTime.UtcNow;
        int count;
        if (_db.Database.IsRelational())
        {
            // Cập nhật nguyên tử có điều kiện Status = Pending: nếu Recycler vừa Accept thì bản ghi bị bỏ qua.
            count = await _db.HandoverRequests
                .Where(h => h.Status == HandoverStatus.Pending && h.CreatedAt < cutoff)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(h => h.Status, HandoverStatus.Expired)
                    .SetProperty(h => h.UpdatedAt, now), ct);
        }
        else
        {
            var pendingExpired = await _db.HandoverRequests
                .Where(h => h.Status == HandoverStatus.Pending && h.CreatedAt < cutoff)
                .ToListAsync(ct);
            foreach (var h in pendingExpired)
            {
                h.Status = HandoverStatus.Expired;
                h.UpdatedAt = now;
            }
            count = pendingExpired.Count;
            if (count > 0) await _db.SaveChangesAsync(ct);
        }

        try
        {
            await _db.TradeOrders
                .Where(o => o.Status == OrderStatus.Pending && o.CreatedAt < cutoff)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(o => o.Status, OrderStatus.Expired)
                    .SetProperty(o => o.UpdatedAt, now), ct);
        }
        catch { /* ignore in isolated tests */ }

        if (count > 0) _logger.LogInformation("JOB-03: đã chuyển {Count} yêu cầu sang Expired.", count);
        return count;
    }

    // ============================================================
    // Helpers
    // ============================================================
    private async Task<HandoverRequest> FindForRecyclerAsync(Guid recyclerId, Guid requestId, CancellationToken ct) =>
        await _db.HandoverRequests
            .Include(h => h.Items)
            .FirstOrDefaultAsync(h => h.Id == requestId && h.RecyclerId == recyclerId, ct)
        ?? throw new NotFoundException("Không tìm thấy yêu cầu bàn giao."); // BR-HND-03

    private void ApplyCancel(HandoverRequest entity, Guid actorId, string reason, HandoverStatus[] allowed)
    {
        if (!allowed.Contains(entity.Status))
            throw new BusinessRuleException("BR-HND-02", "MSG-GEN-01", $"Không thể hủy yêu cầu đang ở trạng thái {entity.Status}.");

        var cleaned = RequireReason(reason);
        var late = entity.ConfirmedTimeSlot.HasValue &&
                   entity.ConfirmedTimeSlot.Value - DateTime.UtcNow <= TimeSpan.FromHours(LateCancelWindowHours);

        entity.Status = HandoverStatus.Cancelled;
        entity.CancelReason = cleaned;
        entity.CancelledBy = actorId;
        entity.IsLateCancellation = late;     // BR-HND-06
        entity.UpdatedAt = DateTime.UtcNow;
    }

    private static string RequireReason(string? reason)
    {
        var r = reason?.Trim();
        if (string.IsNullOrEmpty(r))
            throw new BusinessRuleException("BR-HND-06", "MSG-GEN-01", "Vui lòng nhập lý do.");
        if (r.Length > 500)
            throw new BusinessRuleException("BR-HND-06", "MSG-GEN-01", "Lý do tối đa 500 ký tự.");
        return r;
    }

    private static void EnsureRecyclerAvailable(RecyclerProfile r)
    {
        if (r.VerificationStatus != VerificationStatus.Verified || r.ActiveStatus != RecyclerActiveStatus.Online)
            throw new BusinessRuleException("BR-HND-01", "MSG-GEN-01", "Cơ sở thu gom hiện không nhận yêu cầu bàn giao.");
    }

    private async Task<List<HandoverRequestItem>> ValidateAndBuildItemsAsync(
        Guid recyclerId, List<HandoverItemRequest> input, CancellationToken ct)
    {
        if (input.Count == 0)
            throw new BusinessRuleException("BR-HND-01", "MSG-GEN-01", "Vui lòng thêm ít nhất một loại phế liệu.");

        var ids = input.Select(i => i.WasteCategoryId!.Value).ToList();
        if (ids.Distinct().Count() != ids.Count)
            throw new BusinessRuleException("BR-HND-01", "MSG-GEN-01", "Mỗi loại phế liệu chỉ được xuất hiện một lần trong yêu cầu.");

        var prices = await _db.RecyclerPrices.AsNoTracking()
            .Where(p => p.RecyclerId == recyclerId && ids.Contains(p.WasteCategoryId) && p.Status == PriceStatus.Active)
            .ToDictionaryAsync(p => p.WasteCategoryId, ct);

        var result = new List<HandoverRequestItem>();
        foreach (var it in input)
        {
            var w = it.EstimatedWeightKg!.Value;
            if (w <= 0 || decimal.Round(w, 2) != w)
                throw new BusinessRuleException("BR-HND-01", "MSG-GEN-01", "Khối lượng ước tính phải lớn hơn 0 và tối đa 2 chữ số thập phân.");

            if (!prices.TryGetValue(it.WasteCategoryId!.Value, out var price))
                throw new BusinessRuleException("BR-HND-01", "MSG-GEN-01", "Cơ sở thu gom không nhận một trong các loại phế liệu đã chọn.");

            if (w < price.MinQuantityKg)
                throw new BusinessRuleException("BR-HND-01", "MSG-GEN-01",
                    $"Khối lượng tối thiểu cơ sở thu gom nhận là {price.MinQuantityKg.ToString("0.###", CultureInfo.InvariantCulture)} kg cho mỗi loại.");

            result.Add(new HandoverRequestItem
            {
                WasteCategoryId = it.WasteCategoryId.Value,
                EstimatedWeightKg = w,
                ItemNote = Normalize(it.ItemNote)
            });
        }
        return result;
    }

    private static DateTime ValidateTimeSlot(DateTimeOffset proposed, RecyclerProfile recycler)
    {
        var utc = proposed.UtcDateTime;
        var now = DateTime.UtcNow;
        if (utc < now.AddHours(MinLeadHours))
            throw new BusinessRuleException("BR-NEG-04", "MSG-GEN-01", $"Khung giờ thu gom phải cách hiện tại tối thiểu {MinLeadHours} giờ.");
        if (utc > now.AddDays(MaxAheadDays))
            throw new BusinessRuleException("BR-NEG-04", "MSG-GEN-01", $"Chỉ được hẹn trong vòng {MaxAheadDays} ngày tới.");

        // Nếu cơ sở đã khai báo lịch tuần, khung giờ phải nằm trong giờ mở cửa (ICT)
        if (!string.IsNullOrWhiteSpace(recycler.WeeklyScheduleJson))
        {
            List<ScheduleDayDto>? days = null;
            try { days = JsonSerializer.Deserialize<List<ScheduleDayDto>>(recycler.WeeklyScheduleJson, JsonOptions); }
            catch (JsonException) { /* lịch lỗi -> bỏ qua kiểm tra */ }

            var local = utc + LocalOffset;
            var day = days?.FirstOrDefault(d => d.DayOfWeek == (int)local.DayOfWeek);
            if (day != null)
            {
                if (day.IsClosed)
                    throw new BusinessRuleException("BR-NEG-04", "MSG-GEN-01", "Cơ sở thu gom nghỉ vào ngày đã chọn.");
                if (TimeSpan.TryParse(day.OpenTime, CultureInfo.InvariantCulture, out var open) &&
                    TimeSpan.TryParse(day.CloseTime, CultureInfo.InvariantCulture, out var close) &&
                    (local.TimeOfDay < open || local.TimeOfDay >= close))
                    throw new BusinessRuleException("BR-NEG-04", "MSG-GEN-01",
                        $"Khung giờ nằm ngoài giờ hoạt động của cơ sở ({day.OpenTime} - {day.CloseTime}).");
            }
        }
        return utc;
    }

    private static List<string> ValidatePhotos(List<string> urls)
    {
        var cleaned = urls.Select(u => u?.Trim() ?? string.Empty).ToList();
        if (cleaned.Count is < 1 or > 5)
            throw new BusinessRuleException("BR-HND-01", "MSG-GEN-01", "Vui lòng đính kèm từ 1 đến 5 ảnh.");
        foreach (var u in cleaned)
        {
            if (u.Length > 1000 || !Uri.TryCreate(u, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                throw new BusinessRuleException("BR-HND-01", "MSG-GEN-01", "URL ảnh không hợp lệ.");
        }
        return cleaned;
    }

    private static string ValidateAddress(string address)
    {
        var a = address?.Trim();
        if (string.IsNullOrEmpty(a))
            throw new BusinessRuleException("BR-HND-01", "MSG-GEN-01", "Vui lòng nhập địa chỉ thu gom.");
        return a;
    }

    private static string? Normalize(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task<string> GenerateCodeAsync(CancellationToken ct)
    {
        var prefix = $"HND-{(DateTime.UtcNow + LocalOffset):yyyyMMdd}-";
        if (_db.Database.IsRelational())
        {
            // Số thứ tự lấy từ sequence PostgreSQL (nguyên tử, không trùng khi tạo đồng thời)
            var n = await _db.Database
                .SqlQuery<long>($"SELECT nextval('seq_handover_request_code') AS \"Value\"")
                .SingleAsync(ct);
            return $"{prefix}{n:D4}";
        }

        var count = await _db.HandoverRequests.CountAsync(h => h.RequestCode.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:D4}";
    }

    private static bool Contains(HandoverStatus[] arr, HandoverStatus s) => arr.Contains(s);

    private async Task<HandoverPagedResult<HandoverRequestDto>> PageAsync(
        IQueryable<HandoverRequest> query, string? status, int page, int pageSize, bool viewerIsRecycler, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<HandoverStatus>(status, true, out var parsed) || !Enum.IsDefined(parsed))
                throw new BusinessRuleException("BR-HND-02", "MSG-GEN-01", "Trạng thái lọc không hợp lệ.");
            query = query.Where(h => h.Status == parsed);
        }
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var total = await query.CountAsync(ct);
        var ids = await query.OrderByDescending(h => h.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(h => h.Id).ToListAsync(ct);

        var entities = await LoadFullAsync(ids, ct);
        var dtos = await MapAsync(entities, viewerIsRecycler, ct);
        // giữ thứ tự theo ids
        var ordered = ids.Select(id => dtos.First(d => d.Id == id)).ToList();
        return new HandoverPagedResult<HandoverRequestDto> { Items = ordered, Page = page, PageSize = pageSize, TotalCount = total };
    }

    private async Task<HandoverRequestDto> GetOwnedAsync(
        System.Linq.Expressions.Expression<Func<HandoverRequest, bool>> predicate, bool viewerIsRecycler, CancellationToken ct)
    {
        var id = await _db.HandoverRequests.AsNoTracking().Where(predicate).Select(h => (Guid?)h.Id).FirstOrDefaultAsync(ct)
                 ?? throw new NotFoundException("Không tìm thấy yêu cầu bàn giao.");
        return await ToDtoAsync(id, viewerIsRecycler, ct);
    }

    private async Task<List<HandoverRequest>> LoadFullAsync(List<Guid> ids, CancellationToken ct) =>
        await _db.HandoverRequests.AsNoTracking()
            .Include(h => h.Items).ThenInclude(i => i.WasteCategory)
            .Include(h => h.Recycler)
            .Where(h => ids.Contains(h.Id)).ToListAsync(ct);

    private async Task<HandoverRequestDto> ToDtoAsync(Guid id, bool viewerIsRecycler, CancellationToken ct)
    {
        var list = await LoadFullAsync(new List<Guid> { id }, ct);
        return (await MapAsync(list, viewerIsRecycler, ct)).First();
    }

    private async Task<List<HandoverRequestDto>> MapAsync(List<HandoverRequest> entities, bool viewerIsRecycler, CancellationToken ct)
    {
        var userIds = entities.Select(e => e.UserId).Distinct().ToList();
        var users = await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FullName, u.PhoneNumber }).ToDictionaryAsync(u => u.Id, ct);
        var recyclerUserIds = entities.Select(e => e.RecyclerId).Distinct().ToList();
        var requestIds = entities.Select(e => e.Id).ToList();
        var withReceipt = (await _db.HandoverReceipts.AsNoTracking().Where(r => requestIds.Contains(r.RequestId))
            .Select(r => r.RequestId).ToListAsync(ct)).ToHashSet();

        return entities.Select(e =>
        {
            var revealed = Contains(ContactVisible, e.Status) ||
                           (e.Status == HandoverStatus.Cancelled && e.ConfirmedTimeSlot.HasValue);
            users.TryGetValue(e.UserId, out var u);

            var dto = new HandoverRequestDto
            {
                Id = e.Id,
                RequestCode = e.RequestCode,
                Status = e.Status.ToString(),
                Items = e.Items.OrderBy(i => i.CreatedAt).Select(i => new HandoverItemDto
                {
                    Id = i.Id,
                    WasteCategoryId = i.WasteCategoryId,
                    WasteCategoryCode = i.WasteCategory?.Code ?? string.Empty,
                    WasteCategoryName = i.WasteCategory?.Name ?? string.Empty,
                    EstimatedWeightKg = i.EstimatedWeightKg,
                    ActualWeightKg = i.ActualWeightKg,
                    AgreedPricePerKg = i.AgreedPricePerKg,
                    ItemNote = i.ItemNote
                }).ToList(),
                TotalEstimatedWeightKg = e.Items.Sum(i => i.EstimatedWeightKg),
                PhotoUrls = ParsePhotos(e.WastePhotoUrl),
                ProposedTimeSlot = DateTime.SpecifyKind(e.ProposedTimeSlot, DateTimeKind.Utc),
                ConfirmedTimeSlot = e.ConfirmedTimeSlot.HasValue ? DateTime.SpecifyKind(e.ConfirmedTimeSlot.Value, DateTimeKind.Utc) : null,
                UserNote = e.UserNote,
                RecyclerNote = e.RecyclerNote,
                CancelReason = e.CancelReason,
                CancelledByRole = e.CancelledBy == null ? null : (e.CancelledBy == e.RecyclerId ? "Recycler" : "User"),
                IsLateCancellation = e.IsLateCancellation,
                IsContactRevealed = revealed,
                HasReceipt = withReceipt.Contains(e.Id),
                CanEdit = e.Status == HandoverStatus.Pending,
                CanCancel = UserCancellable.Contains(e.Status),
                CreatedAt = DateTime.SpecifyKind(e.CreatedAt, DateTimeKind.Utc),
                UpdatedAt = DateTime.SpecifyKind(e.UpdatedAt, DateTimeKind.Utc)
            };

            if (viewerIsRecycler)
            {
                // BR-HND-08 / BR-MTH-05: địa chỉ chính xác và SĐT chỉ lộ sau khi Accepted; không bao giờ trả GPS
                dto.PickupAddress = revealed ? e.PickupAddress : null;
                dto.Requester = new HandoverPartyDto
                {
                    Id = e.UserId,
                    Name = u?.FullName ?? string.Empty,
                    Phone = revealed ? u?.PhoneNumber : null,
                    Address = revealed ? e.PickupAddress : null
                };
                dto.CanCancel = e.Status is HandoverStatus.Accepted or HandoverStatus.Scheduled;
                dto.CanEdit = false;
            }
            else
            {
                dto.PickupAddress = e.PickupAddress;
                dto.Recycler = new HandoverPartyDto
                {
                    Id = e.RecyclerId,
                    Name = e.Recycler?.FacilityName ?? string.Empty,
                    Address = e.Recycler?.BusinessAddress,
                    Phone = revealed ? e.Recycler?.ContactPhone : null
                };
            }
            return dto;
        }).ToList();
    }

    private static List<string> ParsePhotos(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<string>();
        try { return JsonSerializer.Deserialize<List<string>>(json, JsonOptions) ?? new List<string>(); }
        catch (JsonException) { return new List<string> { json }; }
    }
}
