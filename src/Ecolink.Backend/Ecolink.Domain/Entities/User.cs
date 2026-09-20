using Ecolink.Domain.Common;
using Ecolink.Domain.Enums;

namespace Ecolink.Domain.Entities;

public class User : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; } = UserRole.Citizen;
    public bool IsActive { get; set; } = true;

    // Navigation properties
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
