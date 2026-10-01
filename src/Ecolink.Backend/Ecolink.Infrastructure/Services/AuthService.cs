using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ILogger<AuthService> _logger;
    private readonly string _jwtSecret;
    private readonly TimeSpan _tokenLifetime = TimeSpan.FromDays(7);

    // Lưu trữ tài khoản in-memory phục vụ demo & test tức thì khi chưa cần kết nối DB
    private readonly ConcurrentDictionary<string, (User User, string PasswordHash, string Salt)> _usersByUsername = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, string> _emailToUsername = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, string> _idToUsername = new();

    public AuthService(ILogger<AuthService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _jwtSecret = configuration["Jwt:SecretKey"] ?? "EcoLink_Super_Secret_Auth_Signing_Key_For_Demo_2026_XYZ123!";

        SeedInitialDemoUsers();
    }

    private void SeedInitialDemoUsers()
    {
        // 1. Tài khoản Người dân (Citizen)
        RegisterInternal(new User
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Username = "citizen",
            Email = "citizen@ecolink.vn",
            FullName = "Nguyễn Văn Dân",
            PhoneNumber = "0901234567",
            Role = UserRole.Citizen,
            EcoProfile = new UserEcoProfile
            {
                CurrentGreenPoints = 150,
                TotalLifetimePoints = 300,
                TreeLevel = 2,
                TotalRecycledKg = 12.5m,
                TotalWasteScanned = 18
            }
        }, "citizen123");

        // 2. Tài khoản Vựa thu gom (Collector)
        RegisterInternal(new User
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Username = "collector",
            Email = "collector@ecolink.vn",
            FullName = "Trần Thu Gom (Vựa phế liệu EcoGreen)",
            PhoneNumber = "0988776655",
            Role = UserRole.Collector
        }, "collector123");

        // 3. Tài khoản Quản trị viên (Admin)
        RegisterInternal(new User
        {
            Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
            Username = "admin",
            Email = "admin@ecolink.vn",
            FullName = "Quản trị viên EcoLink",
            PhoneNumber = "0912345678",
            Role = UserRole.Admin
        }, "admin123");
    }

    private void RegisterInternal(User user, string plainPassword)
    {
        var salt = GenerateSalt();
        var hash = HashPassword(plainPassword, salt);
        user.PasswordHash = hash;

        _usersByUsername[user.Username] = (user, hash, salt);
        _emailToUsername[user.Email] = user.Username;
        _idToUsername[user.Id] = user.Username;
    }

    public Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Task.FromResult(new AuthResponseDto
            {
                Success = false,
                Message = "Tên đăng nhập và mật khẩu không được để trống."
            });
        }

        if (_usersByUsername.ContainsKey(request.Username))
        {
            return Task.FromResult(new AuthResponseDto
            {
                Success = false,
                Message = $"Tên đăng nhập '{request.Username}' đã tồn tại trong hệ thống."
            });
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && _emailToUsername.ContainsKey(request.Email))
        {
            return Task.FromResult(new AuthResponseDto
            {
                Success = false,
                Message = $"Email '{request.Email}' đã được đăng ký bởi tài khoản khác."
            });
        }

        var newUser = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? $"{request.Username.Trim()}@ecolink.local" : request.Email.Trim(),
            FullName = string.IsNullOrWhiteSpace(request.FullName) ? request.Username : request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            Role = request.Role,
            EcoProfile = new UserEcoProfile
            {
                CurrentGreenPoints = 0,
                TotalLifetimePoints = 0,
                TreeLevel = 1,
                TotalRecycledKg = 0m
            }
        };

        RegisterInternal(newUser, request.Password);

        _logger.LogInformation("Người dùng mới đã đăng ký thành công: {Username} ({Role})", newUser.Username, newUser.Role);

        var expiresAt = DateTime.UtcNow.Add(_tokenLifetime);
        var token = GenerateJwtToken(newUser, expiresAt);

        return Task.FromResult(new AuthResponseDto
        {
            Success = true,
            Token = token,
            ExpiresAt = expiresAt,
            User = MapToDto(newUser),
            Message = "Đăng ký tài khoản EcoLink thành công!"
        });
    }

    public Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Task.FromResult(new AuthResponseDto
            {
                Success = false,
                Message = "Vui lòng nhập tên đăng nhập/email và mật khẩu."
            });
        }

        string username = request.UsernameOrEmail.Trim();
        if (_emailToUsername.TryGetValue(username, out var foundUsername))
        {
            username = foundUsername;
        }

        if (!_usersByUsername.TryGetValue(username, out var userRecord))
        {
            return Task.FromResult(new AuthResponseDto
            {
                Success = false,
                Message = "Tài khoản hoặc mật khẩu không chính xác."
            });
        }

        var computedHash = HashPassword(request.Password, userRecord.Salt);
        if (computedHash != userRecord.PasswordHash)
        {
            return Task.FromResult(new AuthResponseDto
            {
                Success = false,
                Message = "Tài khoản hoặc mật khẩu không chính xác."
            });
        }

        var expiresAt = DateTime.UtcNow.Add(_tokenLifetime);
        var token = GenerateJwtToken(userRecord.User, expiresAt);

        _logger.LogInformation("Người dùng đăng nhập thành công: {Username}", userRecord.User.Username);

        return Task.FromResult(new AuthResponseDto
        {
            Success = true,
            Token = token,
            ExpiresAt = expiresAt,
            User = MapToDto(userRecord.User),
            Message = "Đăng nhập thành công! Chào mừng bạn quay trở lại EcoLink."
        });
    }

    public Task<UserInfoDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (_idToUsername.TryGetValue(userId, out var username) &&
            _usersByUsername.TryGetValue(username, out var record))
        {
            return Task.FromResult<UserInfoDto?>(MapToDto(record.User));
        }

        return Task.FromResult<UserInfoDto?>(null);
    }

    public Task<UserInfoDto?> GetUserByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token)) return Task.FromResult<UserInfoDto?>(null);

        // Bỏ tiền tố Bearer nếu có
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            token = token["Bearer ".Length..].Trim();
        }

        var payload = ValidateAndDecodeJwt(token);
        if (payload == null) return Task.FromResult<UserInfoDto?>(null);

        if (payload.TryGetValue("sub", out var subObj) && Guid.TryParse(subObj?.ToString(), out var userId))
        {
            return GetUserByIdAsync(userId, cancellationToken);
        }

        return Task.FromResult<UserInfoDto?>(null);
    }

    public Task<IEnumerable<UserInfoDto>> GetAllDemoUsersAsync(CancellationToken cancellationToken = default)
    {
        var users = _usersByUsername.Values.Select(v => MapToDto(v.User));
        return Task.FromResult(users);
    }

    private static UserInfoDto MapToDto(User user)
    {
        return new UserInfoDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Role = user.Role.ToString(),
            CurrentGreenPoints = user.EcoProfile?.CurrentGreenPoints ?? 0,
            TreeLevel = user.EcoProfile?.TreeLevel ?? 1,
            TotalRecycledKg = user.EcoProfile?.TotalRecycledKg ?? 0m
        };
    }

    #region Security & JWT Implementation (Standard HMAC-SHA256 Token)
    private string GenerateJwtToken(User user, DateTime expiresAt)
    {
        var header = new
        {
            alg = "HS256",
            typ = "JWT"
        };

        var payload = new Dictionary<string, object>
        {
            { "sub", user.Id.ToString() },
            { "unique_name", user.Username },
            { "email", user.Email },
            { "name", user.FullName },
            { "role", user.Role.ToString() },
            { "iat", DateTimeOffset.UtcNow.ToUnixTimeSeconds() },
            { "exp", new DateTimeOffset(expiresAt).ToUnixTimeSeconds() }
        };

        string headerJson = JsonSerializer.Serialize(header);
        string payloadJson = JsonSerializer.Serialize(payload);

        string encodedHeader = Base64UrlEncode(Encoding.UTF8.GetBytes(headerJson));
        string encodedPayload = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));

        string unsignedToken = $"{encodedHeader}.{encodedPayload}";
        string signature = ComputeHmacSha256(unsignedToken, _jwtSecret);

        return $"{unsignedToken}.{signature}";
    }

    private Dictionary<string, object>? ValidateAndDecodeJwt(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            string unsignedToken = $"{parts[0]}.{parts[1]}";
            string expectedSig = ComputeHmacSha256(unsignedToken, _jwtSecret);

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(parts[2]),
                    Encoding.UTF8.GetBytes(expectedSig)))
            {
                return null; // Chữ ký không hợp lệ
            }

            string payloadJson = Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
            var payload = JsonSerializer.Deserialize<Dictionary<string, object>>(payloadJson);
            if (payload == null) return null;

            if (payload.TryGetValue("exp", out var expObj))
            {
                var expSeconds = Convert.ToInt64(expObj.ToString());
                var expDate = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
                if (expDate < DateTimeOffset.UtcNow)
                {
                    return null; // Token hết hạn
                }
            }

            return payload;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Giải mã JWT Token thất bại: {Message}", ex.Message);
            return null;
        }
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        return Base64UrlEncode(hash);
    }

    private static string Base64UrlEncode(byte[] input)
    {
        return Convert.ToBase64String(input)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string input)
    {
        string output = input.Replace('-', '+').Replace('_', '/');
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }
        return Convert.FromBase64String(output);
    }

    private static string GenerateSalt()
    {
        byte[] saltBytes = RandomNumberGenerator.GetBytes(16);
        return Convert.ToBase64String(saltBytes);
    }

    private static string HashPassword(string password, string salt)
    {
        using var sha256 = SHA256.Create();
        byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + salt));
        return Convert.ToBase64String(bytes);
    }
    #endregion
}
