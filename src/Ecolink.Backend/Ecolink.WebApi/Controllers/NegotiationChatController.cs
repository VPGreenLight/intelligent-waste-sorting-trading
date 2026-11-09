using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Controller quản lý Thương lượng &amp; Khung giờ thu gom thời gian thực qua Chatbox (Module NEG: U-08 &amp; R-05).
/// Hỗ trợ cả phía Người dân (U-08) và Cơ sở thu gom (R-05), cùng quyền tra cứu bằng chứng của Staff khi đơn Disputed (BR-NEG-01, BR-DSP-06).
/// </summary>
[ApiController]
[Route("api/v1/trade/orders/{orderId:guid}")]
[Authorize(Roles = "Citizen,User,Recycler,Collector,Staff,Admin")]
[Produces("application/json")]
public class NegotiationChatController : ControllerBase
{
    private static readonly string[] AllowedAttachmentContentTypes =
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private const long MaxAttachmentSizeBytes = 5 * 1024 * 1024; // 5 MB

    private readonly INegotiationChatService _chatService;
    private readonly INegotiationChatNotifier _chatNotifier;
    private readonly IFileStorageService _fileStorageService;
    private readonly ICurrentUserService _currentUser;
    private readonly IValidator<SendChatMessageRequest> _messageValidator;
    private readonly IValidator<SendProposalRequest> _proposalValidator;
    private readonly IValidator<RespondProposalRequest> _respondValidator;

    public NegotiationChatController(
        INegotiationChatService chatService,
        INegotiationChatNotifier chatNotifier,
        IFileStorageService fileStorageService,
        ICurrentUserService currentUser,
        IValidator<SendChatMessageRequest> messageValidator,
        IValidator<SendProposalRequest> proposalValidator,
        IValidator<RespondProposalRequest> respondValidator)
    {
        _chatService = chatService;
        _chatNotifier = chatNotifier;
        _fileStorageService = fileStorageService;
        _currentUser = currentUser;
        _messageValidator = messageValidator;
        _proposalValidator = proposalValidator;
        _respondValidator = respondValidator;
    }

    private Guid CallerUserId =>
        _currentUser.UserId ?? throw new UnauthorizedAccessException("Phiên đăng nhập không hợp lệ hoặc đã hết hạn.");

    private string CallerRole => _currentUser.Role ?? string.Empty;

    /// <summary>
    /// [U-08 &amp; R-05] Lấy thông tin phòng chat (trạng thái khóa đề xuất IsProposalLocked, điều khoản hiện tại, đề xuất Pending mới nhất)
    /// và lịch sử tin nhắn bất biến của đơn bàn giao (BR-NEG-01, BR-NEG-02, BR-NEG-03).
    /// </summary>
    [HttpGet("messages")]
    [ProducesResponseType(typeof(ApiResponse<OrderChatThreadDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChatThread(
        Guid orderId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var thread = await _chatService.GetOrderChatThreadAsync(orderId, CallerUserId, CallerRole, page, pageSize, ct);
        return Ok(ApiResponse<OrderChatThreadDto>.Ok(
            thread,
            "Tải lịch sử thương lượng và điều khoản đơn bàn giao thành công.",
            "MSG-NEG-THREAD-OK"));
    }

    /// <summary>
    /// [U-08 &amp; R-05] Gửi tin nhắn văn bản (TEXT) hoặc hình ảnh (IMAGE) trong phạm vi đơn bàn giao.
    /// Kênh chat thông thường vẫn được giữ mở ngay cả sau khi đơn đã Accepted hoặc Completed để hai bên hỏi đường (BR-NEG-02).
    /// </summary>
    [HttpPost("messages")]
    [Authorize(Roles = "Citizen,User,Recycler,Collector")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> SendMessage(
        Guid orderId,
        [FromBody] SendChatMessageRequest request,
        CancellationToken ct = default)
    {
        var validation = await _messageValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu tin nhắn không hợp lệ.", "MSG-GEN-01", errors));
        }

        var (message, recipientNotification) = await _chatService.SendMessageAsync(
            orderId,
            CallerUserId,
            CallerRole,
            request,
            ct);

        await _chatNotifier.NotifyMessageReceivedAsync(orderId, message, recipientNotification, ct);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<ChatMessageDto>.Ok(
            message,
            "Đã gửi tin nhắn thành công.",
            "MSG-NEG-02"));
    }

    /// <summary>
    /// [U-08 &amp; R-05] Tải ảnh thực tế của phế liệu hoặc điểm hẹn lên Cloudflare R2 để đính kèm vào tin nhắn chat (IMAGE).
    /// </summary>
    [HttpPost("messages/attachments")]
    [Authorize(Roles = "Citizen,User,Recycler,Collector")]
    [ProducesResponseType(typeof(ApiResponse<ChatAttachmentUploadResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UploadChatAttachment(
        Guid orderId,
        IFormFile file,
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Vui lòng chọn tệp hình ảnh hợp lệ để đính kèm.",
                "MSG-GEN-01"));
        }

        if (file.Length > MaxAttachmentSizeBytes)
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Kích thước ảnh đính kèm không được vượt quá 5MB.",
                "MSG-GEN-01"));
        }

