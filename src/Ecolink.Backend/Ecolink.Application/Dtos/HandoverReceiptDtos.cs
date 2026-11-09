using System.ComponentModel.DataAnnotations;

namespace Ecolink.Application.Dtos;

// ============================================================
// Request (R-06)
// ============================================================
public class ReceiptItemRequest
{
    /// <summary>Id dòng phế liệu có sẵn trong yêu cầu. Bỏ trống nếu là dòng phát sinh (dùng WasteCategoryId).</summary>
    public Guid? ItemId { get; set; }

    /// <summary>Loại phế liệu phát sinh khi cân, phải nằm trong danh mục đang thu mua của Recycler (AF-01).</summary>
    public Guid? WasteCategoryId { get; set; }

    /// <summary>Khối lượng cân thực tế (kg): &gt; 0, tối đa 2 chữ số thập phân.</summary>
    [Required(ErrorMessage = "Vui lòng nhập khối lượng cân thực tế.")]
    public decimal? ActualWeightKg { get; set; }
}

public class CreateReceiptRequest
{
    [Required]
    [MinLength(1, ErrorMessage = "Vui lòng nhập khối lượng thực tế cho các loại phế liệu.")]
    public List<ReceiptItemRequest> Items { get; set; } = new();

    /// <summary>1–5 URL ảnh đồng hồ cân (bắt buộc, BR-HND-04).</summary>
    [Required]
    [MinLength(1, ErrorMessage = "Vui lòng tải lên ít nhất 1 ảnh cân.")]
    [MaxLength(5, ErrorMessage = "Tối đa 5 ảnh cân.")]
    public List<string> ScaleProofPhotoUrls { get; set; } = new();

    [MaxLength(1000, ErrorMessage = "URL ảnh biên nhận giấy tối đa 1000 ký tự.")]
    public string? ReceiptImageUrl { get; set; }
}

/// <summary>Khiếu nại sai lệch khối lượng / đơn giá trên biên nhận bàn giao (U-09 Alternative Flow, BR-DSP-06).</summary>
public class DisputeReceiptRequest
{
    [Required(ErrorMessage = "Vui lòng nhập lý do khiếu nại.")]
    [MaxLength(500, ErrorMessage = "Lý do khiếu nại tối đa 500 ký tự.")]
    public string Reason { get; set; } = string.Empty;

    [MaxLength(5, ErrorMessage = "Tối đa 5 ảnh bằng chứng.")]
    public List<string>? EvidencePhotoUrls { get; set; }
}

// ============================================================
// Response
// ============================================================
public class ReceiptLineDto
{
    public Guid? ItemId { get; set; }
    public Guid WasteCategoryId { get; set; }
    public string WasteCategoryCode { get; set; } = string.Empty;
    public string WasteCategoryName { get; set; } = string.Empty;
    public decimal EstimatedWeightKg { get; set; }
    public decimal ActualWeightKg { get; set; }
    public decimal UnitPricePerKg { get; set; }
    public decimal SubTotal { get; set; }
    /// <summary>true nếu là dòng phát sinh khi cân (không có trong yêu cầu ban đầu).</summary>
    public bool IsExtra { get; set; }
}

public class ReceiptDto
{
    /// <summary>null khi chỉ xem trước (preview).</summary>
    public Guid? ReceiptId { get; set; }
    public string? ReceiptCode { get; set; }
    public Guid RequestId { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public string RequestStatus { get; set; } = string.Empty;

    public List<ReceiptLineDto> Lines { get; set; } = new();
    public decimal TotalWeightKg { get; set; }
    public decimal TotalAmountCash { get; set; }

    public decimal EstimatedWeightKg { get; set; }
    /// <summary>Độ lệch % giữa khối lượng thực tế và ước tính (chỉ tính các dòng có trong yêu cầu).</summary>
    public decimal DeviationPercent { get; set; }
    /// <summary>true khi |độ lệch| vượt ngưỡng {X}% (MSG-HND-04). Chỉ cảnh báo, không chặn.</summary>
    public bool HasDeviationWarning { get; set; }
    public string? DeviationMessage { get; set; }

    /// <summary>Điểm đã cộng (sau khi hoàn tất) hoặc điểm sẽ được cộng (xem trước).</summary>
    public long Points { get; set; }
    public bool PointsCredited { get; set; }

    public List<string> ScaleProofPhotoUrls { get; set; } = new();
    public string? ReceiptImageUrl { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? ConfirmedByUserAt { get; set; }
    /// <summary>true sau khi Completed: không thể sửa/xóa (BR-HND-07).</summary>
    public bool IsLocked { get; set; }
    public bool CanConfirm { get; set; }

    public HandoverPartyDto? Recycler { get; set; }
    public HandoverPartyDto? Requester { get; set; }
}

public class TransactionHistoryItemDto
{
    public Guid ReceiptId { get; set; }
    public string ReceiptCode { get; set; } = string.Empty;
    public Guid RequestId { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public DateTime CompletedAt { get; set; }
    public string CounterpartName { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new();
    public decimal TotalWeightKg { get; set; }
    public decimal TotalAmountCash { get; set; }
    public long PointsAwarded { get; set; }
}

public class TransactionHistoryDto
{
    public List<TransactionHistoryItemDto> Items { get; set; } = new();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    /// <summary>Tổng trên toàn bộ kết quả lọc (không chỉ trang hiện tại).</summary>
    public decimal TotalWeightKg { get; set; }
    public decimal TotalAmountCash { get; set; }
    public long TotalPoints { get; set; }
}
