namespace Ecolink.Domain.Common;

public abstract class BaseAuditableEntity : BaseEntity
{
    public string CreatedBy { get; set; } = "System";
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
}
