using Ecolink.Application.Dtos;
using FluentValidation;

namespace Ecolink.Application.Validators;

/// <summary>
/// Validator cho Use Case U-17: Đăng ký nâng cấp lên Cơ sở thu gom (Recycler)
/// </summary>
public class ApplyRecyclerUpgradeValidator : AbstractValidator<ApplyRecyclerUpgradeRequest>
{
    public ApplyRecyclerUpgradeValidator()
    {
        RuleFor(x => x.FacilityName)
            .NotEmpty().WithMessage("Tên cơ sở thu mua không được để trống.")
            .MaximumLength(150).WithMessage("Tên cơ sở tối đa 150 ký tự.");

        RuleFor(x => x.RepresentativeName)
            .NotEmpty().WithMessage("Tên người đại diện pháp luật không được để trống.")
            .MaximumLength(100).WithMessage("Tên người đại diện tối đa 100 ký tự.");

        RuleFor(x => x.BusinessAddress)
            .NotEmpty().WithMessage("Địa chỉ cơ sở kinh doanh không được để trống.")
            .MaximumLength(255).WithMessage("Địa chỉ kinh doanh tối đa 255 ký tự.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90.0, 90.0).WithMessage("Vĩ độ (Latitude) phải nằm trong khoảng [-90, 90].")
            .NotEqual(0).WithMessage("Vui lòng chọn tọa độ GPS hợp lệ trên bản đồ.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180.0, 180.0).WithMessage("Kinh độ (Longitude) phải nằm trong khoảng [-180, 180].")
            .NotEqual(0).WithMessage("Vui lòng chọn tọa độ GPS hợp lệ trên bản đồ.");

        RuleFor(x => x.ContactPhone)
            .NotEmpty().WithMessage("Số điện thoại liên hệ thu gom không được để trống.")
            .Matches(@"^(0|\+84)[0-9]{8,10}$").WithMessage("Số điện thoại liên hệ không đúng định dạng Việt Nam.");

        RuleFor(x => x.LegalPermitUrl)
            .NotEmpty().WithMessage("Vui lòng đính kèm đường dẫn hình ảnh Giấy phép kinh doanh / Chứng từ pháp lý.");

        RuleFor(x => x.BusinessPhotoUrl)
            .NotEmpty().WithMessage("Vui lòng đính kèm đường dẫn hình ảnh chụp thực tế mặt tiền cơ sở / kho bãi.");
    }
}

/// <summary>
/// Validator cho Use Case S-04: Staff phê duyệt hoặc từ chối hồ sơ đăng ký Recycler
/// </summary>
public class VerifyRecyclerApplicationValidator : AbstractValidator<VerifyRecyclerApplicationRequest>
{
    public VerifyRecyclerApplicationValidator()
    {
        // Thực thi quy tắc BR-MTH-02: Từ chối bắt buộc phải ghi rõ lý do
        RuleFor(x => x.RejectionReason)
            .NotEmpty()
            .When(x => !x.IsApproved)
            .WithMessage("Khi từ chối hồ sơ, nhân viên bắt buộc phải ghi rõ lý do từ chối (BR-MTH-02).")
            .MinimumLength(10)
            .When(x => !x.IsApproved)
            .WithMessage("Lý do từ chối phải rõ ràng, tối thiểu 10 ký tự.");
    }
}

