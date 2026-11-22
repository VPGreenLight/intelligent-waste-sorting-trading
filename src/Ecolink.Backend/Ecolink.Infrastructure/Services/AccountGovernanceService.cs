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
/// Hiện thực hóa nghiệp vụ Tra cứu &amp; Kiểm duyệt tài khoản Người dân / Cơ sở thu mua (UC S-06, S-07).
/// Tuân thủ nghiêm ngặt BR-ADM-03, BR-ADM-04, BR-ADM-05.
/// </summary>
public class AccountGovernanceService : IAccountGovernanceService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly IAuthStateCacheInvalidator _authCacheInvalidator;
    private readonly IEmailService _emailService;
    private readonly ILogger<AccountGovernanceService> _logger;

    public AccountGovernanceService(
        ApplicationDbContext dbContext,
        IAuditLogService auditLogService,
        IAuthStateCacheInvalidator authCacheInvalidator,
        IEmailService emailService,
        ILogger<AccountGovernanceService> logger)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _authCacheInvalidator = authCacheInvalidator;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<PagedResult<ManagedAccountSummaryDto>> GetAccountsAsync(
        AccountGovernanceFilterRequest filter,
        CancellationToken ct = default)
    {
        // Chỉ lấy tài khoản bên ngoài (User / Citizen và Recycler), loại trừ Staff & Admin (BR-ADM-03)
        var query = _dbContext.Users
            .Include(u => u.RecyclerProfile)
            .AsNoTracking()
            .Where(u => u.Role == UserRole.User || u.Role == UserRole.Recycler);

        if (!string.IsNullOrWhiteSpace(filter.Role))
        {
            var r = filter.Role.Trim().ToLowerInvariant();
            if (r is "user" or "citizen")
            {
                query = query.Where(u => u.Role == UserRole.User);
            }
            else if (r is "recycler" or "collector")
            {
                query = query.Where(u => u.Role == UserRole.Recycler);
            }
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<UserStatus>(filter.Status.Trim(), true, out var parsedStatus))
        {
            query = query.Where(u => u.Status == parsedStatus);
        }

        if (filter.MinWarnings.HasValue)
        {
            query = query.Where(u => u.WarningCount >= filter.MinWarnings.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(u =>
                u.FullName.ToLower().Contains(kw) ||
                u.Email.ToLower().Contains(kw) ||
                u.Username.ToLower().Contains(kw) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(kw)) ||
                (u.RecyclerProfile != null && (
                    u.RecyclerProfile.FacilityName.ToLower().Contains(kw) ||
                    u.RecyclerProfile.BusinessAddress.ToLower().Contains(kw))));
        }

        var totalRecords = await query.CountAsync(ct);
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var userIds = users.Select(u => u.Id).ToList();

        // Đếm số đơn bàn giao hoàn tất (Completed) cho tập user trên trang hiện tại
        var citizenCompletedCounts = await _dbContext.HandoverRequests
            .AsNoTracking()
            .Where(h => userIds.Contains(h.UserId) && h.Status == HandoverStatus.Completed)
            .GroupBy(h => h.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

        var recyclerCompletedCounts = await _dbContext.HandoverRequests
            .AsNoTracking()
            .Where(h => userIds.Contains(h.RecyclerId) && h.Status == HandoverStatus.Completed)
            .GroupBy(h => h.RecyclerId)
            .Select(g => new { RecyclerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RecyclerId, x => x.Count, ct);

        var items = users.Select(u =>
        {
            var deals = u.Role == UserRole.Recycler
                ? recyclerCompletedCounts.GetValueOrDefault(u.Id, 0)
                : citizenCompletedCounts.GetValueOrDefault(u.Id, 0);

            return MapSummary(u, deals);
        }).ToList();

        return new PagedResult<ManagedAccountSummaryDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords
        };
    }

    public async Task<ManagedAccountDetailDto> GetAccountDetailAsync(
        Guid targetUserId,
        CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.RecyclerProfile)
            .Include(u => u.EcoProfile)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == targetUserId, ct);

        if (user == null || user.Role is UserRole.Staff or UserRole.Admin)
        {
            throw new NotFoundException("Không tìm thấy tài khoản người dùng hoặc cơ sở thu mua yêu cầu.");
        }

        var isRecycler = user.Role == UserRole.Recycler;
        var handoverQuery = _dbContext.HandoverRequests
            .AsNoTracking()
            .Where(h => isRecycler ? h.RecyclerId == targetUserId : h.UserId == targetUserId);

        var totalRequests = await handoverQuery.CountAsync(ct);
        var completedDeals = await handoverQuery.CountAsync(h => h.Status == HandoverStatus.Completed, ct);
        var cancelledLate = await handoverQuery.CountAsync(h => h.IsLateCancellation, ct);

        var recycledKg = await _dbContext.HandoverReceipts
            .AsNoTracking()
            .Where(r => isRecycler ? r.RecyclerId == targetUserId : r.UserId == targetUserId)
            .SumAsync(r => (decimal?)r.TotalWeightKg, ct) ?? 0m;

        var history = await _dbContext.AccountModerationLogs
            .Include(m => m.PerformedByUser)
            .AsNoTracking()
            .Where(m => m.TargetUserId == targetUserId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(20)
            .Select(m => new AccountModerationLogDto
            {
                Id = m.Id,
                ActionType = m.ActionType,
                PreviousStatus = m.PreviousStatus,
                NewStatus = m.NewStatus,
                Reason = m.Reason,
                RelatedComplaintId = m.RelatedComplaintId,
                RevokedTokensCount = m.RevokedTokensCount,
                PerformedBy = m.PerformedBy,
                PerformedByName = m.PerformedByUser.FullName,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);

        var summary = MapSummary(user, completedDeals);

        return new ManagedAccountDetailDto
        {
            UserId = summary.UserId,
            Username = summary.Username,
            FullName = summary.FullName,
            Email = summary.Email,
            PhoneNumber = summary.PhoneNumber,
            Role = summary.Role,
            Area = summary.Area,
            Status = summary.Status,
            VerificationStatus = summary.VerificationStatus,
            CompletedDealsCount = summary.CompletedDealsCount,
            WarningCount = summary.WarningCount,
            ModerationReason = summary.ModerationReason,
            ModeratedAt = summary.ModeratedAt,
            CreatedAt = summary.CreatedAt,
            ActivityStats = new ManagedAccountActivityStatsDto
            {
                TotalHandoverRequests = totalRequests,
                CompletedDealsCount = completedDeals,
                CancelledLateCount = cancelledLate,
                TotalRecycledKg = recycledKg,
                GreenPoints = user.EcoProfile?.CurrentGreenPoints ?? 0,
                TreeLevel = user.EcoProfile?.TreeLevel ?? 1
            },
            ModerationHistory = history
        };
    }

    public async Task<ManagedAccountSummaryDto> LockOrUnlockAccountAsync(
        Guid actorUserId,
        Guid targetUserId,
        LockOrUnlockAccountRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
        {
            throw new BusinessRuleException("BR-ADM-03", "MSG-GEN-01",
                "Mọi thao tác khóa hoặc mở khóa tài khoản bắt buộc phải ghi rõ lý do tối thiểu 10 ký tự (BR-ADM-03).");
        }

        if (actorUserId == targetUserId)
        {
            throw new ForbiddenAccessException("BR-ADM-03", "MSG-ADM-04",
                "Bạn không thể tự khóa hoặc thay đổi trạng thái tài khoản của chính mình.");
        }

        var user = await _dbContext.Users
            .Include(u => u.RecyclerProfile)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, ct);

        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy tài khoản cần xử lý.");
        }

        // BR-ADM-03: Chặn leo thang đặc quyền — endpoint của Staff không được phép tác động Staff / Admin
        if (user.Role is UserRole.Staff or UserRole.Admin)
        {
            throw new ForbiddenAccessException("BR-ADM-03", "MSG-ADM-04",
                "Bạn không có quyền khóa hoặc chỉnh sửa tài khoản Nhân viên / Quản trị viên nội bộ.");
        }

        if (user.Status == UserStatus.Deactivated && !request.IsLocked)
        {
            // Cho phép kích hoạt lại nếu có lý do kiểm toán rõ ràng, hoặc chuyển về Active
        }

        var prevStatus = user.Status;
        var prevWarns = user.WarningCount;
        var cleanReason = request.Reason.Trim();
        var now = DateTime.UtcNow;

        var newStatus = request.IsLocked ? UserStatus.Suspended : UserStatus.Active;
        user.Status = newStatus;
        user.ModerationReason = cleanReason;
        user.ModeratedAt = now;
        user.ModeratedBy = actorUserId;

        if (request.IsLocked && request.IncrementWarning)
        {
            user.WarningCount += 1;
        }

        if (!request.IsLocked)
        {
            user.FailedLoginAttempts = 0;
            user.LockoutUntil = null;
        }

        int revokedCount = 0;
        if (request.IsLocked)
        {
            var activeTokens = await _dbContext.RefreshTokens
                .Where(t => t.UserId == targetUserId && !t.IsRevoked)
                .ToListAsync(ct);

            foreach (var token in activeTokens)
            {
                token.IsRevoked = true;
            }
            revokedCount = activeTokens.Count;

            if (user.RecyclerProfile != null)
            {
                user.RecyclerProfile.ActiveStatus = RecyclerActiveStatus.Offline;
            }
        }

        var actionType = request.IsLocked ? "LOCK" : "UNLOCK";
        _dbContext.AccountModerationLogs.Add(new AccountModerationLog
        {
            Id = Guid.NewGuid(),
            TargetUserId = user.Id,
            PerformedBy = actorUserId,
            ActionType = actionType,
            PreviousStatus = prevStatus.ToString(),
            NewStatus = newStatus.ToString(),
            Reason = cleanReason,
            RelatedComplaintId = request.RelatedComplaintId,
            RevokedTokensCount = revokedCount,
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(ct);

        // Xóa ngay cache phiên xác thực để chặn cả JWT Access Token còn hạn
        _authCacheInvalidator.InvalidateUserAuthState(user.Id);

        await _auditLogService.LogAsync(
            action: request.IsLocked ? "LOCK_ACCOUNT" : "UNLOCK_ACCOUNT",
            entityType: "User",
            entityId: user.Id.ToString(),
            oldValues: new { status = prevStatus.ToString(), warningCount = prevWarns },
            newValues: new { status = newStatus.ToString(), warningCount = user.WarningCount, revokedTokensCount = revokedCount },
            reason: cleanReason,
            explicitActorId: actorUserId,
            ct: ct);

        _logger.LogInformation("Actor {ActorId} đã {Action} tài khoản {TargetId} ({Email}). Lý do: {Reason}",
            actorUserId, actionType, user.Id, user.Email, cleanReason);

        await TryNotifyAccountStatusChangedAsync(user, cleanReason, ct);

        var completedDeals = await _dbContext.HandoverRequests
            .AsNoTracking()
            .CountAsync(h => (user.Role == UserRole.Recycler ? h.RecyclerId == user.Id : h.UserId == user.Id)
                             && h.Status == HandoverStatus.Completed, ct);

        return MapSummary(user, completedDeals);
    }

    public async Task<ManagedAccountSummaryDto> DeactivateAccountAsync(
        Guid actorUserId,
        Guid targetUserId,
        DeactivateAccountRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
        {
            throw new BusinessRuleException("BR-ADM-03", "MSG-GEN-01",
                "Bắt buộc nhập biên bản lý do tối thiểu 10 ký tự khi vô hiệu hóa tài khoản (BR-ADM-03, BR-ADM-04).");
        }

        if (actorUserId == targetUserId)
        {
            throw new ForbiddenAccessException("BR-ADM-03", "MSG-ADM-04",
                "Bạn không thể tự vô hiệu hóa tài khoản của chính mình.");
        }

        var user = await _dbContext.Users
            .Include(u => u.RecyclerProfile)
            .FirstOrDefaultAsync(u => u.Id == targetUserId, ct);

        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy tài khoản cần vô hiệu hóa.");
        }

        if (user.Role is UserRole.Staff or UserRole.Admin)
        {
            throw new ForbiddenAccessException("BR-ADM-03", "MSG-ADM-04",
                "Bạn không có quyền vô hiệu hóa tài khoản Nhân viên / Quản trị viên nội bộ.");
        }

        var prevStatus = user.Status;
        var cleanReason = request.Reason.Trim();
        var now = DateTime.UtcNow;

        // BR-ADM-04: Vô hiệu hóa mềm (Soft-Deactivate), tuyệt đối không xóa dòng vật lý để bảo toàn FK của biên nhận lịch sử
        user.Status = UserStatus.Deactivated;
        user.ModerationReason = cleanReason;
        user.ModeratedAt = now;
        user.ModeratedBy = actorUserId;
        user.WarningCount += 1;

        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == targetUserId && !t.IsRevoked)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
        }

        if (user.RecyclerProfile != null)
        {
            user.RecyclerProfile.ActiveStatus = RecyclerActiveStatus.Offline;
        }

        _dbContext.AccountModerationLogs.Add(new AccountModerationLog
        {
            Id = Guid.NewGuid(),
            TargetUserId = user.Id,
            PerformedBy = actorUserId,
            ActionType = "DEACTIVATE",
            PreviousStatus = prevStatus.ToString(),
            NewStatus = UserStatus.Deactivated.ToString(),
            Reason = cleanReason,
            RevokedTokensCount = activeTokens.Count,
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(ct);
        _authCacheInvalidator.InvalidateUserAuthState(user.Id);

        var completedDeals = await _dbContext.HandoverRequests
            .AsNoTracking()
            .CountAsync(h => (user.Role == UserRole.Recycler ? h.RecyclerId == user.Id : h.UserId == user.Id)
                             && h.Status == HandoverStatus.Completed, ct);

        await _auditLogService.LogAsync(
            action: "DEACTIVATE_ACCOUNT",
            entityType: "User",
            entityId: user.Id.ToString(),
            oldValues: new { status = prevStatus.ToString() },
            newValues: new { status = UserStatus.Deactivated.ToString(), preservedCompletedDeals = completedDeals },
            reason: cleanReason,
            explicitActorId: actorUserId,
            ct: ct);

        await TryNotifyAccountStatusChangedAsync(user, cleanReason, ct);

        return MapSummary(user, completedDeals);
    }

    private async Task TryNotifyAccountStatusChangedAsync(User user, string reason, CancellationToken ct)
    {
        try
        {
            var subject = "[EcoLink] Thông báo cập nhật trạng thái tài khoản";
            var html = $@"<p>Xin chào <strong>{user.FullName}</strong>,</p>
                          <p>Trạng thái tài khoản EcoLink của bạn đã được cập nhật sang: <strong>{user.Status}</strong>.</p>
                          <p><strong>Lý do ghi nhận:</strong> {reason}</p>";
            await _emailService.SendEmailAsync(user.Email, user.FullName, subject, html, ct);
        }
        catch (Exception ex)
        {
            // BR-NTF-06: Không làm gãy luồng nghiệp vụ nếu dịch vụ Email gặp sự cố
            _logger.LogWarning(ex, "Không gửi được email thông báo trạng thái tài khoản tới {Email}", user.Email);
        }
    }

    private static ManagedAccountSummaryDto MapSummary(User user, int completedDealsCount) =>
        new()
        {
            UserId = user.Id,
            Username = user.Username,
            FullName = user.RecyclerProfile != null && !string.IsNullOrWhiteSpace(user.RecyclerProfile.FacilityName)
                ? user.RecyclerProfile.FacilityName
                : user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber ?? user.RecyclerProfile?.ContactPhone,
            Role = user.Role == UserRole.Recycler ? "Recycler" : "User",
            Area = user.RecyclerProfile?.BusinessAddress ?? "Việt Nam",
            Status = user.Status.ToString(),
            VerificationStatus = user.RecyclerProfile?.VerificationStatus.ToString(),
            CompletedDealsCount = completedDealsCount,
            WarningCount = user.WarningCount,
            ModerationReason = user.ModerationReason,
            ModeratedAt = user.ModeratedAt,
            CreatedAt = user.CreatedAt
        };
}
