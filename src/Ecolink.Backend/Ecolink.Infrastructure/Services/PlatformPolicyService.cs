using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Quản lý các mục Điều khoản dịch vụ, Chính sách môi trường và Quyền riêng tư (/terms).
/// </summary>
public class PlatformPolicyService : IPlatformPolicyService
{
    private const string RuleCodeValidation = "BR-POL-01";
    private const string RuleCodeConflict = "BR-POL-02";
    private const string RuleCodeMinActive = "BR-POL-03";
    private const int MaxTitleLength = 200;

    private static readonly HashSet<string> AllowedCategories = new(StringComparer.OrdinalIgnoreCase)
    {
        "TERMS",
        "ENVIRONMENT",
        "PRIVACY"
    };

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACTIVE",
        "DRAFT",
        "INACTIVE"
    };

    private static readonly (string Code, string Category, string Title, string Content, int Order)[] DefaultSeeds =
    {
        (
            "TERM-01",
            "ENVIRONMENT",
            "1. Nguyên tắc phân loại rác tại nguồn",
            "Người tham gia cam kết phân loại rác đúng theo hướng dẫn 3 nhóm (Tái chế, Hữu cơ, Khác). Rác tái chế cần được làm sạch cơ bản, để khô ráo trước khi đặt lịch bàn giao nhằm đảm bảo an toàn vệ sinh và tối ưu giá trị thu gom.",
            1
        ),
        (
            "TERM-02",
            "TERMS",
            "2. Quy chuẩn cân đo và biên nhận giao dịch",
            "Mọi giao dịch thu mua phế liệu qua nền tảng phải được cân đo minh bạch tại thời điểm bàn giao. Biên nhận điện tử được lập tức thì trên ứng dụng, ghi nhận khối lượng, đơn giá và số tiền/điểm xanh chuyển vào tài khoản.",
            2
        ),
        (
            "TERM-03",
            "TERMS",
            "3. Cơ chế tích lũy điểm xanh và đổi quà",
            "Điểm xanh được cộng dồn theo khối lượng rác tái chế đã bàn giao thành công. Điểm tiêu dùng có thể dùng để đổi quà tặng hoặc voucher đối tác. Điểm xếp hạng được bảo lưu trọn đời để thăng hạng cây ảo và mở khóa các huy hiệu cộng đồng.",
            3
        ),
        (
            "ENV-01",
            "ENVIRONMENT",
            "4. Trách nhiệm của cơ sở thu gom và đối tác",
            "Cơ sở thu gom phải có giấy phép hoạt động hợp lệ, cam kết thu gom đúng hẹn và chuyển rác đến các nhà máy tái chế đạt chuẩn môi trường theo định hướng trách nhiệm mở rộng của nhà sản xuất (EPR).",
            4
        ),
        (
            "PRIV-01",
            "PRIVACY",
            "5. Bảo mật thông tin và quyền riêng tư",
            "EcoLink cam kết bảo vệ thông tin cá nhân và dữ liệu vị trí thu gom của người dùng. Dữ liệu chỉ được sử dụng cho mục đích điều phối lộ trình thu gom và thống kê báo cáo tác động môi trường số hóa.",
            5
        )
    };

    private readonly ApplicationDbContext _db;
    private readonly IAuditLogService? _auditLogService;
    private bool _initializedForContext;

    public PlatformPolicyService(ApplicationDbContext db, IAuditLogService? auditLogService = null)
    {
        _db = db;
        _auditLogService = auditLogService;
    }

    public async Task<IReadOnlyList<PlatformPolicySectionDto>> GetPublicPoliciesAsync(
        string? category = null,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var query = _db.PlatformPolicySections
            .AsNoTracking()
            .Where(x => x.Status == "ACTIVE");

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = NormalizeCategory(category);
            query = query.Where(x => x.Category == normalizedCategory);
        }

        var rows = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(MapDto).ToList();
    }

    public async Task<IReadOnlyList<PlatformPolicySectionDto>> GetAdminPoliciesAsync(
        string? category = null,
        string? status = null,
        string? search = null,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var query = _db.PlatformPolicySections.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(category))
        {
            var normalizedCategory = NormalizeCategory(category);
            query = query.Where(x => x.Category == normalizedCategory);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = NormalizeStatus(status);
            query = query.Where(x => x.Status == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var kw = search.Trim();
            if (_db.Database.IsRelational())
            {
                var pattern = $"%{kw}%";
                query = query.Where(x =>
                    EF.Functions.ILike(x.Title, pattern) ||
                    EF.Functions.ILike(x.Code, pattern) ||
                    EF.Functions.ILike(x.Content, pattern));
            }
            else
            {
                query = query.Where(x =>
                    x.Title.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    x.Code.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                    x.Content.Contains(kw, StringComparison.OrdinalIgnoreCase));
            }
        }

        var rows = await query
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.CreatedAt)
            .ToListAsync(ct);

        return rows.Select(MapDto).ToList();
    }

    public async Task<PlatformPolicySectionDto> CreatePolicyAsync(
        Guid adminUserId,
        SavePlatformPolicyRequest request,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var validated = ValidateSaveRequest(request);
        var code = string.IsNullOrWhiteSpace(request.Code)
            ? await GenerateNextCodeAsync(validated.Category, ct)
            : request.Code.Trim().ToUpperInvariant();

        if (code.Length > 40)
        {
            throw new BusinessRuleException(RuleCodeValidation, "MSG-GEN-01", "Mã điều khoản tối đa 40 ký tự.");
        }

        if (await _db.PlatformPolicySections.AsNoTracking().AnyAsync(x => x.Code == code, ct))
        {
            throw new ConflictException(
                RuleCodeConflict,
                "MSG-POL-02",
                $"Mã điều khoản '{code}' đã tồn tại trong hệ thống.");
        }

        var now = DateTime.UtcNow;
        var entity = new PlatformPolicySection
        {
            Code = code,
            Category = validated.Category,
            Title = validated.Title,
            Content = validated.Content,
            DisplayOrder = validated.DisplayOrder,
            Status = validated.Status,
            UpdatedByAdminId = adminUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _db.PlatformPolicySections.Add(entity);
        await _db.SaveChangesAsync(ct);

        if (_auditLogService != null)
        {
            await _auditLogService.LogAsync(
                action: "CREATE_PLATFORM_POLICY",
                entityType: nameof(PlatformPolicySection),
                entityId: entity.Id.ToString(),
                oldValues: null,
                newValues: new { entity.Code, entity.Category, entity.Title, entity.DisplayOrder, entity.Status },
                reason: $"Tạo mới mục điều khoản '{entity.Code}' ({entity.Title}).",
                explicitActorId: adminUserId,
                ct: ct);
        }

        return MapDto(entity);
    }

    public async Task<PlatformPolicySectionDto> UpdatePolicyAsync(
        Guid adminUserId,
        Guid id,
        SavePlatformPolicyRequest request,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var entity = await _db.PlatformPolicySections.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy mục điều khoản với ID '{id}'.");

        var validated = ValidateSaveRequest(request);
        var nextCode = string.IsNullOrWhiteSpace(request.Code)
            ? entity.Code
            : request.Code.Trim().ToUpperInvariant();

        if (nextCode.Length > 40)
        {
            throw new BusinessRuleException(RuleCodeValidation, "MSG-GEN-01", "Mã điều khoản tối đa 40 ký tự.");
        }

        if (!string.Equals(entity.Code, nextCode, StringComparison.OrdinalIgnoreCase) &&
            await _db.PlatformPolicySections.AsNoTracking().AnyAsync(x => x.Code == nextCode && x.Id != id, ct))
        {
            throw new ConflictException(
                RuleCodeConflict,
                "MSG-POL-02",
                $"Mã điều khoản '{nextCode}' đã được sử dụng cho mục khác.");
        }

        if (string.Equals(entity.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(validated.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureNotLastActiveSectionAsync(id, ct);
        }

        var oldSnapshot = new
        {
            entity.Code,
            entity.Category,
            entity.Title,
            entity.Content,
            entity.DisplayOrder,
            entity.Status
        };

        entity.Code = nextCode;
        entity.Category = validated.Category;
        entity.Title = validated.Title;
        entity.Content = validated.Content;
        entity.DisplayOrder = validated.DisplayOrder;
        entity.Status = validated.Status;
        entity.UpdatedByAdminId = adminUserId;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        if (_auditLogService != null)
        {
            await _auditLogService.LogAsync(
                action: "UPDATE_PLATFORM_POLICY",
                entityType: nameof(PlatformPolicySection),
                entityId: entity.Id.ToString(),
                oldValues: oldSnapshot,
                newValues: new { entity.Code, entity.Category, entity.Title, entity.DisplayOrder, entity.Status },
                reason: $"Cập nhật mục điều khoản '{entity.Code}'.",
                explicitActorId: adminUserId,
                ct: ct);
        }

        return MapDto(entity);
    }

    public async Task<PlatformPolicySectionDto> SetPolicyStatusAsync(
        Guid adminUserId,
        Guid id,
        string status,
        CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var entity = await _db.PlatformPolicySections.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy mục điều khoản với ID '{id}'.");

        var nextStatus = NormalizeStatus(status);
        if (string.Equals(entity.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(nextStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureNotLastActiveSectionAsync(id, ct);
        }

        var oldStatus = entity.Status;
        entity.Status = nextStatus;
        entity.UpdatedByAdminId = adminUserId;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        if (_auditLogService != null)
        {
            await _auditLogService.LogAsync(
                action: "UPDATE_PLATFORM_POLICY_STATUS",
                entityType: nameof(PlatformPolicySection),
                entityId: entity.Id.ToString(),
                oldValues: new { Status = oldStatus },
                newValues: new { Status = entity.Status },
                reason: $"Đổi trạng thái mục điều khoản '{entity.Code}' từ {oldStatus} sang {entity.Status}.",
                explicitActorId: adminUserId,
                ct: ct);
        }

        return MapDto(entity);
    }

    public async Task DeletePolicyAsync(Guid adminUserId, Guid id, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct);

        var entity = await _db.PlatformPolicySections.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new NotFoundException($"Không tìm thấy mục điều khoản với ID '{id}'.");

        if (string.Equals(entity.Status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
        {
            await EnsureNotLastActiveSectionAsync(id, ct);
        }

        var snapshot = new { entity.Code, entity.Category, entity.Title, entity.DisplayOrder, entity.Status };
        _db.PlatformPolicySections.Remove(entity);
        await _db.SaveChangesAsync(ct);

        if (_auditLogService != null)
        {
            await _auditLogService.LogAsync(
                action: "DELETE_PLATFORM_POLICY",
                entityType: nameof(PlatformPolicySection),
                entityId: id.ToString(),
                oldValues: snapshot,
                newValues: null,
                reason: $"Xóa mục điều khoản '{entity.Code}' ({entity.Title}).",
                explicitActorId: adminUserId,
                ct: ct);
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initializedForContext) return;

        if (_db.Database.IsRelational())
        {
            await _db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS platform_policy_sections (
                    id uuid PRIMARY KEY,
                    code varchar(40) NOT NULL,
                    category varchar(30) NOT NULL DEFAULT 'TERMS',
                    title varchar(200) NOT NULL,
                    content text NOT NULL,
                    display_order integer NOT NULL DEFAULT 1,
                    status varchar(20) NOT NULL DEFAULT 'ACTIVE',
                    updated_by_admin_id uuid NULL,
                    created_at timestamp with time zone NOT NULL DEFAULT NOW(),
                    updated_at timestamp with time zone NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS ux_platform_policy_sections_code ON platform_policy_sections (code);
                CREATE INDEX IF NOT EXISTS ix_platform_policy_sections_status_order ON platform_policy_sections (status, display_order);
            ", ct);
        }

        if (!await _db.PlatformPolicySections.AsNoTracking().AnyAsync(ct))
        {
            var now = DateTime.UtcNow;
            var entities = DefaultSeeds.Select(seed => new PlatformPolicySection
            {
                Code = seed.Code,
                Category = seed.Category,
                Title = seed.Title,
                Content = seed.Content,
                DisplayOrder = seed.Order,
                Status = "ACTIVE",
                CreatedAt = now,
                UpdatedAt = now
            });
            _db.PlatformPolicySections.AddRange(entities);
            await _db.SaveChangesAsync(ct);
        }

        _initializedForContext = true;
    }

    private async Task EnsureNotLastActiveSectionAsync(Guid excludingId, CancellationToken ct)
    {
        var hasOtherActive = await _db.PlatformPolicySections
            .AsNoTracking()
            .AnyAsync(x => x.Id != excludingId && x.Status == "ACTIVE", ct);

        if (!hasOtherActive)
        {
            throw new BusinessRuleException(
                RuleCodeMinActive,
                RuleCodeMinActive,
                "Hệ thống phải duy trì ít nhất 1 điều khoản đang hoạt động (ACTIVE) trên trang công khai.");
        }
    }

    private async Task<string> GenerateNextCodeAsync(string category, CancellationToken ct)
    {
        var prefix = category switch
        {
            "ENVIRONMENT" => "ENV",
            "PRIVACY" => "PRIV",
            _ => "TERM"
        };

        var existingCodes = await _db.PlatformPolicySections
            .AsNoTracking()
            .Select(x => x.Code)
            .ToListAsync(ct);

        var maxNum = 0;
        foreach (var c in existingCodes)
        {
            if (c.StartsWith(prefix + "-", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(c[(prefix.Length + 1)..], out var num) &&
                num > maxNum)
            {
                maxNum = num;
            }
        }

        return $"{prefix}-{(maxNum + 1):D2}";
    }

    private static (string Category, string Title, string Content, int DisplayOrder, string Status) ValidateSaveRequest(
        SavePlatformPolicyRequest request)
    {
        var title = (request.Title ?? string.Empty).Trim();
        var content = (request.Content ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(title) || title.Length > MaxTitleLength)
        {
            throw new BusinessRuleException(
                RuleCodeValidation,
                "MSG-GEN-01",
                $"Tiêu đề điều khoản là bắt buộc và tối đa {MaxTitleLength} ký tự.");
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new BusinessRuleException(
                RuleCodeValidation,
                "MSG-GEN-01",
                "Nội dung điều khoản là bắt buộc.");
        }

        if (request.DisplayOrder < 1)
        {
            throw new BusinessRuleException(
                RuleCodeValidation,
                "MSG-GEN-01",
                "Thứ tự hiển thị phải từ 1 trở lên.");
        }

        var category = NormalizeCategory(request.Category);
        var status = string.IsNullOrWhiteSpace(request.Status) ? "ACTIVE" : NormalizeStatus(request.Status);
        return (category, title, content, request.DisplayOrder, status);
    }

    private static string NormalizeCategory(string category)
    {
        var normalized = (category ?? string.Empty).Trim().ToUpperInvariant();
        if (!AllowedCategories.Contains(normalized))
        {
            throw new BusinessRuleException(
                RuleCodeValidation,
                "MSG-GEN-01",
                "Nhóm chính sách chỉ chấp nhận TERMS, ENVIRONMENT hoặc PRIVACY.");
        }
        return normalized;
    }

    private static string NormalizeStatus(string status)
    {
        var normalized = (status ?? string.Empty).Trim().ToUpperInvariant();
        if (!AllowedStatuses.Contains(normalized))
        {
            throw new BusinessRuleException(
                RuleCodeValidation,
                "MSG-GEN-01",
                "Trạng thái điều khoản chỉ chấp nhận ACTIVE, DRAFT hoặc INACTIVE.");
        }
        return normalized;
    }

    private static PlatformPolicySectionDto MapDto(PlatformPolicySection x) => new()
    {
        Id = x.Id,
        Code = x.Code,
        Category = x.Category,
        Title = x.Title,
        Content = x.Content,
        DisplayOrder = x.DisplayOrder,
        Status = x.Status,
        UpdatedByAdminId = x.UpdatedByAdminId,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt
    };
}
