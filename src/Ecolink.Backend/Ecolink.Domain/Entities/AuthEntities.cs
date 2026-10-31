using Ecolink.Domain.Common;
using Ecolink.Domain.Enums;
using NetTopologySuite.Geometries;

namespace Ecolink.Domain.Entities;

/// <summary>
/// Hồ sơ đối tác thu gom ve chai / cơ sở tái chế (Quan hệ 1-1 với User).
/// Tích hợp dữ liệu không gian PostGIS phục vụ tìm kiếm bán kính ST_DWithin (UC U-05, R-01, R-02).
/// </summary>
public class RecyclerProfile : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string FacilityName { get; set; } = string.Empty;
    public string? RepresentativeName { get; set; }
    public string BusinessAddress { get; set; } = string.Empty;

    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public Point Location { get; set; } = null!; // NetTopologySuite Point (SRID 4326)

    public string OperatingHours { get; set; } = "08:00 - 18:00";
    public string ContactPhone { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LegalPermitUrl { get; set; }
    public string? BusinessPhotoUrl { get; set; }

    public VerificationStatus VerificationStatus { get; set; } = VerificationStatus.Pending;
    public RecyclerActiveStatus ActiveStatus { get; set; } = RecyclerActiveStatus.Offline;
    public string? RejectionReason { get; set; }

    /// <summary>Ghi chú bận tạm thời hiển thị cho người dân .</summary>
    public string? BusyReason { get; set; }

    /// <summary>Lịch hoạt động cố định theo tuần, lưu dạng JSON (xem ScheduleDayDto).</summary>
    public string? WeeklyScheduleJson { get; set; }

    /// <summary>Bảng giá thu mua phế liệu của cơ sở (R-01).</summary>
    public ICollection<RecyclerPrice> Prices { get; set; } = new List<RecyclerPrice>();
}

/// <summary>
/// Hồ sơ nhân viên vận hành hệ thống Back-office (Quan hệ 1-1 với User).
/// </summary>
public class StaffProfile : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string StaffCode { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Position { get; set; }
    public string Status { get; set; } = "ACTIVE";

    public ICollection<StaffPermissionAssignment> Permissions { get; set; } = new List<StaffPermissionAssignment>();
}

/// <summary>
/// Phân quyền chi tiết cho Staff theo UC A-04 và BR-ADM-02.
/// </summary>
public class StaffPermissionAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StaffId { get; set; }
    public StaffProfile StaffProfile { get; set; } = null!;

    // Mã quyền: REVIEW_AI, HANDLE_COMPLAINT, VERIFY_RECYCLER, MANAGE_BLOG, MANAGE_INVENTORY
    public string PermissionCode { get; set; } = string.Empty;

    public Guid GrantedBy { get; set; }
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Quản lý mã OTP/Link đặt lại mật khẩu an toàn (BR-AUTH-06, BR-AUTH-09).
/// </summary>
public class PasswordResetToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Quản lý Refresh Token an toàn, hỗ trợ Token Rotation và Replay Detection.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public string? ReplacedByTokenHash { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByIp { get; set; }
}
