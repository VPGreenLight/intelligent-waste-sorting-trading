using System.ComponentModel.DataAnnotations;

namespace Ecolink.Application.Dtos;

// ============================================================
// Danh mục phế liệu chuẩn (dropdown "Add Scrap Type")
// ============================================================
public class WasteCategoryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string StandardBinColor { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
}

// ============================================================
// Recycler Profile
// ============================================================
public class RecyclerProfileDto
{
    public Guid RecyclerId { get; set; }
    public string FacilityName { get; set; } = string.Empty;
    public string? RepresentativeName { get; set; }
    public string BusinessAddress { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string OperatingHours { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LegalPermitUrl { get; set; }
    public string? BusinessPhotoUrl { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public string ActiveStatus { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }
    public string? BusyReason { get; set; }
    public int ActivePriceCount { get; set; }
}

public class UpdateRecyclerProfileRequest
{
    [Required(ErrorMessage = "Tên cơ sở là bắt buộc.")]
    [MaxLength(150)]
    public string FacilityName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? RepresentativeName { get; set; }

    [Required(ErrorMessage = "Địa chỉ kinh doanh là bắt buộc.")]
    public string BusinessAddress { get; set; } = string.Empty;

    [Range(-90, 90, ErrorMessage = "Vĩ độ phải nằm trong khoảng -90 đến 90.")]
    public double Latitude { get; set; }

    [Range(-180, 180, ErrorMessage = "Kinh độ phải nằm trong khoảng -180 đến 180.")]
    public double Longitude { get; set; }

    [Required(ErrorMessage = "Số điện thoại liên hệ là bắt buộc.")]
    [MaxLength(20)]
    public string ContactPhone { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? BusinessPhotoUrl { get; set; }
}

// ============================================================
// Pricing (R-01)
// ============================================================
public class RecyclerPriceDto
{
    public Guid Id { get; set; }
    public Guid WasteCategoryId { get; set; }
    public string WasteCategoryCode { get; set; } = string.Empty;
    public string WasteCategoryName { get; set; } = string.Empty;
    public decimal PricePerKg { get; set; }
    public decimal MinQuantityKg { get; set; }
    public string? CollectionConditions { get; set; }
    public bool IsActive { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>True nếu dòng giá chưa được cập nhật quá 30 ngày (JOB-04).</summary>
    public bool IsOutdated { get; set; }
}

public class CreateRecyclerPriceRequest
{
    [Required(ErrorMessage = "Vui lòng chọn loại phế liệu.")]
    public Guid? WasteCategoryId { get; set; }

    [Required(ErrorMessage = "Giá thu mua là bắt buộc.")]
    [Range(1, 1_000_000_000, ErrorMessage = "Giá thu mua phải là số dương.")]
    public decimal? PricePerKg { get; set; }

    [Range(0.001, 100000, ErrorMessage = "Khối lượng tối thiểu phải lớn hơn 0.")]
    public decimal MinQuantityKg { get; set; } = 1.0m;

    [MaxLength(200, ErrorMessage = "Điều kiện thu gom tối đa 200 ký tự.")]
    public string? CollectionConditions { get; set; }
}

public class UpdateRecyclerPriceRequest
{
    [Range(1, 1_000_000_000, ErrorMessage = "Giá thu mua phải là số dương.")]
    public decimal? PricePerKg { get; set; }

    [Range(0.001, 100000, ErrorMessage = "Khối lượng tối thiểu phải lớn hơn 0.")]
    public decimal? MinQuantityKg { get; set; }

    [MaxLength(200, ErrorMessage = "Điều kiện thu gom tối đa 200 ký tự.")]
    public string? CollectionConditions { get; set; }

    /// <summary>true = đang thu mua, false = tạm ngưng thu mua.</summary>
    public bool? IsActive { get; set; }
}

public class BulkPriceItemRequest
{
    [Required]
    public Guid? WasteCategoryId { get; set; }

    /// <summary>Bật/tắt nhận mua loại phế liệu này (toggle ở SCR-RCY-01).</summary>
    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "Giá thu mua là bắt buộc.")]
    [Range(1, 1_000_000_000, ErrorMessage = "Giá thu mua phải là số dương.")]
    public decimal? PricePerKg { get; set; }

    [Range(0.001, 100000, ErrorMessage = "Khối lượng tối thiểu phải lớn hơn 0.")]
    public decimal MinQuantityKg { get; set; } = 1.0m;

    [MaxLength(200, ErrorMessage = "Điều kiện thu gom tối đa 200 ký tự.")]
    public string? CollectionConditions { get; set; }
}

public class BulkSavePricesRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Danh sách giá không được rỗng.")]
    public List<BulkPriceItemRequest> Items { get; set; } = new();
}

// ============================================================
// Availability (R-02)
// ============================================================
public class ScheduleDayDto
{
    /// <summary>0 = Chủ nhật, 1 = Thứ hai, ..., 6 = Thứ bảy.</summary>
    [Range(0, 6, ErrorMessage = "dayOfWeek phải từ 0 (Chủ nhật) đến 6 (Thứ bảy).")]
    public int DayOfWeek { get; set; }

    public bool IsClosed { get; set; }

    /// <summary>Giờ mở cửa dạng "HH:mm". Bỏ trống nếu IsClosed = true.</summary>
    public string? OpenTime { get; set; }

    /// <summary>Giờ đóng cửa dạng "HH:mm". Bỏ trống nếu IsClosed = true.</summary>
    public string? CloseTime { get; set; }
}

public class AvailabilityDto
{
    public bool IsOnline { get; set; }
    public string ActiveStatus { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;
    public string? BusyReason { get; set; }
    public string OperatingHours { get; set; } = string.Empty;
    public List<ScheduleDayDto> WeeklySchedule { get; set; } = new();
    public int ActivePriceCount { get; set; }
}

public class UpdateAvailabilityRequest
{
    [Required(ErrorMessage = "Vui lòng chọn trạng thái Online/Offline.")]
    public bool? IsOnline { get; set; }

    [MaxLength(100, ErrorMessage = "Ghi chú bận tối đa 100 ký tự.")]
    public string? BusyReason { get; set; }
}

public class UpdateScheduleRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Vui lòng khai báo ít nhất một ngày trong lịch tuần.")]
    [MaxLength(7, ErrorMessage = "Lịch tuần tối đa 7 ngày.")]
    public List<ScheduleDayDto> Days { get; set; } = new();
}
