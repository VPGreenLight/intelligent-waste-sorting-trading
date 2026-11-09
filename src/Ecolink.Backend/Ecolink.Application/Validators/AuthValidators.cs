using FluentValidation;
using Ecolink.Application.Dtos;

namespace Ecolink.Application.Validators;

/// <summary>
/// Validator cho DTO đăng ký tài khoản Người dân (Citizen)
/// </summary>
public class RegisterUserRequestDtoValidator : AbstractValidator<RegisterUserRequestDto>
{
    public RegisterUserRequestDtoValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Tên đăng nhập không được để trống.")
            .Length(3, 50).WithMessage("Tên đăng nhập phải từ 3 đến 50 ký tự.")
            .Matches(@"^[a-zA-Z0-9._-]+$").WithMessage("Tên đăng nhập chỉ chứa chữ cái, số và các ký tự . _ -");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu tối thiểu 6 ký tự.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên không được để trống.")
            .MaximumLength(100).WithMessage("Họ và tên tối đa 100 ký tự.");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^(0|\+84)[0-9]{9,10}$")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber))
            .WithMessage("Số điện thoại không hợp lệ (định dạng Việt Nam 10-11 chữ số).");
    }
}

/// <summary>
/// Validator cho DTO đăng ký tài khoản Cơ sở thu gom (Recycler) kèm tọa độ GPS
/// </summary>
public class RegisterRecyclerRequestDtoValidator : AbstractValidator<RegisterRecyclerRequestDto>
{
    public RegisterRecyclerRequestDtoValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Tên đăng nhập không được để trống.")
            .Length(3, 50).WithMessage("Tên đăng nhập phải từ 3 đến 50 ký tự.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu tối thiểu 6 ký tự.");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên người đại diện không được để trống.");

        RuleFor(x => x.FacilityName)
            .NotEmpty().WithMessage("Tên cơ sở thu mua không được để trống.")
            .MaximumLength(150).WithMessage("Tên cơ sở thu mua tối đa 150 ký tự.");

        RuleFor(x => x.RepresentativeName)
            .NotEmpty().WithMessage("Người đại diện pháp luật không được để trống.")
            .MaximumLength(100).WithMessage("Tên người đại diện tối đa 100 ký tự.");

        RuleFor(x => x.BusinessAddress)
            .NotEmpty().WithMessage("Địa chỉ kinh doanh không được để trống.")
            .MaximumLength(255).WithMessage("Địa chỉ kinh doanh tối đa 255 ký tự.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90.0, 90.0).WithMessage("Vĩ độ (Latitude) phải nằm trong khoảng -90 đến 90 độ.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180.0, 180.0).WithMessage("Kinh độ (Longitude) phải nằm trong khoảng -180 đến 180 độ.");
    }
}

/// <summary>
/// Validator cho DTO đăng nhập
/// </summary>
public class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
{
    public LoginRequestDtoValidator()
    {
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty().WithMessage("Tên đăng nhập hoặc Email không được để trống.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.");
    }
}

/// <summary>
/// Validator cho DTO xác thực Google OAuth
/// </summary>
public class GoogleAuthRequestDtoValidator : AbstractValidator<GoogleAuthRequestDto>
{
    public GoogleAuthRequestDtoValidator()
    {
        RuleFor(x => x.IdToken)
            .NotEmpty().WithMessage("Google ID Token không được để trống.");
    }
}

/// <summary>
/// Validator cho DTO yêu cầu quên mật khẩu
/// </summary>
public class ForgotPasswordRequestDtoValidator : AbstractValidator<ForgotPasswordRequestDto>
{
    public ForgotPasswordRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");
    }
}

/// <summary>
/// Validator cho DTO đặt lại mật khẩu mới
/// </summary>
public class ResetPasswordRequestDtoValidator : AbstractValidator<ResetPasswordRequestDto>
{
    public ResetPasswordRequestDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống.")
            .EmailAddress().WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Mã Token không được để trống.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu mới không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu tối thiểu 6 ký tự.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword)
            .When(x => !string.IsNullOrEmpty(x.ConfirmPassword))
            .WithMessage("Mật khẩu xác nhận không khớp với mật khẩu mới.");
    }
}

/// <summary>
/// Validator cho DTO cập nhật hồ sơ cá nhân (SU-01)
/// </summary>
public class UpdateUserProfileRequestDtoValidator : AbstractValidator<UpdateUserProfileRequestDto>
{
    public UpdateUserProfileRequestDtoValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Tên hiển thị không được để trống.")
            .Length(2, 100).WithMessage("Tên hiển thị phải từ 2 đến 100 ký tự.");

        RuleFor(x => x.PhoneNumber)
            .Matches(@"^(0|\+84)[0-9]{9,10}$")
            .When(x => !string.IsNullOrEmpty(x.PhoneNumber))
            .WithMessage("Số điện thoại chưa đúng định dạng Việt Nam.");
    }
}

/// <summary>
/// Validator cho DTO đổi mật khẩu khi đang đăng nhập (SU-02)
/// </summary>
public class ChangePasswordRequestDtoValidator : AbstractValidator<ChangePasswordRequestDto>
{
    public ChangePasswordRequestDtoValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu hiện tại.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Vui lòng nhập mật khẩu mới.")
            .MinimumLength(6).WithMessage("Mật khẩu mới tối thiểu 6 ký tự.")
            .NotEqual(x => x.CurrentPassword).WithMessage("Mật khẩu mới phải khác mật khẩu hiện tại.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword)
            .When(x => !string.IsNullOrEmpty(x.ConfirmPassword))
            .WithMessage("Mật khẩu xác nhận không khớp với mật khẩu mới.");
    }
}
