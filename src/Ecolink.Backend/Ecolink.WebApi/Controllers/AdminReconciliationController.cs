using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Sổ cái đối soát dòng tiền VietQR, xử lý giao dịch bất thường &amp; xuất bảng kê (M-12 / FE-14: UC A-11).
/// Bao phủ STT 127, 128, 129 trong 00_api_inventory_and_endpoint_statistics.md.
/// </summary>
[ApiController]
[Route("api/v1/admin/reconciliations")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminReconciliationController : ControllerBase
{
    private readonly IAdminReconciliationService _reconciliationService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IValidator<ResolvePaymentAnomalyRequest> _resolveValidator;

    public AdminReconciliationController(
        IAdminReconciliationService reconciliationService,
        ICurrentUserService currentUserService,
        IValidator<ResolvePaymentAnomalyRequest> resolveValidator)
    {
        _reconciliationService = reconciliationService;
        _currentUserService = currentUserService;
        _resolveValidator = resolveValidator;
    }

    /// <summary>
    /// [STT 127] [A-11] Theo dõi sổ cái dòng tiền VietQR và lọc các giao dịch bất thường (BR-PAY-06).
    /// </summary>
    [HttpGet("ledger")]
    [ProducesResponseType(typeof(ApiResponse<ReconciliationLedgerResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ReconciliationLedgerResponseDto>>> GetPaymentLedger(
        [FromQuery] ReconciliationLedgerQuery query,
        CancellationToken ct)
    {
        var ledger = await _reconciliationService.GetPaymentLedgerAsync(query, ct);
        return Ok(ApiResponse<ReconciliationLedgerResponseDto>.Ok(
            ledger,
            "Tải sổ cái đối soát tài chính thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 128] [A-11] Đối soát thủ công giao dịch bất thường (nhận cả mã đơn ECO... và mã webhook WH-...) kèm Audit Log (BR-PAY-06, BR-ADM-05).
    /// </summary>
    [HttpPatch("orders/{orderCode}/resolve")]
    [ProducesResponseType(typeof(ApiResponse<ReconciliationLedgerItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<ReconciliationLedgerItemDto>>> ResolvePaymentAnomaly(
        string orderCode,
        [FromBody] ResolvePaymentAnomalyRequest request,
        CancellationToken ct)
    {
        var validation = await _resolveValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var errors = validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail("Thông tin đối soát bất thường không hợp lệ.", "MSG-GEN-01", errors));
        }

        var adminUserId = _currentUserService.UserId
                          ?? throw new UnauthorizedAccessException("Phiên đăng nhập quản trị không hợp lệ.");

        var resolved = await _reconciliationService.ResolvePaymentAnomalyAsync(adminUserId, orderCode, request, ct);
        return Ok(ApiResponse<ReconciliationLedgerItemDto>.Ok(
            resolved,
            "Đã ghi nhận kết quả đối soát giao dịch thành công.",
            "MSG-PAY-08"));
    }

    /// <summary>
    /// [STT 129] [A-11] Xuất bảng kê đối soát doanh thu VietQR theo kỳ kế toán (CSV UTF-8 BOM / XLSX) ẩn danh PII (BR-ADM-06).
    /// </summary>
    [HttpGet("export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportReconciliationReport(
        [FromQuery] ReconciliationExportQuery query,
        CancellationToken ct)
    {
        var adminUserId = _currentUserService.UserId
                          ?? throw new UnauthorizedAccessException("Phiên đăng nhập quản trị không hợp lệ.");

        var exported = await _reconciliationService.ExportReconciliationReportAsync(adminUserId, query, ct);

        Response.Headers["X-EcoLink-Message-Code"] = "MSG-ADM-02";
        Response.Headers["X-EcoLink-Row-Count"] = exported.RowCount.ToString();

        return File(exported.Content, exported.ContentType, exported.FileName);
    }
}
