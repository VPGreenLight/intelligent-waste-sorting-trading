using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Dịch vụ thanh toán gói dịch vụ nội bộ qua Dynamic VietQR &amp; PayOS lưu trữ bền vững trên PostgreSQL 16
/// (UC U-16, R-07 - BR-PAY-01, BR-PAY-02, BR-PAY-03, BR-PAY-04).
/// </summary>
public partial class PaymentService : IPaymentService
{
    private const int QrTimeToLiveMinutes = 15;

    private readonly ApplicationDbContext _dbContext;
    private readonly ISubscriptionEntitlementService _entitlementService;
    private readonly IConfiguration _config;
    private readonly ILogger<PaymentService> _logger;
    private readonly IPayOsService? _payOsService;

    [GeneratedRegex(@"\b(ECO[A-Z0-9]{6,32})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex OrderCodePattern();

    public PaymentService(
        ApplicationDbContext dbContext,
        ISubscriptionEntitlementService entitlementService,
        IConfiguration config,
        ILogger<PaymentService> logger,
        IPayOsService? payOsService = null)
    {
        _dbContext = dbContext;
        _entitlementService = entitlementService;
        _config = config;
        _logger = logger;
        _payOsService = payOsService;
    }

    public async Task<PayInOrderDto> CreatePayInOrderAsync(CreatePayInRequest request, CancellationToken cancellationToken = default)
    {
        Guid resolvedUserId;
        UserRole resolvedRole;

        if (request.UserId.HasValue && request.UserId.Value != Guid.Empty)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.UserId.Value, cancellationToken)
                ?? throw new NotFoundException("Không tìm thấy tài khoản người mua gói dịch vụ.");

            resolvedUserId = user.Id;
            resolvedRole = user.Role;
        }
        else
        {
            var fallbackUser = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Status == UserStatus.Active && (u.Role == UserRole.User || u.Role == UserRole.Recycler))
                .OrderBy(u => u.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new BusinessRuleException("BR-AUTH-08", "MSG-UNAUTHORIZED", "Vui lòng đăng nhập trước khi khởi tạo đơn thanh toán gói dịch vụ.");

            resolvedUserId = fallbackUser.Id;
            resolvedRole = fallbackUser.Role;
        }

        return await CreatePayInOrderAsync(resolvedUserId, resolvedRole, request, cancellationToken);
    }

    public async Task<PayInOrderDto> CreatePayInOrderAsync(
        Guid currentUserId,
        UserRole currentRole,
        CreatePayInRequest request,
        CancellationToken cancellationToken = default)
    {
        await ServicePackageHelpers.EnsureCanonicalPackagesExistAsync(_dbContext, cancellationToken);

        ServicePackage? package = null;
        if (request.PackageId.HasValue && request.PackageId.Value != Guid.Empty)
        {
            package = await _dbContext.ServicePackages
                .FirstOrDefaultAsync(p => p.Id == request.PackageId.Value, cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.PackageCode))
        {
            var normalizedCode = request.PackageCode.Trim().ToUpperInvariant();
            package = await _dbContext.ServicePackages
                .FirstOrDefaultAsync(p => p.PackageCode.ToUpper() == normalizedCode, cancellationToken);
        }

        if (package == null)
        {
            throw new NotFoundException("Không tìm thấy gói dịch vụ yêu cầu.");
        }

        if (!package.IsActive)
        {
            throw new BusinessRuleException("BR-PAY-05", "MSG-PAY-04", $"Gói dịch vụ '{package.PackageName}' hiện đang tạm ngừng mở bán.");
        }

        // Chặn tạo giao dịch VietQR cho gói mặc định miễn phí BASIC (0 VNĐ)
        if (package.IsDefaultFree || package.PriceVnd <= 0m)
        {
            throw new BusinessRuleException(
                "BR-PAY-01",
                "MSG-PAY-03",
                "Gói Basic miễn phí được áp dụng mặc định cho mọi tài khoản, không cần khởi tạo đơn VietQR.");
        }

        // Kiểm tra phân quyền vai trò (U-16 Citizen vs R-07 Recycler)
        if (package.TargetRole == ServiceTargetRole.Recycler && currentRole == UserRole.User)
        {
            throw new ForbiddenAccessException(
                "BR-PAY-03",
                "MSG-ADM-04",
                "Gói dịch vụ thương mại Pro chỉ dành riêng cho tài khoản Cơ sở thu mua (Recycler).");
        }

        if (package.TargetRole == ServiceTargetRole.User && currentRole == UserRole.Recycler)
        {
            throw new BusinessRuleException(
                "BR-PAY-03",
                "MSG-ADM-04",
                "Gói này chỉ áp dụng cho tài khoản Người dân (Citizen).");
        }

        // Kiểm tra chống giả mạo số tiền từ phía Client (BR-PAY-01)
        if (request.Amount.HasValue && request.Amount.Value > 0m && Math.Round(request.Amount.Value, 0) != package.PriceVnd)
        {
            throw new BusinessRuleException(
                "BR-PAY-01",
                "MSG-PAY-03",
                $"Số tiền yêu cầu ({request.Amount.Value:N0} VNĐ) không khớp với giá niêm yết của gói ({package.PriceVnd:N0} VNĐ).");
        }

        // Kiểm tra điều kiện thẩm định KYB cho gói thương mại PRO (BR-PAY-03, BR-MTH-01, MSG-PAY-06)
        if (package.RequiresVerifiedKyb)
        {
            var recyclerProfile = await _dbContext.RecyclerProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.UserId == currentUserId, cancellationToken);

            if (recyclerProfile == null || recyclerProfile.VerificationStatus != VerificationStatus.Verified)
            {
                throw new BusinessRuleException(
                    "BR-PAY-03",
                    "MSG-PAY-06",
                    "Cơ sở thu mua cần hoàn tất xác minh hồ sơ KYB trước khi đăng ký gói Pro Tích xanh.");
            }
        }

