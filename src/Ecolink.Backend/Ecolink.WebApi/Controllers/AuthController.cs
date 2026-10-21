using Ecolink.Application.Common;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Ecolink.WebApi.Controllers;

/// <summary>
/// Phân hệ Xác thực & Quản trị tài khoản (Module 1 - AUTH)
/// Hiện thực hóa 7 endpoints cốt lõi theo clean_architecture_and_coding_guidelines.md
/// và module1_auth_controller_implementation_plan.md
/// </summary>
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
    /// 1. Đăng ký tài khoản Người dân (User/Citizen)
    /// </summary>
    /// <param name="request">Thông tin đăng ký (Username, Password, Email, FullName, PhoneNumber)</param>
    /// <param name="ct">CancellationToken</param>
    /// <response code="201">Tạo tài khoản thành công (MSG-AUTH-08)</response>
    /// <response code="400">Trùng email/username hoặc vi phạm định dạng (MSG-AUTH-01 / MSG-GEN-01)</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Register(
        [FromBody] RegisterUserRequestDto request, CancellationToken ct)
    {
        var result = await _authService.RegisterUserAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<AuthResponseDto>.Ok(
            result, 
            message: "Tạo tài khoản thành công! Vui lòng kiểm tra email để xác minh tài khoản.", 
            messageCode: "MSG-AUTH-08"));
    }

    /// <summary>
    /// 2. Đăng ký tài khoản Đối tác thu mua (Recycler) kèm tọa độ GPS PostGIS
    /// </summary>
    /// <param name="request">Thông tin cơ sở, người đại diện, tọa độ Latitude/Longitude</param>
    /// <param name="ct">CancellationToken</param>
    /// <response code="201">Tạo hồ sơ cơ sở thành công, chờ thẩm định (MSG-MTH-03)</response>
    /// <response code="400">Dữ liệu đầu vào hoặc tọa độ không hợp lệ (MSG-MTH-01 / MSG-GEN-01)</response>
    [HttpPost("register-recycler")]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RegisterRecycler(
        [FromBody] RegisterRecyclerRequestDto request, CancellationToken ct)
    {
        var result = await _authService.RegisterRecyclerAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<AuthResponseDto>.Ok(
            result, 
            message: "Hồ sơ cơ sở của bạn đang được xem xét và sẽ xuất hiện sau khi duyệt.", 
            messageCode: "MSG-MTH-03"));
    }

    /// <summary>
    /// 3. Đăng nhập hệ thống (Hỗ trợ Brute-force lockout sau 5 lần sai - BR-AUTH-04)
    /// </summary>
    /// <param name="request">Thông tin đăng nhập (Username/Email và Password)</param>
    /// <param name="ct">CancellationToken</param>
    /// <response code="200">Đăng nhập thành công, trả về JWT & Refresh Token (MSG-GEN-01)</response>
    /// <response code="400">Sai thông tin đăng nhập hoặc tài khoản bị khóa tạm thời (MSG-AUTH-01 / MSG-AUTH-03)</response>
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
    /// 4. Đăng nhập / Đăng ký tự động qua Google OAuth 2.0 ID Token (BR-AUTH-02, BR-AUTH-07)
    /// </summary>
    /// <param name="request">Google ID Token do Google Identity Services cấp</param>
    /// <param name="ct">CancellationToken</param>
    /// <response code="200">Xác thực Google thành công (MSG-AUTH-09)</response>
    /// <response code="400">Google Token không hợp lệ hoặc đã hết hạn (MSG-AUTH-01)</response>
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
    /// <param name="request">Refresh Token qua Body hoặc lấy tự động từ HttpOnly Cookie</param>
    /// <param name="ct">CancellationToken</param>
    /// <response code="200">Cấp cặp Token mới thành công (MSG-GEN-01)</response>
    /// <response code="401">Phiên làm việc hết hạn hoặc phát hiện tấn công tái sử dụng (MSG-NTF-01)</response>
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
    /// <param name="request">Email nhận liên kết đặt lại mật khẩu</param>
    /// <param name="ct">CancellationToken</param>
    /// <response code="200">Phản hồi đồng nhất chống rò rỉ thông tin người dùng (MSG-AUTH-05)</response>
    [HttpPost("forgot-password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword(
        [FromBody] ForgotPasswordRequestDto request, CancellationToken ct)
    {
        await _authService.ForgotPasswordAsync(request.Email, GetClientIp(), ct);

        // BR-AUTH-06: Phản hồi đồng nhất dù email có tồn tại trong hệ thống hay không để chống enumeration
        return Ok(ApiResponse<object>.Ok(
            null, 
            message: "Nếu email này tồn tại trong hệ thống, liên kết đặt lại mật khẩu đã được gửi.", 
            messageCode: "MSG-AUTH-05"));
    }

    /// <summary>
    /// 7. Đặt lại mật khẩu mới bằng Token bảo mật (BR-AUTH-06)
    /// </summary>
    /// <param name="request">Email, Token xác nhận và Mật khẩu mới</param>
    /// <param name="ct">CancellationToken</param>
    /// <response code="200">Đặt lại mật khẩu thành công (MSG-GEN-01)</response>
    /// <response code="400">Token không hợp lệ hoặc đã hết hạn (MSG-AUTH-04)</response>
    [HttpPost("reset-password")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> ResetPassword(
        [FromBody] ResetPasswordRequestDto request, CancellationToken ct)
    {
        await _authService.ResetPasswordAsync(request, ct);

        return Ok(ApiResponse<object>.Ok(
            null, 
            message: "Đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.", 
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
    private void SetRefreshTokenCookie(string refreshToken)
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = DateTime.UtcNow.AddDays(7)
        };
        Response.Cookies.Append("refreshToken", refreshToken, cookieOptions);
    }

    private string GetClientIp() =>
        Request.Headers.ContainsKey("X-Forwarded-For") 
            ? Request.Headers["X-Forwarded-For"].ToString().Split(',')[0].Trim() 
            : HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
}
