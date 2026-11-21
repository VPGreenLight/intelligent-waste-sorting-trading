using System.Globalization;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Events;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Sổ cái đối soát dòng tiền VietQR, xử lý giao dịch bất thường 5 kịch bản &amp; xuất bảng kê ẩn danh PII (UC A-11 - STT 127, 128, 129).
/// </summary>
public class AdminReconciliationService : IAdminReconciliationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISubscriptionEntitlementService _entitlementService;
    private readonly IAuditLogService _auditLogService;
    private readonly IEventBus? _eventBus;

    public AdminReconciliationService(
        ApplicationDbContext dbContext,
        ISubscriptionEntitlementService entitlementService,
        IAuditLogService auditLogService,
        IEventBus? eventBus = null)
    {
        _dbContext = dbContext;
        _entitlementService = entitlementService;
        _auditLogService = auditLogService;
        _eventBus = eventBus;
    }

    public async Task<ReconciliationLedgerResponseDto> GetPaymentLedgerAsync(
        ReconciliationLedgerQuery query,
        CancellationToken ct = default)
    {
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var (orders, orphanLogs) = await LoadUnifiedRecordsAsync(query.FromDate, query.ToDate, ct);
        var allItems = BuildLedgerItems(orders, orphanLogs);

        var summary = new ReconciliationSummaryDto
        {
            TotalMatchedRevenueVnd = orders
                .Where(o => o.OrderStatus == ServiceOrderStatus.Paid)
                .Sum(o => o.ExpectedAmountVnd),
            PendingOrdersCount = orders.Count(o => o.OrderStatus == ServiceOrderStatus.Pending),
            OpenAnomaliesCount = orders.Count(o => o.ReconciliationStatus is ReconciliationStatus.Underpaid
                                                        or ReconciliationStatus.Overpaid
                                                        or ReconciliationStatus.LateTransfer)
                                 + orphanLogs.Count(w => w.ProcessingResult == "UnmatchedReference" && w.ResolvedAt == null),
            TotalDiscrepancyVnd = allItems
                .Where(i => i.ReconciliationStatus is nameof(ReconciliationStatus.Underpaid)
                                or nameof(ReconciliationStatus.Overpaid)
                                or nameof(ReconciliationStatus.LateTransfer)
                                or nameof(ReconciliationStatus.UnmatchedReference))
                .Sum(i => i.DiscrepancyVnd),
            TotalRefundedVnd = orders
                .Where(o => o.OrderStatus == ServiceOrderStatus.Refunded || o.ReconciliationStatus == ReconciliationStatus.Refunded)
                .Sum(o => o.PaidAmountVnd ?? o.ExpectedAmountVnd)
                + orphanLogs
                    .Where(w => w.ProcessingResult == nameof(ReconciliationStatus.Refunded))
                    .Sum(w => w.ReceivedAmountVnd)
        };

        IEnumerable<ReconciliationLedgerItemDto> filtered = allItems;

        if (!string.IsNullOrWhiteSpace(query.ReconciliationStatus))
        {
            var statusNorm = query.ReconciliationStatus.Trim();
            filtered = filtered.Where(i => i.ReconciliationStatus.Equals(statusNorm, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(query.AnomalyType))
        {
            var anomalyNorm = query.AnomalyType.Trim();
            filtered = filtered.Where(i => i.AnomalyType.Equals(anomalyNorm, StringComparison.OrdinalIgnoreCase));
        }

        if (query.OnlyAnomalies)
        {
            filtered = filtered.Where(i =>
                i.ReconciliationStatus is nameof(ReconciliationStatus.Underpaid)
                    or nameof(ReconciliationStatus.Overpaid)
                    or nameof(ReconciliationStatus.LateTransfer)
                    or nameof(ReconciliationStatus.UnmatchedReference)
                || i.AnomalyType != nameof(PaymentAnomalyType.None));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var kw = query.Search.Trim().ToLowerInvariant();
            filtered = filtered.Where(i =>
                i.ReferenceCode.ToLowerInvariant().Contains(kw)
                || (i.PaymentReference?.ToLowerInvariant().Contains(kw) ?? false)
                || i.PayerName.ToLowerInvariant().Contains(kw)
                || (i.PayerEmail?.ToLowerInvariant().Contains(kw) ?? false));
        }

        var sorted = filtered.OrderByDescending(i => i.CreatedAt).ToList();
        var totalRecords = sorted.Count;
        var pagedItems = sorted.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return new ReconciliationLedgerResponseDto
        {
            Summary = summary,
            Items = new PagedResult<ReconciliationLedgerItemDto>
            {
                Items = pagedItems,
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalRecords = totalRecords
            }
        };
    }

    public async Task<ReconciliationLedgerItemDto> ResolvePaymentAnomalyAsync(
        Guid adminUserId,
        string code,
        ResolvePaymentAnomalyRequest request,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<ReconciliationAction>(request.Action, true, out var action))
        {
            throw new BusinessRuleException("BR-PAY-06", "MSG-GEN-01", $"Hành động đối soát '{request.Action}' không hợp lệ.");
        }

        var normalizedCode = (code ?? string.Empty).Trim().ToUpperInvariant();
        var utcNow = DateTime.UtcNow;

        // Xử lý nhánh bản ghi Webhook mồ côi (WH-...)
        if (normalizedCode.StartsWith("WH-", StringComparison.OrdinalIgnoreCase))
        {
            return await ResolveOrphanWebhookAsync(adminUserId, normalizedCode, action, request, utcNow, ct);
        }

        // Xử lý nhánh Đơn hàng chuẩn (ECO...)
        var order = await _dbContext.ServiceOrders
            .Include(o => o.User)
            .Include(o => o.ResolverUser)
            .Include(o => o.WebhookLogs)
            .FirstOrDefaultAsync(o => o.OrderCode.ToUpper() == normalizedCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy giao dịch hoặc mã webhook '{code}'.");

        if (order.ReconciliationStatus == ReconciliationStatus.Resolved
            || (order.OrderStatus == ServiceOrderStatus.Refunded && action != ReconciliationAction.CloseAnomaly))
        {
            throw new ConflictException("BR-PAY-06", "MSG-PAY-08", $"Giao dịch '{order.OrderCode}' đã được đối soát đóng trước đó.");
        }

        var oldValues = new
        {
            OrderStatus = order.OrderStatus.ToString(),
            PaymentStatus = order.PaymentStatus.ToString(),
            ReconciliationStatus = order.ReconciliationStatus.ToString(),
            order.PaidAmountVnd
        };

        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        switch (action)
        {
            case ReconciliationAction.ApproveAndActivate:
                order.OrderStatus = ServiceOrderStatus.Paid;
                order.PaymentStatus = ServicePaymentStatus.Success;
                order.ReconciliationStatus = ReconciliationStatus.Resolved;
                order.PaidAmountVnd ??= order.ExpectedAmountVnd;
                order.PaidAt ??= utcNow;
                await ServicePackageHelpers.ActivateSubscriptionForOrderAsync(_dbContext, order, utcNow, ct);
                _entitlementService.InvalidateUserEntitlements(order.UserId);
                break;

            case ReconciliationAction.MarkRefunded:
                order.OrderStatus = ServiceOrderStatus.Refunded;
                order.PaymentStatus = ServicePaymentStatus.Refunded;
                order.ReconciliationStatus = ReconciliationStatus.Refunded;
                break;

            case ReconciliationAction.MarkExcessRefunded:
                if (order.AnomalyType != PaymentAnomalyType.Overpaid)
                {
                    throw new BusinessRuleException("BR-PAY-06", "MSG-GEN-01", "Chỉ dùng MarkExcessRefunded cho đơn bị chuyển dư tiền (Overpaid).");
                }
                order.ReconciliationStatus = ReconciliationStatus.Resolved;
                break;

            case ReconciliationAction.CloseAnomaly:
                order.ReconciliationStatus = ReconciliationStatus.Resolved;
                break;

            case ReconciliationAction.ManualMatchOrder:
                throw new BusinessRuleException("BR-PAY-06", "MSG-GEN-01", "Hành động ManualMatchOrder chỉ áp dụng cho mã Webhook mồ côi (WH-...).");
        }

        order.ResolvedBy = adminUserId;
        order.ResolvedAt = utcNow;
        order.ResolutionNote = request.ResolutionNote.Trim();
        order.ResolutionBankRef = string.IsNullOrWhiteSpace(request.ResolutionBankRef) ? null : request.ResolutionBankRef.Trim();
        order.UpdatedAt = utcNow;
        order.UpdatedBy = adminUserId.ToString();

        await _dbContext.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await _auditLogService.LogAsync(
            action: "RESOLVE_PAYMENT_ANOMALY",
            entityType: nameof(ServiceOrder),
            entityId: order.OrderCode,
            oldValues: oldValues,
            newValues: new
            {
                Action = action.ToString(),
                OrderStatus = order.OrderStatus.ToString(),
                PaymentStatus = order.PaymentStatus.ToString(),
                ReconciliationStatus = order.ReconciliationStatus.ToString(),
                order.ResolutionBankRef
            },
            reason: order.ResolutionNote,
            explicitActorId: adminUserId,
            ct: ct);

        if (_eventBus != null && order.UserId != Guid.Empty)
        {
            await _eventBus.PublishAsync(
                new SystemNotificationDispatchedEvent(
                    order.UserId,
                    "Cập nhật đối soát thanh toán VietQR",
                    $"Đơn hàng {order.OrderCode} đã được xử lý đối soát ({action}): trạng thái {order.OrderStatus}.",
                    "PAYMENT_RECONCILIATION",
                    "ServiceOrder",
                    order.Id),
                ct);
        }

        return MapOrderToLedgerItem(order);
    }

    public async Task<ExportedReportFileResult> ExportReconciliationReportAsync(
        Guid adminUserId,
        ReconciliationExportQuery query,
        CancellationToken ct = default)
    {
        var (orders, orphanLogs) = await LoadUnifiedRecordsAsync(query.FromDate, query.ToDate, ct);
        var items = BuildLedgerItems(orders, orphanLogs);

        if (!string.IsNullOrWhiteSpace(query.ReconciliationStatus))
        {
            items = items
                .Where(i => i.ReconciliationStatus.Equals(query.ReconciliationStatus.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        string[] headers =
        [
            "Loại bản ghi",
            "Mã đơn / Webhook",
            "Người thanh toán",
            "Email (Ẩn danh PII)",
            "SĐT (Ẩn danh PII)",
            "Mã gói",
            "Phải thu (VNĐ)",
            "Thực nhận (VNĐ)",
            "Chênh lệch (VNĐ)",
            "Trạng thái đơn",
            "Trạng thái đối soát",
            "Mã tham chiếu NH",
            "Thời điểm tạo (UTC)",
            "Ghi chú đối soát"
        ];

        // BR-ADM-06: Bắt buộc che PII email & SĐT khi xuất bảng kê
        var rows = items.Select(i => new[]
        {
            i.RecordType,
            i.ReferenceCode,
            i.PayerName,
            PiiMaskingHelper.MaskEmail(i.PayerEmail),
            PiiMaskingHelper.MaskPhone(i.PayerPhone),
            i.PackageCode ?? "-",
            i.ExpectedAmountVnd.ToString("0", CultureInfo.InvariantCulture),
            i.PaidAmountVnd.ToString("0", CultureInfo.InvariantCulture),
            i.DiscrepancyVnd.ToString("0", CultureInfo.InvariantCulture),
            i.OrderStatus,
            i.ReconciliationStatus,
            i.PaymentReference ?? "-",
            i.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            i.ResolutionNote ?? i.AnomalyNote ?? "-"
        }).ToList();

        var isXlsx = string.Equals(query.Format?.Trim(), "xlsx", StringComparison.OrdinalIgnoreCase);
        var content = isXlsx
            ? TabularReportExportHelper.BuildMinimalOpenXmlWorkbook("VietQR Reconciliation", headers, rows)
            : TabularReportExportHelper.BuildUtf8BomCsv(headers, rows);

        var fileExt = isXlsx ? "xlsx" : "csv";
        var contentType = isXlsx
            ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
            : "text/csv; charset=utf-8";
        var fileName = $"ecolink-reconciliation-{DateTime.UtcNow:yyyy-MM-dd}.{fileExt}";

        await _auditLogService.LogAsync(
            action: "EXPORT_RECONCILIATION",
            entityType: "ReconciliationReport",
            entityId: fileName,
            oldValues: null,
            newValues: new { Format = fileExt, query.ReconciliationStatus, RowCount = rows.Count },
            reason: $"Admin xuất bảng kê đối soát dòng tiền VietQR ({rows.Count} dòng).",
            explicitActorId: adminUserId,
            ct: ct);

        return new ExportedReportFileResult
        {
            Content = content,
            ContentType = contentType,
            FileName = fileName,
            RowCount = rows.Count
        };
    }

    private async Task<ReconciliationLedgerItemDto> ResolveOrphanWebhookAsync(
        Guid adminUserId,
        string webhookCode,
        ReconciliationAction action,
        ResolvePaymentAnomalyRequest request,
        DateTime utcNow,
        CancellationToken ct)
    {
        var log = await _dbContext.PaymentWebhookLogs
            .Include(w => w.ResolverUser)
            .FirstOrDefaultAsync(w => w.WebhookCode.ToUpper() == webhookCode, ct)
            ?? throw new NotFoundException($"Không tìm thấy bản ghi webhook '{webhookCode}'.");

        if (log.ResolvedAt.HasValue)
        {
            throw new ConflictException("BR-PAY-06", "MSG-PAY-08", $"Bản ghi webhook '{webhookCode}' đã được xử lý trước đó.");
        }

        await using var tx = await _dbContext.Database.BeginTransactionAsync(ct);

        if (action == ReconciliationAction.ManualMatchOrder)
        {
            var targetCodeNorm = request.TargetOrderCode!.Trim().ToUpperInvariant();
            var targetOrder = await _dbContext.ServiceOrders
                .Include(o => o.User)
                .FirstOrDefaultAsync(o => o.OrderCode.ToUpper() == targetCodeNorm, ct)
                ?? throw new NotFoundException($"Không tìm thấy đơn hàng đích '{request.TargetOrderCode}'.");

            if (targetOrder.OrderStatus == ServiceOrderStatus.Paid)
            {
                throw new ConflictException("BR-PAY-06", "MSG-PAY-08", $"Đơn hàng '{targetOrder.OrderCode}' đã được thanh toán.");
            }

            if (log.ReceivedAmountVnd < targetOrder.ExpectedAmountVnd)
            {
                throw new BusinessRuleException(
                    "BR-PAY-06",
                    "MSG-PAY-08",
                    $"Số tiền thực nhận ({log.ReceivedAmountVnd:N0} VNĐ) nhỏ hơn giá trị đơn đích ({targetOrder.ExpectedAmountVnd:N0} VNĐ).");
            }

            targetOrder.PaidAmountVnd = log.ReceivedAmountVnd;
            targetOrder.OrderStatus = ServiceOrderStatus.Paid;
            targetOrder.PaymentStatus = ServicePaymentStatus.Success;
            targetOrder.ReconciliationStatus = ReconciliationStatus.Resolved;
            targetOrder.PaymentReference ??= log.BankReference ?? log.WebhookCode;
            targetOrder.PaidAt = utcNow;
            targetOrder.ResolvedBy = adminUserId;
            targetOrder.ResolvedAt = utcNow;
            targetOrder.ResolutionNote = request.ResolutionNote.Trim();
            targetOrder.ResolutionBankRef = log.BankReference;
            targetOrder.UpdatedAt = utcNow;
            targetOrder.UpdatedBy = adminUserId.ToString();

            log.MatchedOrderId = targetOrder.Id;
            log.ProcessingResult = nameof(ReconciliationStatus.Resolved);
            log.ResolvedBy = adminUserId;
            log.ResolvedAt = utcNow;
            log.ResolutionNote = request.ResolutionNote.Trim();
            log.ResolutionBankRef = string.IsNullOrWhiteSpace(request.ResolutionBankRef) ? log.BankReference : request.ResolutionBankRef.Trim();

            await ServicePackageHelpers.ActivateSubscriptionForOrderAsync(_dbContext, targetOrder, utcNow, ct);
            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            _entitlementService.InvalidateUserEntitlements(targetOrder.UserId);

            await _auditLogService.LogAsync(
                action: "RESOLVE_PAYMENT_ANOMALY",
                entityType: nameof(PaymentWebhookLog),
                entityId: log.WebhookCode,
                oldValues: new { ProcessingResult = "UnmatchedReference", MatchedOrderId = (Guid?)null },
                newValues: new { Action = action.ToString(), TargetOrderCode = targetOrder.OrderCode, log.ReceivedAmountVnd },
                reason: log.ResolutionNote,
                explicitActorId: adminUserId,
                ct: ct);

            return MapOrderToLedgerItem(targetOrder);
        }

        if (action == ReconciliationAction.MarkRefunded || action == ReconciliationAction.CloseAnomaly)
        {
            log.ProcessingResult = action == ReconciliationAction.MarkRefunded
                ? nameof(ReconciliationStatus.Refunded)
                : nameof(ReconciliationStatus.Resolved);
            log.ResolvedBy = adminUserId;
            log.ResolvedAt = utcNow;
            log.ResolutionNote = request.ResolutionNote.Trim();
            log.ResolutionBankRef = string.IsNullOrWhiteSpace(request.ResolutionBankRef) ? null : request.ResolutionBankRef.Trim();

            await _dbContext.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            await _auditLogService.LogAsync(
                action: "RESOLVE_PAYMENT_ANOMALY",
                entityType: nameof(PaymentWebhookLog),
                entityId: log.WebhookCode,
                oldValues: new { ProcessingResult = "UnmatchedReference" },
                newValues: new { Action = action.ToString(), log.ProcessingResult, log.ResolutionBankRef },
                reason: log.ResolutionNote,
                explicitActorId: adminUserId,
                ct: ct);

            return MapOrphanWebhookToLedgerItem(log);
        }

        throw new BusinessRuleException(
            "BR-PAY-06",
            "MSG-GEN-01",
            "Giao dịch webhook mồ côi chỉ hỗ trợ khớp lệnh (ManualMatchOrder), hoàn tiền (MarkRefunded) hoặc đóng bất thường (CloseAnomaly).");
    }

    private async Task<(List<ServiceOrder> Orders, List<PaymentWebhookLog> OrphanLogs)> LoadUnifiedRecordsAsync(
        DateOnly? fromDate,
        DateOnly? toDate,
        CancellationToken ct)
    {
        var ordersQuery = _dbContext.ServiceOrders
            .AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.ResolverUser)
            .Include(o => o.WebhookLogs)
            .AsQueryable();

        var orphansQuery = _dbContext.PaymentWebhookLogs
            .AsNoTracking()
            .Include(w => w.ResolverUser)
            .Where(w => w.MatchedOrderId == null && w.ProcessingResult != "InvalidSignature");

        if (fromDate.HasValue)
        {
            var fromUtc = fromDate.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            ordersQuery = ordersQuery.Where(o => o.CreatedAt >= fromUtc);
            orphansQuery = orphansQuery.Where(w => w.ReceivedAt >= fromUtc);
        }

        if (toDate.HasValue)
        {
            var toExclusiveUtc = toDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            ordersQuery = ordersQuery.Where(o => o.CreatedAt < toExclusiveUtc);
            orphansQuery = orphansQuery.Where(w => w.ReceivedAt < toExclusiveUtc);
        }

        var orders = await ordersQuery.OrderByDescending(o => o.CreatedAt).ToListAsync(ct);
        var orphanLogs = await orphansQuery.OrderByDescending(w => w.ReceivedAt).ToListAsync(ct);
        return (orders, orphanLogs);
    }

    private static List<ReconciliationLedgerItemDto> BuildLedgerItems(
        List<ServiceOrder> orders,
        List<PaymentWebhookLog> orphanLogs)
    {
        var list = new List<ReconciliationLedgerItemDto>(orders.Count + orphanLogs.Count);
        list.AddRange(orders.Select(MapOrderToLedgerItem));
        list.AddRange(orphanLogs.Select(MapOrphanWebhookToLedgerItem));
        return list.OrderByDescending(x => x.CreatedAt).ToList();
    }

    private static ReconciliationLedgerItemDto MapOrderToLedgerItem(ServiceOrder o)
    {
        var latestRaw = o.WebhookLogs
            .OrderByDescending(w => w.ReceivedAt)
            .Select(w => w.RawPayload)
            .FirstOrDefault();

        var paid = o.PaidAmountVnd ?? 0m;
        var discrepancy = o.PaidAmountVnd.HasValue ? paid - o.ExpectedAmountVnd : 0m;

        return new ReconciliationLedgerItemDto
        {
            RecordType = "Order",
            ReferenceCode = o.OrderCode,
            UserId = o.UserId,
            PayerName = o.User?.FullName ?? o.AccountName,
            PayerEmail = o.User?.Email,
            PayerPhone = o.User?.PhoneNumber,
            PayerRole = o.User?.Role.ToString(),
            PackageCode = o.PackageCodeSnapshot,
            PackageName = o.PackageNameSnapshot,
            ExpectedAmountVnd = o.ExpectedAmountVnd,
            PaidAmountVnd = paid,
            DiscrepancyVnd = discrepancy,
            BankCode = o.BankCode,
            PaymentReference = o.PaymentReference,
            OrderStatus = o.OrderStatus.ToString(),
            ReconciliationStatus = o.ReconciliationStatus.ToString(),
            AnomalyType = o.AnomalyType.ToString(),
            AnomalyNote = o.AnomalyNote,
            RawWebhookJson = latestRaw,
            ResolvedByName = o.ResolverUser?.FullName,
            ResolvedAt = o.ResolvedAt,
            ResolutionNote = o.ResolutionNote,
            ResolutionBankRef = o.ResolutionBankRef,
            CreatedAt = o.CreatedAt,
            PaidAt = o.PaidAt
        };
    }

    private static ReconciliationLedgerItemDto MapOrphanWebhookToLedgerItem(PaymentWebhookLog w)
    {
        var recStatus = w.ProcessingResult is nameof(ReconciliationStatus.Resolved) or nameof(ReconciliationStatus.Refunded)
            ? w.ProcessingResult
            : nameof(ReconciliationStatus.UnmatchedReference);

        return new ReconciliationLedgerItemDto
        {
            RecordType = "OrphanWebhook",
            ReferenceCode = w.WebhookCode,
            UserId = null,
            PayerName = "Giao dịch chưa khớp tài khoản",
            PayerEmail = null,
            PayerPhone = null,
            PayerRole = null,
            PackageCode = null,
            PackageName = w.TransferDescription,
            ExpectedAmountVnd = 0m,
            PaidAmountVnd = w.ReceivedAmountVnd,
            DiscrepancyVnd = w.ReceivedAmountVnd,
            BankCode = w.GatewaySource,
            PaymentReference = w.BankReference,
            OrderStatus = "Unmatched",
            ReconciliationStatus = recStatus,
            AnomalyType = w.AnomalyType.ToString(),
            AnomalyNote = $"Không tìm thấy mã đơn trong nội dung '{w.TransferDescription}'.",
            RawWebhookJson = w.RawPayload,
            ResolvedByName = w.ResolverUser?.FullName,
            ResolvedAt = w.ResolvedAt,
            ResolutionNote = w.ResolutionNote,
            ResolutionBankRef = w.ResolutionBankRef,
            CreatedAt = w.ReceivedAt,
            PaidAt = w.ReceivedAt
        };
    }
}
