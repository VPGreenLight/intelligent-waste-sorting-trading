using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Ecolink.Infrastructure.Services;
using Ecolink.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Ecolink.UnitTests.Module01_Auth;

[Trait("Module", "Module01_Authentication")]
public class AuthServiceUnitTests
{
    private static (
        AuthService Sut,
        ApplicationDbContext Db,
        ITokenService TokenService,
        IEmailService EmailService) CreateSut()
    {
        var db = TestDbContextFactory.Create();
        var tokenService = Substitute.For<ITokenService>();
        var emailService = Substitute.For<IEmailService>();
        var config = TestDbContextFactory.CreateConfiguration();

        tokenService.GenerateAccessToken(Arg.Any<User>())
            .Returns(ci => $"jwt-access-{ci.Arg<User>().Id:N}");
        tokenService.GenerateRefreshToken()
            .Returns(_ =>
            {
                var raw = $"rt-{Guid.NewGuid():N}";
                return (raw, $"hash-{raw}");
            });
        tokenService.HashToken(Arg.Any<string>())
            .Returns(ci => $"hash-{ci.Arg<string>()}");
        tokenService.GetAccessTokenExpiration()
            .Returns(_ => DateTime.UtcNow.AddMinutes(30));
        tokenService.GetRefreshTokenExpiration()
            .Returns(_ => DateTime.UtcNow.AddDays(7));

        var sut = new AuthService(
            db,
            tokenService,
            emailService,
            config,
            NullLogger<AuthService>.Instance);

        return (sut, db, tokenService, emailService);
    }

    [Fact]
    public async Task UTC001_RegisterUserAsync_ValidCitizenRequest_CreatesActiveUserAndEcoProfileLevel1()
    {
        var (sut, db, _, _) = CreateSut();
        var req = new RegisterUserRequestDto
        {
            Username = "new_citizen_01",
            Email = "new_citizen_01@ecolink.vn",
            Password = "Password@123",
            FullName = "Nguyễn Công Dân",
            PhoneNumber = "0901112233"
        };

        var res = await sut.RegisterUserAsync(req);

        res.Success.Should().BeTrue();
        res.AccessToken.Should().NotBeNullOrWhiteSpace();
        res.RefreshToken.Should().NotBeNullOrWhiteSpace();
        res.User.Role.Should().Be(UserRole.Citizen.ToString());
        res.User.Status.Should().Be(UserStatus.Active.ToString());
        res.User.CurrentGreenPoints.Should().Be(0);
        res.User.TreeLevel.Should().Be(1);

        var savedUser = await db.Users.Include(u => u.EcoProfile).FirstAsync(u => u.Id == res.User.Id);
        BCrypt.Net.BCrypt.Verify("Password@123", savedUser.PasswordHash).Should().BeTrue();
        savedUser.EcoProfile.Should().NotBeNull();
        savedUser.EcoProfile!.TreeLevel.Should().Be(1);
    }

