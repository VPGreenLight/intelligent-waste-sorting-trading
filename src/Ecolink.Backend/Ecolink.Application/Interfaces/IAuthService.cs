using Ecolink.Application.Dtos;

namespace Ecolink.Application.Interfaces;

public interface IAuthService
{
    /// <summary>
    /// 1. Đăng ký tài khoản Người dân (Citizen)
    /// </summary>
    Task<AuthResponseDto> RegisterUserAsync(RegisterUserRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 2. Đăng ký tài khoản Điểm/Cơ sở thu gom (Recycler) kèm tọa độ GPS
    /// </summary>
    Task<AuthResponseDto> RegisterRecyclerAsync(RegisterRecyclerRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 3. Đăng nhập hệ thống (kèm IP client để kiểm soát Brute-force lockout)
    /// </summary>
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, string? clientIp, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 4. Đăng nhập / Đăng ký nhanh bằng Google OAuth (Google ID Token)
    /// </summary>
    Task<AuthResponseDto> AuthenticateGoogleAsync(string idToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// 5. Làm mới Access Token thông qua Refresh Token (Token Rotation & Replay Detection)
    /// </summary>
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, string? clientIp = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 6. Yêu cầu gửi email đặt lại mật khẩu (Rate limit 1 mail / phút)
    /// </summary>
    Task ForgotPasswordAsync(string email, string? clientIp = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 7. Đặt lại mật khẩu mới bằng Token bảo mật
    /// </summary>
    Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken cancellationToken = default);

    // ================= PHƯƠNG THỨC BỔ TRỢ & TƯƠNG THÍCH =================
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<UserInfoDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserInfoDto?> GetUserByTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IEnumerable<UserInfoDto>> GetAllDemoUsersAsync(CancellationToken cancellationToken = default);
}
