using System.Globalization;
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
/// Dịch vụ Khiếu nại Giao dịch 2 chiều (U-11) &amp; Hồ sơ Trọng tài 360 độ của Staff (S-05).
/// Tuân thủ BR-DSP-01, BR-DSP-02, BR-DSP-04, BR-DSP-06, BR-GAM-01, BR-GAM-03, BR-ADM-03, BR-ADM-05.
/// </summary>
public class ComplaintService : IComplaintService
{
    public const int ComplaintWindowDays = 7;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _db;
    private readonly IFileStorageService _fileStorage;
    private readonly IAuditLogService _auditLogService;
    private readonly IReviewService _reviewService;
    private readonly ILogger<ComplaintService> _logger;
    private readonly IPointsService? _pointsService;

    public ComplaintService(
        ApplicationDbContext db,
        IFileStorageService fileStorage,
        IAuditLogService auditLogService,
        IReviewService reviewService,
        ILogger<ComplaintService> logger,
        IPointsService? pointsService = null)
    {
        _db = db;
        _fileStorage = fileStorage;
        _auditLogService = auditLogService;
        _reviewService = reviewService;
        _logger = logger;
        _pointsService = pointsService;
    }

    public Task<RecyclerDocumentUploadResponseDto> UploadEvidenceAsync(
        Stream fileStream,
        string originalFileName,
        string contentType,
        CancellationToken ct = default)
    {
        return _fileStorage.UploadRecyclerDocumentAsync(
            fileStream,
            originalFileName,
            contentType,
            "complaint-evidences",
            ct);
    }

    public async Task<ComplaintDetailDto> SubmitComplaintAsync(
        Guid callerUserId,
        SubmitComplaintRequest request,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<ComplaintCategory>(request.Category?.Trim(), ignoreCase: true, out var category))
        {
            throw new BusinessRuleException(
                "BR-DSP-02",
                "MSG-GEN-01",
                "Nhóm nguyên nhân khiếu nại không hợp lệ.");
        }

        var handover = await _db.HandoverRequests
            .FirstOrDefaultAsync(h => h.Id == request.HandoverRequestId, ct)
            ?? throw new NotFoundException("Không tìm thấy yêu cầu bàn giao liên quan.");

        string complainantRole;
        Guid respondentId;
        if (handover.UserId == callerUserId)
        {
            complainantRole = "Citizen";
            respondentId = handover.RecyclerId;
        }
        else if (handover.RecyclerId == callerUserId)
        {
            complainantRole = "Recycler";
            respondentId = handover.UserId;
        }
        else
        {
            throw new ForbiddenAccessException(
                "BR-DSP-01",
                "MSG-ADM-04",
                "Chỉ người dân hoặc cơ sở thu mua trực tiếp tham gia đơn bàn giao mới có quyền gửi khiếu nại.");
        }

        var isAllowedStatus = handover.Status is HandoverStatus.AwaitingConfirmation
            or HandoverStatus.Disputed
            or HandoverStatus.Completed
            or HandoverStatus.Accepted
            or HandoverStatus.Scheduled
            || (handover.Status == HandoverStatus.Cancelled && category is ComplaintCategory.NoShow or ComplaintCategory.Misconduct);

        if (!isAllowedStatus)
        {
            throw new BusinessRuleException(
                "BR-DSP-01",
                "MSG-DSP-03",
                "Bạn không thể khiếu nại giao dịch này nữa (quá thời hạn 7 ngày hoặc trạng thái không hợp lệ).");
        }

