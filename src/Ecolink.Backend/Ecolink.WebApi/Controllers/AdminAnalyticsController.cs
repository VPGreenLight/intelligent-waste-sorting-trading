using System.Security.Claims;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Phân hệ Giám sát Điều hành, Xuất Báo cáo Ẩn danh &amp; Tra cứu Nhật ký Kiểm toán (FE-18 / M-15: UC A-01, A-02).
/// Bao phủ các endpoint STT 150 - STT 153 trong 00_api_inventory_and_endpoint_statistics.md.
/// </summary>
[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "Admin")]
[Produces("application/json")]
public class AdminAnalyticsController : ControllerBase
{
    private readonly IAdminAnalyticsService _analyticsService;
    private readonly IAuditLogService _auditLogService;

    public AdminAnalyticsController(
        IAdminAnalyticsService analyticsService,
        IAuditLogService auditLogService)
    {
        _analyticsService = analyticsService;
        _auditLogService = auditLogService;
    }

    /// <summary>
    /// [STT 150] [A-01] Hiển thị bảng chỉ số KPI điều hành: sản lượng tái chế (kg/tấn), độ chính xác AI, người dùng tích cực &amp; doanh thu VietQR (BR-ADM-07).
    /// </summary>
    [HttpGet("analytics/kpi-summary")]
    [ProducesResponseType(typeof(ApiResponse<ExecutiveKpiSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<ExecutiveKpiSummaryDto>>> GetExecutiveKpiSummary(
        [FromQuery] ExecutiveKpiQueryRequest query,
        CancellationToken ct)
    {
        var summary = await _analyticsService.GetExecutiveKpiSummaryAsync(query, ct);
        return Ok(ApiResponse<ExecutiveKpiSummaryDto>.Ok(
            summary,
            "Tải chỉ số điều hành hệ thống thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 151] [A-01] Truy vấn dữ liệu biểu đồ thống kê theo kỳ: xu hướng giao dịch, tỷ trọng phế liệu và chất lượng AI theo nhóm (BR-ADM-07).
    /// </summary>
    [HttpGet("analytics/charts")]
    [ProducesResponseType(typeof(ApiResponse<AnalyticsChartsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AnalyticsChartsDto>>> GetAnalyticsCharts(
        [FromQuery] int months = 6,
        CancellationToken ct = default)
    {
        var charts = await _analyticsService.GetAnalyticsChartsAsync(months, ct);
        return Ok(ApiResponse<AnalyticsChartsDto>.Ok(
            charts,
            "Tải dữ liệu biểu đồ phân tích thành công.",
            "MSG-GEN-01"));
    }

    /// <summary>
    /// [STT 152] [A-02] Trích xuất báo cáo tổng hợp hệ thống ra tệp CSV (UTF-8 BOM) hoặc XLSX ẩn danh hóa PII (BR-ADM-06) và tự động ghi Audit Log (BR-ADM-05).
    /// </summary>
    [HttpGet("reports/export")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportSystemReport(
        [FromQuery] ReportExportQueryRequest query,
        CancellationToken ct)
    {
        var adminUserId = GetCurrentUserId();
        var exported = await _analyticsService.ExportSystemReportAsync(adminUserId, query, ct);

        Response.Headers["X-EcoLink-Message-Code"] = "MSG-ADM-02";
        Response.Headers["X-EcoLink-Row-Count"] = exported.RowCount.ToString();

        return File(exported.Content, exported.ContentType, exported.FileName);
    }

    /// <summary>
    /// [STT 153] [A-02] Tra cứu nhật ký kiểm toán hệ thống bất biến (Immutable Audit Trail chỉ đọc theo BR-ADM-05).
    /// </summary>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditLogItemDto>>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<PagedResult<AuditLogItemDto>>>> GetSystemAuditLogs(
        [FromQuery] AuditLogFilterRequest filter,
        CancellationToken ct)
    {
        var logs = await _auditLogService.GetAuditLogsAsync(filter, ct);
        return Ok(ApiResponse<PagedResult<AuditLogItemDto>>.Ok(
            logs,
            "Lấy nhật ký kiểm toán hệ thống thành công.",
            "MSG-GEN-01"));
    }

    private Guid GetCurrentUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (!Guid.TryParse(sub, out var userId))
        {
            throw new UnauthorizedAccessException("Không xác định được định danh quản trị viên từ Token.");
        }
        return userId;
    }
}
