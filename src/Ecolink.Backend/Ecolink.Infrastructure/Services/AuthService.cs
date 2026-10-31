using System.Security.Claims;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Google.Apis.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Hiện thực hóa toàn bộ 7 nghiệp vụ xác thực & tài khoản phân hệ Auth (Module 1).
/// Kết nối trực tiếp PostgreSQL 16 + PostGIS 3.4 qua ApplicationDbContext.
/// </summary>
public class AuthService : IAuthService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        ApplicationDbContext dbContext,
        ITokenService tokenService,
        IEmailService emailService,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// 1. Đăng ký tài khoản Người dân (Citizen)
    /// </summary>
    public async Task<AuthResponseDto> RegisterUserAsync(RegisterUserRequestDto request, CancellationToken ct = default)
    {
        await EnsureDemoUsersAsync(ct);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedUsername = request.Username.Trim().ToLowerInvariant();

        // BR-AUTH-01: Kiểm tra Email và Username duy nhất
        if (await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail, ct))
        {
            throw new BusinessRuleException("BR-AUTH-01", "MSG-AUTH-01", "Email này đã được sử dụng. Vui lòng chọn email khác.");
        }

        if (await _dbContext.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername, ct))
        {
            throw new BusinessRuleException("BR-AUTH-01", "MSG-AUTH-01", "Tên đăng nhập này đã tồn tại. Vui lòng chọn tên khác.");
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            Role = UserRole.Citizen,
            Status = UserStatus.Active, // BR-AUTH-02: User kích hoạt
            IsGoogleAuth = false,
            EcoProfile = new UserEcoProfile
            {
                CurrentGreenPoints = 0,
                TotalLifetimePoints = 0,
                TreeLevel = 1,
                TreeExperience = 0,
                TotalRecycledKg = 0m,
                TotalWasteScanned = 0
            }
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Người dùng Citizen mới đăng ký thành công: {Username} ({Email})", user.Username, user.Email);

        return await GenerateAuthResponseAsync(user, ct: ct);
    }

    /// <summary>
    /// 2. Đăng ký tài khoản Đối tác thu gom (Recycler) kèm tọa độ GPS PostGIS
    /// </summary>
    public async Task<AuthResponseDto> RegisterRecyclerAsync(RegisterRecyclerRequestDto request, CancellationToken ct = default)
    {
        await EnsureDemoUsersAsync(ct);

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedUsername = request.Username.Trim().ToLowerInvariant();

        // BR-AUTH-01: Kiểm tra tính duy nhất
        if (await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail, ct))
        {
            throw new BusinessRuleException("BR-AUTH-01", "MSG-AUTH-01", "Email này đã được sử dụng.");
        }

        if (await _dbContext.Users.AnyAsync(u => u.Username.ToLower() == normalizedUsername, ct))
        {
            throw new BusinessRuleException("BR-AUTH-01", "MSG-AUTH-01", "Tên đăng nhập này đã tồn tại.");
        }

        var point = new Point(request.Longitude, request.Latitude) { SRID = 4326 };
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username.Trim(),
            Email = normalizedEmail,
            PasswordHash = passwordHash,
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber?.Trim(),
            Role = UserRole.Recycler,
            Status = UserStatus.PendingVerification, // Chờ duyệt hồ sơ theo BR-MTH-01
            IsGoogleAuth = false
        };

        var recyclerProfile = new RecyclerProfile
        {
            UserId = user.Id,
            User = user,
            FacilityName = request.FacilityName.Trim(),
            RepresentativeName = request.RepresentativeName.Trim(),
            BusinessAddress = request.BusinessAddress.Trim(),
            Latitude = (decimal)request.Latitude,
            Longitude = (decimal)request.Longitude,
            Location = point,
            OperatingHours = request.OperatingHours ?? "08:00 - 18:00",
            ContactPhone = request.ContactPhone ?? request.PhoneNumber ?? string.Empty,
            Description = request.Description,
            LegalPermitUrl = request.LegalPermitUrl,
            BusinessPhotoUrl = request.BusinessPhotoUrl,
            VerificationStatus = VerificationStatus.Pending,
            ActiveStatus = RecyclerActiveStatus.Offline
        };

        _dbContext.Users.Add(user);
        _dbContext.RecyclerProfiles.Add(recyclerProfile);
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Đối tác Recycler mới đăng ký hồ sơ: {Facility} (User: {Username})", recyclerProfile.FacilityName, user.Username);

        return await GenerateAuthResponseAsync(user, "MSG-MTH-03", ct);
    }

    /// <summary>
    /// 3. Đăng nhập hệ thống (kèm kiểm tra Brute-force lockout sau 5 lần sai - BR-AUTH-04)
    /// </summary>
    public Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default) =>
        LoginAsync(request, null, ct);

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, string? clientIp, CancellationToken ct = default)
    {
        await EnsureDemoUsersAsync(ct);

        var loginIdentifier = request.UsernameOrEmail.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .Include(u => u.EcoProfile)
            .FirstOrDefaultAsync(u => u.Username.ToLower() == loginIdentifier || u.Email.ToLower() == loginIdentifier, ct);

        if (user == null)
        {
            throw new BusinessRuleException("BR-AUTH-03", "MSG-AUTH-01", "Tên đăng nhập, email hoặc mật khẩu không chính xác.");
        }

        // BR-AUTH-04: Kiểm tra trạng thái khóa tạm thời (Lockout)
        if (user.Status == UserStatus.Locked && user.LockoutUntil.HasValue)
        {
            if (user.LockoutUntil.Value > DateTime.UtcNow)
            {
                var remainingMinutes = Math.Ceiling((user.LockoutUntil.Value - DateTime.UtcNow).TotalMinutes);
                throw new BusinessRuleException("BR-AUTH-04", "MSG-AUTH-03",
                    $"Tài khoản tạm thời bị khóa do nhập sai mật khẩu quá 5 lần. Vui lòng thử lại sau {remainingMinutes} phút.");
            }

            // Hết thời gian khóa, tự động mở lại
            user.Status = UserStatus.Active;
            user.FailedLoginAttempts = 0;
            user.LockoutUntil = null;
        }

        // Kiểm tra mật khẩu (hỗ trợ BCrypt hoặc đăng nhập mật khẩu trống nếu là tài khoản Google)
        bool isPasswordValid = false;
        if (!string.IsNullOrEmpty(user.PasswordHash))
        {
            isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        }

        if (!isPasswordValid)
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
            {
                user.Status = UserStatus.Locked;
                user.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
                await _dbContext.SaveChangesAsync(ct);

                _logger.LogWarning("Tài khoản {Username} bị khóa 15 phút do nhập sai 5 lần từ IP {IP}", user.Username, clientIp);
                throw new BusinessRuleException("BR-AUTH-04", "MSG-AUTH-03",
                    "Tài khoản tạm thời bị khóa 15 phút do nhập sai mật khẩu 5 lần liên tiếp.");
            }

            await _dbContext.SaveChangesAsync(ct);
            throw new BusinessRuleException("BR-AUTH-03", "MSG-AUTH-01", "Tên đăng nhập, email hoặc mật khẩu không chính xác.");
        }

        // Đăng nhập thành công -> Reset số lần sai
        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;
        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Người dùng {Username} đăng nhập thành công từ IP {IP}", user.Username, clientIp);

        return await GenerateAuthResponseAsync(user, clientIp: clientIp, ct: ct);
    }

    /// <summary>
    /// 4. Đăng nhập / Đăng ký nhanh bằng Google OAuth (BR-AUTH-02, BR-AUTH-07)
    /// </summary>
    public async Task<AuthResponseDto> AuthenticateGoogleAsync(string idToken, CancellationToken ct = default)
    {
        await EnsureDemoUsersAsync(ct);

        GoogleJsonWebSignature.Payload payload;
        try
        {
            // Hỗ trợ mock token cho môi trường dev / swagger test nhanh
            if (idToken.StartsWith("mock-", StringComparison.OrdinalIgnoreCase))
            {
                var email = idToken.Contains("@") ? idToken.Replace("mock-", "") : "googleuser@gmail.com";
                payload = new GoogleJsonWebSignature.Payload
                {
                    Email = email,
                    Name = "Người dùng Google EcoLink",
                    EmailVerified = true,
                    Picture = "https://lh3.googleusercontent.com/a/default-avatar"
                };
            }
            else
            {
                var clientId = _configuration["GoogleAuth:ClientId"];
                var settings = new GoogleJsonWebSignature.ValidationSettings
                {
                    Audience = !string.IsNullOrEmpty(clientId) && !clientId.Contains("mock") 
                        ? new[] { clientId } 
                        : null
                };
                payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Xác thực Google ID Token thất bại");
            throw new BusinessRuleException("BR-AUTH-07", "MSG-AUTH-01", "Token Google không hợp lệ hoặc đã hết hạn.");
        }

        var normalizedEmail = payload.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .Include(u => u.EcoProfile)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, ct);

        if (user == null)
        {
            // Tự động tạo tài khoản mới nếu chưa tồn tại
            var username = normalizedEmail.Split('@')[0] + "_" + Guid.NewGuid().ToString("N")[..4];
            user = new User
            {
                Id = Guid.NewGuid(),
                Username = username,
                Email = normalizedEmail,
                FullName = payload.Name ?? "Người dùng Google",
                AvatarUrl = payload.Picture,
                IsGoogleAuth = true,
                Role = UserRole.Citizen,
                Status = payload.EmailVerified ? UserStatus.Active : UserStatus.PendingVerification,
                EcoProfile = new UserEcoProfile
                {
                    CurrentGreenPoints = 0,
                    TotalLifetimePoints = 0,
                    TreeLevel = 1,
                    TreeExperience = 0,
                    TotalRecycledKg = 0m,
                    TotalWasteScanned = 0
                }
            };
            _dbContext.Users.Add(user);
        }
        else
        {
            user.IsGoogleAuth = true;
            if (!string.IsNullOrEmpty(payload.Picture) && string.IsNullOrEmpty(user.AvatarUrl))
            {
                user.AvatarUrl = payload.Picture;
            }
        }

        await _dbContext.SaveChangesAsync(ct);
        return await GenerateAuthResponseAsync(user, "MSG-AUTH-09", ct: ct);
    }

    /// <summary>
    /// 5. Làm mới Access Token (Token Rotation & Replay Attack Detection)
    /// </summary>
    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken, string? clientIp = null, CancellationToken ct = default)
    {
        var tokenHash = _tokenService.HashToken(refreshToken);

        var existingToken = await _dbContext.RefreshTokens
            .Include(r => r.User)
            .ThenInclude(u => u.EcoProfile)
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

        if (existingToken == null)
        {
            throw new BusinessRuleException("BR-AUTH-05", "MSG-NTF-01", "Phiên làm việc không tồn tại hoặc đã hết hạn. Vui lòng đăng nhập lại.");
        }

        // Phát hiện Replay Attack: Token đã bị thu hồi nhưng vẫn được dùng lại
        if (existingToken.IsRevoked)
        {
            _logger.LogWarning("Phát hiện Token Replay Attack! Thu hồi toàn bộ RefreshToken của UserId {UserId}", existingToken.UserId);
            var allUserTokens = await _dbContext.RefreshTokens
                .Where(r => r.UserId == existingToken.UserId && !r.IsRevoked)
                .ToListAsync(ct);

            foreach (var t in allUserTokens)
            {
                t.IsRevoked = true;
            }

            await _dbContext.SaveChangesAsync(ct);
            throw new BusinessRuleException("BR-AUTH-05", "MSG-AUTH-10", "Phát hiện phiên làm việc bất thường (tái sử dụng token đã hủy). Vui lòng đăng nhập lại.");
        }

        if (existingToken.ExpiresAt <= DateTime.UtcNow)
        {
            existingToken.IsRevoked = true;
            await _dbContext.SaveChangesAsync(ct);
            throw new BusinessRuleException("BR-AUTH-05", "MSG-NTF-01", "Phiên làm việc đã hết hạn. Vui lòng đăng nhập lại.");
        }

        // Token Rotation: Hủy token cũ, tạo token mới
        var (newRawToken, newTokenHash) = _tokenService.GenerateRefreshToken();
        existingToken.IsRevoked = true;
        existingToken.ReplacedByTokenHash = newTokenHash;

        var newRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = existingToken.UserId,
            TokenHash = newTokenHash,
            ExpiresAt = _tokenService.GetRefreshTokenExpiration(),
            IsRevoked = false,
            CreatedByIp = clientIp,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.RefreshTokens.Add(newRefreshToken);
        await _dbContext.SaveChangesAsync(ct);

        var newAccessToken = _tokenService.GenerateAccessToken(existingToken.User);

        return new AuthResponseDto
        {
            Success = true,
            AccessToken = newAccessToken,
            RefreshToken = newRawToken,
            TokenType = "Bearer",
            ExpiresAt = _tokenService.GetAccessTokenExpiration(),
            User = MapToUserInfoDto(existingToken.User),
            Message = "Làm mới phiên làm việc thành công."
        };
    }

    /// <summary>
    /// 6. Yêu cầu gửi email đặt lại mật khẩu (Rate limit 1 mail / 1 phút theo BR-AUTH-09)
    /// </summary>
    public async Task ForgotPasswordAsync(string email, string? clientIp = null, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, ct);

        // BR-AUTH-06: Luôn phản hồi thành công âm thầm dù email có tồn tại hay không để chống quét email
        if (user == null)
        {
            _logger.LogInformation("Yêu cầu quên mật khẩu cho email không tồn tại: {Email}", normalizedEmail);
            return;
        }

        // BR-AUTH-09: Giới hạn tần suất gửi email (1 lần mỗi 60 giây)
        var recentToken = await _dbContext.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.CreatedAt >= DateTime.UtcNow.AddMinutes(-1))
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (recentToken != null)
        {
            _logger.LogWarning("Email {Email} bị rate limit yêu cầu quên mật khẩu trong 1 phút", normalizedEmail);
            // Tuân thủ BR-AUTH-09: Chặn âm thầm, không gửi mail liên tục
            return;
        }

        // Sinh token an toàn 32 ký tự URL-friendly
        var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N")[..8];
        var tokenHash = _tokenService.HashToken(rawToken);

        var resetTokenRecord = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15), // BR-AUTH-06: 15 phút
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.PasswordResetTokens.Add(resetTokenRecord);
        await _dbContext.SaveChangesAsync(ct);

        // Gửi email qua SMTP
        await _emailService.SendPasswordResetEmailAsync(user.Email, user.FullName, rawToken, ct);
    }

    /// <summary>
    /// 7. Đặt lại mật khẩu mới bằng Token bảo mật (BR-AUTH-06)
    /// </summary>
    public async Task ResetPasswordAsync(ResetPasswordRequestDto request, CancellationToken ct = default)
    {
        if (request.NewPassword.Length < 6)
        {
            throw new BusinessRuleException("BR-AUTH-06", "MSG-AUTH-01", "Mật khẩu mới tối thiểu phải có 6 ký tự.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, ct);

        if (user == null)
        {
            throw new BusinessRuleException("BR-AUTH-06", "MSG-AUTH-06", "Liên kết hoặc mã xác nhận không hợp lệ hoặc đã hết hạn.");
        }

        var tokenHash = _tokenService.HashToken(request.Token.Trim());

        var tokenRecord = await _dbContext.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.TokenHash == tokenHash && t.UsedAt == null)
            .FirstOrDefaultAsync(ct);

        if (tokenRecord == null || tokenRecord.ExpiresAt <= DateTime.UtcNow)
        {
            throw new BusinessRuleException("BR-AUTH-06", "MSG-AUTH-06", "Liên kết hoặc mã xác nhận không hợp lệ hoặc đã hết hạn.");
        }

        // Đánh dấu token đã sử dụng
        tokenRecord.UsedAt = DateTime.UtcNow;

        // Cập nhật mật khẩu mới
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;
        if (user.Status == UserStatus.Locked)
        {
            user.Status = UserStatus.Active;
        }

        // BR-AUTH-06: Hủy toàn bộ Refresh Tokens để kick tất cả các phiên làm việc cũ
        var activeTokens = await _dbContext.RefreshTokens
            .Where(r => r.UserId == user.Id && !r.IsRevoked)
            .ToListAsync(ct);

        foreach (var t in activeTokens)
        {
            t.IsRevoked = true;
        }

        await _dbContext.SaveChangesAsync(ct);
        _logger.LogInformation("Người dùng {Username} ({Email}) đã đặt lại mật khẩu mới thành công", user.Username, user.Email);
    }

    // ================= HELPER & BACKWARD COMPATIBILITY METHODS =================

    public Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        return RegisterUserAsync(request, cancellationToken);
    }

    public async Task<UserInfoDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await EnsureDemoUsersAsync(cancellationToken);
        var user = await _dbContext.Users
            .Include(u => u.EcoProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        return user != null ? MapToUserInfoDto(user) : null;
    }

    public async Task<UserInfoDto?> GetUserByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        await EnsureDemoUsersAsync(cancellationToken);

        string cleanToken = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? token[7..].Trim()
            : token.Trim();

        try
        {
            var principal = _tokenService.GetPrincipalFromExpiredToken(cleanToken);
            var userIdStr = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                            ?? principal?.FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdStr, out var userId))
            {
                return await GetUserByIdAsync(userId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể giải mã Claims từ Token");
        }

        return null;
    }

    public async Task<IEnumerable<UserInfoDto>> GetAllDemoUsersAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDemoUsersAsync(cancellationToken);
        var users = await _dbContext.Users
            .Include(u => u.EcoProfile)
            .Take(10)
            .ToListAsync(cancellationToken);

        return users.Select(MapToUserInfoDto);
    }

    private async Task<AuthResponseDto> GenerateAuthResponseAsync(
        User user,
        string messageCode = "MSG-GEN-01",
        CancellationToken ct = default,
        string? clientIp = null)
    {
        var accessToken = _tokenService.GenerateAccessToken(user);
        var (rawRefreshToken, refreshTokenHash) = _tokenService.GenerateRefreshToken();

        var refreshTokenRecord = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = refreshTokenHash,
            ExpiresAt = _tokenService.GetRefreshTokenExpiration(),
            IsRevoked = false,
            CreatedByIp = clientIp,
            CreatedAt = DateTime.UtcNow
        };

        _dbContext.RefreshTokens.Add(refreshTokenRecord);
        await _dbContext.SaveChangesAsync(ct);

        return new AuthResponseDto
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = rawRefreshToken,
            TokenType = "Bearer",
            ExpiresAt = _tokenService.GetAccessTokenExpiration(),
            User = MapToUserInfoDto(user),
            Message = messageCode == "MSG-MTH-03" 
                ? "Hồ sơ cơ sở của bạn đang được xem xét và sẽ xuất hiện sau khi duyệt."
                : "Thao tác xác thực thành công."
        };
    }

    private static UserInfoDto MapToUserInfoDto(User user)
    {
        return new UserInfoDto
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = user.AvatarUrl,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            IsGoogleAuth = user.IsGoogleAuth,
            CurrentGreenPoints = user.EcoProfile?.CurrentGreenPoints ?? 0,
            TreeLevel = user.EcoProfile?.TreeLevel ?? 1,
            TotalRecycledKg = user.EcoProfile?.TotalRecycledKg ?? 0m
        };
    }

    /// <summary>
    /// Đảm bảo luôn có các tài khoản Demo, Danh mục phế liệu chuẩn và Bảng giá mẫu (BR-MTH-03) sẵn sàng trong PostgreSQL
    /// </summary>
    private async Task EnsureDemoUsersAsync(CancellationToken ct)
    {
        var demoCollectorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        if (!await _dbContext.Users.AnyAsync(ct))
        {
            var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("citizen123");
            var collectorPasswordHash = BCrypt.Net.BCrypt.HashPassword("collector123");
            var adminPasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123");

            var demoCitizen = new User
            {
                Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                Username = "citizen",
                Email = "citizen@ecolink.vn",
                PasswordHash = defaultPasswordHash,
                FullName = "Nguyễn Văn Dân",
                PhoneNumber = "0901234567",
                Role = UserRole.Citizen,
                Status = UserStatus.Active,
                EcoProfile = new UserEcoProfile
                {
                    CurrentGreenPoints = 150,
                    TotalLifetimePoints = 300,
                    TreeLevel = 2,
                    TreeExperience = 45,
                    TotalRecycledKg = 12.5m,
                    TotalWasteScanned = 18
                }
            };

            var demoCollector = new User
            {
                Id = demoCollectorId,
                Username = "collector",
                Email = "collector@ecolink.vn",
                PasswordHash = collectorPasswordHash,
                FullName = "Trần Thu Gom (Vựa phế liệu EcoGreen)",
                PhoneNumber = "0988776655",
                Role = UserRole.Recycler,
                Status = UserStatus.Active,
                RecyclerProfile = new RecyclerProfile
                {
                    UserId = demoCollectorId,
                    FacilityName = "Vựa phế liệu EcoGreen",
                    RepresentativeName = "Trần Thu Gom",
                    BusinessAddress = "123 Đường Nguyễn Hữu Cảnh, P. 22, Bình Thạnh, TP.HCM",
                    Latitude = 10.7938m,
                    Longitude = 106.7182m,
                    Location = new Point(106.7182, 10.7938) { SRID = 4326 },
                    OperatingHours = "07:30 - 18:30",
                    ContactPhone = "0988776655",
                    VerificationStatus = VerificationStatus.Verified,
                    ActiveStatus = RecyclerActiveStatus.Online
                }
            };

            var demoAdmin = new User
            {
                Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                Username = "admin",
                Email = "admin@ecolink.vn",
                PasswordHash = adminPasswordHash,
                FullName = "Quản trị viên EcoLink",
                PhoneNumber = "0912345678",
                Role = UserRole.Admin,
                Status = UserStatus.Active
            };

            _dbContext.Users.AddRange(demoCitizen, demoCollector, demoAdmin);
            await _dbContext.SaveChangesAsync(ct);

            _logger.LogInformation("Khởi tạo thành công 3 tài khoản Demo (citizen, collector, admin) trong PostgreSQL");
        }

        // Seed 6 danh mục rác chuẩn (waste_categories) nếu chưa có
        var defaultCategories = new (string Code, string Name, string BinColor, bool IsRecyclable)[]
        {
            ("plastic", "Nhựa tái chế (Plastic)", "Cam", true),
            ("paper", "Giấy báo & Tài liệu (Paper)", "Xanh Dương", true),
            ("glass", "Thủy tinh (Glass)", "Trắng", true),
            ("metal", "Kim loại & Vỏ lon (Metal)", "Xám Kim Loại", true),
            ("cardboard", "Bìa Carton (Cardboard)", "Nâu Vàng", true),
            ("trash", "Rác thải thông thường / Không thể tái chế", "Đen", false)
        };

        var existingCodes = await _dbContext.WasteCategories.Select(c => c.Code).ToListAsync(ct);
        var missingCategories = defaultCategories.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missingCategories.Count > 0)
        {
            foreach (var (code, name, binColor, isRecyclable) in missingCategories)
            {
                _dbContext.WasteCategories.Add(new WasteCategory
                {
                    Code = code,
                    Name = name,
                    StandardBinColor = binColor,
                    IsRecyclable = isRecyclable,
                    Status = "Active"
                });
            }
            await _dbContext.SaveChangesAsync(ct);
        }

        // Đảm bảo tài khoản demoCollector (đang Online) luôn có bảng giá niêm yết hợp lệ theo BR-MTH-03
        var hasCollectorPrices = await _dbContext.RecyclerPrices.AnyAsync(p => p.RecyclerId == demoCollectorId, ct);
        if (!hasCollectorPrices && await _dbContext.RecyclerProfiles.AnyAsync(r => r.UserId == demoCollectorId, ct))
        {
            var categoriesByCode = await _dbContext.WasteCategories
                .Where(c => c.IsRecyclable)
                .ToDictionaryAsync(c => c.Code, ct);

            var demoPrices = new (string Code, decimal PricePerKg, decimal MinKg, string Conditions)[]
            {
                ("plastic", 7000m, 1.0m, "Chai nhựa PET/HDPE đã tráng sạch, bẹp chai"),
                ("paper", 4500m, 2.0m, "Giấy báo, tài liệu khô ráo, không dính dầu mỡ"),
                ("metal", 25000m, 0.5m, "Vỏ lon nhôm, sắt vụn đã dốc sạch nước"),
                ("cardboard", 3800m, 3.0m, "Thùng carton gập phẳng, khô ráo")
            };

            foreach (var (code, price, minKg, cond) in demoPrices)
            {
                if (categoriesByCode.TryGetValue(code, out var cat))
                {
                    _dbContext.RecyclerPrices.Add(new RecyclerPrice
                    {
                        RecyclerId = demoCollectorId,
                        WasteCategoryId = cat.Id,
                        PricePerKg = price,
                        MinQuantityKg = minKg,
                        CollectionConditions = cond,
                        Status = PriceStatus.Active
                    });
                }
            }

            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation("Đã khởi tạo bảng giá mẫu (recycler_price_list) cho Vựa phế liệu EcoGreen (BR-MTH-03).");
        }
    }
}
