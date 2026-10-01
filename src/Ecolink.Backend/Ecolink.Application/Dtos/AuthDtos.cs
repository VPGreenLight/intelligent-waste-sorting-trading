using Ecolink.Domain.Enums;

namespace Ecolink.Application.Dtos;

public class RegisterRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public UserRole Role { get; set; } = UserRole.Citizen;
}

public class LoginRequestDto
{
    /// <summary>
    /// Có thể đăng nhập bằng Username hoặc Email
    /// </summary>
    public string UsernameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResponseDto
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? TokenType { get; set; } = "Bearer";
    public DateTime? ExpiresAt { get; set; }
    public UserInfoDto? User { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class UserInfoDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public int CurrentGreenPoints { get; set; }
    public int TreeLevel { get; set; }
    public decimal TotalRecycledKg { get; set; }
}
