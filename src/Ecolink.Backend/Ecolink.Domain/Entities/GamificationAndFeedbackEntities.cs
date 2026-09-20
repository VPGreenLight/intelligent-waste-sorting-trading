using Ecolink.Domain.Common;
using Ecolink.Domain.Enums;

namespace Ecolink.Domain.Entities;

public class EcoVoucher : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int PointsRequired { get; set; }
    public decimal DiscountAmount { get; set; }
    public string PartnerSponsor { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public int QuantityAvailable { get; set; }
    public bool IsActive { get; set; } = true;
}

public class AIFeedbackLog : BaseEntity
{
    public Guid? UserId { get; set; }
    public User? User { get; set; }

    public string ImageUrl { get; set; } = string.Empty;
    public string PredictedLabel { get; set; } = string.Empty;
    public double ConfidenceScore { get; set; }
    public string UserReportedLabel { get; set; } = string.Empty;
    public string? AdminVerifiedLabel { get; set; }
    public FeedbackStatus Status { get; set; } = FeedbackStatus.Pending;
    public string? Note { get; set; }
}