    [Fact]
    public async Task UTC002_RegisterUserAsync_DuplicateEmail_ThrowsBrAuth01()
    {
        var (sut, _, _, _) = CreateSut();
        var req = new RegisterUserRequestDto
        {
            Username = "unique_user_99",
            Email = "citizen@ecolink.vn", // trùng email demo citizen
            Password = "Password@123",
            FullName = "Trùng Email"
        };

        var act = () => sut.RegisterUserAsync(req);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-01");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-01");
    }

    [Fact]
    public async Task UTC003_RegisterUserAsync_DuplicateUsername_ThrowsBrAuth01()
    {
        var (sut, _, _, _) = CreateSut();
        var req = new RegisterUserRequestDto
        {
            Username = "citizen", // trùng username demo citizen
            Email = "unique_citizen_99@ecolink.vn",
            Password = "Password@123",
            FullName = "Trùng Username"
        };

        var act = () => sut.RegisterUserAsync(req);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-01");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-01");
    }

    [Fact]
    public async Task UTC004_RegisterUserAsync_WhitespaceAndUppercaseEmail_NormalizesAndTrims()
    {
        var (sut, db, _, _) = CreateSut();
        var req = new RegisterUserRequestDto
        {
            Username = "  user_trim_04  ",
            Email = "  USER04@Gmail.COM  ",
            Password = "Password@123",
            FullName = "  Lê Văn Trim  "
        };

        var res = await sut.RegisterUserAsync(req);

        res.User.Email.Should().Be("user04@gmail.com");
        res.User.Username.Should().Be("user_trim_04");
        res.User.FullName.Should().Be("Lê Văn Trim");

        var entity = await db.Users.FirstAsync(u => u.Id == res.User.Id);
        entity.Email.Should().Be("user04@gmail.com");
    }

    [Fact]
    public async Task UTC005_RegisterRecyclerAsync_ValidRequest_CreatesPendingRecyclerAndPostGisPoint()
    {
        var (sut, db, _, _) = CreateSut();
        var req = new RegisterRecyclerRequestDto
        {
            Username = "recycler_new_05",
            Email = "recycler05@ecolink.vn",
            Password = "Password@123",
            FullName = "Phạm Thu Mua",
            FacilityName = "Trạm Tái Chế Hòa Lạc Mới",
            RepresentativeName = "Phạm Thu Mua",
            BusinessAddress = "Khu CNC Hòa Lạc, Thạch Thất, Hà Nội",
            Latitude = 21.0124,
            Longitude = 105.5253,
            ContactPhone = "0988112233",
            LegalPermitUrl = "https://cdn.ecolink.test/permit.pdf",
            BusinessPhotoUrl = "https://cdn.ecolink.test/photo.jpg"
        };

        var res = await sut.RegisterRecyclerAsync(req);

        res.Success.Should().BeTrue();
        res.User.Role.Should().Be(UserRole.Recycler.ToString());
        res.User.Status.Should().Be(UserStatus.PendingVerification.ToString());

        var profile = await db.RecyclerProfiles.FirstAsync(r => r.UserId == res.User.Id);
        profile.VerificationStatus.Should().Be(VerificationStatus.Pending);
        profile.ActiveStatus.Should().Be(RecyclerActiveStatus.Offline);
        profile.Location.Should().NotBeNull();
        profile.Location!.SRID.Should().Be(4326);
        profile.Location.X.Should().BeApproximately(105.5253, 0.0001);
        profile.Location.Y.Should().BeApproximately(21.0124, 0.0001);
    }

    [Fact]
    public async Task UTC006_RegisterRecyclerAsync_DuplicateEmail_ThrowsBrAuth01()
    {
        var (sut, _, _, _) = CreateSut();
        var req = new RegisterRecyclerRequestDto
        {
            Username = "recycler_dup_06",
            Email = "collector@ecolink.vn", // trùng email demo collector
            Password = "Password@123",
            FullName = "Cơ Sở Trùng",
            FacilityName = "Cơ Sở Trùng",
            RepresentativeName = "Đại Diện",
            BusinessAddress = "Hà Nội",
            Latitude = 21.01,
            Longitude = 105.52
        };

        var act = () => sut.RegisterRecyclerAsync(req);

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-01");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-01");
    }

    [Fact]
    public async Task UTC007_LoginAsync_ValidCredentials_ResetsFailedAttemptsAndReturnsTokens()
    {
        var (sut, db, _, _) = CreateSut();
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Username = "login_ok_07",
            Email = "login07@ecolink.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("ValidPass@123"),
            FullName = "User 07",
            Role = UserRole.Citizen,
            Status = UserStatus.Active,
            FailedLoginAttempts = 3
        });
        await db.SaveChangesAsync();

        var res = await sut.LoginAsync(new LoginRequestDto
        {
            UsernameOrEmail = "login07@ecolink.vn",
            Password = "ValidPass@123"
        }, "127.0.0.1");

        res.Success.Should().BeTrue();
        res.AccessToken.Should().NotBeNullOrWhiteSpace();
        res.RefreshToken.Should().NotBeNullOrWhiteSpace();

        var updated = await db.Users.FirstAsync(u => u.Id == userId);
        updated.FailedLoginAttempts.Should().Be(0);
        updated.LockoutUntil.Should().BeNull();
    }

    [Fact]
    public async Task UTC008_LoginAsync_NonExistentAccount_ThrowsBrAuth03()
    {
        var (sut, _, _, _) = CreateSut();

        var act = () => sut.LoginAsync(new LoginRequestDto
        {
            UsernameOrEmail = "nonexistent_user@ecolink.vn",
            Password = "AnyPassword123"
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-03");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-01");
    }

    [Fact]
    public async Task UTC009_LoginAsync_WrongPasswordBelow5Attempts_IncrementsFailedLoginAttempts()
    {
        var (sut, db, _, _) = CreateSut();
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Username = "user_wrong_pass_09",
            Email = "user09@ecolink.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPass@123"),
            FullName = "User 09",
            Role = UserRole.Citizen,
            Status = UserStatus.Active,
            FailedLoginAttempts = 2
        });
        await db.SaveChangesAsync();

        var act = () => sut.LoginAsync(new LoginRequestDto
        {
            UsernameOrEmail = "user_wrong_pass_09",
            Password = "WrongPass@999"
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-03");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-01");

        var updated = await db.Users.FirstAsync(u => u.Id == userId);
        updated.FailedLoginAttempts.Should().Be(3);
        updated.Status.Should().Be(UserStatus.Active);
    }

    [Fact]
    public async Task UTC010_LoginAsync_FifthConsecutiveWrongPassword_LocksAccountFor15Minutes_BrAuth04()
    {
        var (sut, db, _, _) = CreateSut();
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Username = "user_lockout_10",
            Email = "user10@ecolink.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPass@123"),
            FullName = "User 10",
            Role = UserRole.Citizen,
            Status = UserStatus.Active,
            FailedLoginAttempts = 4
        });
        await db.SaveChangesAsync();

        var act = () => sut.LoginAsync(new LoginRequestDto
        {
            UsernameOrEmail = "user_lockout_10",
            Password = "WrongPass@5th"
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-04");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-03");

        var updated = await db.Users.FirstAsync(u => u.Id == userId);
        updated.FailedLoginAttempts.Should().Be(5);
        updated.Status.Should().Be(UserStatus.Locked);
        updated.LockoutUntil.Should().NotBeNull();
        updated.LockoutUntil!.Value.Should().BeAfter(DateTime.UtcNow.AddMinutes(14));
    }

    [Fact]
    public async Task UTC011_LoginAsync_AccountCurrentlyLocked_RejectsEvenWithValidPassword_BrAuth04()
    {
        var (sut, db, _, _) = CreateSut();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "locked_user_11",
            Email = "user11@ecolink.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPass@123"),
            FullName = "User 11",
            Role = UserRole.Citizen,
            Status = UserStatus.Locked,
            FailedLoginAttempts = 5,
            LockoutUntil = DateTime.UtcNow.AddMinutes(10)
        });
        await db.SaveChangesAsync();

        var act = () => sut.LoginAsync(new LoginRequestDto
        {
            UsernameOrEmail = "locked_user_11",
            Password = "CorrectPass@123"
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-04");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-03");
    }

    [Fact]
    public async Task UTC012_LoginAsync_LockoutExpiredAndValidPassword_AutoUnlocksAndLogsIn()
    {
        var (sut, db, _, _) = CreateSut();
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Username = "expired_lock_12",
            Email = "user12@ecolink.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPass@123"),
            FullName = "User 12",
            Role = UserRole.Citizen,
            Status = UserStatus.Locked,
            FailedLoginAttempts = 5,
            LockoutUntil = DateTime.UtcNow.AddMinutes(-1)
        });
        await db.SaveChangesAsync();

        var res = await sut.LoginAsync(new LoginRequestDto
        {
            UsernameOrEmail = "expired_lock_12",
            Password = "CorrectPass@123"
        });

        res.Success.Should().BeTrue();
        var updated = await db.Users.FirstAsync(u => u.Id == userId);
        updated.Status.Should().Be(UserStatus.Active);
        updated.FailedLoginAttempts.Should().Be(0);
        updated.LockoutUntil.Should().BeNull();
    }

    [Fact]
    public async Task UTC013_LoginAsync_SuspendedAccount_ThrowsBrAuth05()
    {
        var (sut, db, _, _) = CreateSut();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "suspended_user_13",
            Email = "user13@ecolink.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPass@123"),
            FullName = "User 13",
            Role = UserRole.Citizen,
            Status = UserStatus.Suspended
        });
        await db.SaveChangesAsync();

        var act = () => sut.LoginAsync(new LoginRequestDto
        {
            UsernameOrEmail = "suspended_user_13",
            Password = "CorrectPass@123"
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-05");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-04");
    }

    [Fact]
    public async Task UTC014_LoginAsync_GoogleLinkedAccountWithoutPasswordHash_ThrowsBrAuth03()
    {
        var (sut, db, _, _) = CreateSut();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "google_only_14",
            Email = "google14@gmail.com",
            PasswordHash = null,
            IsGoogleAuth = true,
            FullName = "Google User 14",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        });
        await db.SaveChangesAsync();

        var act = () => sut.LoginAsync(new LoginRequestDto
        {
            UsernameOrEmail = "google14@gmail.com",
            Password = "AnyPassword123"
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-03");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-01");
    }

    [Fact]
    public async Task UTC015_AuthenticateGoogleAsync_ValidTokenForNewEmail_AutoProvisionsCitizenAccount()
    {
        var (sut, db, _, _) = CreateSut();

        var res = await sut.AuthenticateGoogleAsync("mock-newgoogle15@gmail.com");

        res.Success.Should().BeTrue();
        res.User.Email.Should().Be("newgoogle15@gmail.com");
        res.User.Role.Should().Be(UserRole.Citizen.ToString());
        res.User.Status.Should().Be(UserStatus.Active.ToString());
        res.User.IsGoogleAuth.Should().BeTrue();

        var created = await db.Users.Include(u => u.EcoProfile).FirstAsync(u => u.Email == "newgoogle15@gmail.com");
        created.EcoProfile.Should().NotBeNull();
        created.EcoProfile!.TreeLevel.Should().Be(1);
    }

    [Fact]
    public async Task UTC016_AuthenticateGoogleAsync_ExistingSuspendedUser_ThrowsBrAuth05()
    {
        var (sut, db, _, _) = CreateSut();
        db.Users.Add(new User
        {
            Id = Guid.NewGuid(),
            Username = "suspended_google_16",
            Email = "suspended16@gmail.com",
            FullName = "Suspended Google",
            Role = UserRole.Citizen,
            Status = UserStatus.Suspended,
            IsGoogleAuth = true
        });
        await db.SaveChangesAsync();

        var act = () => sut.AuthenticateGoogleAsync("mock-suspended16@gmail.com");

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-05");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-04");
    }

    [Fact]
    public async Task UTC017_AuthenticateGoogleAsync_InvalidJwtIdToken_ThrowsBrAuth07()
    {
        var (sut, _, _, _) = CreateSut();

        var act = () => sut.AuthenticateGoogleAsync("invalid.jwt.token");

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-07");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-01");
    }

    [Fact]
    public async Task UTC018_RefreshTokenAsync_ValidActiveToken_RotatesTokenAndRevokesOldToken()
    {
        var (sut, db, _, _) = CreateSut();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "refresh_user_18",
            Email = "refresh18@ecolink.vn",
            FullName = "Refresh User 18",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        var oldToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = "hash-raw-valid-18",
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            IsRevoked = false
        };
        db.Users.Add(user);
        db.RefreshTokens.Add(oldToken);
        await db.SaveChangesAsync();

        var res = await sut.RefreshTokenAsync("raw-valid-18", "127.0.0.1");

        res.Success.Should().BeTrue();
        res.AccessToken.Should().NotBeNullOrWhiteSpace();
        res.RefreshToken.Should().NotBe("raw-valid-18");

        var updatedOld = await db.RefreshTokens.FirstAsync(r => r.Id == oldToken.Id);
        updatedOld.IsRevoked.Should().BeTrue();
        updatedOld.ReplacedByTokenHash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UTC019_RefreshTokenAsync_RevokedTokenReplayAttack_RevokesAllUserTokensAndThrowsBrAuth05()
    {
        var (sut, db, _, _) = CreateSut();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "replay_user_19",
            Email = "replay19@ecolink.vn",
            FullName = "Replay User 19",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        var revokedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = "hash-revoked-token-19",
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            IsRevoked = true
        };
        var activeSiblingToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = "hash-active-sibling-19",
            ExpiresAt = DateTime.UtcNow.AddDays(5),
            IsRevoked = false
        };
        db.Users.Add(user);
        db.RefreshTokens.AddRange(revokedToken, activeSiblingToken);
        await db.SaveChangesAsync();

        var act = () => sut.RefreshTokenAsync("revoked-token-19");

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-05");
        ex.Which.MessageCode.Should().Be("MSG-AUTH-10");

        var siblingAfter = await db.RefreshTokens.FirstAsync(r => r.Id == activeSiblingToken.Id);
        siblingAfter.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task UTC020_RefreshTokenAsync_ExpiredToken_MarksRevokedAndThrowsBrAuth05()
    {
        var (sut, db, _, _) = CreateSut();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "expired_token_user_20",
            Email = "expired20@ecolink.vn",
            FullName = "Expired User 20",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        var expiredToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            User = user,
            TokenHash = "hash-expired-token-20",
            ExpiresAt = DateTime.UtcNow.AddMinutes(-5),
            IsRevoked = false
        };
        db.Users.Add(user);
        db.RefreshTokens.Add(expiredToken);
        await db.SaveChangesAsync();

        var act = () => sut.RefreshTokenAsync("expired-token-20");

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-AUTH-05");
        ex.Which.MessageCode.Should().Be("MSG-NTF-01");

        var updated = await db.RefreshTokens.FirstAsync(r => r.Id == expiredToken.Id);
        updated.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task UTC021_ForgotPasswordAsync_RequestedWithin1Minute_RateLimitsAndDoesNotSendSecondEmail()
    {
        var (sut, db, _, emailService) = CreateSut();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "forgot_rate_21",
            Email = "forgot21@ecolink.vn",
            FullName = "Forgot User 21",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        db.Users.Add(user);
        db.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "hash-recent-21",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow.AddSeconds(-20)
        });
        await db.SaveChangesAsync();

        await sut.ForgotPasswordAsync("forgot21@ecolink.vn");

        await emailService.DidNotReceiveWithAnyArgs()
            .SendPasswordResetEmailAsync(default!, default!, default!, default);
        var tokenCount = await db.PasswordResetTokens.CountAsync(t => t.UserId == user.Id);
        tokenCount.Should().Be(1);
    }

    [Fact]
    public async Task UTC022_ResetAndChangePasswordAsync_ValidatesLengthAndToken_UpdatesHashAndRevokesSessions()
    {
        var (sut, db, _, _) = CreateSut();
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = "reset_user_22",
            Email = "reset22@ecolink.vn",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPass@123"),
            FullName = "Reset User 22",
            Role = UserRole.Citizen,
            Status = UserStatus.Locked,
            FailedLoginAttempts = 5,
            LockoutUntil = DateTime.UtcNow.AddMinutes(10)
        };
        var resetToken = new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "hash-valid-reset-22",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow.AddMinutes(-2)
        };
        var activeRefreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = "hash-session-22",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false
        };
        db.Users.Add(user);
        db.PasswordResetTokens.Add(resetToken);
        db.RefreshTokens.Add(activeRefreshToken);
        await db.SaveChangesAsync();

        // 1. Mật khẩu < 6 ký tự -> BR-AUTH-06
        var shortAct = () => sut.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = "reset22@ecolink.vn",
            Token = "valid-reset-22",
            NewPassword = "123"
        });
        (await shortAct.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-AUTH-06");

        // 2. Đặt lại mật khẩu hợp lệ -> mở khóa tài khoản, đánh dấu UsedAt và thu hồi RefreshToken
        await sut.ResetPasswordAsync(new ResetPasswordRequestDto
        {
            Email = "reset22@ecolink.vn",
            Token = "valid-reset-22",
            NewPassword = "NewStrongPass@123"
        });

        var updatedUser = await db.Users.FirstAsync(u => u.Id == user.Id);
        updatedUser.Status.Should().Be(UserStatus.Active);
        updatedUser.FailedLoginAttempts.Should().Be(0);
        BCrypt.Net.BCrypt.Verify("NewStrongPass@123", updatedUser.PasswordHash).Should().BeTrue();
        (await db.RefreshTokens.FirstAsync(r => r.Id == activeRefreshToken.Id)).IsRevoked.Should().BeTrue();
    }
}

