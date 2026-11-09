using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Danh mục phế liệu chuẩn của hệ thống (dùng cho dropdown "Add Scrap Type" và bộ lọc tìm cơ sở thu gom).
/// </summary>
[ApiController]
[Route("api/v1/waste-categories")]
[Authorize]
[Produces("application/json")]
public class WasteCategoryController : ControllerBase
{
    private readonly IRecyclerManagementService _service;

    public WasteCategoryController(IRecyclerManagementService service)
    {
        _service = service;
    }

    /// <summary>Lấy danh sách loại phế liệu có thể thu mua (không gồm rác thải thông thường).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<WasteCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var data = await _service.GetWasteCategoriesAsync(ct);
        return Ok(ApiResponse<IReadOnlyList<WasteCategoryDto>>.Ok(data));
    }
}
