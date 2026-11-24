using Ecolink.Application.Dtos;
using Ecolink.Application.Validators;
using FluentAssertions;
using Xunit;
using static Ecolink.UnitTests.Validators.ValidatorAssert;

namespace Ecolink.UnitTests.Validators;

[Trait("Layer", "Unit")]
[Trait("Area", "Validators")]
public class AuthValidatorTests
{
    // ───────── RegisterUserRequestDtoValidator ─────────
    private readonly RegisterUserRequestDtoValidator _reg = new();

    private static RegisterUserRequestDto ValidReg() => new()
    {
        Username = "user_01", Email = "a@b.com", Password = "password123",
        FullName = "Nguyen Van A", PhoneNumber = "0912345678"
    };

    [Fact]
    public void Register_ValidModel_Passes() => IsValid(_reg, ValidReg()).Should().BeTrue();

    [Theory]
    [InlineData("", true)]
    [InlineData("ab", true)]
    [InlineData("abc", false)]
    [InlineData("a.b_c-d", false)]
    [InlineData("user name", true)]
    [InlineData("user@x", true)]
    [InlineData("tên_có_dấu", true)]
    public void Register_Username_Rules(string value, bool expectError)
    {
        var m = ValidReg(); m.Username = value;
        Has(_reg, m, nameof(m.Username)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(51, true)]
    public void Register_Username_MaxLength(int len, bool expectError)
    {
        var m = ValidReg(); m.Username = Str(len);
        Has(_reg, m, nameof(m.Username)).Should().Be(expectError);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("abc", true)]
    [InlineData("a@", true)]
    [InlineData("a@b.com", false)]
    public void Register_Email_Rules(string value, bool expectError)
    {
        var m = ValidReg(); m.Email = value;
        Has(_reg, m, nameof(m.Email)).Should().Be(expectError);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("1234567", true)]
    [InlineData("12345678", false)]
    public void Register_Password_MinLength(string value, bool expectError)
    {
        var m = ValidReg(); m.Password = value;
        Has(_reg, m, nameof(m.Password)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void Register_FullName_Length(int len, bool expectError)
    {
        var m = ValidReg(); m.FullName = Str(len);
        Has(_reg, m, nameof(m.FullName)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("0912345678", false)]
    [InlineData("+84912345678", false)]
    [InlineData("09123456789", false)]      // 0 + 10 chữ số: vẫn hợp lệ
    [InlineData("091234567", true)]         // 0 + 8 chữ số
    [InlineData("091234567890", true)]      // 0 + 11 chữ số
    [InlineData("1912345678", true)]
    [InlineData("abcdefghij", true)]
    [InlineData("0912 345 678", true)]
    public void Register_PhoneNumber_Rules(string? value, bool expectError)
    {
        var m = ValidReg(); m.PhoneNumber = value;
        Has(_reg, m, nameof(m.PhoneNumber)).Should().Be(expectError);
    }

    // ───────── RegisterRecyclerRequestDtoValidator ─────────
    private readonly RegisterRecyclerRequestDtoValidator _regRec = new();

    private static RegisterRecyclerRequestDto ValidRec() => new()
    {
        Username = "recycler1", Email = "r@b.com", Password = "password123", FullName = "Tran B",
        FacilityName = "Co so A", RepresentativeName = "Tran B", BusinessAddress = "1 Le Loi",
        Latitude = 10.77, Longitude = 106.69
    };

    [Fact]
    public void RegisterRecycler_ValidModel_Passes() => IsValid(_regRec, ValidRec()).Should().BeTrue();

    [Theory]
    [InlineData(-90.0, false)]
    [InlineData(90.0, false)]
    [InlineData(0.0, false)]
    [InlineData(90.0001, true)]
    [InlineData(-90.1, true)]
    public void RegisterRecycler_Latitude_Boundary(double lat, bool expectError)
    {
        var m = ValidRec(); m.Latitude = lat;
        Has(_regRec, m, nameof(m.Latitude)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(-180.0, false)]
    [InlineData(180.0, false)]
    [InlineData(180.0001, true)]
    [InlineData(-181, true)]
    public void RegisterRecycler_Longitude_Boundary(double lng, bool expectError)
    {
        var m = ValidRec(); m.Longitude = lng;
        Has(_regRec, m, nameof(m.Longitude)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(150, false)]
    [InlineData(151, true)]
    public void RegisterRecycler_FacilityName_Length(int len, bool expectError)
    {
        var m = ValidRec(); m.FacilityName = Str(len);
        Has(_regRec, m, nameof(m.FacilityName)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void RegisterRecycler_RepresentativeName_Length(int len, bool expectError)
    {
        var m = ValidRec(); m.RepresentativeName = Str(len);
        Has(_regRec, m, nameof(m.RepresentativeName)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(255, false)]
    [InlineData(256, true)]
    public void RegisterRecycler_BusinessAddress_Length(int len, bool expectError)
    {
        var m = ValidRec(); m.BusinessAddress = Str(len);
        Has(_regRec, m, nameof(m.BusinessAddress)).Should().Be(expectError);
    }

    // ───────── Login / Google / Forgot ─────────
    [Theory]
    [InlineData("", "x", true, false)]
    [InlineData("u", "", false, true)]
    [InlineData("", "", true, true)]
    [InlineData("u", "p", false, false)]
    public void Login_RequiredFields(string user, string pass, bool userErr, bool passErr)
    {
        var v = new LoginRequestDtoValidator();
        var m = new LoginRequestDto { UsernameOrEmail = user, Password = pass };
        Has(v, m, nameof(m.UsernameOrEmail)).Should().Be(userErr);
        Has(v, m, nameof(m.Password)).Should().Be(passErr);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("eyJhbGciOi...", true)]
    public void GoogleAuth_IdToken_Required(string token, bool valid)
        => IsValid(new GoogleAuthRequestDtoValidator(), new GoogleAuthRequestDto { IdToken = token }).Should().Be(valid);

    [Theory]
    [InlineData("", false)]
    [InlineData("not-an-email", false)]
    [InlineData("a@b.com", true)]
    public void ForgotPassword_Email(string email, bool valid)
        => IsValid(new ForgotPasswordRequestDtoValidator(), new ForgotPasswordRequestDto { Email = email }).Should().Be(valid);

    // ───────── ResetPasswordRequestDtoValidator ─────────
    private readonly ResetPasswordRequestDtoValidator _reset = new();

    private static ResetPasswordRequestDto ValidReset() => new()
    { Email = "a@b.com", Token = "tok", NewPassword = "abcdefgh", ConfirmPassword = "abcdefgh" };

    [Fact]
    public void ResetPassword_ValidModel_Passes() => IsValid(_reset, ValidReset()).Should().BeTrue();

    [Theory]
    [InlineData(null, false)]       // không nhập xác nhận → bỏ qua rule Equal
    [InlineData("", false)]
    [InlineData("abcdefgh", false)]
    [InlineData("abcdefgX", true)]
    public void ResetPassword_Confirm_MustMatchWhenProvided(string? confirm, bool expectError)
    {
        var m = ValidReset(); m.ConfirmPassword = confirm;
        Has(_reset, m, nameof(m.ConfirmPassword)).Should().Be(expectError);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("1234567", true)]
    [InlineData("12345678", false)]
    public void ResetPassword_NewPassword_MinLength(string pw, bool expectError)
    {
        var m = ValidReset(); m.NewPassword = pw; m.ConfirmPassword = null;
        Has(_reset, m, nameof(m.NewPassword)).Should().Be(expectError);
    }

    [Fact]
    public void ResetPassword_EmptyToken_Fails()
    {
        var m = ValidReset(); m.Token = "";
        Has(_reset, m, nameof(m.Token)).Should().BeTrue();
    }

    // ───────── UpdateUserProfileRequestDtoValidator ─────────
    private readonly UpdateUserProfileRequestDtoValidator _profile = new();

    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void UpdateProfile_FullName_Length(int len, bool expectError)
    {
        var m = new UpdateUserProfileRequestDto { FullName = Str(len) };
        Has(_profile, m, nameof(m.FullName)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("0987654321", false)]
    [InlineData("+84987654321", false)]
    [InlineData("98765", true)]
    public void UpdateProfile_Phone(string? phone, bool expectError)
    {
        var m = new UpdateUserProfileRequestDto { FullName = "Nguyen A", PhoneNumber = phone };
        Has(_profile, m, nameof(m.PhoneNumber)).Should().Be(expectError);
    }

    // ───────── ChangePasswordRequestDtoValidator ─────────
    private readonly ChangePasswordRequestDtoValidator _change = new();

    [Fact]
    public void ChangePassword_NewEqualsCurrent_Fails()
    {
        var m = new ChangePasswordRequestDto { CurrentPassword = "abcdefgh", NewPassword = "abcdefgh" };
        Has(_change, m, nameof(m.NewPassword)).Should().BeTrue();
    }

    [Fact]
    public void ChangePassword_Valid_Passes()
    {
        var m = new ChangePasswordRequestDto { CurrentPassword = "oldpass123", NewPassword = "newpass123", ConfirmPassword = "newpass123" };
        IsValid(_change, m).Should().BeTrue();
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("1234567", true)]
    [InlineData("12345678", false)]
    public void ChangePassword_NewPassword_MinLength(string pw, bool expectError)
    {
        var m = new ChangePasswordRequestDto { CurrentPassword = "zzzzzzzz", NewPassword = pw };
        Has(_change, m, nameof(m.NewPassword)).Should().Be(expectError);
    }

    [Fact]
    public void ChangePassword_EmptyCurrent_Fails()
    {
        var m = new ChangePasswordRequestDto { CurrentPassword = "", NewPassword = "newpass123" };
        Has(_change, m, nameof(m.CurrentPassword)).Should().BeTrue();
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("newpass123", false)]
    [InlineData("otherpass", true)]
    public void ChangePassword_Confirm_MustMatchWhenProvided(string? confirm, bool expectError)
    {
        var m = new ChangePasswordRequestDto { CurrentPassword = "oldpass123", NewPassword = "newpass123", ConfirmPassword = confirm };
        Has(_change, m, nameof(m.ConfirmPassword)).Should().Be(expectError);
    }
}
