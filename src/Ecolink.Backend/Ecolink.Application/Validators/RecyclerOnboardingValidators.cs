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

        RuleFor(x => x.RecyclerType)
            .Must(t => string.IsNullOrWhiteSpace(t) ||
                       t.Equals("Facility", StringComparison.OrdinalIgnoreCase) ||
                       t.Equals("Individual", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Loại hình đối tác chỉ hỗ trợ 'Facility' (Cơ sở cố định) hoặc 'Individual' (Thu mua nhỏ lẻ).");

        RuleFor(x => x.IdentityCardFrontUrl)
            .NotEmpty()
            .When(x => string.Equals(x.RecyclerType, "Individual", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Người thu mua nhỏ lẻ bắt buộc đính kèm ảnh mặt trước Căn cước công dân (CCCD).");

        RuleFor(x => x.IdentityCardBackUrl)
            .NotEmpty()
            .When(x => string.Equals(x.RecyclerType, "Individual", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Người thu mua nhỏ lẻ bắt buộc đính kèm ảnh mặt sau Căn cước công dân (CCCD).");

        RuleFor(x => x.LegalPermitUrl)
            .NotEmpty()
            .When(x => !string.Equals(x.RecyclerType, "Individual", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Vui lòng đính kèm đường dẫn hình ảnh Giấy phép kinh doanh / Chứng từ pháp lý.");

        RuleFor(x => x.BusinessPhotoUrl)
            .NotEmpty()
            .When(x => !string.Equals(x.RecyclerType, "Individual", StringComparison.OrdinalIgnoreCase) && x.BusinessPhotoUrls == null)
            .WithMessage("Vui lòng đính kèm đường dẫn hình ảnh chụp thực tế mặt tiền cơ sở / kho bãi.");

        RuleFor(x => x.BusinessPhotoUrls)
            .Must(list => list != null && list.Count == 5 && list.All(u => !string.IsNullOrWhiteSpace(u)))
            .When(x => !string.Equals(x.RecyclerType, "Individual", StringComparison.OrdinalIgnoreCase) && x.BusinessPhotoUrls != null)
            .WithMessage("Cơ sở thu mua cố định bắt buộc đính kèm đúng đủ 5 ảnh thực tế mặt tiền / kho bãi (5/5 ảnh).");
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

