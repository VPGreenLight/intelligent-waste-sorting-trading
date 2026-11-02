using Ecolink.Application.Dtos;
using FluentValidation;

namespace Ecolink.Application.Validators;

/// <summary>
/// Bộ kiểm tra dữ liệu đầu vào bằng FluentValidation cho Module Recycler Profile, Pricing &amp; Availability (R-01, R-02)
/// và Location-Based Recycler Discovery (U-05).
/// </summary>
public class UpdateRecyclerProfileRequestValidator : AbstractValidator<UpdateRecyclerProfileRequest>
{
    public UpdateRecyclerProfileRequestValidator()
    {
        RuleFor(x => x.FacilityName)
            .NotEmpty().WithMessage("Tên cơ sở là bắt buộc.")
            .MaximumLength(150).WithMessage("Tên cơ sở tối đa 150 ký tự.");

        RuleFor(x => x.RepresentativeName)
            .MaximumLength(100).WithMessage("Tên người đại diện tối đa 100 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.RepresentativeName));

        RuleFor(x => x.BusinessAddress)
            .NotEmpty().WithMessage("Địa chỉ kinh doanh là bắt buộc.");

        RuleFor(x => x.Latitude)
            .InclusiveBetween(-90.0, 90.0).WithMessage("Vĩ độ phải nằm trong khoảng -90 đến 90.");

        RuleFor(x => x.Longitude)
            .InclusiveBetween(-180.0, 180.0).WithMessage("Kinh độ phải nằm trong khoảng -180 đến 180.");

        RuleFor(x => x.ContactPhone)
            .NotEmpty().WithMessage("Số điện thoại liên hệ là bắt buộc.")
            .MaximumLength(20).WithMessage("Số điện thoại tối đa 20 ký tự.");

        RuleFor(x => x.OperatingHours)
            .MaximumLength(100).WithMessage("Giờ hoạt động tối đa 100 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.OperatingHours));
    }
}

public class CreateRecyclerPriceRequestValidator : AbstractValidator<CreateRecyclerPriceRequest>
{
    public CreateRecyclerPriceRequestValidator()
    {
        RuleFor(x => x.WasteCategoryId)
            .NotNull().WithMessage("Vui lòng chọn loại phế liệu.")
            .NotEqual(Guid.Empty).WithMessage("Mã loại phế liệu không hợp lệ.");

        RuleFor(x => x.PricePerKg)
            .NotNull().WithMessage("Giá thu mua là bắt buộc.")
            .GreaterThan(0).WithMessage("Giá thu mua phải là số nguyên dương (VND/kg).")
            .Must(p => !p.HasValue || p.Value == decimal.Truncate(p.Value))
            .WithMessage("Giá thu mua phải là số nguyên dương (VND/kg).");

        RuleFor(x => x.MinQuantityKg)
            .GreaterThan(0).WithMessage("Khối lượng tối thiểu phải lớn hơn 0.");

        RuleFor(x => x.CollectionConditions)
            .MaximumLength(200).WithMessage("Điều kiện thu gom tối đa 200 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.CollectionConditions));
    }
}

public class UpdateRecyclerPriceRequestValidator : AbstractValidator<UpdateRecyclerPriceRequest>
{
    public UpdateRecyclerPriceRequestValidator()
    {
        RuleFor(x => x.PricePerKg)
            .GreaterThan(0).WithMessage("Giá thu mua phải là số nguyên dương (VND/kg).")
            .Must(p => !p.HasValue || p.Value == decimal.Truncate(p.Value))
            .WithMessage("Giá thu mua phải là số nguyên dương (VND/kg).")
            .When(x => x.PricePerKg.HasValue);

        RuleFor(x => x.MinQuantityKg)
            .GreaterThan(0).WithMessage("Khối lượng tối thiểu phải lớn hơn 0.")
            .When(x => x.MinQuantityKg.HasValue);

        RuleFor(x => x.CollectionConditions)
            .MaximumLength(200).WithMessage("Điều kiện thu gom tối đa 200 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.CollectionConditions));
    }
}

public class BulkSavePricesRequestValidator : AbstractValidator<BulkSavePricesRequest>
{
    public BulkSavePricesRequestValidator()
    {
        RuleFor(x => x.Items)
            .NotEmpty().WithMessage("Danh sách giá không được rỗng.");

        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.WasteCategoryId)
                .NotNull().WithMessage("Vui lòng chọn loại phế liệu.")
                .NotEqual(Guid.Empty).WithMessage("Mã loại phế liệu không hợp lệ.");

            item.RuleFor(i => i.PricePerKg)
                .NotNull().WithMessage("Giá thu mua là bắt buộc.")
                .GreaterThan(0).WithMessage("Giá thu mua phải là số nguyên dương (VND/kg).")
                .Must(p => !p.HasValue || p.Value == decimal.Truncate(p.Value))
                .WithMessage("Giá thu mua phải là số nguyên dương (VND/kg).");

            item.RuleFor(i => i.MinQuantityKg)
                .GreaterThan(0).WithMessage("Khối lượng tối thiểu phải lớn hơn 0.");

            item.RuleFor(i => i.CollectionConditions)
                .MaximumLength(200).WithMessage("Điều kiện thu gom tối đa 200 ký tự.")
                .When(i => !string.IsNullOrEmpty(i.CollectionConditions));
        });
    }
}

public class UpdateAvailabilityRequestValidator : AbstractValidator<UpdateAvailabilityRequest>
{
    public UpdateAvailabilityRequestValidator()
    {
        RuleFor(x => x.IsOnline)
            .NotNull().WithMessage("Vui lòng chọn trạng thái Online/Offline.");

        RuleFor(x => x.BusyReason)
            .MaximumLength(100).WithMessage("Ghi chú bận tối đa 100 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.BusyReason));
    }
}

public class UpdateScheduleRequestValidator : AbstractValidator<UpdateScheduleRequest>
{
    public UpdateScheduleRequestValidator()
    {
        RuleFor(x => x.Days)
            .NotEmpty().WithMessage("Vui lòng khai báo ít nhất một ngày trong lịch tuần.")
            .Must(d => d.Count <= 7).WithMessage("Lịch tuần tối đa 7 ngày.");

        RuleForEach(x => x.Days).ChildRules(day =>
        {
            day.RuleFor(d => d.DayOfWeek)
                .InclusiveBetween(0, 6).WithMessage("dayOfWeek phải từ 0 (Chủ nhật) đến 6 (Thứ bảy).");
        });
    }
}

public class NearbyRecyclerFilterValidator : AbstractValidator<NearbyRecyclerFilterRequest>
{
    private static readonly string[] AllowedCategories = { "plastic", "paper", "cardboard", "metal", "glass" };

