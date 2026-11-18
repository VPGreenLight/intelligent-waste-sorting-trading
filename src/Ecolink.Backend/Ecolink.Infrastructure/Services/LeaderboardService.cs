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
/// Quản lý Chu kỳ Bảng xếp hạng (UC A-08: Tuần/Tháng/Quý) và tổng hợp Xếp hạng công dân (STT 077, 100–102).
/// Điểm xếp hạng tính trên điểm tích lũy (TotalEarned / điểm earned trong kỳ), đổi quà không làm tụt hạng (BR-GAM-07).
/// Chỉ một chu kỳ được đặt IsActive = true tại một thời điểm (ux uq_leaderboard_cycles_single_active).
/// </summary>
public class LeaderboardService : ILeaderboardService
{
    private const string Rule = "BR-GAM-07";
    private const int MaxNameLength = 150;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly IPointsService _points;
    private readonly IAuditLogService? _auditLog;
    private readonly ILogger<LeaderboardService>? _logger;

    public LeaderboardService(
        ApplicationDbContext db,
        IPointsService points,
        IAuditLogService? auditLog = null,
        ILogger<LeaderboardService>? logger = null)
    {
        _db = db;
        _points = points;
        _auditLog = auditLog;
        _logger = logger;
    }

    public async Task<List<LeaderboardCycleDto>> GetCyclesAsync(CancellationToken ct = default)
    {
        var cycles = await _db.LeaderboardCycles
            .AsNoTracking()
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.StartsAt)
            .ThenByDescending(c => c.CreatedAt)
            .ToListAsync(ct);

