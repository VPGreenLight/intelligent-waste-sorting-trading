using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Thực thi nghiệp vụ Module: In-App Real-Time Negotiation Chatbox (U-08 &amp; R-05).
/// Đảm bảo tuân thủ nghiêm ngặt các quy tắc BR-NEG-01..05, BR-HND-02, BR-DSP-06 và thông điệp MSG-NEG-01..04.
/// </summary>
public class NegotiationChatService : INegotiationChatService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<NegotiationChatService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public NegotiationChatService(
        ApplicationDbContext db,
        ILogger<NegotiationChatService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<OrderChatThreadDto> GetOrderChatThreadAsync(
        Guid orderId,
        Guid callerUserId,
        string callerRole,
        int page = 1,
        int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var context = await LoadAndAuthorizeOrderContextAsync(
            orderId,
            callerUserId,
            callerRole,
            allowStaffOnDisputed: true,
            ct);

        var query = _db.ChatMessages
            .AsNoTracking()
            .Include(m => m.SenderUser)
            .Where(m => m.TradeOrderId == orderId);

        var totalCount = await query.CountAsync(ct);

        var unreadCount = await _db.ChatMessages
            .AsNoTracking()
            .CountAsync(m => m.TradeOrderId == orderId && m.SenderUserId != callerUserId && !m.IsRead, ct);

        var activeProposalEntity = await _db.ChatMessages
            .AsNoTracking()
            .Include(m => m.SenderUser)
            .Where(m => m.TradeOrderId == orderId
                        && m.MessageType == ChatMessageType.Proposal
                        && m.ProposalStatus == ProposalStatus.Pending)
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync(ct);

        var items = await query
            .OrderBy(m => m.SentAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var messageDtos = items
            .Select(m => MapToMessageDto(m, context.CitizenName, context.FacilityName, context.Order.UserId, context.RecyclerUserId))
            .ToList();

        return new OrderChatThreadDto
        {
            OrderId = context.Order.Id,
            CitizenUserId = context.Order.UserId,
            CitizenName = context.CitizenName,
            RecyclerUserId = context.RecyclerUserId,
            FacilityName = context.FacilityName,
            OrderStatus = context.Order.Status.ToString(),
            CategoryCode = context.Order.CategoryCode,
            EstimatedWeightKg = context.Order.EstimatedWeightKg,
            InitialPricePerKg = context.Order.InitialPricePerKg,
            AgreedPricePerKg = context.Order.AgreedPricePerKg,
            ProposedTimeSlot = context.Order.ProposedTimeSlot,
            ConfirmedTimeSlot = context.Order.ConfirmedTimeSlot,
            PickupAddress = context.Order.PickupAddress,
            ScheduledAt = context.Order.ScheduledAt,
            AcceptedAt = context.Order.AcceptedAt,
            IsProposalLocked = !IsProposalAllowed(context.Order.Status),
            IsChatOpen = IsGeneralChatAllowed(context.Order.Status),
            UnreadCount = unreadCount,
            ActiveProposal = activeProposalEntity != null
                ? MapToMessageDto(activeProposalEntity, context.CitizenName, context.FacilityName, context.Order.UserId, context.RecyclerUserId)
                : null,
            Messages = new PagedResult<ChatMessageDto>
            {
                Items = messageDtos,
                TotalRecords = totalCount,
                PageNumber = page,
                PageSize = pageSize
            }
        };
    }

    public async Task<(ChatMessageDto Message, string RecipientNotification)> SendMessageAsync(
        Guid orderId,
        Guid senderUserId,
        string senderRole,
        SendChatMessageRequest request,
        CancellationToken ct = default)
    {
        var context = await LoadAndAuthorizeOrderContextAsync(
            orderId,
            senderUserId,
            senderRole,
            allowStaffOnDisputed: false,
            ct);

        // BR-NEG-02: Kênh chat thông thường (TEXT/IMAGE) vẫn được giữ mở sau khi đã Accepted hoặc Completed,
        // chỉ đóng khi đơn đã bị hủy/từ chối/hết hạn (Rejected, Cancelled, Expired).
        if (!IsGeneralChatAllowed(context.Order.Status))
        {
            throw new BusinessRuleException(
                "BR-NEG-02",
                "MSG-NEG-CHAT-CLOSED",
                $"Đơn bàn giao đang ở trạng thái '{context.Order.Status}' nên không thể gửi tin nhắn mới.");
        }

        var isImage = string.Equals(request.MessageType?.Trim(), "Image", StringComparison.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        var chatMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            TradeOrderId = orderId,
            SenderUserId = senderUserId,
            SenderRole = context.IsCallerCitizen ? UserRole.Citizen.ToString() : UserRole.Recycler.ToString(),
            MessageType = isImage ? ChatMessageType.Image : ChatMessageType.Text,
            MessageContent = request.MessageContent?.Trim(),
            AttachmentUrl = isImage ? request.AttachmentUrl?.Trim() : null,
            IsRead = false,
            SentAt = now,
            CreatedAt = now
        };

        _db.ChatMessages.Add(chatMessage);
        await _db.SaveChangesAsync(ct);

        var senderDisplayName = context.IsCallerCitizen ? context.CitizenName : context.FacilityName;
        var notification = $"Bạn có tin nhắn mới từ {senderDisplayName}."; // MSG-NEG-02

        _logger.LogInformation(
            "[NEG] Tin nhắn {Type} mới trong đơn {OrderId} từ {SenderRole} ({SenderId}).",
            chatMessage.MessageType, orderId, chatMessage.SenderRole, senderUserId);

        var dto = MapToMessageDto(chatMessage, context.CitizenName, context.FacilityName, context.Order.UserId, context.RecyclerUserId);
        return (dto, notification);
    }

    public async Task<(ChatMessageDto ProposalMessage, IReadOnlyList<Guid> SupersededMessageIds, string ToastMessage, string RecipientNotification)> SendProposalAsync(
        Guid orderId,
        Guid senderUserId,
        string senderRole,
        SendProposalRequest request,
        CancellationToken ct = default)
    {
        var context = await LoadAndAuthorizeOrderContextAsync(
            orderId,
            senderUserId,
            senderRole,
            allowStaffOnDisputed: false,
            ct);

        // BR-NEG-02: Việc đưa ra hoặc sửa đổi đề xuất giá/khung giờ chỉ được phép khi yêu cầu ở trạng thái Pending hoặc Negotiating,
        // và sẽ bị khóa khi yêu cầu chuyển sang Accepted.
        if (!IsProposalAllowed(context.Order.Status))
        {
            throw new BusinessRuleException(
                "BR-NEG-02",
                "MSG-NEG-PROPOSAL-LOCKED",
                $"Đơn bàn giao đang ở trạng thái '{context.Order.Status}'. Việc đưa ra hoặc sửa đổi đề xuất giá/khung giờ chỉ được phép khi đơn đang 'Pending' hoặc 'Negotiating' (BR-NEG-02).");
        }

        var now = DateTime.UtcNow;

        // BR-NEG-03: Chỉ đề xuất mới nhất có hiệu lực; mỗi đề xuất phản hồi mới sẽ ghi đè (Superseded) đề xuất Pending trước đó.
        var previousPendingProposals = await _db.ChatMessages
            .Where(m => m.TradeOrderId == orderId
                        && m.MessageType == ChatMessageType.Proposal
                        && m.ProposalStatus == ProposalStatus.Pending)
            .ToListAsync(ct);

        var supersededIds = new List<Guid>();
        foreach (var prev in previousPendingProposals)
        {
            prev.ProposalStatus = ProposalStatus.Superseded;
            prev.UpdatedAt = now;
            prev.ProposalDataJson = SerializeProposalPayload(new NegotiationProposalPayloadDto
            {
                ProposedPricePerKg = prev.ProposedPricePerKg ?? 0m,
                ProposedWeightKg = prev.ProposedWeightKg ?? context.Order.EstimatedWeightKg,
                ProposedTimeSlot = prev.ProposedTimeSlot ?? string.Empty,
                Note = prev.MessageContent,
                ProposalStatus = ProposalStatus.Superseded.ToString()
            });
            supersededIds.Add(prev.Id);
        }

        // BR-HND-02 & BR-NEG-04: Chuyển trạng thái đơn từ Pending -> Negotiating và cập nhật khung giờ đề xuất mới nhất
        if (context.Order.Status == OrderStatus.Pending)
        {
            context.Order.Status = OrderStatus.Negotiating;
        }

        var normalizedTimeSlot = request.ProposedTimeSlot.Trim();
        context.Order.ProposedTimeSlot = normalizedTimeSlot;
        context.Order.UpdatedAt = now;

        var payload = new NegotiationProposalPayloadDto
        {
            ProposedPricePerKg = request.ProposedPricePerKg,
            ProposedWeightKg = request.ProposedWeightKg,
            ProposedTimeSlot = normalizedTimeSlot,
            Note = request.Note?.Trim(),
            ProposalStatus = ProposalStatus.Pending.ToString()
        };

        var proposalMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            TradeOrderId = orderId,
            SenderUserId = senderUserId,
            SenderRole = context.IsCallerCitizen ? UserRole.Citizen.ToString() : UserRole.Recycler.ToString(),
            MessageType = ChatMessageType.Proposal,
            MessageContent = request.Note?.Trim()
                             ?? $"Đề xuất thương lượng: {request.ProposedPricePerKg:N0} VNĐ/kg • {request.ProposedWeightKg:N1} kg • Khung giờ: {normalizedTimeSlot}",
            ProposalStatus = ProposalStatus.Pending,
            ProposedPricePerKg = request.ProposedPricePerKg,
            ProposedWeightKg = request.ProposedWeightKg,
            ProposedTimeSlot = normalizedTimeSlot,
            ProposalDataJson = SerializeProposalPayload(payload),
            IsRead = false,
            SentAt = now,
            CreatedAt = now
        };

        _db.ChatMessages.Add(proposalMessage);
        await HandoverTradeSyncHelper.SyncNegotiationToHandoverAsync(
            _db,
            context.Order,
            agreedPricePerKg: null,
            proposedWeightKg: null,
            timeSlotText: normalizedTimeSlot,
            ct);
        await _db.SaveChangesAsync(ct);

        var senderDisplayName = context.IsCallerCitizen ? context.CitizenName : context.FacilityName;
        var toastMessage = context.IsCallerCitizen
            ? $"Yêu cầu trao đổi của bạn đã được gửi đến {context.FacilityName}." // MSG-NEG-01
            : $"Đã gửi đề xuất thương lượng mới đến {context.CitizenName}.";
        var recipientNotification = $"Bạn có tin nhắn mới từ {senderDisplayName}."; // MSG-NEG-02

        _logger.LogInformation(
            "[NEG] Đề xuất thương lượng mới {ProposalId} trong đơn {OrderId} (Ghi đè {SupersededCount} đề xuất cũ).",
            proposalMessage.Id, orderId, supersededIds.Count);

        var dto = MapToMessageDto(proposalMessage, context.CitizenName, context.FacilityName, context.Order.UserId, context.RecyclerUserId);
        return (dto, supersededIds, toastMessage, recipientNotification);
    }

    public async Task<(ProposalAcceptanceResultDto Result, string ToastMessage)> RespondToProposalAsync(
        Guid orderId,
        Guid proposalMessageId,
        Guid responderUserId,
        string responderRole,
        RespondProposalRequest request,
        CancellationToken ct = default)
    {
        var context = await LoadAndAuthorizeOrderContextAsync(
            orderId,
            responderUserId,
            responderRole,
            allowStaffOnDisputed: false,
            ct);

        // BR-NEG-02: Chỉ cho phép phản hồi đề xuất khi đơn đang Pending hoặc Negotiating
        if (!IsProposalAllowed(context.Order.Status))
        {
            throw new BusinessRuleException(
                "BR-NEG-02",
                "MSG-NEG-PROPOSAL-LOCKED",
                $"Đơn bàn giao đang ở trạng thái '{context.Order.Status}' nên không thể phản hồi đề xuất.");
        }

        var proposalMessage = await _db.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == proposalMessageId
                                      && m.TradeOrderId == orderId
                                      && m.MessageType == ChatMessageType.Proposal, ct)
            ?? throw new NotFoundException("Không tìm thấy bản đề xuất thương lượng trong đơn bàn giao này.");

        // BR-NEG-03: Người gửi không được tự chấp nhận đề xuất của chính mình
        if (proposalMessage.SenderUserId == responderUserId)
        {
            throw new BusinessRuleException(
                "BR-NEG-03",
                "MSG-NEG-SELF-RESPONSE",
                "Bạn không thể tự phản hồi đề xuất do chính mình tạo ra. Chỉ đối phương mới có quyền chấp nhận hoặc từ chối.");
        }

        // BR-NEG-03: Chỉ đề xuất mới nhất đang Pending mới có hiệu lực
        if (proposalMessage.ProposalStatus == ProposalStatus.Superseded)
        {
            throw new BusinessRuleException(
                "BR-NEG-03",
                "MSG-NEG-SUPERSEDED",
                "Đề xuất này đã bị ghi đè bởi một đề xuất mới hơn. Chỉ đề xuất mới nhất đang hiệu lực mới có thể chấp nhận (BR-NEG-03).");
        }

        if (proposalMessage.ProposalStatus != ProposalStatus.Pending)
        {
            throw new BusinessRuleException(
                "BR-NEG-03",
                "MSG-NEG-ALREADY-RESPONDED",
                $"Đề xuất này đã ở trạng thái '{proposalMessage.ProposalStatus}' và không thể phản hồi lại.");
        }

        var now = DateTime.UtcNow;
        var responderDisplayName = context.IsCallerCitizen ? context.CitizenName : context.FacilityName;
        ChatMessage systemMessage;
        string toastMessage;

        if (request.IsAccepted)
        {
            proposalMessage.ProposalStatus = ProposalStatus.Accepted;
            proposalMessage.IsRead = true;
            proposalMessage.ReadAt = now;
            proposalMessage.UpdatedAt = now;

            // BR-NEG-03 & BR-NEG-05: Lưu cứng (Snapshot) các điều khoản đã thống nhất và khóa khung giờ thu gom chính thức vào đơn
            context.Order.AgreedPricePerKg = proposalMessage.ProposedPricePerKg ?? context.Order.InitialPricePerKg;
            context.Order.EstimatedWeightKg = proposalMessage.ProposedWeightKg ?? context.Order.EstimatedWeightKg;
            context.Order.ConfirmedTimeSlot = !string.IsNullOrWhiteSpace(proposalMessage.ProposedTimeSlot)
                ? proposalMessage.ProposedTimeSlot
                : context.Order.ProposedTimeSlot ?? context.Order.ScheduledAt.ToString("yyyy-MM-dd HH:mm");
            context.Order.Status = OrderStatus.Accepted;
            context.Order.AcceptedAt = now;
            context.Order.UpdatedAt = now;

            toastMessage = $"{responderDisplayName} đã chấp nhận đề nghị của bạn."; // MSG-NEG-03

            systemMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                TradeOrderId = orderId,
                SenderUserId = responderUserId,
                SenderRole = "System",
                MessageType = ChatMessageType.System,
                MessageContent = $"{toastMessage} Điều khoản đã khóa: {context.Order.AgreedPricePerKg:N0} VNĐ/kg • {context.Order.EstimatedWeightKg:N1} kg • Khung giờ hẹn: {context.Order.ConfirmedTimeSlot}.",
                IsRead = false,
                SentAt = now,
                CreatedAt = now
            };
        }
        else
        {
            proposalMessage.ProposalStatus = ProposalStatus.Rejected;
            proposalMessage.IsRead = true;
            proposalMessage.ReadAt = now;
            proposalMessage.UpdatedAt = now;

            context.Order.Status = OrderStatus.Negotiating;
            context.Order.UpdatedAt = now;

            toastMessage = $"{responderDisplayName} đã từ chối đề nghị và yêu cầu thương lượng lại.";
            var reasonSuffix = !string.IsNullOrWhiteSpace(request.ResponseNote)
                ? $" Lý do: {request.ResponseNote.Trim()}"
                : string.Empty;

            systemMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                TradeOrderId = orderId,
                SenderUserId = responderUserId,
                SenderRole = "System",
                MessageType = ChatMessageType.System,
                MessageContent = $"{toastMessage}{reasonSuffix}",
                IsRead = false,
                SentAt = now,
                CreatedAt = now
            };
        }

        proposalMessage.ProposalDataJson = SerializeProposalPayload(new NegotiationProposalPayloadDto
        {
            ProposedPricePerKg = proposalMessage.ProposedPricePerKg ?? 0m,
            ProposedWeightKg = proposalMessage.ProposedWeightKg ?? context.Order.EstimatedWeightKg,
            ProposedTimeSlot = proposalMessage.ProposedTimeSlot ?? string.Empty,
            Note = proposalMessage.MessageContent,
            ProposalStatus = proposalMessage.ProposalStatus.Value.ToString(),
            RespondedByUserId = responderUserId,
            RespondedAt = now,
            ResponseNote = request.ResponseNote?.Trim()
        });

        _db.ChatMessages.Add(systemMessage);
        await HandoverTradeSyncHelper.SyncNegotiationToHandoverAsync(
            _db,
            context.Order,
            agreedPricePerKg: request.IsAccepted ? context.Order.AgreedPricePerKg : null,
            proposedWeightKg: request.IsAccepted ? context.Order.EstimatedWeightKg : null,
            timeSlotText: request.IsAccepted ? context.Order.ConfirmedTimeSlot : null,
            ct);
        await _db.SaveChangesAsync(ct);

        var resultDto = new ProposalAcceptanceResultDto
        {
            OrderId = context.Order.Id,
            ProposalMessageId = proposalMessage.Id,
            ProposalStatus = proposalMessage.ProposalStatus.Value.ToString(),
            OrderStatus = context.Order.Status.ToString(),
            AgreedPricePerKg = context.Order.AgreedPricePerKg,
            ConfirmedWeightKg = context.Order.EstimatedWeightKg,
            ConfirmedTimeSlot = context.Order.ConfirmedTimeSlot,
            AcceptedAt = context.Order.AcceptedAt,
            IsProposalLocked = !IsProposalAllowed(context.Order.Status),
            SystemMessage = MapToMessageDto(systemMessage, context.CitizenName, context.FacilityName, context.Order.UserId, context.RecyclerUserId)
        };

        return (resultDto, toastMessage);
    }

    public async Task<(ProposalAcceptanceResultDto Result, string ToastMessage)> AcceptOrderInitialTermsAsync(
        Guid orderId,
        Guid recyclerUserId,
        string recyclerRole,
        CancellationToken ct = default)
    {
        var context = await LoadAndAuthorizeOrderContextAsync(
            orderId,
            recyclerUserId,
            recyclerRole,
            allowStaffOnDisputed: false,
            ct);

        if (context.IsCallerCitizen)
        {
            throw new ForbiddenAccessException(
                "BR-NEG-05",
                "MSG-NEG-ONLY-RECYCLER",
                "Chỉ cơ sở thu gom (Recycler) nhận đơn mới có quyền bấm chấp nhận yêu cầu bàn giao ban đầu (BR-NEG-05).");
        }

        if (!IsProposalAllowed(context.Order.Status))
        {
            throw new BusinessRuleException(
                "BR-NEG-02",
                "MSG-NEG-PROPOSAL-LOCKED",
                $"Đơn bàn giao đang ở trạng thái '{context.Order.Status}' nên không thể chốt lại điều khoản.");
        }

        // Nếu đang có đề xuất Pending do Citizen gửi, tự động chấp nhận đề xuất đó
        var latestPendingProposal = await _db.ChatMessages
            .Where(m => m.TradeOrderId == orderId
                        && m.MessageType == ChatMessageType.Proposal
                        && m.ProposalStatus == ProposalStatus.Pending)
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync(ct);

        if (latestPendingProposal != null && latestPendingProposal.SenderUserId != recyclerUserId)
        {
            return await RespondToProposalAsync(
                orderId,
                latestPendingProposal.Id,
                recyclerUserId,
                recyclerRole,
                new RespondProposalRequest { IsAccepted = true },
                ct);
        }

        var now = DateTime.UtcNow;
        if (latestPendingProposal != null)
        {
            latestPendingProposal.ProposalStatus = ProposalStatus.Accepted;
            latestPendingProposal.UpdatedAt = now;
        }

        // BR-NEG-05: Khung giờ thu gom chính thức được chốt tại thời điểm Recycler bấm "Chấp nhận" yêu cầu bàn giao
        context.Order.AgreedPricePerKg ??= context.Order.InitialPricePerKg;
        context.Order.ConfirmedTimeSlot = !string.IsNullOrWhiteSpace(context.Order.ProposedTimeSlot)
            ? context.Order.ProposedTimeSlot
            : context.Order.ScheduledAt.ToString("yyyy-MM-dd HH:mm");
        context.Order.Status = OrderStatus.Accepted;
        context.Order.AcceptedAt = now;
        context.Order.UpdatedAt = now;

        var toastMessage = $"{context.FacilityName} đã chấp nhận đề nghị của bạn."; // MSG-NEG-03

        var systemMessage = new ChatMessage
        {
            Id = Guid.NewGuid(),
            TradeOrderId = orderId,
            SenderUserId = recyclerUserId,
            SenderRole = "System",
            MessageType = ChatMessageType.System,
            MessageContent = $"{toastMessage} Khung giờ thu gom chính thức đã chốt: {context.Order.ConfirmedTimeSlot}.",
            IsRead = false,
            SentAt = now,
            CreatedAt = now
        };

        _db.ChatMessages.Add(systemMessage);
        await HandoverTradeSyncHelper.SyncNegotiationToHandoverAsync(
            _db,
            context.Order,
            agreedPricePerKg: context.Order.AgreedPricePerKg,
            proposedWeightKg: context.Order.EstimatedWeightKg,
            timeSlotText: context.Order.ConfirmedTimeSlot,
            ct);
        await _db.SaveChangesAsync(ct);

        var resultDto = new ProposalAcceptanceResultDto
        {
            OrderId = context.Order.Id,
            ProposalMessageId = latestPendingProposal?.Id,
            ProposalStatus = ProposalStatus.Accepted.ToString(),
            OrderStatus = context.Order.Status.ToString(),
            AgreedPricePerKg = context.Order.AgreedPricePerKg,
            ConfirmedWeightKg = context.Order.EstimatedWeightKg,
            ConfirmedTimeSlot = context.Order.ConfirmedTimeSlot,
            AcceptedAt = context.Order.AcceptedAt,
            IsProposalLocked = true,
            SystemMessage = MapToMessageDto(systemMessage, context.CitizenName, context.FacilityName, context.Order.UserId, context.RecyclerUserId)
        };

        return (resultDto, toastMessage);
    }

    public async Task<int> MarkMessagesAsReadAsync(
        Guid orderId,
        Guid readerUserId,
        string readerRole,
        CancellationToken ct = default)
    {
        await LoadAndAuthorizeOrderContextAsync(
            orderId,
            readerUserId,
            readerRole,
            allowStaffOnDisputed: false,
            ct);

        var unreadMessages = await _db.ChatMessages
            .Where(m => m.TradeOrderId == orderId && m.SenderUserId != readerUserId && !m.IsRead)
            .ToListAsync(ct);

        if (unreadMessages.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
            msg.ReadAt = now;
            msg.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return unreadMessages.Count;
    }

    public async Task<bool> VerifyChatRoomAccessAsync(
        Guid orderId,
        Guid callerUserId,
        string callerRole,
        CancellationToken ct = default)
    {
        try
        {
            await LoadAndAuthorizeOrderContextAsync(
                orderId,
                callerUserId,
                callerRole,
                allowStaffOnDisputed: true,
                ct);
            return true;
        }
        catch (Exception ex) when (ex is ForbiddenAccessException or NotFoundException)
        {
            return false;
        }
    }

    // ============================================================
    // PRIVATE HELPERS
    // ============================================================

    private static bool IsProposalAllowed(OrderStatus status) =>
        status is OrderStatus.Pending or OrderStatus.Negotiating;

    private static bool IsGeneralChatAllowed(OrderStatus status) =>
        status is OrderStatus.Pending
            or OrderStatus.Negotiating
            or OrderStatus.Accepted
            or OrderStatus.Scheduled
            or OrderStatus.AwaitingConfirmation
            or OrderStatus.Completed
            or OrderStatus.Disputed;

    private async Task<OrderAccessContext> LoadAndAuthorizeOrderContextAsync(
        Guid orderId,
        Guid callerUserId,
        string callerRole,
        bool allowStaffOnDisputed,
        CancellationToken ct)
    {
        var order = await HandoverTradeSyncHelper.EnsureTradeOrderForChatAsync(_db, orderId, ct)
            ?? throw new NotFoundException($"Không tìm thấy yêu cầu bàn giao '{orderId}'.");

        // Xác định RecyclerUserId và FacilityName từ RecyclerProfile hoặc CollectionPartner
        Guid recyclerUserId;
        string facilityName;

        if (order.RecyclerProfile != null)
        {
            recyclerUserId = order.RecyclerProfile.UserId;
            facilityName = order.RecyclerProfile.FacilityName;
        }
        else if (order.Partner != null)
        {
            recyclerUserId = order.Partner.UserId;
            facilityName = order.Partner.FacilityName;
        }
        else
        {
            var profile = await _db.RecyclerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == order.PartnerId || r.UserId == order.RecyclerUserId, ct);

            if (profile != null)
            {
                recyclerUserId = profile.UserId;
                facilityName = profile.FacilityName;
            }
            else
            {
                recyclerUserId = order.RecyclerUserId ?? order.PartnerId;
                facilityName = "Cơ sở thu gom";
            }
        }

        var citizenName = !string.IsNullOrWhiteSpace(order.User?.FullName)
            ? order.User.FullName
            : order.User?.Username ?? "Người dân";

        var isCallerCitizen = callerUserId == order.UserId;
        var isCallerRecycler = callerUserId == recyclerUserId || callerUserId == order.PartnerId;
        var isStaffOrAdmin = string.Equals(callerRole, "Staff", StringComparison.OrdinalIgnoreCase)
                             || string.Equals(callerRole, "Admin", StringComparison.OrdinalIgnoreCase);

        // BR-NEG-01 & BR-DSP-06: Chỉ hiển thị cho User và Recycler của yêu cầu đó (hoặc Staff khi đơn đang Disputed)
        if (!isCallerCitizen && !isCallerRecycler)
        {
            if (allowStaffOnDisputed && isStaffOrAdmin && order.Status == OrderStatus.Disputed)
            {
                return new OrderAccessContext(order, recyclerUserId, citizenName, facilityName, false);
            }

            throw new ForbiddenAccessException(
                "BR-NEG-01",
                "MSG-NEG-FORBIDDEN",
                "Tin nhắn thương lượng chỉ hiển thị cho Người dân và Cơ sở thu gom thuộc yêu cầu bàn giao này (BR-NEG-01).");
        }

        return new OrderAccessContext(order, recyclerUserId, citizenName, facilityName, isCallerCitizen);
    }

    private static ChatMessageDto MapToMessageDto(
        ChatMessage m,
        string citizenName,
        string facilityName,
        Guid citizenUserId,
        Guid recyclerUserId)
    {
        string senderName;
        if (string.Equals(m.SenderRole, "System", StringComparison.OrdinalIgnoreCase) || m.MessageType == ChatMessageType.System)
        {
            senderName = "Hệ thống EcoLink";
        }
        else if (m.SenderUserId == citizenUserId)
        {
            senderName = citizenName;
        }
        else if (m.SenderUserId == recyclerUserId)
        {
            senderName = facilityName;
        }
        else
        {
            senderName = m.SenderUser?.FullName ?? m.SenderRole;
        }

        NegotiationProposalPayloadDto? proposal = null;
        if (m.MessageType == ChatMessageType.Proposal)
        {
            if (!string.IsNullOrWhiteSpace(m.ProposalDataJson))
            {
                try
                {
                    proposal = JsonSerializer.Deserialize<NegotiationProposalPayloadDto>(m.ProposalDataJson, JsonOptions);
                }
                catch
                {
                    // Fallback nếu JSON cũ không khớp cấu trúc
                }
            }

            proposal ??= new NegotiationProposalPayloadDto
            {
                ProposedPricePerKg = m.ProposedPricePerKg ?? 0m,
                ProposedWeightKg = m.ProposedWeightKg ?? 0m,
                ProposedTimeSlot = m.ProposedTimeSlot ?? string.Empty,
                Note = m.MessageContent,
                ProposalStatus = (m.ProposalStatus ?? ProposalStatus.Pending).ToString()
            };

            if (m.ProposalStatus.HasValue)
            {
                proposal.ProposalStatus = m.ProposalStatus.Value.ToString();
            }
        }

        return new ChatMessageDto
        {
            Id = m.Id,
            OrderId = m.TradeOrderId,
            SenderUserId = m.SenderUserId,
            SenderName = senderName,
            SenderRole = m.SenderRole,
            MessageType = m.MessageType.ToString(),
            MessageContent = m.MessageContent,
            AttachmentUrl = m.AttachmentUrl,
            Proposal = proposal,
            IsRead = m.IsRead,
            SentAt = m.SentAt,
            ReadAt = m.ReadAt
        };
    }

    private static string SerializeProposalPayload(NegotiationProposalPayloadDto payload) =>
        JsonSerializer.Serialize(payload, JsonOptions);

    private sealed record OrderAccessContext(
        TradeOrder Order,
        Guid RecyclerUserId,
        string CitizenName,
        string FacilityName,
        bool IsCallerCitizen);
}
