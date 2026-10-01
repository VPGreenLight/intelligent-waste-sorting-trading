using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Đăng ký tài khoản mới (Người dân hoặc Điểm thu gom)
    /// </summary>
    /// <param name="request">Thông tin đăng ký (Username, Password, Email, Role,...)</param>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request, CancellationToken ct)
    {
        var result = await _authService.RegisterAsync(request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Đăng nhập tài khoản bằng Username hoặc Email
    /// </summary>
    /// <param name="request">Thông tin đăng nhập</param>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        if (!result.Success)
        {
            return BadRequest(result);
        }
        return Ok(result);
    }

    /// <summary>
    /// Lấy thông tin người dùng hiện tại qua Bearer token (Hỗ trợ qua Header hoặc Query Param)
    /// </summary>
    /// <param name="authorization">Header 'Authorization: Bearer <token>'</param>
    /// <param name="token">Hoặc dán trực tiếp token vào đây để test nhanh trên Swagger</param>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserInfoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetCurrentUser(
        [FromHeader(Name = "Authorization")] string? authorization = null,
        [FromQuery] string? token = null,
        CancellationToken ct = default)
    {
        string? rawToken = authorization;
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            rawToken = Request.Headers.Authorization.ToString();
        }
        if (string.IsNullOrWhiteSpace(rawToken) && !string.IsNullOrWhiteSpace(token))
        {
            rawToken = token;
        }

        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return Unauthorized(new { message = "Thiếu Authorization Header hoặc query param 'token'. Vui lòng đính kèm Bearer token." });
        }

        var user = await _authService.GetUserByTokenAsync(rawToken, ct);
        if (user == null)
        {
            return Unauthorized(new { message = "Token không hợp lệ hoặc đã hết hạn." });
        }

        return Ok(user);
    }

    /// <summary>
    /// Danh sách các tài khoản Demo có sẵn để test nhanh trên Swagger
    /// </summary>
    [HttpGet("demo-users")]
    public async Task<IActionResult> GetDemoUsers(CancellationToken ct)
    {
        var users = await _authService.GetAllDemoUsersAsync(ct);
        return Ok(new
        {
            message = "Tài khoản mẫu để test nhanh: citizen / citizen123 | collector / collector123 | admin / admin123",
            accounts = users
        });
    }

    /// <summary>
    /// Giả lập sinh token Basic Auth chuẩn VietQR / TingoPay (dành cho đối tác cổng thanh toán)
    /// </summary>
    [HttpPost("vietqr-token")]
    public IActionResult GenerateVietQrToken()
    {
        // Kiểm tra header Authorization: Basic <base64(client_id:client_secret)>
        string? authHeader = Request.Headers.Authorization;
        if (string.IsNullOrWhiteSpace(authHeader) || !authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return Unauthorized(new
            {
                code = "401",
                message = "Yêu cầu Basic Auth với Client ID và Client Secret từ đối tác VietQR."
            });
        }

        var mockToken = Guid.NewGuid().ToString("N");
        return Ok(new
        {
            access_token = mockToken,
            token_type = "Bearer",
            expires_in = 86400,
            message = "VietQR / TingoPay authentication handshake successful."
        });
    }
}
