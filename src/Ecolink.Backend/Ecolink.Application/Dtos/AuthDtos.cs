using System.ComponentModel.DataAnnotations;
using Ecolink.Domain.Enums;

namespace Ecolink.Application.Dtos;

/// <summary>
/// DTO Đăng ký tài khoản Người dân (Citizen)
/// </summary>
public class RegisterUserRequestDto
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 50 ký tự")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ và tên không được để trống")]
    [StringLength(100, ErrorMessage = "Họ và tên tối đa 100 ký tự")]
    public string FullName { get; set; } = string.Empty;

    [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
    public string? PhoneNumber { get; set; }
}

/// <summary>
/// Alias hỗ trợ tương thích ngược với code cũ
/// </summary>
public class RegisterRequestDto : RegisterUserRequestDto
{
    public UserRole Role { get; set; } = UserRole.Citizen;
}

/// <summary>
/// DTO Đăng ký tài khoản Điểm / Cơ sở thu mua phế liệu (Recycler) kèm tọa độ GPS
/// </summary>
public class RegisterRecyclerRequestDto
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ và tên người đại diện không được để trống")]
    public string FullName { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Tên cơ sở thu mua không được để trống")]
    [StringLength(150)]
    public string FacilityName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Người đại diện pháp luật không được để trống")]
    [StringLength(100)]
    public string RepresentativeName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Địa chỉ kinh doanh không được để trống")]
    [StringLength(255)]
    public string BusinessAddress { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vĩ độ (Latitude) không được để trống")]
    [Range(-90.0, 90.0, ErrorMessage = "Vĩ độ phải nằm trong khoảng -90 đến 90")]
    public double Latitude { get; set; }

    [Required(ErrorMessage = "Kinh độ (Longitude) không được để trống")]
    [Range(-180.0, 180.0, ErrorMessage = "Kinh độ phải nằm trong khoảng -180 đến 180")]
    public double Longitude { get; set; }

    public string? OperatingHours { get; set; }
    public string? ContactPhone { get; set; }
    public string? Description { get; set; }
    public string? LegalPermitUrl { get; set; }
    public string? BusinessPhotoUrl { get; set; }
}

/// <summary>
/// DTO Đăng nhập
/// </summary>
public class LoginRequestDto
{
    [Required(ErrorMessage = "Tên đăng nhập hoặc Email không được để trống")]
    public string UsernameOrEmail { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// DTO Đăng nhập / Đăng ký qua Google OAuth
/// </summary>
public class GoogleAuthRequestDto
{
    [Required(ErrorMessage = "Google ID Token không được để trống")]
    public string IdToken { get; set; } = string.Empty;
}

/// <summary>
/// DTO Làm mới Access Token
/// </summary>
public class RefreshTokenRequestDto
{
    public string? RefreshToken { get; set; }
}

/// <summary>
/// DTO Yêu cầu gửi email đặt lại mật khẩu
/// </summary>
public class ForgotPasswordRequestDto
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
    public string Email { get; set; } = string.Empty;
}

/// <summary>
/// DTO Đặt lại mật khẩu mới bằng Token
/// </summary>
public class ResetPasswordRequestDto
{
    [Required(ErrorMessage = "Email không được để trống")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Token không được để trống")]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu mới không được để trống")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
    public string NewPassword { get; set; } = string.Empty;

    public string? ConfirmPassword { get; set; }
}

/// <summary>
/// DTO Kết quả xác thực trả về cho Client
/// </summary>
public class AuthResponseDto
{
    public bool Success { get; set; } = true;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTime? ExpiresAt { get; set; }
    public UserInfoDto? User { get; set; }
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Property tương thích với code cũ
    /// </summary>
    public string? Token
    {
        get => AccessToken;
        set => AccessToken = value ?? string.Empty;
    }
}

/// <summary>
/// DTO Thông tin người dùng
/// </summary>
public class UserInfoDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsGoogleAuth { get; set; }
    public int CurrentGreenPoints { get; set; }
    public int TreeLevel { get; set; }
    public decimal TotalRecycledKg { get; set; }
}
