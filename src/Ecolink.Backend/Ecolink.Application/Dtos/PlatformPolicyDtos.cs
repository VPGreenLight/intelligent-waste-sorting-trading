namespace Ecolink.Application.Dtos;

public class PlatformPolicySectionDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Category { get; set; } = "TERMS";
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public Guid? UpdatedByAdminId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SavePlatformPolicyRequest
{
    public string? Code { get; set; }
    public string Category { get; set; } = "TERMS";
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int DisplayOrder { get; set; } = 1;
    public string? Status { get; set; } = "ACTIVE";
}

public class SetPlatformPolicyStatusRequest
{
    public string Status { get; set; } = "ACTIVE";
}
