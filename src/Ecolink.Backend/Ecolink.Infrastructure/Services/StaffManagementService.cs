using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Constants;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Hiện thực hóa nghiệp vụ Quản lý Nhân sự Vận hành (UC A-03) và Phân quyền RBAC hạt mịn (UC A-04).
/// Tuân thủ BR-ADM-01, BR-ADM-02, BR-ADM-03, BR-ADM-05.
/// </summary>
public class StaffManagementService : IStaffManagementService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;
    private readonly IAuthStateCacheInvalidator _authCacheInvalidator;
    private readonly ILogger<StaffManagementService> _logger;

    public StaffManagementService(
        ApplicationDbContext dbContext,
        IAuditLogService auditLogService,
        IAuthStateCacheInvalidator authCacheInvalidator,
        ILogger<StaffManagementService> logger)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
        _authCacheInvalidator = authCacheInvalidator;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StaffAccountDto>> GetStaffAccountsAsync(
        StaffAccountFilterRequest filter,
        CancellationToken ct = default)
    {
        await EnsureOrphanStaffProfilesInitializedAsync(ct);

        var query = _dbContext.StaffProfiles
            .Include(s => s.User)
            .Include(s => s.Permissions)
            .AsNoTracking()
            .Where(s => s.User.Role == UserRole.Staff);

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<UserStatus>(filter.Status.Trim(), true, out var parsedStatus))
        {
            query = query.Where(s => s.User.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(filter.Department))
        {
            var dept = filter.Department.Trim().ToLower();
            query = query.Where(s => s.Department != null && s.Department.ToLower().Contains(dept));
        }

        if (!string.IsNullOrWhiteSpace(filter.PermissionCode))
        {
            var perm = StaffPermissions.Normalize(filter.PermissionCode);
            query = query.Where(s => s.Permissions.Any(p => p.PermissionCode == perm));
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(s =>
                s.StaffCode.ToLower().Contains(kw) ||
                s.User.FullName.ToLower().Contains(kw) ||
                s.User.Email.ToLower().Contains(kw) ||
                s.User.Username.ToLower().Contains(kw));
        }

        var list = await query
            .OrderBy(s => s.StaffCode)
            .ToListAsync(ct);

        return list.Select(MapStaffDto).ToList();
    }

    public async Task<StaffAccountDto> CreateStaffAccountAsync(
        Guid adminUserId,
        CreateStaffAccountRequest request,
        CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        if (await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail, ct))
        {
            throw new BusinessRuleException("BR-AUTH-01", "MSG-AUTH-01",
                "Email công việc này đã được sử dụng trên hệ thống.");
        }

        var baseUsername = string.IsNullOrWhiteSpace(request.Username)
            ? normalizedEmail.Split('@')[0]
            : request.Username.Trim();

        var finalUsername = baseUsername;
        if (await _dbContext.Users.AnyAsync(u => u.Username.ToLower() == finalUsername.ToLower(), ct))
        {
            if (!string.IsNullOrWhiteSpace(request.Username))
            {
                throw new BusinessRuleException("BR-AUTH-01", "MSG-AUTH-01",
                    "Tên đăng nhập nhân viên đã tồn tại. Vui lòng chọn tên đăng nhập khác.");
            }
            finalUsername = $"{baseUsername}_{Guid.NewGuid().ToString("N")[..4]}";
        }

        var staffCode = string.IsNullOrWhiteSpace(request.StaffCode)
            ? await GenerateNextStaffCodeAsync(ct)
            : request.StaffCode.Trim().ToUpperInvariant();

        if (await _dbContext.StaffProfiles.AnyAsync(s => s.StaffCode == staffCode, ct))
        {
            throw new BusinessRuleException("BR-ADM-01", "MSG-GEN-01",
                $"Mã nhân viên '{staffCode}' đã tồn tại.");
        }

        var rawPassword = string.IsNullOrWhiteSpace(request.InitialPassword)
            ? "Staff@2026!Eco"
            : request.InitialPassword.Trim();

        var normalizedPerms = (request.InitialPermissions ?? new List<string>())
            .Select(StaffPermissions.Normalize)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedPerms.Any(c => !StaffPermissions.IsValid(c)))
        {
            throw new BusinessRuleException("BR-ADM-02", "MSG-GEN-01",
                "Danh sách quyền khởi tạo chứa mã quyền không hợp lệ.");
        }

        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var user = new User
        {
            Id = userId,
            Username = finalUsername,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(rawPassword),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            Role = UserRole.Staff,
            Status = UserStatus.Active,
            IsGoogleAuth = false
        };

        var profile = new StaffProfile
        {
            UserId = userId,
            User = user,
            StaffCode = staffCode,
            Department = string.IsNullOrWhiteSpace(request.Department) ? "Vận hành" : request.Department.Trim(),
            Position = string.IsNullOrWhiteSpace(request.Position) ? "Chuyên viên vận hành" : request.Position.Trim(),
            Status = "ACTIVE",
            Permissions = normalizedPerms.Select(code => new StaffPermissionAssignment
            {
                Id = Guid.NewGuid(),
                StaffId = userId,
                PermissionCode = code,
                GrantedBy = adminUserId,
                GrantedAt = now
            }).ToList()
        };

        _dbContext.Users.Add(user);
        _dbContext.StaffProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            action: "CREATE_STAFF",
            entityType: "StaffProfile",
            entityId: userId.ToString(),
            oldValues: null,
            newValues: new
            {
                staffCode = profile.StaffCode,
                email = user.Email,
                department = profile.Department,
                position = profile.Position,
                permissions = normalizedPerms
            },
            reason: "Khởi tạo tài khoản Nhân viên vận hành nội bộ (BR-ADM-01)",
            explicitActorId: adminUserId,
            ct: ct);

        _logger.LogInformation("Admin {AdminId} đã tạo tài khoản Staff {StaffCode} ({Email})",
            adminUserId, profile.StaffCode, user.Email);

        return MapStaffDto(profile);
    }

    public async Task<StaffAccountDto> UpdateStaffAccountAsync(
        Guid adminUserId,
        Guid staffId,
        UpdateStaffAccountRequest request,
        CancellationToken ct = default)
    {
        var profile = await _dbContext.StaffProfiles
            .Include(s => s.User)
            .Include(s => s.Permissions)
            .FirstOrDefaultAsync(s => s.UserId == staffId && s.User.Role == UserRole.Staff, ct);

        if (profile == null)
        {
            throw new NotFoundException("Không tìm thấy hồ sơ nhân viên vận hành.");
        }

        var oldSnapshot = new
        {
            fullName = profile.User.FullName,
            phoneNumber = profile.User.PhoneNumber,
            department = profile.Department,
            position = profile.Position
        };

        profile.User.FullName = request.FullName.Trim();
        profile.User.PhoneNumber = request.PhoneNumber?.Trim();
        profile.Department = request.Department?.Trim();
        profile.Position = request.Position?.Trim();

        var passwordReset = false;
        if (!string.IsNullOrWhiteSpace(request.ResetTemporaryPassword))
        {
            profile.User.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.ResetTemporaryPassword.Trim());
            profile.User.FailedLoginAttempts = 0;
            profile.User.LockoutUntil = null;
            passwordReset = true;

            var activeTokens = await _dbContext.RefreshTokens
                .Where(t => t.UserId == staffId && !t.IsRevoked)
                .ToListAsync(ct);

            foreach (var t in activeTokens)
            {
                t.IsRevoked = true;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        _authCacheInvalidator.InvalidateUserAuthState(staffId);

        await _auditLogService.LogAsync(
            action: "UPDATE_STAFF",
            entityType: "StaffProfile",
            entityId: staffId.ToString(),
            oldValues: oldSnapshot,
            newValues: new
            {
                fullName = profile.User.FullName,
                phoneNumber = profile.User.PhoneNumber,
                department = profile.Department,
                position = profile.Position,
                passwordReset
            },
            reason: passwordReset ? "Cập nhật hồ sơ và cấp lại mật khẩu tạm" : "Cập nhật thông tin nhân sự",
            explicitActorId: adminUserId,
            ct: ct);

        return MapStaffDto(profile);
    }

    public async Task<StaffAccountDto> UpdateStaffStatusAsync(
        Guid adminUserId,
        Guid staffId,
        UpdateStaffStatusRequest request,
        CancellationToken ct = default)
    {
        if (adminUserId == staffId)
        {
            throw new ForbiddenAccessException("BR-ADM-03", "MSG-ADM-04",
                "Bạn không thể tự khóa hoặc vô hiệu hóa tài khoản đang đăng nhập của chính mình.");
        }

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 10)
        {
            throw new BusinessRuleException("BR-ADM-03", "MSG-GEN-01",
                "Bắt buộc nhập lý do thay đổi trạng thái nhân sự tối thiểu 10 ký tự (BR-ADM-03).");
        }

        if (!Enum.TryParse<UserStatus>(request.Status.Trim(), true, out var targetStatus) ||
            targetStatus is not (UserStatus.Active or UserStatus.Suspended or UserStatus.Deactivated))
        {
            throw new BusinessRuleException("BR-ADM-03", "MSG-GEN-01",
                "Trạng thái nhân sự không hợp lệ. Chỉ chấp nhận Active, Suspended hoặc Deactivated.");
        }

        var profile = await _dbContext.StaffProfiles
            .Include(s => s.User)
            .Include(s => s.Permissions)
            .FirstOrDefaultAsync(s => s.UserId == staffId && s.User.Role == UserRole.Staff, ct);

        if (profile == null)
        {
            throw new NotFoundException("Không tìm thấy tài khoản nhân viên cần cập nhật trạng thái.");
        }

        var prevUserStatus = profile.User.Status;
        var prevStaffStatus = profile.Status;
        var cleanReason = request.Reason.Trim();
        var now = DateTime.UtcNow;

        profile.User.Status = targetStatus;
        profile.User.ModerationReason = cleanReason;
        profile.User.ModeratedAt = now;
        profile.User.ModeratedBy = adminUserId;
        profile.Status = targetStatus == UserStatus.Active ? "ACTIVE" : "INACTIVE";

        int revokedTokens = 0;
        if (targetStatus != UserStatus.Active)
        {
            var tokens = await _dbContext.RefreshTokens
                .Where(t => t.UserId == staffId && !t.IsRevoked)
                .ToListAsync(ct);

            foreach (var t in tokens)
            {
                t.IsRevoked = true;
            }
            revokedTokens = tokens.Count;
        }
        else
        {
            profile.User.FailedLoginAttempts = 0;
            profile.User.LockoutUntil = null;
        }

        _dbContext.AccountModerationLogs.Add(new AccountModerationLog
        {
            Id = Guid.NewGuid(),
            TargetUserId = staffId,
            PerformedBy = adminUserId,
            ActionType = targetStatus == UserStatus.Active ? "UNLOCK" : (targetStatus == UserStatus.Deactivated ? "DEACTIVATE" : "LOCK"),
            PreviousStatus = prevUserStatus.ToString(),
            NewStatus = targetStatus.ToString(),
            Reason = cleanReason,
            RevokedTokensCount = revokedTokens,
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(ct);
        _authCacheInvalidator.InvalidateUserAuthState(staffId);

        await _auditLogService.LogAsync(
            action: "UPDATE_STAFF_STATUS",
            entityType: "StaffProfile",
            entityId: staffId.ToString(),
            oldValues: new { userStatus = prevUserStatus.ToString(), staffStatus = prevStaffStatus },
            newValues: new { userStatus = targetStatus.ToString(), staffStatus = profile.Status, revokedTokens },
            reason: cleanReason,
            explicitActorId: adminUserId,
            ct: ct);

        return MapStaffDto(profile);
    }

    public IReadOnlyList<PermissionCatalogItemDto> GetPermissionCatalog() =>
        StaffPermissions.Catalog.Select(x => new PermissionCatalogItemDto
        {
            Code = x.Code,
            LegacyAlias = x.LegacyAlias,
            Label = x.Label,
            Description = x.Description
        }).ToList();

    public async Task<StaffPermissionsDetailDto> GetStaffPermissionsAsync(
        Guid staffId,
        CancellationToken ct = default)
    {
        await EnsureOrphanStaffProfilesInitializedAsync(ct);

        var profile = await _dbContext.StaffProfiles
            .Include(s => s.User)
            .Include(s => s.Permissions)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == staffId && s.User.Role == UserRole.Staff, ct);

        if (profile == null)
        {
            throw new NotFoundException("Không tìm thấy hồ sơ nhân viên vận hành.");
        }

        var labelMap = StaffPermissions.Catalog.ToDictionary(x => x.Code, x => x.Label, StringComparer.OrdinalIgnoreCase);

        return new StaffPermissionsDetailDto
        {
            StaffId = profile.UserId,
            StaffCode = profile.StaffCode,
            FullName = profile.User.FullName,
            Email = profile.User.Email,
            AssignedPermissions = profile.Permissions
                .OrderBy(p => p.PermissionCode)
                .Select(p => new StaffPermissionAssignmentItemDto
                {
                    PermissionCode = p.PermissionCode,
                    Label = labelMap.GetValueOrDefault(p.PermissionCode, p.PermissionCode),
                    GrantedBy = p.GrantedBy,
                    GrantedAt = p.GrantedAt
                })
                .ToList()
        };
    }

    public async Task<StaffAccountDto> UpdateStaffPermissionsAsync(
        Guid adminUserId,
        Guid staffId,
        UpdateStaffPermissionsRequest request,
        CancellationToken ct = default)
    {
        await EnsureOrphanStaffProfilesInitializedAsync(ct);

        var profile = await _dbContext.StaffProfiles
            .Include(s => s.User)
            .Include(s => s.Permissions)
            .FirstOrDefaultAsync(s => s.UserId == staffId && s.User.Role == UserRole.Staff, ct);

        if (profile == null)
        {
            throw new NotFoundException("Không tìm thấy hồ sơ nhân viên cần phân quyền.");
        }

        var desiredCodes = (request.Permissions ?? new List<string>())
            .Select(StaffPermissions.Normalize)
            .Where(c => !string.IsNullOrEmpty(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        if (desiredCodes.Any(c => !StaffPermissions.IsValid(c)))
        {
            throw new BusinessRuleException("BR-ADM-02", "MSG-GEN-01",
                "Danh sách phân quyền chứa mã quyền không hợp lệ.");
        }

        var previousCodes = profile.Permissions
            .Select(p => p.PermissionCode)
            .OrderBy(c => c)
            .ToList();

        var toRemove = profile.Permissions
            .Where(p => !desiredCodes.Contains(p.PermissionCode, StringComparer.OrdinalIgnoreCase))
            .ToList();

        foreach (var rem in toRemove)
        {
            _dbContext.StaffPermissionAssignments.Remove(rem);
            profile.Permissions.Remove(rem);
        }

        var existingSet = profile.Permissions
            .Select(p => p.PermissionCode)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = DateTime.UtcNow;
        foreach (var code in desiredCodes.Where(c => !existingSet.Contains(c)))
        {
            var newPerm = new StaffPermissionAssignment
            {
                Id = Guid.NewGuid(),
                StaffId = staffId,
                PermissionCode = code,
                GrantedBy = adminUserId,
                GrantedAt = now
            };
            _dbContext.StaffPermissionAssignments.Add(newPerm);
            if (!profile.Permissions.Contains(newPerm))
            {
                profile.Permissions.Add(newPerm);
            }
        }

        await _dbContext.SaveChangesAsync(ct);

        // Có hiệu lực kiểm soát truy cập tức thời không cần đăng nhập lại
        _authCacheInvalidator.InvalidateUserAuthState(staffId);

        await _auditLogService.LogAsync(
            action: "UPDATE_STAFF_PERMISSIONS",
            entityType: "StaffProfile",
            entityId: staffId.ToString(),
            oldValues: new { permissions = previousCodes },
            newValues: new { permissions = desiredCodes },
            reason: string.IsNullOrWhiteSpace(request.Reason)
                ? "Cập nhật ma trận phân quyền RBAC nhân viên (UC A-04)"
                : request.Reason.Trim(),
            explicitActorId: adminUserId,
            ct: ct);

        return MapStaffDto(profile);
    }

    private async Task EnsureOrphanStaffProfilesInitializedAsync(CancellationToken ct)
    {
        var orphanStaffUsers = await _dbContext.Users
            .Where(u => u.Role == UserRole.Staff && u.StaffProfile == null)
            .ToListAsync(ct);

        if (orphanStaffUsers.Count == 0) return;

        foreach (var staffUser in orphanStaffUsers)
        {
            var code = await GenerateNextStaffCodeAsync(ct);
            var isDefaultDemoStaff = staffUser.Username.Equals("staff", StringComparison.OrdinalIgnoreCase);

            var profile = new StaffProfile
            {
                UserId = staffUser.Id,
                StaffCode = code,
                Department = "Vận hành tổng hợp",
                Position = isDefaultDemoStaff ? "Trưởng ca vận hành" : "Nhân viên vận hành",
                Status = staffUser.Status == UserStatus.Active ? "ACTIVE" : "INACTIVE"
            };

            if (isDefaultDemoStaff)
            {
                foreach (var permCode in StaffPermissions.AllCodes)
                {
                    profile.Permissions.Add(new StaffPermissionAssignment
                    {
                        Id = Guid.NewGuid(),
                        StaffId = staffUser.Id,
                        PermissionCode = permCode,
                        GrantedBy = staffUser.Id,
                        GrantedAt = DateTime.UtcNow
                    });
                }
            }

            _dbContext.StaffProfiles.Add(profile);
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    private async Task<string> GenerateNextStaffCodeAsync(CancellationToken ct)
    {
        var existingCodes = await _dbContext.StaffProfiles
            .IgnoreQueryFilters()
            .Select(s => s.StaffCode)
            .ToListAsync(ct);

        int maxSeq = 0;
        foreach (var code in existingCodes)
        {
            if (code.StartsWith("NV-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(code[3..], out var num) &&
                num > maxSeq)
            {
                maxSeq = num;
            }
        }

        return $"NV-{maxSeq + 1:D3}";
    }

    private static StaffAccountDto MapStaffDto(StaffProfile profile) =>
        new()
        {
            StaffId = profile.UserId,
            StaffCode = profile.StaffCode,
            Username = profile.User.Username,
            FullName = profile.User.FullName,
            Email = profile.User.Email,
            PhoneNumber = profile.User.PhoneNumber,
            Department = profile.Department,
            Position = profile.Position,
            UserStatus = profile.User.Status.ToString(),
            StaffStatus = profile.Status,
            Permissions = profile.Permissions
                .Select(p => p.PermissionCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c)
                .ToList(),
            CreatedAt = profile.CreatedAt
        };
}
