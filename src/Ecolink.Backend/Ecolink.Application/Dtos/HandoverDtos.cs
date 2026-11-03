using System.ComponentModel.DataAnnotations;

namespace Ecolink.Application.Dtos;

// ============================================================
// Request
// ============================================================
public class HandoverItemRequest
{
    [Required(ErrorMessage = "Vui lòng chọn loại phế liệu.")]
    public Guid? WasteCategoryId { get; set; }

    /// <summary>Khối lượng ước tính (kg): > 0, tối đa 2 chữ số thập phân.</summary>
    [Required(ErrorMessage = "Vui lòng nhập khối lượng ước tính.")]
    public decimal? EstimatedWeightKg { get; set; }

    [MaxLength(200, ErrorMessage = "Ghi chú dòng phế liệu tối đa 200 ký tự.")]
    public string? ItemNote { get; set; }
}

public class CreateHandoverRequest
{
    [Required(ErrorMessage = "Vui lòng chọn cơ sở thu gom.")]
    public Guid? RecyclerId { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Vui lòng thêm ít nhất một loại phế liệu.")]
    public List<HandoverItemRequest> Items { get; set; } = new();

    /// <summary>1–5 URL ảnh phế liệu (đã upload lên Cloudflare R2).</summary>
    [Required]
    [MinLength(1, ErrorMessage = "Vui lòng đính kèm ít nhất 1 ảnh.")]
    [MaxLength(5, ErrorMessage = "Tối đa 5 ảnh.")]
    public List<string> PhotoUrls { get; set; } = new();

    /// <summary>Khung giờ mong muốn: tối thiểu 2 giờ nữa, tối đa 7 ngày tới.</summary>
    [Required(ErrorMessage = "Vui lòng chọn khung giờ thu gom.")]
    public DateTimeOffset? ProposedTimeSlot { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ thu gom.")]
    [MaxLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự.")]
    public string PickupAddress { get; set; } = string.Empty;

    [Range(-90, 90)] public decimal? Latitude { get; set; }
    [Range(-180, 180)] public decimal? Longitude { get; set; }

    [MaxLength(200, ErrorMessage = "Ghi chú tối đa 200 ký tự.")]
    public string? UserNote { get; set; }
}

/// <summary>Chỉnh sửa yêu cầu khi còn Pending (U-07). Không đổi được cơ sở thu gom.</summary>
public class UpdateHandoverRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Vui lòng thêm ít nhất một loại phế liệu.")]
    public List<HandoverItemRequest> Items { get; set; } = new();

    [Required]
    [MinLength(1, ErrorMessage = "Vui lòng đính kèm ít nhất 1 ảnh.")]
    [MaxLength(5, ErrorMessage = "Tối đa 5 ảnh.")]
    public List<string> PhotoUrls { get; set; } = new();

    [Required(ErrorMessage = "Vui lòng chọn khung giờ thu gom.")]
    public DateTimeOffset? ProposedTimeSlot { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ thu gom.")]
    [MaxLength(500, ErrorMessage = "Địa chỉ tối đa 500 ký tự.")]
    public string PickupAddress { get; set; } = string.Empty;

    [Range(-90, 90)] public decimal? Latitude { get; set; }
    [Range(-180, 180)] public decimal? Longitude { get; set; }

    [MaxLength(200, ErrorMessage = "Ghi chú tối đa 200 ký tự.")]
    public string? UserNote { get; set; }
}

/// <summary>Hủy / từ chối: lý do bắt buộc.</summary>
public class HandoverReasonRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do.")]
    [MaxLength(500, ErrorMessage = "Lý do tối đa 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;
}

public class AcceptHandoverRequest
{
    [MaxLength(200, ErrorMessage = "Ghi chú tối đa 200 ký tự.")]
    public string? Note { get; set; }
}

// ============================================================
// Response
// ============================================================
public class HandoverItemDto
{
    public Guid Id { get; set; }
    public Guid WasteCategoryId { get; set; }
    public string WasteCategoryCode { get; set; } = string.Empty;
    public string WasteCategoryName { get; set; } = string.Empty;
    public decimal EstimatedWeightKg { get; set; }
    public decimal? ActualWeightKg { get; set; }
    public decimal? AgreedPricePerKg { get; set; }
    public string? ItemNote { get; set; }
}

public class HandoverPartyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Chỉ có giá trị sau khi yêu cầu được chấp nhận (BR-HND-08).</summary>
    public string? Phone { get; set; }
    public string? Address { get; set; }
}

public class HandoverRequestDto
{
    public Guid Id { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<HandoverItemDto> Items { get; set; } = new();
    public decimal TotalEstimatedWeightKg { get; set; }
    public List<string> PhotoUrls { get; set; } = new();
    public DateTime ProposedTimeSlot { get; set; }
    public DateTime? ConfirmedTimeSlot { get; set; }
    public string? PickupAddress { get; set; }
    public string? UserNote { get; set; }
    public string? RecyclerNote { get; set; }
    public string? CancelReason { get; set; }
    public string? CancelledByRole { get; set; }
    public bool IsLateCancellation { get; set; }
    public bool IsContactRevealed { get; set; }
    public bool CanEdit { get; set; }
    public bool CanCancel { get; set; }
    public HandoverPartyDto? Recycler { get; set; }
    public HandoverPartyDto? Requester { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class HandoverPagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
}
