using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Backend Geocoding Proxy phục vụ tìm kiếm khu vực trên bản đồ (UC U-05, BR-MTH-05).
/// Giấu API Key (nếu dùng LocationIQ), đính kèm User-Agent hợp lệ, lưu đệm IMemoryCache và giới hạn tốc độ gọi Nominatim.
/// </summary>
[ApiController]
[Route("api/v1/geocoding")]
[Produces("application/json")]
public class GeocodingController : ControllerBase
{
    private readonly IGeocodingProxyService _geocodingService;

    public GeocodingController(IGeocodingProxyService geocodingService)
    {
        _geocodingService = geocodingService;
    }

    /// <summary>
    /// Tra cứu toạ độ khu vực theo địa danh tại Việt Nam (chỉ gọi khi người dùng nhấn Enter / Tìm kiếm).
    /// </summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<GeocodingSearchResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<GeocodingSearchResponseDto>>> SearchPlaces(
        [FromQuery] string? q,
        [FromQuery] int limit = 5,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2)
        {
            return BadRequest(ApiResponse<object>.Fail(
                message: "Vui lòng nhập tối thiểu 2 ký tự tên phường, quận, chợ hoặc khu vực cần tìm.",
                messageCode: "MSG-GEN-01"));
        }

        var result = await _geocodingService.SearchPlacesAsync(q, limit, ct);
        return Ok(ApiResponse<GeocodingSearchResponseDto>.Ok(
            result,
            message: result.Items.Count > 0
                ? $"Tìm thấy {result.Items.Count} khu vực phù hợp."
                : "Không tìm thấy địa danh phù hợp. Hãy thử nhập tên Phường, Quận hoặc Chợ lớn gần bạn.",
            messageCode: "MSG-GEN-01"));
    }
}