    public NearbyRecyclerFilterValidator()
    {
        RuleFor(x => x.Lat)
            .InclusiveBetween(-90.0, 90.0)
            .WithMessage("Vĩ độ (Lat) phải nằm trong khoảng từ -90 đến 90.")
            .NotEqual(0)
            .WithMessage("Cần quyền truy cập vị trí để tìm cơ sở thu mua gần bạn. Vui lòng bật trong phần cài đặt (MSG-MTH-02).");

        RuleFor(x => x.Lng)
            .InclusiveBetween(-180.0, 180.0)
            .WithMessage("Kinh độ (Lng) phải nằm trong khoảng từ -180 đến 180.")
            .NotEqual(0)
            .WithMessage("Cần quyền truy cập vị trí để tìm cơ sở thu mua gần bạn. Vui lòng bật trong phần cài đặt (MSG-MTH-02).");

        RuleFor(x => x.RadiusKm)
            .InclusiveBetween(0.5, 50.0)
            .WithMessage("Bán kính tìm kiếm (RadiusKm) phải nằm trong khoảng từ 0.5 km đến 50 km.");

        RuleFor(x => x.Category)
            .Must(c => string.IsNullOrWhiteSpace(c) || AllowedCategories.Contains(c.Trim().ToLowerInvariant()))
            .WithMessage("Danh mục phế liệu lọc chỉ hỗ trợ: plastic, paper, cardboard, metal, glass.");
    }
}

