using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Phân hệ Tìm kiếm Cơ sở Thu gom theo Vị trí Địa lý (Module: Location-Based Recycler Discovery - UC U-05).
/// Truy vấn các cơ sở đã xác minh (Verified) và đang mở cửa (Online) kèm bảng giá niêm yết (RecyclerPrices).
/// </summary>
[ApiController]
[Route("api/v1/recyclers")]
[Produces("application/json")]
public class RecyclersDiscoveryController : ControllerBase
{
    private readonly IGeoMatchingService _geoMatchingService;
    private readonly IValidator<NearbyRecyclerFilterRequest> _nearbyValidator;

    public RecyclersDiscoveryController(
        IGeoMatchingService geoMatchingService,
        IValidator<NearbyRecyclerFilterRequest> nearbyValidator)
    {
        _geoMatchingService = geoMatchingService;
        _nearbyValidator = nearbyValidator;
    }

    /// <summary>
    /// 1. [U-05] Tìm kiếm các Cơ sở thu gom đã xác minh trong bán kính GPS (BR-MTH-04, BR-MTH-05)
    /// </summary>
    [HttpGet("nearby")]
    [ProducesResponseType(typeof(ApiResponse<NearbyRecyclerSearchResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<NearbyRecyclerSearchResponseDto>>> SearchNearby(
        [FromQuery] NearbyRecyclerFilterRequest filter,
        CancellationToken ct)
    {
        var validationResult = await _nearbyValidator.ValidateAsync(filter, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList();
            return BadRequest(ApiResponse<object>.Fail(
                "Cần quyền truy cập vị trí để tìm cơ sở thu mua gần bạn. Vui lòng bật trong phần cài đặt.",
                "MSG-MTH-02",
                errors));
        }

        var result = await _geoMatchingService.SearchNearbyRecyclersAsync(filter, ct);

        if (result.TotalFound == 0)
        {
            return Ok(ApiResponse<NearbyRecyclerSearchResponseDto>.Ok(
                result,
                message: "Không tìm thấy cơ sở thu mua rác gần bạn.",
                messageCode: "MSG-MTH-01"));
        }

        return Ok(ApiResponse<NearbyRecyclerSearchResponseDto>.Ok(
            result,
            message: $"Tìm thấy {result.TotalFound} cơ sở thu mua trong bán kính {result.SearchRadiusKm} km.",
            messageCode: "MSG-GEN-01"));
    }

    /// <summary>
    /// 2. [U-05] Xem thông tin công khai, giờ mở cửa và bảng giá niêm yết của một Cơ sở thu gom cụ thể trên bản đồ
    /// </summary>
    [HttpGet("{recyclerId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<CollectionPartnerDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CollectionPartnerDto>>> GetRecyclerDetail(
        Guid recyclerId,
        [FromQuery] double? lat = null,
        [FromQuery] double? lng = null,
        CancellationToken ct = default)
    {
        var result = await _geoMatchingService.GetRecyclerPublicDetailAsync(recyclerId, lat, lng, ct);
        return Ok(ApiResponse<CollectionPartnerDto>.Ok(
            result,
            message: "Lấy thông tin cơ sở thu mua thành công.",
            messageCode: "MSG-GEN-01"));
    }
}

