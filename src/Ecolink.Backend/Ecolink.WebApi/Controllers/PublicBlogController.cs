using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>Blog công khai cho khách / người dùng: chỉ bài đã Approved và đã đến giờ xuất bản (BR-ADM-08).</summary>
[ApiController]
[Route("api/v1/blog-posts")]
[AllowAnonymous]
[Produces("application/json")]
public class PublicBlogController : ControllerBase
{
    private readonly IBlogPostService _service;

    public PublicBlogController(IBlogPostService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<BlogPagedResult<BlogPostSummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] string? tag,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 12, CancellationToken ct = default)
    {
        var data = await _service.ListPublishedAsync(search, tag, page, pageSize, ct);
        return Ok(ApiResponse<BlogPagedResult<BlogPostSummaryDto>>.Ok(data));
    }

    [HttpGet("{slug}")]
    [ProducesResponseType(typeof(ApiResponse<PublicBlogPostDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBySlug(string slug, CancellationToken ct)
    {
        var data = await _service.GetPublishedBySlugAsync(slug, ct);
        return Ok(ApiResponse<PublicBlogPostDto>.Ok(data));
    }
}