        if (!AllowedAttachmentContentTypes.Contains(file.ContentType.ToLowerInvariant()))
        {
            return BadRequest(ApiResponse<object>.Fail(
                "Chỉ hỗ trợ định dạng ảnh JPEG (.jpg), PNG (.png) hoặc WebP (.webp).",
                "MSG-GEN-01"));
        }

        var hasAccess = await _chatService.VerifyChatRoomAccessAsync(orderId, CallerUserId, CallerRole, ct);
        if (!hasAccess)
        {
            return StatusCode(StatusCodes.Status403Forbidden, ApiResponse<object>.Fail(
                "Bạn không có quyền tải ảnh đính kèm vào đơn bàn giao này (BR-NEG-01).",
                "MSG-NEG-FORBIDDEN"));
        }

        await using var stream = file.OpenReadStream();
        var uploaded = await _fileStorageService.UploadRecyclerDocumentAsync(
            stream,
            file.FileName,
            file.ContentType,
            $"chat-attachments/{orderId:N}",
            ct);

        var responseData = new ChatAttachmentUploadResponseDto
        {
            AttachmentUrl = uploaded.FileUrl,
            FileName = file.FileName,
            ContentType = file.ContentType,
            FileSizeBytes = file.Length,
            UploadedAt = uploaded.UploadedAt
        };

