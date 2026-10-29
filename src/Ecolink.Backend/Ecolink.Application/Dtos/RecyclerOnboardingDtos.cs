namespace Ecolink.Application.Dtos;

/// <summary>
/// Request DTO cho Use Case U-17: Người dân nộp hoặc nộp lại hồ sơ nâng cấp lên Recycler
/// </summary>
public class ApplyRecyclerUpgradeRequest
{
    public string FacilityName { get; set; } = string.Empty;
    public string RepresentativeName { get; set; } = string.Empty;
    public string BusinessAddress { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string ContactPhone { get; set; } = string.Empty;
    public string? OperatingHours { get; set; } = "08:00 - 18:00";
    public string? Description { get; set; }
    public string LegalPermitUrl { get; set; } = string.Empty;
    public string BusinessPhotoUrl { get; set; } = string.Empty;
}

/// <summary>
/// Request DTO cho Use Case S-04: Staff phê duyệt hoặc từ chối hồ sơ đăng ký Recycler
/// </summary>
public class VerifyRecyclerApplicationRequest
{
    public bool IsApproved { get; set; }
    public string? RejectionReason { get; set; }
}

/// <summary>
/// Query Filter DTO cho danh sách hồ sơ Recycler (chuẩn phân trang mục 4 api_design_and_response_contract_guidelines.md)
/// </summary>
public class RecyclerApplicationFilterRequest
{
    public string? Status { get; set; } // Pending, Verified, Rejected
    public string? Keyword { get; set; } // Tìm theo tên cơ sở, địa chỉ, email, SĐT
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string SortBy { get; set; } = "createdAt";
    public string SortOrder { get; set; } = "asc"; // Ưu tiên đơn nộp trước (FIFO)
}

/// <summary>
/// Response DTO hiển thị chi tiết hồ sơ đăng ký Recycler cho cả User (U-17) và Staff (S-04)
/// </summary>
public class RecyclerApplicationDetailDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ApplicantFullName { get; set; } = string.Empty;
    public string CurrentRole { get; set; } = string.Empty;

    public string FacilityName { get; set; } = string.Empty;
    public string? RepresentativeName { get; set; }
    public string BusinessAddress { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public string OperatingHours { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LegalPermitUrl { get; set; }
    public string? BusinessPhotoUrl { get; set; }

    public string VerificationStatus { get; set; } = string.Empty;
    public string ActiveStatus { get; set; } = string.Empty;
    public string? RejectionReason { get; set; }

    public DateTime SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
}

/// <summary>
/// Response DTO khi tải chứng từ/ảnh cơ sở lên Cloudflare R2 thành công
/// </summary>
public class RecyclerDocumentUploadResponseDto
{
    public string FileUrl { get; set; } = string.Empty;
    public string ObjectKey { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Khung dữ liệu phân trang chuẩn toàn hệ thống
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalRecords / PageSize) : 0;
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}

