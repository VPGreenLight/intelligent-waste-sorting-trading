using Ecolink.Domain.Common;
using Ecolink.Domain.Enums;

namespace Ecolink.Domain.Entities;

/// <summary>
/// Bảng người dùng cốt lõi đại diện cho mọi tác nhân trong hệ thống (User, Recycler, Staff, Admin).
/// Kế thừa BaseAuditableEntity theo database_design_and_audit_guidelines.md.
/// </summary>
public class User : BaseAuditableEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; } // Null nếu chỉ đăng nhập Google OAuth
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsGoogleAuth { get; set; } = false;
    public UserStatus Status { get; set; } = UserStatus.Active;

    // Cơ chế chống Brute-force mật khẩu (BR-AUTH-04)
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LockoutUntil { get; set; }

    // Helper property phục vụ tương thích ngược
    public bool IsActive
    {
        get => Status == UserStatus.Active;
        set => Status = value ? UserStatus.Active : UserStatus.Suspended;
    }

    // Navigation properties - Module 1
    public RecyclerProfile? RecyclerProfile { get; set; }
    public StaffProfile? StaffProfile { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();

    // Navigation properties - Các phân hệ khác
    public UserEcoProfile? EcoProfile { get; set; }
    public CollectionPartner? PartnerProfile { get; set; }
    public ICollection<TradeOrder> TradeOrders { get; set; } = new List<TradeOrder>();
    public ICollection<AIFeedbackLog> Feedbacks { get; set; } = new List<AIFeedbackLog>();
}

public class UserEcoProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public int CurrentGreenPoints { get; set; } = 0;
    public int TotalLifetimePoints { get; set; } = 0;
    public int TreeLevel { get; set; } = 1;
    public int TreeExperience { get; set; } = 0;
    public int TotalWasteScanned { get; set; } = 0;
    public decimal TotalRecycledKg { get; set; } = 0m;
}
