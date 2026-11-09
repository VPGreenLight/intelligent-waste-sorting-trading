using System.Security.Claims;
using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Phân hệ Xác thực & Quản trị tài khoản (Module 1 - AUTH)
/// Hiện thực hóa các endpoints cốt lõi theo clean_architecture_and_coding_guidelines.md
/// và module1_auth_controller_implementation_plan.md
/// </summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 1. Đăng ký tài khoản Người dân (User/Citizen)
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register(
        [FromBody] RegisterUserRequestDto request, CancellationToken ct)
    {
        var result = await _authService.RegisterUserAsync(request, ct);
        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
        }

        return StatusCode(StatusCodes.Status201Created, ApiResponse<AuthResponseDto>.Ok(
            result,
            message: "Tạo tài khoản thành công! Chào mừng bạn gia nhập cộng đồng EcoLink.",
            messageCode: "MSG-AUTH-08"));
    }

    /// <summary>
    /// 2. Đăng ký tài khoản Đối tác thu mua (Recycler) kèm tọa độ GPS PostGIS
    /// </summary>
    [HttpPost("register-recycler")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RegisterRecycler(
        [FromBody] RegisterRecyclerRequestDto request, CancellationToken ct)
    {
        var result = await _authService.RegisterRecyclerAsync(request, ct);
        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
        }

        return StatusCode(StatusCodes.Status201Created, ApiResponse<AuthResponseDto>.Ok(
            result,
            message: "Hồ sơ cơ sở của bạn đang được xem xét và sẽ xuất hiện sau khi duyệt.",
            messageCode: "MSG-MTH-03"));
    }

    /// <summary>
    /// 3. Đăng nhập hệ thống (Hỗ trợ Brute-force lockout sau 5 lần sai - BR-AUTH-04)
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login(
        [FromBody] LoginRequestDto request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, GetClientIp(), ct);

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
        }

        return Ok(ApiResponse<AuthResponseDto>.Ok(
            result,
            message: "Đăng nhập thành công.",
            messageCode: "MSG-GEN-01"));
    }

    /// <summary>
    /// Cấu hình công khai Google OAuth 2.0 Client ID cho trình duyệt mở cửa sổ Google Account Chooser
    /// </summary>
    [HttpGet("google-config")]
    public IActionResult GetGooglePublicConfig()
    {
        var clientId = _configuration["GoogleAuth:ClientId"] ?? string.Empty;
        var enabled = !string.IsNullOrWhiteSpace(clientId)
                      && !clientId.Contains("mock", StringComparison.OrdinalIgnoreCase)
                      && !clientId.StartsWith('{');

        return Ok(ApiResponse<object>.Ok(
            new { enabled, clientId = enabled ? clientId : string.Empty },
            message: "Cấu hình công khai Google OAuth 2.0.",
            messageCode: "MSG-GEN-01"));
    }

    /// <summary>
    /// 4. Đăng nhập / Đăng ký tự động qua Google OAuth 2.0 ID Token hoặc Access Token (BR-AUTH-02, BR-AUTH-07)
    /// </summary>
    [HttpPost("google")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> GoogleLogin(
        [FromBody] GoogleAuthRequestDto request, CancellationToken ct)
    {
        var result = await _authService.AuthenticateGoogleAsync(request.IdToken, ct);

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
        }

        return Ok(ApiResponse<AuthResponseDto>.Ok(
            result,
            message: "Xác thực qua Google thành công.",
            messageCode: "MSG-AUTH-09"));
    }

    /// <summary>
    /// 5. Làm mới Access Token (Token Rotation & Replay Attack Detection - BR-AUTH-05)
    /// </summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshToken(
        [FromBody] RefreshTokenRequestDto? request, CancellationToken ct)
    {
        var token = request?.RefreshToken ?? Request.Cookies["refreshToken"];
        if (string.IsNullOrWhiteSpace(token))
        {
            return Unauthorized(ApiResponse<AuthResponseDto>.Fail(
                message: "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.",
                messageCode: "MSG-NTF-01"));
        }

        var result = await _authService.RefreshTokenAsync(token, GetClientIp(), ct);

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            SetRefreshTokenCookie(result.RefreshToken);
        }

        return Ok(ApiResponse<AuthResponseDto>.Ok(
            result,
            message: "Làm mới phiên đăng nhập thành công.",
            messageCode: "MSG-GEN-01"));
    }

    /// <summary>
    /// 6. Yêu cầu đặt lại mật khẩu (Gửi email token qua Google SMTP - BR-AUTH-06, BR-AUTH-09)
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object?>>> ForgotPassword(
        [FromBody] ForgotPasswordRequestDto request, CancellationToken ct)
    {
        await _authService.ForgotPasswordAsync(request.Email, GetClientIp(), ct);

        // BR-AUTH-06: Phản hồi đồng nhất dù email có tồn tại trong hệ thống hay không để chống enumeration
        return Ok(ApiResponse<object?>.Ok(
            null,
            message: "Nếu email này tồn tại trong hệ thống, liên kết đặt lại mật khẩu đã được gửi.",
            messageCode: "MSG-AUTH-05"));
    }

    /// <summary>
    /// 7. Đặt lại mật khẩu mới bằng Token bảo mật (BR-AUTH-06)
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object?>>> ResetPassword(
        [FromBody] ResetPasswordRequestDto request, CancellationToken ct)
    {
        await _authService.ResetPasswordAsync(request, ct);

        return Ok(ApiResponse<object?>.Ok(
            null,
            message: "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.",
            messageCode: "MSG-GEN-01"));
    }

    /// <summary>
    /// 8. Cập nhật hồ sơ cá nhân của người dùng đang đăng nhập (SU-01)
    /// </summary>
    [Authorize]
    [HttpPut("profile")]
    [ProducesResponseType(typeof(ApiResponse<UserInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UserInfoDto>>> UpdateProfile(
        [FromBody] UpdateUserProfileRequestDto request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<UserInfoDto>.Fail(
                message: "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.",
                messageCode: "MSG-NTF-01"));
        }

        var updated = await _authService.UpdateProfileAsync(userId.Value, request, ct);
        return Ok(ApiResponse<UserInfoDto>.Ok(
            updated,
            message: "Đã lưu hồ sơ cá nhân thành công.",
            messageCode: "MSG-GEN-01"));
    }

    /// <summary>
    /// 9. Đổi mật khẩu tài khoản đang đăng nhập và thu hồi các phiên thiết bị khác (SU-02)
    /// </summary>
    [Authorize]
    [HttpPost("change-password")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object?>>> ChangePassword(
        [FromBody] ChangePasswordRequestDto request, CancellationToken ct)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
        {
            return Unauthorized(ApiResponse<object?>.Fail(
                message: "Phiên đăng nhập không hợp lệ. Vui lòng đăng nhập lại.",
                messageCode: "MSG-NTF-01"));
        }

        var currentRefreshToken = Request.Cookies["refreshToken"];
        await _authService.ChangePasswordAsync(userId.Value, request, currentRefreshToken, ct);

        return Ok(ApiResponse<object?>.Ok(
            null,
            message: "Đã đổi mật khẩu thành công. Các phiên đăng nhập trên thiết bị khác đã được đăng xuất.",
            messageCode: "MSG-GEN-01"));
    }

    /// <summary>
    /// 10. Đăng xuất khỏi phiên làm việc hiện tại và xóa HttpOnly Refresh Token Cookie
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse<object?>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object?>>> Logout(
        [FromBody] LogoutRequestDto? request, CancellationToken ct)
    {
        var refreshToken = request?.RefreshToken ?? Request.Cookies["refreshToken"];
        var userId = GetCurrentUserId();
        await _authService.LogoutAsync(refreshToken, userId, ct);

        Response.Cookies.Delete("refreshToken");
        return Ok(ApiResponse<object?>.Ok(
            null,
            message: "Đã đăng xuất thành công.",
            messageCode: "MSG-GEN-01"));
    }

    // ================= TIỆN ÍCH DÀNH CHO PHÁT TRIỂN & TEST =================

    /// <summary>
    /// Lấy thông tin người dùng hiện tại qua Bearer token (Hỗ trợ qua Header hoặc Query Param)
    /// </summary>
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
            message = "Tài khoản mẫu để test nhanh: citizen / citizen123 | collector / collector123 | staff / staff123 | admin / admin123",
            accounts = users
        });
    }

    /// <summary>
    /// Giả lập sinh token Basic Auth chuẩn VietQR / TingoPay (dành cho đối tác cổng thanh toán)
    /// </summary>
    [HttpPost("vietqr-token")]
    public IActionResult GenerateVietQrToken()
    {
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

    // ================= HELPER METHODS =================
    private Guid? GetCurrentUserId()
    {
        var rawId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(rawId, out var id) ? id : null;
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddDays(7)
        };
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

    private string GetClientIp() =>
        Request.Headers.ContainsKey("X-Forwarded-For")
            ? Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim()
            : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
}
