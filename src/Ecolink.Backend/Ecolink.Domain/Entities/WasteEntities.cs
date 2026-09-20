using Ecolink.Domain.Common;

namespace Ecolink.Domain.Entities;

public class WasteCategory : BaseEntity
{
    public string Code { get; set; } = string.Empty; // plastic, paper, glass, metal, cardboard, trash
    public string Name { get; set; } = string.Empty; // Nhựa tái chế, Giấy, Thủy tinh...
    public string? Description { get; set; }
    public string StandardBinColor { get; set; } = string.Empty; // Cam, Xanh lá, Trắng...
    public string? IconUrl { get; set; }

    // Navigation
    public SortingRule? SortingRule { get; set; }
    public ICollection<AcceptedMaterial> AcceptedMaterials { get; set; } = new List<AcceptedMaterial>();
}

public class SortingRule : BaseEntity
{
    public Guid WasteCategoryId { get; set; }
    public WasteCategory? WasteCategory { get; set; }

    public string PreparationSteps { get; set; } = string.Empty; // Rửa sạch, để ráo, bẹp chai
    public string HandlingAction { get; set; } = string.Empty;   // Bỏ vào thùng tái chế hoặc liên hệ người thu mua
    public string? HazardWarning { get; set; }                   // Chú ý nắp nhọn, pin dễ cháy
    public string ApplicableRegion { get; set; } = "VN_NATIONAL";
}