        var utcNow = DateTime.UtcNow;

        // Tự động hủy đơn Pending cũ cùng người dùng & cùng gói để tránh quét nhầm QR cũ
        var stalePendingOrders = await _dbContext.ServiceOrders
            .Where(o => o.UserId == currentUserId
                        && o.PackageId == package.Id
                        && o.OrderStatus == ServiceOrderStatus.Pending)
            .ToListAsync(cancellationToken);

        foreach (var stale in stalePendingOrders)
        {
            stale.OrderStatus = stale.QrExpiresAt <= utcNow ? ServiceOrderStatus.Expired : ServiceOrderStatus.Cancelled;
            stale.PaymentStatus = ServicePaymentStatus.Failed;
            stale.UpdatedAt = utcNow;
            stale.UpdatedBy = currentUserId.ToString();
        }

        string bankCode = ResolveConfig("VietQr:BankCode", "TPB");
        if (bankCode.Equals("TP", StringComparison.OrdinalIgnoreCase)) bankCode = "TPB";
        string bankName = ResolveConfig("VietQr:BankName", "TPBank");
        string accountNo = ResolveConfig("VietQr:AccountNo", "05773216801");
        string accountName = ResolveConfig("VietQr:AccountName", "ECOLINK PLATFORM SERVICE");

        string orderCode = GenerateUniqueOrderCode(utcNow);
        string transferContent = orderCode;
        string description = string.IsNullOrWhiteSpace(request.Description)
            ? $"Thanh toan goi {package.PackageCode} EcoLink"
            : request.Description.Trim();
        if (description.Length > 250) description = description[..250];

        string qrCodeUrl = BuildDynamicVietQrImageUrl(bankCode, accountNo, package.PriceVnd, transferContent, accountName);
        string? qrPayload = null;
        string? checkoutUrl = qrCodeUrl;
        long? gatewayOrderCode = null;

        // Chỉ gọi PayOS SDK nếu cấu hình ClientId thật đã được nạp; nếu chưa thì dùng chuẩn EMVCo VietQR trực tiếp
        if (_payOsService != null && IsRealPayOsConfigured())
        {
            var payOsRes = await _payOsService.CreatePaymentLinkAsync(new CreatePayOsPaymentRequest
            {
                Amount = (int)package.PriceVnd,
                Description = $"EcoLink {package.PackageCode} {orderCode}",
            }, cancellationToken);

            if (payOsRes.Success)
            {
                gatewayOrderCode = payOsRes.OrderCode;
                if (!string.IsNullOrWhiteSpace(payOsRes.QrCodeUrl))
                {
                    qrPayload = payOsRes.QrCodeUrl;
                }
                if (!string.IsNullOrWhiteSpace(payOsRes.CheckoutUrl))
                {
                    checkoutUrl = payOsRes.CheckoutUrl;
                }
            }
        }

        var order = new ServiceOrder
        {
            Id = Guid.NewGuid(),
            OrderCode = orderCode,
            UserId = currentUserId,
            PackageId = package.Id,
            PackageCodeSnapshot = package.PackageCode,
            PackageNameSnapshot = package.PackageName,
            DurationDaysSnapshot = package.DurationDays,
            FeaturesSnapshot = package.FeaturesJson,
            ExpectedAmountVnd = package.PriceVnd,
            PaymentMethod = "VIETQR",
            Description = description,
            QrPayload = qrPayload,
            QrCodeUrl = qrCodeUrl,
            CheckoutUrl = checkoutUrl,
            TransferContent = transferContent,
            BankCode = bankCode,
            BankName = bankName,
            AccountNo = accountNo,
            AccountName = accountName,
            GatewayOrderCode = gatewayOrderCode,
            OrderStatus = ServiceOrderStatus.Pending,
            PaymentStatus = ServicePaymentStatus.Pending,
            ReconciliationStatus = ReconciliationStatus.Pending,
            AnomalyType = PaymentAnomalyType.None,
            QrExpiresAt = utcNow.AddMinutes(QrTimeToLiveMinutes),
            CreatedAt = utcNow,
            CreatedBy = currentUserId.ToString()
        };