        return cycles.Select(ToCycleDto).ToList();
    }

    public async Task<LeaderboardCycleDto> CreateCycleAsync(Guid adminId, SaveLeaderboardCycleRequest request, CancellationToken ct = default)
    {
        var (name, period, startsAt, endsAt) = ValidateCycleRequest(request);
        var now = DateTime.UtcNow;

        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        if (request.IsActive)
        {
            await DeactivateOtherActiveCyclesAsync(null, now, ct);
        }

        var status = request.IsActive
            ? LeaderboardCycleStatus.Active
            : (startsAt > now ? LeaderboardCycleStatus.Upcoming : LeaderboardCycleStatus.Active);

        var entity = new LeaderboardCycle
        {
            Id = Guid.NewGuid(),
            Name = name,
            PeriodType = period,
            StartsAt = startsAt,
            EndsAt = endsAt,
            IsActive = request.IsActive,
            Status = status,
            Top1BonusPoints = request.Top1BonusPoints,
            Top2BonusPoints = request.Top2BonusPoints,
            Top3BonusPoints = request.Top3BonusPoints,
            CreatedByAdminId = adminId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.LeaderboardCycles.Add(entity);
        await _db.SaveChangesAsync(ct);

        if (_auditLog != null)
        {
            await _auditLog.LogAsync(
                action: "LEADERBOARD_CYCLE_CREATE",
                entityType: "LeaderboardCycle",
                entityId: entity.Id.ToString(),
                oldValues: null,
                newValues: ToCycleDto(entity),
                explicitActorId: adminId,
                ct: ct);
        }

        if (tx != null) await tx.CommitAsync(ct);

        _logger?.LogInformation("Admin {AdminId} đã tạo chu kỳ bảng xếp hạng {CycleName} ({CycleId})", adminId, entity.Name, entity.Id);
        return ToCycleDto(entity);
    }

    public async Task<LeaderboardCycleDto> UpdateCycleAsync(Guid adminId, Guid cycleId, SaveLeaderboardCycleRequest request, CancellationToken ct = default)
    {
        var entity = await _db.LeaderboardCycles.FirstOrDefaultAsync(c => c.Id == cycleId, ct)
                     ?? throw new NotFoundException("Không tìm thấy chu kỳ bảng xếp hạng.");

        if (entity.Status is LeaderboardCycleStatus.Finalized or LeaderboardCycleStatus.Cancelled)
            throw GamErrors.Rule(Rule, "MSG-GEN-01", "Chu kỳ đã chốt hoặc đã hủy không thể chỉnh sửa.");

        var (name, period, startsAt, endsAt) = ValidateCycleRequest(request);
        var before = ToCycleDto(entity);
        var now = DateTime.UtcNow;

        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        if (request.IsActive)
        {
            await DeactivateOtherActiveCyclesAsync(cycleId, now, ct);
        }

        entity.Name = name;
        entity.PeriodType = period;
        entity.StartsAt = startsAt;
        entity.EndsAt = endsAt;
        entity.IsActive = request.IsActive;
        entity.Status = request.IsActive
            ? LeaderboardCycleStatus.Active
            : (startsAt > now ? LeaderboardCycleStatus.Upcoming : LeaderboardCycleStatus.Active);
        entity.Top1BonusPoints = request.Top1BonusPoints;
        entity.Top2BonusPoints = request.Top2BonusPoints;
        entity.Top3BonusPoints = request.Top3BonusPoints;
        entity.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        if (_auditLog != null)
        {
            await _auditLog.LogAsync(
                action: "LEADERBOARD_CYCLE_UPDATE",
                entityType: "LeaderboardCycle",
                entityId: entity.Id.ToString(),
                oldValues: before,
                newValues: ToCycleDto(entity),
                explicitActorId: adminId,
                ct: ct);
        }

        if (tx != null) await tx.CommitAsync(ct);
        return ToCycleDto(entity);
    }

    public async Task<LeaderboardCycleDto> ActivateCycleAsync(Guid adminId, Guid cycleId, CancellationToken ct = default)
    {
        var entity = await _db.LeaderboardCycles.FirstOrDefaultAsync(c => c.Id == cycleId, ct)
                     ?? throw new NotFoundException("Không tìm thấy chu kỳ bảng xếp hạng.");

        if (entity.Status is LeaderboardCycleStatus.Finalized or LeaderboardCycleStatus.Cancelled)
            throw GamErrors.Rule(Rule, "MSG-GEN-01", "Không thể kích hoạt lại chu kỳ đã chốt hoặc đã hủy.");

        var before = ToCycleDto(entity);
        var now = DateTime.UtcNow;

        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        await DeactivateOtherActiveCyclesAsync(cycleId, now, ct);

        entity.IsActive = true;
        entity.Status = LeaderboardCycleStatus.Active;
        entity.UpdatedAt = now;
        await _db.SaveChangesAsync(ct);

        if (_auditLog != null)
        {
            await _auditLog.LogAsync(
                action: "LEADERBOARD_CYCLE_ACTIVATE",
                entityType: "LeaderboardCycle",
                entityId: entity.Id.ToString(),
                oldValues: before,
                newValues: ToCycleDto(entity),
                explicitActorId: adminId,
                ct: ct);
        }

        if (tx != null) await tx.CommitAsync(ct);
        return ToCycleDto(entity);
    }

    public async Task<LeaderboardCycleDto> FinalizeCycleAsync(Guid adminId, Guid cycleId, CancellationToken ct = default)
    {
        var cycle = await _db.LeaderboardCycles.FirstOrDefaultAsync(c => c.Id == cycleId, ct)
                    ?? throw new NotFoundException("Không tìm thấy chu kỳ bảng xếp hạng.");

        if (cycle.Status is LeaderboardCycleStatus.Finalized or LeaderboardCycleStatus.Cancelled)
            throw GamErrors.Rule(Rule, "MSG-GEN-01", "Chu kỳ bảng xếp hạng này đã được chốt hoặc hủy trước đó.");

        var before = ToCycleDto(cycle);
        var now = DateTime.UtcNow;
        var ranked = await ComputeRankedEntriesAsync(cycle, viewerUserId: null, ct);

        var bonuses = new[] { cycle.Top1BonusPoints, cycle.Top2BonusPoints, cycle.Top3BonusPoints };
        var winners = new List<LeaderboardWinnerDto>();

        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        foreach (var entry in ranked.Where(r => r.TotalPoints > 0).Take(3))
        {
            var bonus = entry.Rank is >= 1 and <= 3 ? bonuses[entry.Rank - 1] : 0L;
            if (bonus > 0)
            {
                await _points.CreditAsync(
                    entry.UserId,
                    bonus,
                    PointActionType.LeaderboardBonus,
                    "LeaderboardCycle",
                    cycle.Id.ToString(),
                    $"LEADERBOARD:{cycle.Id}:RANK:{entry.Rank}:{entry.UserId}",
                    $"Thưởng Top {entry.Rank} bảng xếp hạng: {cycle.Name}",
                    ct);
            }

            winners.Add(new LeaderboardWinnerDto
            {
                Rank = entry.Rank,
                UserId = entry.UserId,
                FullName = entry.FullName,
                Area = entry.Area,
                Points = entry.TotalPoints,
                BonusPointsAwarded = bonus
            });
        }

        cycle.Status = LeaderboardCycleStatus.Finalized;
        cycle.IsActive = false;
        cycle.FinalizedByAdminId = adminId;
        cycle.FinalizedAt = now;
        cycle.UpdatedAt = now;
        cycle.WinnersSnapshotJson = JsonSerializer.Serialize(winners, Json);
        await _db.SaveChangesAsync(ct);

        if (_auditLog != null)
        {
            await _auditLog.LogAsync(
                action: "LEADERBOARD_CYCLE_FINALIZE",
                entityType: "LeaderboardCycle",
                entityId: cycle.Id.ToString(),
                oldValues: before,
                newValues: ToCycleDto(cycle),
                explicitActorId: adminId,
                ct: ct);
        }

        if (tx != null) await tx.CommitAsync(ct);

        _logger?.LogInformation("Admin {AdminId} đã chốt chu kỳ {CycleId} với {WinnerCount} người nhận thưởng.", adminId, cycleId, winners.Count);
        return ToCycleDto(cycle);
    }

    public async Task<LeaderboardViewDto> GetLeaderboardViewAsync(
        int top = 10,
        string? periodType = null,
        Guid? cycleId = null,
        Guid? viewerUserId = null,
        CancellationToken ct = default)
    {
        var cycle = await ResolveReferenceCycleAsync(periodType, cycleId, ct);
        if (cycle != null && cycle.Status == LeaderboardCycleStatus.Finalized && !string.IsNullOrWhiteSpace(cycle.WinnersSnapshotJson))
        {
            var winners = ParseWinners(cycle.WinnersSnapshotJson);
            if (winners.Count > 0 && cycleId.HasValue)
            {
                var snapshotItems = winners.Take(Math.Clamp(top, 1, 100)).Select((w, idx) => new LeaderboardEntryDto
                {
                    Rank = w.Rank > 0 ? w.Rank : idx + 1,
                    UserId = w.UserId,
                    FullName = w.FullName,
                    Area = w.Area,
                    TotalPoints = (int)Math.Min(int.MaxValue, w.Points),
                    PeriodPoints = w.Points,
                    IsCurrentUser = viewerUserId.HasValue && w.UserId == viewerUserId.Value
                }).ToList();

                return new LeaderboardViewDto
                {
                    ActiveCycle = ToCycleDto(cycle),
                    Items = snapshotItems,
                    MyEntry = viewerUserId.HasValue ? snapshotItems.FirstOrDefault(x => x.UserId == viewerUserId.Value) : null
                };
            }
        }

        var allRanked = await ComputeRankedEntriesAsync(cycle, viewerUserId, ct);
        var limit = Math.Clamp(top, 1, 100);

        return new LeaderboardViewDto
        {
            ActiveCycle = cycle != null ? ToCycleDto(cycle) : null,
            Items = allRanked.Take(limit).ToList(),
            MyEntry = viewerUserId.HasValue ? allRanked.FirstOrDefault(x => x.UserId == viewerUserId.Value) : null
        };
    }

    // ------------------------------------------------------------------
    private async Task<LeaderboardCycle?> ResolveReferenceCycleAsync(string? periodType, Guid? cycleId, CancellationToken ct)
    {
        if (cycleId.HasValue)
        {
            return await _db.LeaderboardCycles.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cycleId.Value, ct)
                   ?? throw new NotFoundException("Không tìm thấy chu kỳ bảng xếp hạng.");
        }

        var cycles = await _db.LeaderboardCycles
            .AsNoTracking()
            .OrderByDescending(c => c.IsActive)
            .ThenByDescending(c => c.StartsAt)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(periodType))
        {
            var parsed = ParsePeriodType(periodType);
            return cycles.FirstOrDefault(c => c.PeriodType == parsed && c.IsActive)
                   ?? cycles.FirstOrDefault(c => c.PeriodType == parsed && c.Status != LeaderboardCycleStatus.Cancelled)
                   ?? cycles.FirstOrDefault(c => c.PeriodType == parsed);
        }

        return cycles.FirstOrDefault(c => c.IsActive) ?? cycles.FirstOrDefault(c => c.Status != LeaderboardCycleStatus.Cancelled);
    }

    private async Task<List<LeaderboardEntryDto>> ComputeRankedEntriesAsync(
        LeaderboardCycle? cycle,
        Guid? viewerUserId,
        CancellationToken ct)
    {
        var citizens = await _db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.User && u.Status == UserStatus.Active)
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync(ct);

        if (citizens.Count == 0) return new List<LeaderboardEntryDto>();

        var userIds = citizens.Select(c => c.Id).ToList();

        var wallets = await _db.UserPointWallets
            .AsNoTracking()
            .Where(w => userIds.Contains(w.UserId))
            .ToDictionaryAsync(w => w.UserId, ct);

        var trees = await _db.VirtualTreeProgresses
            .AsNoTracking()
            .Where(t => userIds.Contains(t.UserId))
            .ToDictionaryAsync(t => t.UserId, ct);

        var ecoProfiles = await _db.UserEcoProfiles
            .AsNoTracking()
            .Where(e => userIds.Contains(e.UserId))
            .ToDictionaryAsync(e => e.UserId, ct);

        var completedHandovers = await _db.HandoverRequests
            .AsNoTracking()
            .Where(h => h.Status == HandoverStatus.Completed && userIds.Contains(h.UserId))
            .Select(h => new { h.Id, h.UserId, h.PickupAddress, h.UpdatedAt })
            .ToListAsync(ct);

        var handoverIds = completedHandovers.Select(h => h.Id).ToList();
        var receiptsByRequest = handoverIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : (await _db.HandoverReceipts
                .AsNoTracking()
                .Where(r => handoverIds.Contains(r.RequestId))
                .Select(r => new { r.RequestId, r.TotalWeightKg })
                .ToListAsync(ct))
                .GroupBy(r => r.RequestId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.TotalWeightKg));

        var recycledByUser = new Dictionary<Guid, decimal>();
        var areaByUser = new Dictionary<Guid, string>();
        foreach (var grp in completedHandovers.GroupBy(h => h.UserId))
        {
            recycledByUser[grp.Key] = grp.Sum(h => receiptsByRequest.GetValueOrDefault(h.Id, 0m));
            var latestAddress = grp.OrderByDescending(h => h.UpdatedAt).Select(h => h.PickupAddress).FirstOrDefault();
            var formatted = FormatArea(latestAddress);
            if (!string.IsNullOrEmpty(formatted)) areaByUser[grp.Key] = formatted;
        }

        // Fall back to any HandoverRequest address if user has no Completed order yet
        var usersWithoutArea = userIds.Where(id => !areaByUser.ContainsKey(id)).ToList();
        if (usersWithoutArea.Count > 0)
        {
            var fallbackAddresses = await _db.HandoverRequests
                .AsNoTracking()
                .Where(h => usersWithoutArea.Contains(h.UserId))
                .Select(h => new { h.UserId, h.PickupAddress, h.CreatedAt })
                .ToListAsync(ct);

            foreach (var grp in fallbackAddresses.GroupBy(h => h.UserId))
            {
                var addr = grp.OrderByDescending(x => x.CreatedAt).Select(x => x.PickupAddress).FirstOrDefault();
                var formatted = FormatArea(addr);
                if (!string.IsNullOrEmpty(formatted)) areaByUser[grp.Key] = formatted;
            }
        }

        var periodEarnedByUser = new Dictionary<Guid, long>();
        if (cycle != null)
        {
            var periodTx = await _db.PointTransactions
                .AsNoTracking()
                .Where(t => userIds.Contains(t.UserId)
                            && t.Points > 0
                            && t.ActionType != PointActionType.LeaderboardBonus
                            && t.CreatedAt >= cycle.StartsAt
                            && t.CreatedAt <= cycle.EndsAt)
                .Select(t => new { t.UserId, t.Points })
                .ToListAsync(ct);

            foreach (var grp in periodTx.GroupBy(t => t.UserId))
            {
                periodEarnedByUser[grp.Key] = grp.Sum(x => x.Points);
            }
        }

        var hasPeriodActivity = periodEarnedByUser.Values.Any(v => v > 0);

        var candidates = new List<(
            Guid UserId,
            string FullName,
            string Area,
            long Score,
            long LifetimeEarned,
            long PeriodEarned,
            long Spendable,
            int TreeLevel,
            decimal RecycledKg)>();

        foreach (var c in citizens)
        {
            wallets.TryGetValue(c.Id, out var wallet);
            trees.TryGetValue(c.Id, out var tree);
            ecoProfiles.TryGetValue(c.Id, out var eco);

            var lifetimeEarned = Math.Max(wallet?.TotalEarned ?? 0L, (long)(eco?.TotalLifetimePoints ?? 0));
            var periodEarned = periodEarnedByUser.GetValueOrDefault(c.Id, 0L);
            var score = hasPeriodActivity ? periodEarned : lifetimeEarned;

            if (score <= 0 && (!viewerUserId.HasValue || c.Id != viewerUserId.Value))
                continue;

            var spendable = wallet?.CurrentBalance ?? eco?.CurrentGreenPoints ?? 0L;
            var level = Math.Max(tree?.TreeLevel ?? 1, eco?.TreeLevel ?? 1);
            var recycledKg = Math.Max(recycledByUser.GetValueOrDefault(c.Id, 0m), eco?.TotalRecycledKg ?? 0m);
            var area = areaByUser.GetValueOrDefault(c.Id, "Cộng đồng Xanh EcoLink");

            candidates.Add((c.Id, c.FullName, area, score, lifetimeEarned, hasPeriodActivity ? periodEarned : lifetimeEarned, spendable, level, recycledKg));
        }

        var sorted = candidates
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.LifetimeEarned)
            .ThenByDescending(x => x.RecycledKg)
            .ThenBy(x => x.FullName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var result = new List<LeaderboardEntryDto>(sorted.Count);
        for (var i = 0; i < sorted.Count; i++)
        {
            var cur = sorted[i];
            var diffAbove = i == 0 ? 0L : Math.Max(0L, sorted[i - 1].Score - cur.Score);

            result.Add(new LeaderboardEntryDto
            {
                Rank = i + 1,
                UserId = cur.UserId,
                FullName = cur.FullName,
                Area = cur.Area,
                TotalPoints = (int)Math.Min(int.MaxValue, cur.Score),
                PeriodPoints = cur.PeriodEarned,
                SpendablePoints = cur.Spendable,
                TreeLevel = cur.TreeLevel,
                TotalRecycledKg = cur.RecycledKg,
                IsCurrentUser = viewerUserId.HasValue && cur.UserId == viewerUserId.Value,
                PointsNeededForNextRank = diffAbove
            });
        }

        return result;
    }

    private async Task DeactivateOtherActiveCyclesAsync(Guid? excludeId, DateTime now, CancellationToken ct)
    {
        var activeCycles = await _db.LeaderboardCycles
            .Where(c => c.IsActive && (!excludeId.HasValue || c.Id != excludeId.Value))
            .ToListAsync(ct);

        if (activeCycles.Count == 0) return;

        foreach (var c in activeCycles)
        {
            c.IsActive = false;
            c.UpdatedAt = now;
        }

        // Lưu bước tắt chu kỳ cũ trước khi bật chu kỳ mới để bảo toàn partial unique index uq_leaderboard_cycles_single_active
        await _db.SaveChangesAsync(ct);
    }

    private static (string Name, LeaderboardPeriodType Period, DateTime StartsAt, DateTime EndsAt) ValidateCycleRequest(SaveLeaderboardCycleRequest request)
    {
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
            throw GamErrors.Rule(Rule, "MSG-AUTH-02", "Vui lòng nhập tên chu kỳ bảng xếp hạng.");
        if (name.Length > MaxNameLength)
            throw GamErrors.Rule(Rule, "MSG-NTF-03", $"Tên chu kỳ tối đa {MaxNameLength} ký tự.");

        var period = ParsePeriodType(request.PeriodType);

        var startsAt = request.StartsAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.StartsAt, DateTimeKind.Utc)
            : request.StartsAt.ToUniversalTime();
        var endsAt = request.EndsAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.EndsAt, DateTimeKind.Utc)
            : request.EndsAt.ToUniversalTime();

        if (endsAt <= startsAt)
            throw GamErrors.Rule(Rule, "MSG-AUTH-02", "Thời gian kết thúc chu kỳ phải lớn hơn thời gian bắt đầu.");

        if (request.Top1BonusPoints < 0 || request.Top2BonusPoints < 0 || request.Top3BonusPoints < 0)
            throw GamErrors.Rule(Rule, "MSG-AUTH-02", "Điểm thưởng Top 1/2/3 không được là số âm.");

        if (request.Top1BonusPoints < request.Top2BonusPoints || request.Top2BonusPoints < request.Top3BonusPoints)
            throw GamErrors.Rule(Rule, "MSG-AUTH-02", "Điểm thưởng phải theo thứ tự giảm dần (Top 1 >= Top 2 >= Top 3).");

        return (name, period, startsAt, endsAt);
    }

    private static LeaderboardPeriodType ParsePeriodType(string? raw)
    {
        var key = (raw ?? string.Empty).Trim().ToLowerInvariant();
        return key switch
        {
            "weekly" or "week" or "tuần" => LeaderboardPeriodType.Weekly,
            "monthly" or "month" or "tháng" => LeaderboardPeriodType.Monthly,
            "quarterly" or "quarter" or "quý" => LeaderboardPeriodType.Quarterly,
            _ => throw GamErrors.Rule(Rule, "MSG-AUTH-02", "Chu kỳ bảng xếp hạng chỉ hỗ trợ: Weekly, Monthly hoặc Quarterly.")
        };
    }

    private static string FormatArea(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return string.Empty;
        var parts = address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length >= 2) return $"{parts[^2]}, {parts[^1]}";
        return parts[0];
    }

    private static List<LeaderboardWinnerDto> ParseWinners(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<LeaderboardWinnerDto>();
        try { return JsonSerializer.Deserialize<List<LeaderboardWinnerDto>>(json, Json) ?? new List<LeaderboardWinnerDto>(); }
        catch (JsonException) { return new List<LeaderboardWinnerDto>(); }
    }

    internal static LeaderboardCycleDto ToCycleDto(LeaderboardCycle c) => new()
    {
        Id = c.Id,
        Name = c.Name,
        PeriodType = c.PeriodType.ToString(),
        StartsAt = c.StartsAt,
        EndsAt = c.EndsAt,
        IsActive = c.IsActive,
        Status = c.Status.ToString(),
        Top1BonusPoints = c.Top1BonusPoints,
        Top2BonusPoints = c.Top2BonusPoints,
        Top3BonusPoints = c.Top3BonusPoints,
        FinalizedAt = c.FinalizedAt,
        Winners = ParseWinners(c.WinnersSnapshotJson),
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };
}
