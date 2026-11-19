using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Lập biên nhận bàn giao phía Recycler (UC R-06).</summary>
[ApiController]
[Route("api/v1/recycler/handover-requests/{requestId:guid}/receipt")]
[Authorize(Roles = "Recycler,Collector")]
[Produces("application/json")]
public class RecyclerReceiptController : ControllerBase
{
    private static readonly string[] AllowedProofExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
    private readonly IHandoverReceiptService _service;
    private readonly ICurrentUserService _currentUser;
    private readonly IFileStorageService? _fileStorage;

    public RecyclerReceiptController(
        IHandoverReceiptService service,
        ICurrentUserService currentUser,
        IFileStorageService? fileStorage = null)
    {
        _service = service;
        _currentUser = currentUser;
        _fileStorage = fileStorage;
    }

    private Guid RecyclerId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>Tải ảnh bằng chứng cân phế liệu lên Cloudflare R2 trước khi lập biên nhận (BR-HND-04).</summary>
    [HttpPost("upload-proof")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<RecyclerDocumentUploadResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<RecyclerDocumentUploadResponseDto>>> UploadProof(
        Guid requestId,
        IFormFile? file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail("Vui lòng chọn tệp ảnh bằng chứng cân hợp lệ.", "MSG-GEN-01"));
        }

        if (file.Length > 10 * 1024 * 1024)
        {
            return BadRequest(ApiResponse<object>.Fail("Dung lượng ảnh bằng chứng cân tối đa 10 MB.", "MSG-GEN-01"));
        }

        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrEmpty(ext) || !AllowedProofExtensions.Contains(ext))
        {
            return BadRequest(ApiResponse<object>.Fail("Chỉ hỗ trợ ảnh định dạng JPG, PNG hoặc WEBP.", "MSG-CLS-01"));
        }

        if (_fileStorage == null)
        {
            return BadRequest(ApiResponse<object>.Fail("Dịch vụ lưu trữ ảnh hiện chưa được cấu hình.", "MSG-GEN-01"));
        }

        await using var stream = file.OpenReadStream();
        var uploaded = await _fileStorage.UploadRecyclerDocumentAsync(
            stream,
            file.FileName,
            file.ContentType,
            $"receipt-proofs/{requestId:N}",
            ct);

        return Ok(ApiResponse<RecyclerDocumentUploadResponseDto>.Ok(
            uploaded,
            "Tải ảnh bằng chứng cân thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>Xem trước biên nhận (không lưu): tổng khối lượng, tổng tiền theo đơn giá đã chốt, độ lệch so với ước tính.</summary>
    [HttpPost("preview")]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Preview(Guid requestId, [FromBody] CreateReceiptRequest request, CancellationToken ct)
    {
        var data = await _service.PreviewAsync(RecyclerId, requestId, request, ct);
        return Ok(ApiResponse<ReceiptDto>.Ok(data, data.DeviationMessage, data.HasDeviationWarning ? "MSG-HND-04" : null));
    }

    /// <summary>
    /// Phát hành biên nhận số (R-06): lưu khối lượng thực tế, khóa đơn giá, chuyển yêu cầu sang AwaitingConfirmation
    /// để người dân xác nhận. Bắt buộc có ít nhất 1 ảnh cân (BR-HND-04).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(Guid requestId, [FromBody] CreateReceiptRequest request, CancellationToken ct)
    {
        var data = await _service.CreateAsync(RecyclerId, requestId, request, ct);
        var body = ApiResponse<ReceiptDto>.Ok(data,
            data.DeviationMessage ?? "Biên nhận đã được gửi để người dân xác nhận.",
            data.HasDeviationWarning ? "MSG-HND-04" : null);
        return CreatedAtAction(nameof(Get), new { requestId }, body);
    }

    /// <summary>Xem biên nhận đã phát hành của yêu cầu.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<ReceiptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid requestId, CancellationToken ct)
    {
        var data = await _service.GetForRecyclerAsync(RecyclerId, requestId, ct);
        return Ok(ApiResponse<ReceiptDto>.Ok(data));
    }
}

/// <summary>Lịch sử giao dịch của Recycler (màn hình Transaction History, UC U-12 phía cơ sở).</summary>
[ApiController]
[Route("api/v1/recycler/transactions")]
[Authorize(Roles = "Recycler,Collector")]
[Produces("application/json")]
public class RecyclerTransactionController : ControllerBase
{
    private readonly IHandoverReceiptService _service;
    private readonly ICurrentUserService _currentUser;

    public RecyclerTransactionController(IHandoverReceiptService service, ICurrentUserService currentUser)
    {
        _service = service;
        _currentUser = currentUser;
    }

    private Guid RecyclerId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    /// <summary>Các giao dịch đã hoàn tất của cơ sở: khối lượng, giá trị, điểm. Lọc theo khoảng ngày, có phân trang và tổng cộng.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<TransactionHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory([FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var data = await _service.GetRecyclerHistoryAsync(RecyclerId, from, to, page, pageSize, ct);
        return Ok(ApiResponse<TransactionHistoryDto>.Ok(data));
    }
}