        _dbContext.ServiceOrders.Add(order);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Đã khởi tạo đơn VietQR {OrderCode} cho User={UserId}, Package={PackageCode}, Amount={Amount:N0} VNĐ",
            order.OrderCode, currentUserId, package.PackageCode, order.ExpectedAmountVnd);

        return MapOrderToDto(order, null);
    }

    public async Task<PayInOrderDto?> GetOrderAsync(string orderCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderCode)) return null;

        var normalized = orderCode.Trim().ToUpperInvariant();
        var order = await _dbContext.ServiceOrders
            .Include(o => o.Subscription)
            .FirstOrDefaultAsync(o => o.OrderCode.ToUpper() == normalized, cancellationToken);

        if (order == null) return null;

        await SweepOrderExpiryIfNeededAsync(order, cancellationToken);
        return MapOrderToDto(order, order.Subscription);
    }

    public async Task<PayInOrderDto> GetOrderForUserAsync(
        Guid currentUserId,
        bool isStaffOrAdmin,
        string orderCode,
        CancellationToken cancellationToken = default)
    {
        var normalized = (orderCode ?? string.Empty).Trim().ToUpperInvariant();
        var order = await _dbContext.ServiceOrders
            .Include(o => o.Subscription)
            .FirstOrDefaultAsync(o => o.OrderCode.ToUpper() == normalized, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đơn thanh toán '{orderCode}'.");

        if (!isStaffOrAdmin && order.UserId != currentUserId)
        {
            throw new ForbiddenAccessException(
                "BR-AUTH-08",
                "MSG-ADM-04",
                "Bạn không có quyền truy cập đơn thanh toán của tài khoản khác.");
        }

        await SweepOrderExpiryIfNeededAsync(order, cancellationToken);
        return MapOrderToDto(order, order.Subscription);
    }

    public async Task<IEnumerable<PayInOrderDto>> GetAllOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await _dbContext.ServiceOrders
            .AsNoTracking()
            .Include(o => o.Subscription)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        return orders.Select(o => MapOrderToDto(o, o.Subscription)).ToList();
    }

    public async Task<PagedResult<PayInOrderDto>> GetUserOrdersAsync(
        Guid currentUserId,
        string? status,
        int pageNumber = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _dbContext.ServiceOrders
            .AsNoTracking()
            .Include(o => o.Subscription)
            .Where(o => o.UserId == currentUserId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<ServiceOrderStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(o => o.OrderStatus == parsedStatus);
        }

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PayInOrderDto>
        {
            Items = items.Select(o => MapOrderToDto(o, o.Subscription)).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = total
        };
    }

    public async Task<PayInOrderDto> CancelPendingOrderAsync(
        Guid currentUserId,
        string orderCode,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = (orderCode ?? string.Empty).Trim().ToUpperInvariant();
        var order = await _dbContext.ServiceOrders
            .FirstOrDefaultAsync(o => o.OrderCode.ToUpper() == normalized, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đơn thanh toán '{orderCode}'.");

        if (order.UserId != currentUserId)
        {
            throw new ForbiddenAccessException("BR-AUTH-08", "MSG-ADM-04", "Bạn không có quyền hủy đơn thanh toán của người khác.");
        }

        if (order.OrderStatus == ServiceOrderStatus.Paid)
        {
            throw new ConflictException("BR-PAY-01", "MSG-PAY-03", "Đơn hàng đã thanh toán thành công, không thể hủy.");
        }

        if (order.OrderStatus != ServiceOrderStatus.Pending)
        {
            return MapOrderToDto(order, null);
        }

        order.OrderStatus = ServiceOrderStatus.Cancelled;
        order.PaymentStatus = ServicePaymentStatus.Failed;
        order.AnomalyNote = string.IsNullOrWhiteSpace(reason) ? order.AnomalyNote : reason.Trim();
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = currentUserId.ToString();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return MapOrderToDto(order, null);
    }

    public Task<WebhookResponseDto> ProcessWebhookAsync(VietQrWebhookPayload payload, CancellationToken cancellationToken = default)
        => ProcessWebhookInternalAsync(payload, headerSecret: null, trustedInternalCall: false, cancellationToken);

    public Task<WebhookResponseDto> ProcessWebhookAsync(VietQrWebhookPayload payload, string? headerSecret, CancellationToken cancellationToken = default)
        => ProcessWebhookInternalAsync(payload, headerSecret, trustedInternalCall: false, cancellationToken);

    public async Task<PayInOrderDto> SimulateTransferSuccessAsync(string orderCode, CancellationToken cancellationToken = default)
        => await SimulateTransferAsync(orderCode, null, cancellationToken);

    public async Task<PayInOrderDto> SimulateTransferAsync(
        string orderCode,
        SimulateTransferRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        EnsureSimulatorAllowed();

        var normalized = (orderCode ?? string.Empty).Trim().ToUpperInvariant();
        var order = await _dbContext.ServiceOrders
            .FirstOrDefaultAsync(o => o.OrderCode.ToUpper() == normalized, cancellationToken)
            ?? throw new NotFoundException($"Không tìm thấy đơn thanh toán '{orderCode}'.");

        var scenario = (request?.Scenario ?? "Exact").Trim();
        var utcNow = DateTime.UtcNow;

        decimal amountToSend = order.ExpectedAmountVnd;
        string? targetOrderCode = order.OrderCode;
        string descriptionToSend = $"CHUYEN KHOAN {order.OrderCode}";

        if (request?.SimulatedAmount is > 0m)
        {
            amountToSend = Math.Round(request.SimulatedAmount.Value, 0);
        }
        else if (scenario.Equals("Underpaid", StringComparison.OrdinalIgnoreCase))
        {
            amountToSend = Math.Max(1000m, order.ExpectedAmountVnd - Math.Min(9_000m, Math.Round(order.ExpectedAmountVnd / 2m, 0)));
        }
        else if (scenario.Equals("Overpaid", StringComparison.OrdinalIgnoreCase))
        {
            amountToSend = order.ExpectedAmountVnd + 10_000m;
        }
        else if (scenario.Equals("LateTransfer", StringComparison.OrdinalIgnoreCase))
        {
            if (order.OrderStatus == ServiceOrderStatus.Pending)
            {
                order.QrExpiresAt = utcNow.AddMinutes(-5);
                order.OrderStatus = ServiceOrderStatus.Expired;
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else if (scenario.Equals("Unmatched", StringComparison.OrdinalIgnoreCase))
        {
            targetOrderCode = null;
            descriptionToSend = string.IsNullOrWhiteSpace(request?.SimulatedDescription)
                ? "CHUYEN KHOAN NHAM NOI DUNG"
                : request.SimulatedDescription.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request?.SimulatedDescription) && !scenario.Equals("Unmatched", StringComparison.OrdinalIgnoreCase))
        {
            descriptionToSend = request.SimulatedDescription.Trim();
        }

        var simulatedPayload = new VietQrWebhookPayload
        {
            OrderCode = targetOrderCode,
            Amount = amountToSend,
            Description = descriptionToSend,
            Reference = $"SIM_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            TransactionDateTime = utcNow.ToString("O")
        };

        await ProcessWebhookInternalAsync(simulatedPayload, headerSecret: null, trustedInternalCall: true, cancellationToken);

        var refreshed = await _dbContext.ServiceOrders
            .AsNoTracking()
            .Include(o => o.Subscription)
            .FirstAsync(o => o.Id == order.Id, cancellationToken);

        return MapOrderToDto(refreshed, refreshed.Subscription);
    }

    private async Task<WebhookResponseDto> ProcessWebhookInternalAsync(
        VietQrWebhookPayload payload,
        string? headerSecret,
        bool trustedInternalCall,
        CancellationToken ct)
    {
        var utcNow = DateTime.UtcNow;
        var (parsedOrderCodeRaw, receivedAmount, description, reference, txDateTime, gatewayOrderCode, gatewaySource) =
            NormalizeWebhookPayload(payload, trustedInternalCall);

        if (receivedAmount <= 0m)
        {
            return new WebhookResponseDto
            {
                Success = false,
                Amount = receivedAmount,
                Status = "INVALID_PAYLOAD",
                Message = "Số tiền giao dịch trong Webhook phải lớn hơn 0 VNĐ."
            };
        }

        string rawJson = JsonSerializer.Serialize(payload);
        string webhookCode = GenerateWebhookCode(utcNow);

        // 1. Xác thực chữ ký / Webhook Secret (MSG-PAY-05)
        if (!trustedInternalCall && !VerifyWebhookSignature(payload, headerSecret, parsedOrderCodeRaw, receivedAmount, reference))
        {
            var invalidLog = new PaymentWebhookLog
            {
                Id = Guid.NewGuid(),
                WebhookCode = webhookCode,
                IdempotencyKey = ServicePackageHelpers.ComputeSha256Hex($"INVALID|{webhookCode}|{Guid.NewGuid():N}"),
                GatewaySource = gatewaySource,
                ParsedOrderCode = parsedOrderCodeRaw,
                ReceivedAmountVnd = receivedAmount,
                BankReference = reference,
                TransferDescription = description,
                SignatureValid = false,
                RawPayload = rawJson,
                ProcessingResult = "InvalidSignature",
                AnomalyType = PaymentAnomalyType.None,
                ReceivedAt = utcNow
            };

            _dbContext.PaymentWebhookLogs.Add(invalidLog);
            await _dbContext.SaveChangesAsync(ct);

            return new WebhookResponseDto
            {
                Success = false,
                WebhookCode = webhookCode,
                OrderCode = parsedOrderCodeRaw,
                Amount = receivedAmount,
                Status = "INVALID_SIGNATURE",
                Message = "Dữ liệu hoặc chữ ký xác thực Webhook thanh toán không hợp lệ."
            };
        }

        // 2. Truy tìm mã đơn từ trường trực tiếp, gatewayOrderCode, hoặc nội dung chuyển khoản
        ServiceOrder? matchedOrder = await FindMatchingOrderAsync(parsedOrderCodeRaw, gatewayOrderCode, description, ct);
        var resolvedCodeForKey = matchedOrder?.OrderCode ?? parsedOrderCodeRaw ?? "UNMATCHED";
        var refToken = !string.IsNullOrWhiteSpace(reference)
            ? reference.Trim().ToUpperInvariant()
            : $"{resolvedCodeForKey}|{receivedAmount:0}|{(string.IsNullOrWhiteSpace(txDateTime) ? utcNow.ToString("yyyyMMddHHmmssfff") : txDateTime.Trim())}";

        var idempotencyKey = ServicePackageHelpers.ComputeSha256Hex($"{gatewaySource}|{refToken}|{resolvedCodeForKey}");

        // 3. Kiểm tra Idempotency (BR-PAY-02)
        if (await _dbContext.PaymentWebhookLogs.AsNoTracking().AnyAsync(w => w.IdempotencyKey == idempotencyKey, ct))
        {
            return new WebhookResponseDto
            {
                Success = true,
                OrderCode = matchedOrder?.OrderCode ?? parsedOrderCodeRaw,
                Amount = receivedAmount,
                Status = "ALREADY_PROCESSED",
                ReconciliationStatus = matchedOrder?.ReconciliationStatus.ToString(),
                Message = "Giao dịch thanh toán này đã được xử lý trước đó (Idempotent BR-PAY-02)."
            };
        }

        // 4. Phân loại 5 kịch bản giao dịch (Matched, DuplicateIgnored, UnmatchedReference, LateTransfer, Underpaid, Overpaid)
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);

        if (matchedOrder == null)
        {
            var orphanLog = new PaymentWebhookLog
            {
                Id = Guid.NewGuid(),
                WebhookCode = webhookCode,
                IdempotencyKey = idempotencyKey,
                GatewaySource = gatewaySource,
                MatchedOrderId = null,
                ParsedOrderCode = parsedOrderCodeRaw,
                ReceivedAmountVnd = receivedAmount,
                BankReference = reference,
                TransferDescription = description,
                SignatureValid = true,
                RawPayload = rawJson,
                ProcessingResult = "UnmatchedReference",
                AnomalyType = PaymentAnomalyType.UnmatchedReference,
                ReceivedAt = utcNow
            };

            _dbContext.PaymentWebhookLogs.Add(orphanLog);
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            _logger.LogWarning(
                "[Webhook Anomaly] Giao dịch mồ côi {WebhookCode} không khớp mã đơn. Amount={Amount:N0}, Desc={Desc}",
                webhookCode, receivedAmount, description);

            return new WebhookResponseDto
            {
                Success = true,
                WebhookCode = webhookCode,
                OrderCode = null,
                Amount = receivedAmount,
                Status = "ANOMALY_FLAGGED",
                ReconciliationStatus = nameof(ReconciliationStatus.UnmatchedReference),
                Message = $"Đã ghi nhận giao dịch chưa khớp mã đơn ({webhookCode}) vào sổ cái đối soát Admin."
            };
        }

        // Nếu đơn đã thanh toán trước đó bằng một mã tham chiếu khác
        if (matchedOrder.OrderStatus == ServiceOrderStatus.Paid)
        {
            var dupLog = new PaymentWebhookLog
            {
                Id = Guid.NewGuid(),
                WebhookCode = webhookCode,
                IdempotencyKey = idempotencyKey,
                GatewaySource = gatewaySource,
                MatchedOrderId = matchedOrder.Id,
                ParsedOrderCode = matchedOrder.OrderCode,
                ReceivedAmountVnd = receivedAmount,
                BankReference = reference,
                TransferDescription = description,
                SignatureValid = true,
                RawPayload = rawJson,
                ProcessingResult = "DuplicateIgnored",
                AnomalyType = PaymentAnomalyType.None,
                ReceivedAt = utcNow
            };

            _dbContext.PaymentWebhookLogs.Add(dupLog);
            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new WebhookResponseDto
            {
                Success = true,
                WebhookCode = webhookCode,
                OrderCode = matchedOrder.OrderCode,
                Amount = matchedOrder.ExpectedAmountVnd,
                Status = "ALREADY_PROCESSED",
                ReconciliationStatus = matchedOrder.ReconciliationStatus.ToString(),
                Message = "Đơn hàng này đã được xác nhận thanh toán trước đó."
            };
        }

        string effectiveReference = string.IsNullOrWhiteSpace(reference)
            ? $"REF_{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}"
            : reference.Trim();

        // Chuyển khoản trễ hạn QR 15 phút hoặc đơn đã hủy/hết hạn -> LateTransfer
        if (matchedOrder.OrderStatus is ServiceOrderStatus.Expired or ServiceOrderStatus.Cancelled || utcNow > matchedOrder.QrExpiresAt)
        {
            if (matchedOrder.OrderStatus == ServiceOrderStatus.Pending && utcNow > matchedOrder.QrExpiresAt)
            {
                matchedOrder.OrderStatus = ServiceOrderStatus.Expired;
            }

            matchedOrder.PaidAmountVnd = (matchedOrder.PaidAmountVnd ?? 0m) + receivedAmount;
            matchedOrder.PaymentStatus = ServicePaymentStatus.Anomaly;
            matchedOrder.ReconciliationStatus = ReconciliationStatus.LateTransfer;
            matchedOrder.AnomalyType = PaymentAnomalyType.LateTransfer;
            matchedOrder.AnomalyNote = $"Nhận chuyển khoản {receivedAmount:N0} VNĐ sau khi mã QR đã hết hạn hoặc đơn đã hủy ({matchedOrder.QrExpiresAt:yyyy-MM-dd HH:mm} UTC).";
            matchedOrder.PaymentReference ??= effectiveReference;
            matchedOrder.UpdatedAt = utcNow;

            _dbContext.PaymentWebhookLogs.Add(BuildOrderWebhookLog(
                webhookCode, idempotencyKey, gatewaySource, matchedOrder, receivedAmount, effectiveReference, description, rawJson,
                "LateTransfer", PaymentAnomalyType.LateTransfer, utcNow));

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new WebhookResponseDto
            {
                Success = true,
                WebhookCode = webhookCode,
                OrderCode = matchedOrder.OrderCode,
                Amount = receivedAmount,
                Status = "ANOMALY_FLAGGED",
                ReconciliationStatus = nameof(ReconciliationStatus.LateTransfer),
                Message = "Mã thanh toán đã hết hạn; giao dịch đã được chuyển vào hàng đợi đối soát Admin."
            };
        }

        // Chuyển thiếu tiền -> Underpaid (GIỮ Pending, KHÔNG kích hoạt gói)
        if (receivedAmount < matchedOrder.ExpectedAmountVnd)
        {
            var shortage = matchedOrder.ExpectedAmountVnd - receivedAmount;
            matchedOrder.PaidAmountVnd = receivedAmount;
            matchedOrder.PaymentStatus = ServicePaymentStatus.Anomaly;
            matchedOrder.ReconciliationStatus = ReconciliationStatus.Underpaid;
            matchedOrder.AnomalyType = PaymentAnomalyType.Underpaid;
            matchedOrder.AnomalyNote = $"Khách chuyển {receivedAmount:N0} VNĐ, thiếu {shortage:N0} VNĐ so với giá gói ({matchedOrder.ExpectedAmountVnd:N0} VNĐ).";
            matchedOrder.PaymentReference = effectiveReference;
            matchedOrder.UpdatedAt = utcNow;

            _dbContext.PaymentWebhookLogs.Add(BuildOrderWebhookLog(
                webhookCode, idempotencyKey, gatewaySource, matchedOrder, receivedAmount, effectiveReference, description, rawJson,
                "Underpaid", PaymentAnomalyType.Underpaid, utcNow));

            await _dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return new WebhookResponseDto
            {
                Success = true,
                WebhookCode = webhookCode,
                OrderCode = matchedOrder.OrderCode,
                Amount = receivedAmount,
                Status = "ANOMALY_FLAGGED",
                ReconciliationStatus = nameof(ReconciliationStatus.Underpaid),
                Message = matchedOrder.AnomalyNote
            };
        }

        // Thanh toán đủ hoặc dư tiền trong hạn -> Kích hoạt gói dịch vụ ngay (Matched / Overpaid)
        var isOverpaid = receivedAmount > matchedOrder.ExpectedAmountVnd;
        var reconciliationStatus = isOverpaid ? ReconciliationStatus.Overpaid : ReconciliationStatus.Matched;
        var anomalyType = isOverpaid ? PaymentAnomalyType.Overpaid : PaymentAnomalyType.None;

        matchedOrder.PaidAmountVnd = receivedAmount;
        matchedOrder.OrderStatus = ServiceOrderStatus.Paid;
        matchedOrder.PaymentStatus = ServicePaymentStatus.Success;
        matchedOrder.ReconciliationStatus = reconciliationStatus;
        matchedOrder.AnomalyType = anomalyType;
        matchedOrder.AnomalyNote = isOverpaid
            ? $"Khách chuyển dư {(receivedAmount - matchedOrder.ExpectedAmountVnd):N0} VNĐ so với giá gói ({matchedOrder.ExpectedAmountVnd:N0} VNĐ); đã kích hoạt gói ngay và chờ đối soát hoàn dư."
            : null;
        matchedOrder.PaymentReference = effectiveReference;
        matchedOrder.GatewayTransactionNo = effectiveReference;
        matchedOrder.PaidAt = utcNow;
        matchedOrder.UpdatedAt = utcNow;

        _dbContext.PaymentWebhookLogs.Add(BuildOrderWebhookLog(
            webhookCode, idempotencyKey, gatewaySource, matchedOrder, receivedAmount, effectiveReference, description, rawJson,
            isOverpaid ? "Overpaid" : "Matched", anomalyType, utcNow));

        await ServicePackageHelpers.ActivateSubscriptionForOrderAsync(_dbContext, matchedOrder, utcNow, ct);
        await _dbContext.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        _entitlementService.InvalidateUserEntitlements(matchedOrder.UserId);

        _logger.LogInformation(
            "Xác nhận thanh toán VietQR {OrderCode}: Amount={Amount:N0}, Reconciliation={Rec}",
            matchedOrder.OrderCode, receivedAmount, reconciliationStatus);

        return new WebhookResponseDto
        {
            Success = true,
            WebhookCode = webhookCode,
            OrderCode = matchedOrder.OrderCode,
            Amount = receivedAmount,
            Status = "PAID",
            ReconciliationStatus = reconciliationStatus.ToString(),
            Message = "Thanh toán dịch vụ của bạn đã được xác nhận thành công."
        };
    }

    private async Task<ServiceOrder?> FindMatchingOrderAsync(
        string? parsedOrderCodeRaw,
        long? gatewayOrderCode,
        string? description,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(parsedOrderCodeRaw))
        {
            var codeNorm = parsedOrderCodeRaw.Trim().ToUpperInvariant();
            var direct = await _dbContext.ServiceOrders
                .FirstOrDefaultAsync(o => o.OrderCode.ToUpper() == codeNorm, ct);
            if (direct != null) return direct;
        }

        if (gatewayOrderCode.HasValue && gatewayOrderCode.Value > 0)
        {
            var byGateway = await _dbContext.ServiceOrders
                .FirstOrDefaultAsync(o => o.GatewayOrderCode == gatewayOrderCode.Value, ct);
            if (byGateway != null) return byGateway;
        }

        if (!string.IsNullOrWhiteSpace(description))
        {
            var regexMatch = OrderCodePattern().Match(description);
            if (regexMatch.Success)
            {
                var extracted = regexMatch.Groups[1].Value.ToUpperInvariant();
                var byRegex = await _dbContext.ServiceOrders
                    .FirstOrDefaultAsync(o => o.OrderCode.ToUpper() == extracted, ct);
                if (byRegex != null) return byRegex;
            }

            var recentOrders = await _dbContext.ServiceOrders
                .OrderByDescending(o => o.CreatedAt)
                .Take(200)
                .ToListAsync(ct);

            foreach (var candidate in recentOrders)
            {
                if (description.Contains(candidate.OrderCode, StringComparison.OrdinalIgnoreCase))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private async Task SweepOrderExpiryIfNeededAsync(ServiceOrder order, CancellationToken ct)
    {
        if (order.OrderStatus == ServiceOrderStatus.Pending && DateTime.UtcNow > order.QrExpiresAt)
        {
            order.OrderStatus = ServiceOrderStatus.Expired;
            order.PaymentStatus = ServicePaymentStatus.Failed;
            order.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
        }
    }

    private bool VerifyWebhookSignature(
        VietQrWebhookPayload payload,
        string? headerSecret,
        string? parsedOrderCode,
        decimal receivedAmount,
        string? reference)
    {
        var vietQrSecret = ResolveOptionalSecret("VietQr:WebhookSecret");
        var payOsChecksumKey = ResolveOptionalSecret("PayOS:ChecksumKey");

        if (!string.IsNullOrEmpty(vietQrSecret))
        {
            if (!string.IsNullOrWhiteSpace(headerSecret) && FixedEquals(headerSecret.Trim(), vietQrSecret))
            {
                return true;
            }

            if (!string.IsNullOrWhiteSpace(payload.Signature))
            {
                var canonical = $"{parsedOrderCode}|{receivedAmount:0}|{reference}";
                var expectedSig = ComputeHmacSha256Hex(vietQrSecret, canonical);
                if (FixedEquals(payload.Signature.Trim().ToUpperInvariant(), expectedSig.ToUpperInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        if (!string.IsNullOrEmpty(payOsChecksumKey) && payload.Data != null)
        {
            if (string.IsNullOrWhiteSpace(payload.Signature)) return false;
            var payOsCanonical = $"amount={receivedAmount:0}&description={payload.Data.Description}&orderCode={payload.Data.OrderCode}&reference={payload.Data.Reference}";
            var expectedPayOs = ComputeHmacSha256Hex(payOsChecksumKey, payOsCanonical);
            return FixedEquals(payload.Signature.Trim().ToUpperInvariant(), expectedPayOs.ToUpperInvariant());
        }

        // Nếu không cấu hình secret ở dev nhưng caller chủ động gửi Signature -> không hợp lệ nếu chữ ký sai
        if (!string.IsNullOrWhiteSpace(payload.Signature))
        {
            return false;
        }

        return true;
    }

    private void EnsureSimulatorAllowed()
    {
        var configuredToggle = _config["VietQr:EnableSimulator"];
        if (bool.TryParse(configuredToggle, out var explicitFlag))
        {
            if (!explicitFlag)
            {
                throw new ForbiddenAccessException(
                    "BR-PAY-01",
                    "MSG-ADM-04",
                    "Chức năng giả lập chuyển khoản đang bị khóa trên môi trường này.");
            }
            return;
        }

        var envName = _config["ASPNETCORE_ENVIRONMENT"]
                      ?? _config["DOTNET_ENVIRONMENT"]
                      ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        if (string.Equals(envName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenAccessException(
                "BR-PAY-01",
                "MSG-ADM-04",
                "Không được phép chạy giả lập thanh toán trên môi trường Production.");
        }
    }

    private static PaymentWebhookLog BuildOrderWebhookLog(
        string webhookCode,
        string idempotencyKey,
        string gatewaySource,
        ServiceOrder matchedOrder,
        decimal receivedAmount,
        string? reference,
        string? description,
        string rawJson,
        string processingResult,
        PaymentAnomalyType anomalyType,
        DateTime utcNow)
    {
        return new PaymentWebhookLog
        {
            Id = Guid.NewGuid(),
            WebhookCode = webhookCode,
            IdempotencyKey = idempotencyKey,
            GatewaySource = gatewaySource,
            MatchedOrderId = matchedOrder.Id,
            ParsedOrderCode = matchedOrder.OrderCode,
            ReceivedAmountVnd = receivedAmount,
            BankReference = reference,
            TransferDescription = description,
            SignatureValid = true,
            RawPayload = rawJson,
            ProcessingResult = processingResult,
            AnomalyType = anomalyType,
            ReceivedAt = utcNow
        };
    }

    private static (
        string? OrderCodeRaw,
        decimal Amount,
        string? Description,
        string? Reference,
        string? TxDateTime,
        long? GatewayOrderCode,
        string GatewaySource) NormalizeWebhookPayload(VietQrWebhookPayload payload, bool trustedInternalCall)
    {
        if (payload.Data != null)
        {
            return (
                payload.Data.OrderCode?.ToString(),
                payload.Data.Amount,
                payload.Data.Description ?? payload.Description,
                payload.Data.Reference ?? payload.Reference,
                payload.Data.TransactionDateTime ?? payload.TransactionDateTime,
                payload.Data.OrderCode,
                trustedInternalCall ? "SIMULATOR" : "PAYOS");
        }

        long? numericGatewayCode = long.TryParse(payload.OrderCode, out var gwCode) ? gwCode : null;
        return (
            payload.OrderCode,
            payload.Amount,
            payload.Description,
            payload.Reference,
            payload.TransactionDateTime,
            numericGatewayCode,
            trustedInternalCall ? "SIMULATOR" : "VIETQR");
    }

    private static PayInOrderDto MapOrderToDto(ServiceOrder order, UserSubscription? sub)
    {
        ActiveSubscriptionDto? activeSubDto = null;
        if (sub != null)
        {
            var utcNow = DateTime.UtcNow;
            activeSubDto = new ActiveSubscriptionDto
            {
                SubscriptionId = sub.Id,
                PackageId = sub.PackageId,
                PackageCode = sub.PackageCode,
                PackageName = order.PackageNameSnapshot,
                SourceOrderCode = order.OrderCode,
                StartsAt = sub.StartsAt,
                ExpiresAt = sub.ExpiresAt,
                RemainingDays = sub.ExpiresAt > utcNow ? (int)Math.Ceiling((sub.ExpiresAt - utcNow).TotalDays) : 0,
                Status = sub.Status.ToString(),
                Features = ServicePackageHelpers.ParseFeatures(sub.FeaturesSnapshot)
            };
        }

        return new PayInOrderDto
        {
            Id = order.Id,
            UserId = order.UserId,
            PackageId = order.PackageId,
            PackageCode = order.PackageCodeSnapshot,
            PackageName = order.PackageNameSnapshot,
            DurationDays = order.DurationDaysSnapshot,
            OrderCode = order.OrderCode,
            Amount = order.ExpectedAmountVnd,
            PaidAmount = order.PaidAmountVnd,
            Description = order.Description,
            TransferContent = order.TransferContent,
            BankCode = order.BankCode,
            BankName = order.BankName,
            AccountNo = order.AccountNo,
            AccountName = order.AccountName,
            QrCodeUrl = order.QrCodeUrl,
            QrPayload = order.QrPayload,
            CheckoutUrl = order.CheckoutUrl,
            Status = order.OrderStatus.ToString().ToUpperInvariant(),
            ReconciliationStatus = order.ReconciliationStatus.ToString(),
            AnomalyType = order.AnomalyType.ToString(),
            AnomalyNote = order.AnomalyNote,
            CreatedAt = order.CreatedAt,
            ExpiresAt = order.QrExpiresAt,
            PaidAt = order.PaidAt,
            TransactionReference = order.PaymentReference,
            ActivatedSubscription = activeSubDto
        };
    }

    private string ResolveConfig(string key, string fallback)
    {
        var val = _config[key];
        if (string.IsNullOrWhiteSpace(val) || (val.StartsWith('{') && val.EndsWith('}')))
        {
            return fallback;
        }
        return val.Trim();
    }

    private string? ResolveOptionalSecret(string key)
    {
        var val = _config[key];
        if (string.IsNullOrWhiteSpace(val)
            || (val.StartsWith('{') && val.EndsWith('}'))
            || val.StartsWith("YOUR_PAYOS_", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        return val.Trim();
    }

    private bool IsRealPayOsConfigured()
    {
        return !string.IsNullOrEmpty(ResolveOptionalSecret("PayOS:ClientId"))
               && !string.IsNullOrEmpty(ResolveOptionalSecret("PayOS:ApiKey"))
               && !string.IsNullOrEmpty(ResolveOptionalSecret("PayOS:ChecksumKey"));
    }

    private static string BuildDynamicVietQrImageUrl(
        string bankCode,
        string accountNo,
        decimal amountVnd,
        string transferContent,
        string accountName)
    {
        return $"https://img.vietqr.io/image/{bankCode}-{accountNo}-compact2.png" +
               $"?amount={(long)amountVnd}" +
               $"&addInfo={Uri.EscapeDataString(transferContent)}" +
               $"&accountName={Uri.EscapeDataString(accountName)}";
    }

    private static string GenerateUniqueOrderCode(DateTime utcNow)
    {
        var suffix = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        return $"ECO{utcNow:yyMMdd}{Random.Shared.Next(100, 999)}{suffix}";
    }

    private static string GenerateWebhookCode(DateTime utcNow)
    {
        var suffix = Guid.NewGuid().ToString("N")[..4].ToUpperInvariant();
        return $"WH-{utcNow:yyyyMMdd}-{suffix}";
    }

    private static string ComputeHmacSha256Hex(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash);
    }

    private static bool FixedEquals(string a, string b)
    {
        var bytesA = Encoding.UTF8.GetBytes(a);
        var bytesB = Encoding.UTF8.GetBytes(b);
        return bytesA.Length == bytesB.Length && CryptographicOperations.FixedTimeEquals(bytesA, bytesB);
    }
}