        return Ok(ApiResponse<ChatAttachmentUploadResponseDto>.Ok(
            responseData,
            "Tải ảnh đính kèm lên hệ thống lưu trữ thành công.",
            "MSG-NEG-UPLOAD-OK"));
    }

    /// <summary>
    /// [U-08 &amp; R-05] Gửi đề xuất / phản đề xuất (PROPOSAL) về đơn giá (VNĐ/kg), khối lượng ước tính (kg) và khung giờ thu gom (BR-NEG-04).
    /// Tự động ghi đè (Superseded) các đề xuất trước đó (BR-NEG-03). Bị khóa sau khi đơn đã chuyển sang Accepted (BR-NEG-02).
    /// </summary>
    [HttpPost("proposals")]
    [Authorize(Roles = "Citizen,User,Recycler,Collector")]
    [ProducesResponseType(typeof(ApiResponse<ChatMessageDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> SendProposal(
        Guid orderId,
        [FromBody] SendProposalRequest request,
        CancellationToken ct = default)
    {
        var validation = await _proposalValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu đề xuất thương lượng không hợp lệ.", "MSG-GEN-01", errors));
        }

        var (proposalMessage, supersededIds, toastMessage, recipientNotification) = await _chatService.SendProposalAsync(
            orderId,
            CallerUserId,
            CallerRole,
            request,
            ct);

        await _chatNotifier.NotifyProposalCreatedAsync(
            orderId,
            proposalMessage,
            supersededIds,
            recipientNotification,
            ct);

        return StatusCode(StatusCodes.Status201Created, ApiResponse<ChatMessageDto>.Ok(
            proposalMessage,
            toastMessage,
            "MSG-NEG-01"));
    }

    /// <summary>
    /// [U-08 &amp; R-05] Đối phương bấm Chấp nhận (IsAccepted = true) hoặc Từ chối (IsAccepted = false) bản đề xuất mới nhất đang hiệu lực (BR-NEG-03).
    /// Khi chấp nhận: lưu cứng đơn giá, khối lượng và khóa khung giờ thu gom chính thức vào đơn (BR-NEG-05, MSG-NEG-03).
    /// </summary>
    [HttpPost("proposals/{messageId:guid}/respond")]
    [Authorize(Roles = "Citizen,User,Recycler,Collector")]
    [ProducesResponseType(typeof(ApiResponse<ProposalAcceptanceResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RespondToProposal(
        Guid orderId,
        Guid messageId,
        [FromBody] RespondProposalRequest request,
        CancellationToken ct = default)
    {
        var validation = await _respondValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => e.ErrorMessage).ToList();
            return BadRequest(ApiResponse<object>.Fail("Dữ liệu phản hồi đề xuất không hợp lệ.", "MSG-GEN-01", errors));
        }

        var (result, toastMessage) = await _chatService.RespondToProposalAsync(
            orderId,
            messageId,
            CallerUserId,
            CallerRole,
            request,
            ct);

        await _chatNotifier.NotifyProposalRespondedAsync(orderId, result, toastMessage, ct);

        return Ok(ApiResponse<ProposalAcceptanceResultDto>.Ok(
            result,
            toastMessage,
            "MSG-NEG-03"));
    }

    /// <summary>
    /// [R-05] Cơ sở thu gom (Recycler) bấm "Chấp nhận" trực tiếp điều khoản &amp; khung giờ đề xuất của đơn bàn giao
    /// để chốt lịch hẹn thu gom chính thức (BR-NEG-05, MSG-NEG-03).
    /// </summary>
    [HttpPost("accept-terms")]
    [Authorize(Roles = "Recycler,Collector")]
    [ProducesResponseType(typeof(ApiResponse<ProposalAcceptanceResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AcceptOrderInitialTerms(
        Guid orderId,
        CancellationToken ct = default)
    {
        var (result, toastMessage) = await _chatService.AcceptOrderInitialTermsAsync(
            orderId,
            CallerUserId,
            CallerRole,
            ct);

        await _chatNotifier.NotifyProposalRespondedAsync(orderId, result, toastMessage, ct);

        return Ok(ApiResponse<ProposalAcceptanceResultDto>.Ok(
            result,
            toastMessage,
            "MSG-NEG-03"));
    }

    /// <summary>
    /// [U-08 &amp; R-05] Đánh dấu toàn bộ tin nhắn chưa đọc của đối phương trong đơn bàn giao là đã đọc.
    /// </summary>
    [HttpPut("messages/read")]
    [Authorize(Roles = "Citizen,User,Recycler,Collector")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkMessagesAsRead(
        Guid orderId,
        CancellationToken ct = default)
    {
        var markedCount = await _chatService.MarkMessagesAsReadAsync(orderId, CallerUserId, CallerRole, ct);
        var readAtUtc = DateTime.UtcNow;

        if (markedCount > 0)
        {
            await _chatNotifier.NotifyMessagesReadAsync(orderId, CallerUserId, markedCount, readAtUtc, ct);
        }

        return Ok(ApiResponse<object>.Ok(
            new { orderId, markedCount, readAtUtc },
            $"Đã đánh dấu đã đọc {markedCount} tin nhắn.",
            "MSG-NEG-READ-OK"));
    }
}

