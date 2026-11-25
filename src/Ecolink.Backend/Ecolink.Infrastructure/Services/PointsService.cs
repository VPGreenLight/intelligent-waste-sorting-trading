using System.Collections.Concurrent;
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
/// Ví điểm + sổ cái điểm (BR-GAM-01, BR-GAM-03, BR-GAM-08). Số dư không bao giờ âm: trừ điểm dùng UPDATE có điều kiện
/// (balance >= points) và có CHECK constraint ở DB. Mọi biến động có idempotency_key duy nhất để không xử lý trùng.
/// </summary>
public class PointsService : IPointsService
{
    private static readonly ConcurrentDictionary<Guid, byte> AwardedFertilizerReceipts = new();

    private readonly ApplicationDbContext _db;
    private readonly IGamificationConfigService _config;
    private readonly ILogger<PointsService> _logger;

    public PointsService(ApplicationDbContext db, IGamificationConfigService config, ILogger<PointsService> logger)
    {
        _db = db;
        _config = config;
        _logger = logger;
    }

    public async Task<WalletDto> GetWalletAsync(Guid userId, CancellationToken ct = default)
    {
        await EnsureWalletAsync(userId, ct);
        var w = await _db.UserPointWallets.AsNoTracking().FirstAsync(x => x.UserId == userId, ct);
        return new WalletDto { Balance = w.CurrentBalance, TotalEarned = w.TotalEarned, TotalSpent = w.TotalSpent, UpdatedAt = w.UpdatedAt };
    }

    public async Task<GamPagedResult<PointTransactionDto>> GetTransactionsAsync(Guid userId, DateTime? from, DateTime? to,
        string? actionType, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.PointTransactions.AsNoTracking().Where(t => t.UserId == userId);
        if (from.HasValue) q = q.Where(t => t.CreatedAt >= from.Value.ToUniversalTime());
        if (to.HasValue) q = q.Where(t => t.CreatedAt <= to.Value.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            var key = actionType.Trim().Replace("_", string.Empty);
            if (!Enum.TryParse<PointActionType>(key, true, out var parsed) || !Enum.IsDefined(parsed))
                throw GamErrors.Rule("BR-GAM-01", "MSG-GEN-01", "Loại giao dịch điểm không hợp lệ.");
            q = q.Where(t => t.ActionType == parsed);
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return new GamPagedResult<PointTransactionDto>
        {
            Page = page, PageSize = pageSize, TotalCount = total,
            Items = rows.Select(t => new PointTransactionDto
            {
                Id = t.Id, Points = t.Points, ActionType = ActionText(t.ActionType), SourceType = t.SourceType,
                SourceId = t.SourceId, Description = t.Description, CreatedAt = t.CreatedAt
            }).ToList()
        };
    }

    public Task<long> CreditAsync(Guid userId, long points, PointActionType action, string? sourceType, string? sourceId,
        string idempotencyKey, string? description, CancellationToken ct = default)
    {
        if (points <= 0) return Task.FromResult(0L);
        return InTxAsync(async () =>
        {
            if (await _db.PointTransactions.AnyAsync(t => t.IdempotencyKey == idempotencyKey, ct)) return 0L;

            var walletId = await EnsureWalletAsync(userId, ct);
            _db.PointTransactions.Add(new PointTransaction
            {
                WalletId = walletId, UserId = userId, Points = points, ActionType = action, SourceType = sourceType,
                SourceId = sourceId, IdempotencyKey = idempotencyKey, Description = description
            });
            try
            {
                await _db.SaveChangesAsync(ct);   // đụng unique idempotency_key nếu hai tiến trình cùng cộng -> bỏ qua
            }
            catch (DbUpdateException ex) when (GamErrors.IsUniqueViolation(ex, "ux_point_transactions_idem"))
            {
                _db.ChangeTracker.Clear();
                return 0L;
            }

            var now = DateTime.UtcNow;
            if (_db.Database.IsRelational())
            {
                await _db.UserPointWallets.Where(w => w.Id == walletId).ExecuteUpdateAsync(s => s
                    .SetProperty(w => w.CurrentBalance, w => w.CurrentBalance + points)
                    .SetProperty(w => w.TotalEarned, w => w.TotalEarned + points)
                    .SetProperty(w => w.UpdatedAt, now), ct);
            }
            else
            {
                var wallet = await _db.UserPointWallets.FirstAsync(w => w.Id == walletId, ct);
                wallet.CurrentBalance += points;
                wallet.TotalEarned += points;
                wallet.UpdatedAt = now;
                await _db.SaveChangesAsync(ct);
            }
            return points;
        }, ct);
    }

    public Task<bool> TryDebitAsync(Guid userId, long points, PointActionType action, string? sourceType, string? sourceId,
        string idempotencyKey, string? description, CancellationToken ct = default)
    {
        if (points <= 0) return Task.FromResult(true);
        return InTxAsync(async () =>
        {
            var walletId = await EnsureWalletAsync(userId, ct);
            var now = DateTime.UtcNow;
            if (_db.Database.IsRelational())
            {
                var affected = await _db.UserPointWallets
                    .Where(w => w.Id == walletId && w.CurrentBalance >= points)      // BR-GAM-03: không âm
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(w => w.CurrentBalance, w => w.CurrentBalance - points)
                        .SetProperty(w => w.TotalSpent, w => w.TotalSpent + points)
                        .SetProperty(w => w.UpdatedAt, now), ct);
                if (affected == 0) return false;
            }
            else
            {
                var wallet = await _db.UserPointWallets.FirstOrDefaultAsync(w => w.Id == walletId && w.CurrentBalance >= points, ct);
                if (wallet == null) return false;
                wallet.CurrentBalance -= points;
                wallet.TotalSpent += points;
                wallet.UpdatedAt = now;
            }

            _db.PointTransactions.Add(new PointTransaction
            {
                WalletId = walletId, UserId = userId, Points = -points, ActionType = action, SourceType = sourceType,
                SourceId = sourceId, IdempotencyKey = idempotencyKey, Description = description
            });
            await _db.SaveChangesAsync(ct);
            return true;
        }, ct);
    }

