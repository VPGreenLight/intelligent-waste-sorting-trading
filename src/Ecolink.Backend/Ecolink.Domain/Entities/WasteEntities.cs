using Ecolink.Domain.Common;

namespace Ecolink.Domain.Entities;

public class WasteCategory : BaseEntity
{
    public string Code { get; set; } = string.Empty; // plastic, paper, glass, metal, cardboard, trash
    public string Name { get; set; } = string.Empty; // Nhựa tái chế, Giấy, Thủy tinh...
    public string? Description { get; set; }
    public string StandardBinColor { get; set; } = string.Empty; // Cam, Xanh lá, Trắng...
    public string? IconUrl { get; set; }
    public bool IsRecyclable { get; set; } = true; // Có giá trị thương mại tái chế hay không (false với trash)
    public string Status { get; set; } = "Active"; // Active, Inactive (BR-SRT-04: chỉ ngừng sử dụng, không xóa vật lý)

    // Navigation
    public SortingRule? SortingRule { get; set; }
    public ICollection<AcceptedMaterial> AcceptedMaterials { get; set; } = new List<AcceptedMaterial>();
}

public class SortingRule : BaseEntity
{
    public Guid WasteCategoryId { get; set; }
    public WasteCategory? WasteCategory { get; set; }

    public string Title { get; set; } = string.Empty;            // Tiêu đề hướng dẫn phân loại
    public string PreparationSteps { get; set; } = string.Empty; // Bước sơ chế làm sạch trước khi vứt
    public string SortingInstruction { get; set; } = string.Empty; // Cách phân tách và chọn thùng chứa
    public string HandlingAction { get; set; } = string.Empty;   // Hướng dẫn bàn giao cho vựa hoặc điểm thu gom
    public string? HazardWarning { get; set; }                   // Cảnh báo an toàn (nắp nhọn, pin dễ cháy)
    public string ApplicableRegion { get; set; } = "VN_NATIONAL";
    public string Status { get; set; } = "ACTIVE";
}
