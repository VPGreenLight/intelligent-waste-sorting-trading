using Ecolink.Domain.Enums;

namespace Ecolink.Domain.Entities;

/// <summary>
/// Dòng giá thu mua tham chiếu theo từng loại phế liệu của một cơ sở Recycler (UC R-01).
/// Ánh xạ bảng RecyclerPriceList trong báo cáo thiết kế (mục 3.3.1), bổ sung duy nhất cột collection_conditions.
/// RecyclerId chính là RecyclerProfile.UserId. Không xóa mềm: tạm ngưng mua bằng Status = Inactive (BR-MTH-07).
/// </summary>
public class RecyclerPrice
{
    public Guid Id { get; set; } = Guid.NewGuid();   

    public Guid RecyclerId { get; set; }             
    public RecyclerProfile? Recycler { get; set; }

    public Guid WasteCategoryId { get; set; }       
    public WasteCategory? WasteCategory { get; set; }

    /// <summary>Giá thu mua VND/kg.</summary>
    public decimal PricePerKg { get; set; }

    /// <summary>Khối lượng tối thiểu để thu gom (kg), mặc định 1.0.</summary>
    public decimal MinQuantityKg { get; set; } = 1.0m;

    /// <summary>[Cột bổ sung] Điều kiện thu gom đặc biệt .</summary>
    public string? CollectionConditions { get; set; }

    public PriceStatus Status { get; set; } = PriceStatus.Active;

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }

    /// <summary>Mốc cập nhật gần nhất, dùng cho cờ "giá cũ quá 30 ngày" (JOB-04). Service tự gán khi thay đổi.</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