    public async Task<long> AwardHandoverPointsAsync(Guid receiptId, CancellationToken ct = default)
    {
        var receipt = await _db.HandoverReceipts.AsNoTracking().FirstOrDefaultAsync(r => r.Id == receiptId, ct);
        if (receipt == null) return 0;

        // Chỉ xử lý cho giao dịch đã Completed (không xử lý cho đơn bị hủy / tranh chấp)
        var status = await _db.HandoverRequests.AsNoTracking().Where(h => h.Id == receipt.RequestId)
            .Select(h => h.Status).FirstOrDefaultAsync(ct);
        if (status != HandoverStatus.Completed) return 0;

        // Không cộng Điểm Xanh khi hoàn tất giao dịch bàn giao; chỉ tặng +2 túi phân bón Cây ảo (idempotent theo receipt.Id).
        if (AwardedFertilizerReceipts.TryAdd(receipt.Id, 1))
        {
            await TreeCareShared.AwardHandoverFertilizerAsync(_db, receipt.UserId, 2, ct);
        }

        if (receipt.PointsAwarded != 0)
        {
            if (_db.Database.IsRelational())
            {
                await _db.HandoverReceipts.Where(r => r.Id == receiptId)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.PointsAwarded, 0L), ct);
            }
            else
            {
                var tracked = await _db.HandoverReceipts.FirstOrDefaultAsync(r => r.Id == receiptId, ct);
                if (tracked != null)
                {
                    tracked.PointsAwarded = 0;
                    await _db.SaveChangesAsync(ct);
                }
            }
        }

        _logger.LogInformation("Hoàn tất bàn giao biên nhận {Receipt}: không cộng Điểm Xanh, đã ghi nhận phân bón Cây ảo.", receipt.ReceiptCode);
        return 0L;
    }

    // ------------------------------------------------------------------
    /// <summary>Tạo ví nếu chưa có (race-safe) và trả wallet_id.</summary>
    private async Task<Guid> EnsureWalletAsync(Guid userId, CancellationToken ct)
    {
        var existing = await _db.UserPointWallets.AsNoTracking().Where(w => w.UserId == userId)
            .Select(w => (Guid?)w.Id).FirstOrDefaultAsync(ct);
        if (existing.HasValue) return existing.Value;

        var newId = Guid.NewGuid();
        if (!_db.Database.IsRelational())
        {
            var wallet = new UserPointWallet
            {
                Id = newId,
                UserId = userId,
                CurrentBalance = 0,
                TotalEarned = 0,
                TotalSpent = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _db.UserPointWallets.Add(wallet);
            await _db.SaveChangesAsync(ct);
            return wallet.Id;
        }

        await _db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO user_point_wallets (wallet_id, user_id, current_balance, total_earned, total_spent, updated_at)
            VALUES ({newId}, {userId}, 0, 0, 0, NOW()) ON CONFLICT (user_id) DO NOTHING", ct);
        return await _db.UserPointWallets.AsNoTracking().Where(w => w.UserId == userId).Select(w => w.Id).FirstAsync(ct);
    }

    /// <summary>Dùng transaction sẵn có của người gọi; nếu chưa có thì tự mở một transaction.</summary>
    private async Task<T> InTxAsync<T>(Func<Task<T>> body, CancellationToken ct)
    {
        if (_db.Database.CurrentTransaction != null) return await body();
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        var result = await body();
        await tx.CommitAsync(ct);
        return result;
    }

    internal static string ActionText(PointActionType a) => a switch
    {
        PointActionType.HandoverReward => "Handover_Reward",
        PointActionType.DailyCheckin => "Daily_Checkin",
        PointActionType.RedeemGift => "Redeem_Gift",
        PointActionType.TreeCare => "Tree_Care",
        PointActionType.LeaderboardBonus => "Leaderboard_Bonus",
        _ => "Adjustment"
    };
}