        var receipt = await _db.HandoverReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == handover.Id, ct);

        var timeAnchor = receipt?.ConfirmedByUserAt ?? handover.ActualHandoverAt ?? handover.UpdatedAt;
        if ((DateTime.UtcNow - timeAnchor) > TimeSpan.FromDays(ComplaintWindowDays))
        {
            throw new BusinessRuleException(
                "BR-DSP-01",
                "MSG-DSP-03",
                "Bạn không thể khiếu nại giao dịch này nữa (quá thời hạn 7 ngày hoặc trạng thái không hợp lệ).");
        }

        var hasActiveComplaint = await _db.TransactionComplaints
            .AnyAsync(c => c.RequestId == handover.Id &&
                           (c.Status == ComplaintStatus.Open || c.Status == ComplaintStatus.Investigating),
                ct);

        if (hasActiveComplaint)
        {
            throw new ConflictException(
                "BR-DSP-01",
                "MSG-DSP-04",
                "Đã có một khiếu nại đang mở hoặc đang xử lý cho giao dịch này.");
        }

        var normalizedUrls = (request.EvidencePhotoUrls ?? new List<string>())
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Select(u => u.Trim())
            .Take(5)
            .ToList();

        var now = DateTime.UtcNow;
        var complaint = new TransactionComplaint
        {
            Id = Guid.NewGuid(),
            ComplaintCode = await GenerateUniqueComplaintCodeAsync(ct),
            RequestId = handover.Id,
            ReceiptId = receipt?.Id,
            ComplainantId = callerUserId,
            ComplainantRole = complainantRole,
            RespondentId = respondentId,
            Category = category,
            Description = request.Description.Trim(),
            EvidenceUrlsJson = JsonSerializer.Serialize(normalizedUrls, JsonOptions),
            Status = ComplaintStatus.Open,
            CreatedAt = now,
            CreatedBy = callerUserId.ToString()
        };

        if (handover.Status == HandoverStatus.AwaitingConfirmation)
        {
            handover.Status = HandoverStatus.Disputed;
            handover.UpdatedAt = now;
        }

        _db.TransactionComplaints.Add(complaint);

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("uq_transaction_complaints_active_request", StringComparison.OrdinalIgnoreCase) == true)
        {
            throw new ConflictException(
                "BR-DSP-01",
                "MSG-DSP-04",
                "Đã có một khiếu nại đang mở hoặc đang xử lý cho giao dịch này.");
        }

        await HandoverTradeSyncHelper.SyncHandoverToTradeOrderAsync(
            _db,
            handover.Id,
            callerUserId,
            $"[Khiếu nại #{complaint.ComplaintCode} — {category}] {complaint.Description}",
            ct);

        _logger.LogInformation(
            "Đã tiếp nhận phiếu khiếu nại {Code} ({Role}) cho yêu cầu bàn giao {RequestId}.",
            complaint.ComplaintCode, complainantRole, handover.Id);

        return await LoadComplaintDetailAsync(complaint.Id, ct);
    }

    public async Task<PagedResult<ComplaintSummaryDto>> GetMyComplaintsAsync(
        Guid callerUserId,
        MyComplaintFilterRequest filter,
        CancellationToken ct = default)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);

        var query = _db.TransactionComplaints
            .AsNoTracking()
            .Where(c => c.ComplainantId == callerUserId || c.RespondentId == callerUserId);

        if (string.Equals(filter.Role?.Trim(), "submitted", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.ComplainantId == callerUserId);
        }
        else if (string.Equals(filter.Role?.Trim(), "against", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(c => c.RespondentId == callerUserId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<ComplaintStatus>(filter.Status.Trim(), ignoreCase: true, out var statusFilter))
        {
            query = query.Where(c => c.Status == statusFilter);
        }

        var totalRecords = await query.CountAsync(ct);

        var rows = await query
            .Include(c => c.Request)
            .Include(c => c.Receipt)
            .Include(c => c.Complainant)
            .Include(c => c.Respondent)
            .OrderByDescending(c => c.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<ComplaintSummaryDto>
        {
            Items = rows.Select(c => MapToSummaryDto(c)).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords
        };
    }

    public async Task<StaffComplaintQueueResponseDto> GetStaffComplaintQueueAsync(
        StaffComplaintFilterRequest filter,
        CancellationToken ct = default)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);

        var openCount = await _db.TransactionComplaints.AsNoTracking().CountAsync(c => c.Status == ComplaintStatus.Open, ct);
        var investigatingCount = await _db.TransactionComplaints.AsNoTracking().CountAsync(c => c.Status == ComplaintStatus.Investigating, ct);
        var resolvedCount = await _db.TransactionComplaints.AsNoTracking().CountAsync(c => c.Status == ComplaintStatus.Resolved, ct);
        var dismissedCount = await _db.TransactionComplaints.AsNoTracking().CountAsync(c => c.Status == ComplaintStatus.Dismissed, ct);
        var reportedReviewsCount = await _db.TransactionReviews.AsNoTracking().CountAsync(r => r.Status == ReviewStatus.Reported, ct);

        var query = _db.TransactionComplaints
            .AsNoTracking()
            .Include(c => c.Request)
            .Include(c => c.Receipt)
            .Include(c => c.Complainant)
            .Include(c => c.Respondent)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<ComplaintStatus>(filter.Status.Trim(), ignoreCase: true, out var parsedStatus))
        {
            query = query.Where(c => c.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(filter.Category) &&
            Enum.TryParse<ComplaintCategory>(filter.Category.Trim(), ignoreCase: true, out var parsedCategory))
        {
            query = query.Where(c => c.Category == parsedCategory);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var kw = filter.Search.Trim().ToLowerInvariant();
            query = query.Where(c =>
                c.ComplaintCode.ToLower().Contains(kw) ||
                c.Description.ToLower().Contains(kw) ||
                (c.Request != null && c.Request.RequestCode.ToLower().Contains(kw)) ||
                (c.Complainant != null && (c.Complainant.FullName.ToLower().Contains(kw) || c.Complainant.Email.ToLower().Contains(kw))) ||
                (c.Respondent != null && (c.Respondent.FullName.ToLower().Contains(kw) || c.Respondent.Email.ToLower().Contains(kw))));
        }

        var totalRecords = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(c => c.Status == ComplaintStatus.Open || c.Status == ComplaintStatus.Investigating)
            .ThenByDescending(c => c.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        var reportedReviews = filter.IncludeReportedReviews
            ? await _reviewService.GetReportedReviewsForStaffAsync(ct)
            : new List<TransactionReviewDto>();

        return new StaffComplaintQueueResponseDto
        {
            Complaints = new PagedResult<ComplaintSummaryDto>
            {
                Items = rows.Select(c => MapToSummaryDto(c)).ToList(),
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = totalRecords
            },
            OpenCount = openCount,
            InvestigatingCount = investigatingCount,
            ResolvedCount = resolvedCount,
            DismissedCount = dismissedCount,
            ReportedReviewsCount = reportedReviewsCount,
            ReportedReviews = reportedReviews
        };
    }

    public async Task<StaffComplaintDossierDto> GetStaffComplaintDossierAsync(
        Guid staffUserId,
        Guid complaintId,
        bool claimTicket = true,
        CancellationToken ct = default)
    {
        var complaint = await _db.TransactionComplaints
            .Include(c => c.Complainant)
            .Include(c => c.Respondent)
            .Include(c => c.Request)!.ThenInclude(r => r!.Items).ThenInclude(i => i.WasteCategory)
            .Include(c => c.Receipt)
            .FirstOrDefaultAsync(c => c.Id == complaintId, ct)
            ?? throw new NotFoundException("Không tìm thấy hồ sơ khiếu nại yêu cầu.");

        if (claimTicket && complaint.Status == ComplaintStatus.Open)
        {
            complaint.Status = ComplaintStatus.Investigating;
            complaint.AssignedStaffId = staffUserId;
            complaint.TakenAt = DateTime.UtcNow;
            complaint.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            await _auditLogService.LogAsync(
                "CLAIM_COMPLAINT",
                "TransactionComplaint",
                complaint.Id.ToString(),
                oldValues: new { Status = ComplaintStatus.Open.ToString() },
                newValues: new { Status = ComplaintStatus.Investigating.ToString(), AssignedStaffId = staffUserId },
                reason: $"Nhân viên tiếp nhận hồ sơ khiếu nại {complaint.ComplaintCode}.",
                explicitActorId: staffUserId,
                ct: ct);
        }

        var handover = complaint.Request;
        var receipt = complaint.Receipt ?? await _db.HandoverReceipts
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.RequestId == complaint.RequestId, ct);

        // Đọc toàn bộ lịch sử chat thương lượng bất biến (BR-NEG-01, BR-DSP-06)
        var chatMessages = await _db.ChatMessages
            .AsNoTracking()
            .Include(m => m.SenderUser)
            .Where(m => m.TradeOrderId == complaint.RequestId)
            .OrderBy(m => m.SentAt)
            .Select(m => new DossierChatMessageDto
            {
                MessageId = m.Id,
                SenderUserId = m.SenderUserId,
                SenderName = m.SenderUser != null ? m.SenderUser.FullName : "Hệ thống",
                SenderRole = m.SenderRole,
                MessageType = m.MessageType.ToString(),
                Content = m.MessageContent ?? string.Empty,
                AttachmentImageUrl = m.AttachmentUrl,
                ProposedPricePerKg = m.ProposedPricePerKg,
                ProposedWeightKg = m.ProposedWeightKg,
                ProposalStatus = m.ProposalStatus.HasValue ? m.ProposalStatus.Value.ToString() : null,
                SentAt = DateTime.SpecifyKind(m.SentAt, DateTimeKind.Utc)
            })
            .ToListAsync(ct);

        return new StaffComplaintDossierDto
        {
            Complaint = MapToSummaryDto(complaint),
            Complainant = MapParty(complaint.Complainant),
            Respondent = MapParty(complaint.Respondent),
            HandoverRequestCode = handover?.RequestCode ?? string.Empty,
            HandoverStatus = handover?.Status.ToString() ?? string.Empty,
            PickupAddress = handover?.PickupAddress ?? string.Empty,
            PreferredTimeSlot = handover != null
                ? HandoverTradeSyncHelper.FormatSlotText(handover.ConfirmedTimeSlot ?? handover.ProposedTimeSlot)
                : null,
            WastePhotoUrls = ParseUrlList(handover?.WastePhotoUrl),
            Items = handover?.Items.Select(i => new DossierHandoverItemDto
            {
                ItemId = i.Id,
                CategoryCode = i.WasteCategory?.Code ?? string.Empty,
                CategoryName = i.WasteCategory?.Name ?? string.Empty,
                EstimatedWeightKg = i.EstimatedWeightKg,
                ActualWeightKg = i.ActualWeightKg,
                AgreedPricePerKg = i.AgreedPricePerKg
            }).ToList() ?? new List<DossierHandoverItemDto>(),
            Receipt = receipt == null ? null : new DossierReceiptSnapshotDto
            {
                ReceiptId = receipt.Id,
                ReceiptCode = receipt.ReceiptCode,
                TotalWeightKg = receipt.TotalWeightKg,
                TotalAmountCash = receipt.TotalAmountCash,
                PointsAwarded = receipt.PointsAwarded,
                ScaleProofPhotoUrls = ParseUrlList(receipt.ScaleProofPhotoUrl),
                ReceiptImageUrl = receipt.ReceiptImageUrl,
                CreatedAt = DateTime.SpecifyKind(receipt.CreatedAt, DateTimeKind.Utc),
                ConfirmedByUserAt = receipt.ConfirmedByUserAt.HasValue
                    ? DateTime.SpecifyKind(receipt.ConfirmedByUserAt.Value, DateTimeKind.Utc)
                    : null
            },
            NegotiationMessages = chatMessages
        };
    }

    public async Task<ComplaintDetailDto> ResolveComplaintAsync(
        Guid staffUserId,
        Guid complaintId,
        ResolveComplaintRequest request,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<ComplaintResolutionOutcome>(request.ResolutionOutcome?.Trim(), ignoreCase: true, out var outcome))
        {
            throw new BusinessRuleException(
                "BR-DSP-04",
                "MSG-GEN-01",
                "Kết quả phán quyết không hợp lệ.");
        }

        await using var tx = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(ct)
            : null;

        var complaint = await _db.TransactionComplaints
            .FirstOrDefaultAsync(c => c.Id == complaintId, ct)
            ?? throw new NotFoundException("Không tìm thấy phiếu khiếu nại.");

        if (complaint.Status is ComplaintStatus.Resolved or ComplaintStatus.Dismissed)
        {
            throw new ConflictException(
                "BR-DSP-04",
                "MSG-DSP-04",
                "Phiếu khiếu nại này đã được giải quyết hoặc đóng trước đó.");
        }

        var handover = await _db.HandoverRequests
            .FirstOrDefaultAsync(h => h.Id == complaint.RequestId, ct);

        var receipt = await _db.HandoverReceipts
            .FirstOrDefaultAsync(r => r.RequestId == complaint.RequestId, ct);

        var now = DateTime.UtcNow;
        var cleanNote = request.ResolutionNote.Trim();
        var oldValues = new
        {
            ComplaintStatus = complaint.Status.ToString(),
            HandoverStatus = handover?.Status.ToString(),
            ReceiptPointsAwarded = receipt?.PointsAwarded ?? 0
        };

        var pointsRevoked = false;
        var warningLogged = false;

        if (handover != null)
        {
            switch (outcome)
            {
                case ComplaintResolutionOutcome.CancelDisputedOrder:
                    handover.Status = HandoverStatus.Cancelled;
                    handover.CancelReason = $"[Trọng tài {complaint.ComplaintCode}] {cleanNote}";
                    handover.UpdatedAt = now;

                    if (receipt != null && receipt.PointsAwarded > 0)
                    {
                        var revokeAmount = (int)Math.Min(int.MaxValue, receipt.PointsAwarded);
                        await ClawBackPointsSafeAsync(handover.UserId, revokeAmount, receipt.TotalWeightKg, complaint.Id, complaint.ComplaintCode, ct);
                        await ClawBackPointsSafeAsync(handover.RecyclerId, revokeAmount, receipt.TotalWeightKg, complaint.Id, complaint.ComplaintCode, ct);
                        receipt.PointsAwarded = 0;
                        pointsRevoked = true;
                    }
                    break;

                case ComplaintResolutionOutcome.ConfirmReceiptAsIs:
                case ComplaintResolutionOutcome.CompensateUser:
                    if (handover.Status is HandoverStatus.Disputed or HandoverStatus.AwaitingConfirmation)
                    {
                        handover.Status = HandoverStatus.Completed;
                        handover.UpdatedAt = now;

                        if (receipt != null && receipt.ConfirmedByUserAt == null)
                        {
                            receipt.ConfirmedByUserAt = now;
                            if (receipt.PointsAwarded == 0)
                            {
                                receipt.PointsAwarded = (long)Math.Round(
                                    receipt.TotalWeightKg * HandoverReceiptService.PointsPerKg,
                                    0,
                                    MidpointRounding.AwayFromZero);
                            }
                            await _db.SaveChangesAsync(ct);
                            if (_pointsService != null)
                            {
                                await _pointsService.AwardHandoverPointsAsync(receipt.Id, ct);
                            }
                            await HandoverTradeSyncHelper.SyncReceiptCompletionAsync(_db, receipt, ct);
                        }
                    }
                    break;

                case ComplaintResolutionOutcome.Dismissed:
                    if (handover.Status == HandoverStatus.Disputed)
                    {
                        handover.Status = receipt?.ConfirmedByUserAt != null
                            ? HandoverStatus.Completed
                            : (receipt != null ? HandoverStatus.AwaitingConfirmation : HandoverStatus.Accepted);
                        handover.UpdatedAt = now;
                    }
                    break;

                case ComplaintResolutionOutcome.UpholdComplainant:
                    if (handover.Status == HandoverStatus.Disputed && receipt != null)
                    {
                        handover.Status = HandoverStatus.Completed;
                        handover.UpdatedAt = now;
                    }
                    break;
            }

            await HandoverTradeSyncHelper.SyncHandoverToTradeOrderAsync(
                _db,
                handover.Id,
                staffUserId,
                $"[Phán quyết Trọng tài #{complaint.ComplaintCode} — {outcome}] {cleanNote}",
                ct);
        }

        if (request.IssueWarningToRespondent)
        {
            var respondent = await _db.Users.FirstOrDefaultAsync(u => u.Id == complaint.RespondentId, ct);
            if (respondent != null)
            {
                var warnReason = string.IsNullOrWhiteSpace(request.WarningReason)
                    ? cleanNote
                    : request.WarningReason.Trim();

                respondent.WarningCount += 1;
                respondent.ModerationReason = warnReason;
                respondent.ModeratedAt = now;
                respondent.ModeratedBy = staffUserId;

                _db.AccountModerationLogs.Add(new AccountModerationLog
                {
                    Id = Guid.NewGuid(),
                    TargetUserId = respondent.Id,
                    PerformedBy = staffUserId,
                    ActionType = "WARN",
                    PreviousStatus = respondent.Status.ToString(),
                    NewStatus = respondent.Status.ToString(),
                    Reason = warnReason.Length > 500 ? warnReason[..500] : warnReason,
                    RelatedComplaintId = complaint.Id,
                    RevokedTokensCount = 0,
                    CreatedAt = now
                });

                warningLogged = true;
            }
        }

        complaint.Status = outcome == ComplaintResolutionOutcome.Dismissed
            ? ComplaintStatus.Dismissed
            : ComplaintStatus.Resolved;
        complaint.AssignedStaffId ??= staffUserId;
        complaint.TakenAt ??= now;
        complaint.ResolutionOutcome = outcome;
        complaint.ResolutionNote = cleanNote;
        complaint.ResolvedByStaffId = staffUserId;
        complaint.ResolvedAt = now;
        complaint.PointsRevoked = pointsRevoked;
        complaint.WarningLogged = warningLogged;
        complaint.UpdatedAt = now;

        await _db.SaveChangesAsync(ct);

        await _auditLogService.LogAsync(
            "RESOLVE_COMPLAINT",
            "TransactionComplaint",
            complaint.Id.ToString(),
            oldValues: oldValues,
            newValues: new
            {
                ComplaintStatus = complaint.Status.ToString(),
                ResolutionOutcome = outcome.ToString(),
                HandoverStatus = handover?.Status.ToString(),
                PointsRevoked = pointsRevoked,
                WarningLogged = warningLogged
            },
            reason: cleanNote,
            explicitActorId: staffUserId,
            ct: ct);

        if (tx != null)
        {
            await tx.CommitAsync(ct);
        }

        _logger.LogInformation(
            "Staff {StaffId} đã giải quyết khiếu nại {Code} với phán quyết {Outcome} (PointsRevoked={Revoked}, Warning={Warn}).",
            staffUserId, complaint.ComplaintCode, outcome, pointsRevoked, warningLogged);

        return await LoadComplaintDetailAsync(complaint.Id, ct);
    }

    private async Task ClawBackPointsSafeAsync(
        Guid userId,
        int pointsToRevoke,
        decimal weightKg,
        Guid complaintId,
        string complaintCode,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var ecoProfile = await _db.UserEcoProfiles.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (ecoProfile != null)
        {
            // BR-GAM-03: Điểm hiện có không bao giờ âm sau thu hồi
            ecoProfile.CurrentGreenPoints = Math.Max(0, ecoProfile.CurrentGreenPoints - pointsToRevoke);
            ecoProfile.TotalLifetimePoints = Math.Max(0, ecoProfile.TotalLifetimePoints - pointsToRevoke);
            ecoProfile.TotalRecycledKg = Math.Max(0m, ecoProfile.TotalRecycledKg - weightKg);
            ecoProfile.UpdatedAt = now;
        }

        if (pointsToRevoke <= 0) return;

        var wallet = await _db.UserPointWallets.FirstOrDefaultAsync(w => w.UserId == userId, ct);
        if (wallet == null || wallet.CurrentBalance <= 0) return;

        var idemKey = $"COMPLAINT_CLAWBACK:{complaintId}:{userId}";
        var alreadyRevoked = await _db.PointTransactions
            .AsNoTracking()
            .AnyAsync(t => t.IdempotencyKey == idemKey, ct);
        if (alreadyRevoked) return;

        // BR-GAM-03: chỉ trừ tối đa bằng số dư khả dụng hiện tại (không bao giờ âm)
        var actualDeducted = Math.Min(wallet.CurrentBalance, (long)pointsToRevoke);
        if (actualDeducted <= 0) return;

        wallet.CurrentBalance -= actualDeducted;
        wallet.TotalEarned = Math.Max(0L, wallet.TotalEarned - actualDeducted);
        wallet.UpdatedAt = now;

        _db.PointTransactions.Add(new PointTransaction
        {
            WalletId = wallet.Id,
            UserId = userId,
            Points = -actualDeducted,
            ActionType = PointActionType.Adjustment,
            SourceType = "ComplaintClawback",
            SourceId = complaintId.ToString(),
            IdempotencyKey = idemKey,
            Description = $"Thu hồi điểm theo phán quyết khiếu nại #{complaintCode}",
            CreatedAt = now
        });
    }

    private async Task<ComplaintDetailDto> LoadComplaintDetailAsync(Guid complaintId, CancellationToken ct)
    {
        var entity = await _db.TransactionComplaints
            .AsNoTracking()
            .Include(c => c.Request)
            .Include(c => c.Receipt)
            .Include(c => c.Complainant)
            .Include(c => c.Respondent)
            .FirstAsync(c => c.Id == complaintId, ct);

        return MapToDetailDto(entity);
    }

    private async Task<string> GenerateUniqueComplaintCodeAsync(CancellationToken ct)
    {
        var dateTag = DateTime.UtcNow.AddHours(7).ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        for (var i = 0; i < 12; i++)
        {
            var candidate = $"CMP-{dateTag}-{Random.Shared.Next(1000, 9999)}";
            var exists = await _db.TransactionComplaints
                .IgnoreQueryFilters()
                .AnyAsync(c => c.ComplaintCode == candidate, ct);
            if (!exists) return candidate;
        }

        return $"CMP-{dateTag}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }

    private static DossierPartyInfoDto MapParty(User? user) => user == null
        ? new DossierPartyInfoDto()
        : new DossierPartyInfoDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            WarningCount = user.WarningCount
        };

    private static ComplaintSummaryDto MapToSummaryDto(TransactionComplaint c) => FillSummary(new ComplaintSummaryDto(), c);

    private static ComplaintDetailDto MapToDetailDto(TransactionComplaint c) => FillSummary(new ComplaintDetailDto(), c);

    private static T FillSummary<T>(T target, TransactionComplaint c) where T : ComplaintSummaryDto
    {
        target.ComplaintId = c.Id;
        target.ComplaintCode = c.ComplaintCode;
        target.RequestId = c.RequestId;
        target.RequestCode = c.Request?.RequestCode ?? string.Empty;
        target.HandoverStatus = c.Request?.Status.ToString() ?? string.Empty;
        target.ReceiptId = c.ReceiptId;
        target.ReceiptCode = c.Receipt?.ReceiptCode;
        target.ComplainantId = c.ComplainantId;
        target.ComplainantName = c.Complainant?.FullName ?? string.Empty;
        target.ComplainantRole = c.ComplainantRole;
        target.RespondentId = c.RespondentId;
        target.RespondentName = c.Respondent?.FullName ?? string.Empty;
        target.Category = c.Category.ToString();
        target.Description = c.Description;
        target.EvidencePhotoUrls = ParseUrlList(c.EvidenceUrlsJson);
        target.Status = c.Status.ToString();
        target.AssignedStaffId = c.AssignedStaffId;
        target.ResolutionOutcome = c.ResolutionOutcome?.ToString();
        target.ResolutionNote = c.ResolutionNote;
        target.ResolvedAt = c.ResolvedAt.HasValue ? DateTime.SpecifyKind(c.ResolvedAt.Value, DateTimeKind.Utc) : null;
        target.PointsRevoked = c.PointsRevoked;
        target.WarningLogged = c.WarningLogged;
        target.CreatedAt = DateTime.SpecifyKind(c.CreatedAt, DateTimeKind.Utc);
        return target;
    }

    private static List<string> ParseUrlList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new List<string>();
        var trimmed = raw.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                return JsonSerializer.Deserialize<List<string>>(trimmed, JsonOptions)?
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .Select(u => u.Trim())
                    .ToList() ?? new List<string>();
            }
            catch (JsonException)
            {
                // fallback below
            }
        }

        return trimmed
            .Split(new[] { '\n', '\r', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
    }
}
