using Ecolink.Domain.Common;

namespace Ecolink.Domain.Entities;

/// <summary>
/// Mục Điều khoản dịch vụ, Chính sách môi trường &amp; Quyền riêng tư hiển thị công khai tại /terms.
/// </summary>
public class PlatformPolicySection : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = "TERMS"; // TERMS | ENVIRONMENT | PRIVACY
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 1;
    public string Status { get; set; } = "ACTIVE"; // ACTIVE | DRAFT | INACTIVE
    public Guid? UpdatedByAdminId { get; set; }
}
