using Ecolink.Application.Dtos;
using FluentValidation;

namespace Ecolink.Application.Validators;

public class CreateWasteCategoryValidator : AbstractValidator<CreateWasteCategoryRequest>
{
    public CreateWasteCategoryValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã danh mục rác không được để trống.")
            .MaximumLength(50).WithMessage("Mã danh mục tối đa 50 ký tự.")
            .Matches("^[a-z0-9_]+$").WithMessage("Mã danh mục chỉ được gồm chữ thường không dấu, chữ số và dấu gạch dưới (_).");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục rác không được để trống.")
            .MaximumLength(100).WithMessage("Tên danh mục tối đa 100 ký tự.");

        RuleFor(x => x.StandardBinColor)
            .NotEmpty().WithMessage("Màu thùng chuẩn không được để trống.")
            .MaximumLength(50).WithMessage("Màu thùng chuẩn tối đa 50 ký tự.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Mô tả tối đa 500 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.IconUrl)
            .MaximumLength(500).WithMessage("Đường dẫn biểu tượng tối đa 500 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.IconUrl));
    }
}

public class UpdateWasteCategoryValidator : AbstractValidator<UpdateWasteCategoryRequest>
{
    public UpdateWasteCategoryValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tên danh mục rác không được để trống.")
            .MaximumLength(100).WithMessage("Tên danh mục tối đa 100 ký tự.");

        RuleFor(x => x.StandardBinColor)
            .NotEmpty().WithMessage("Màu thùng chuẩn không được để trống.")
            .MaximumLength(50).WithMessage("Màu thùng chuẩn tối đa 50 ký tự.");

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Mô tả tối đa 500 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.Description));

        RuleFor(x => x.IconUrl)
            .MaximumLength(500).WithMessage("Đường dẫn biểu tượng tối đa 500 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.IconUrl));
    }
}

public class ToggleWasteCategoryStatusValidator : AbstractValidator<ToggleWasteCategoryStatusRequest>
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Active",
        "Inactive"
    };

    public ToggleWasteCategoryStatusValidator()
    {
        RuleFor(x => x.Status)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Trạng thái danh mục không được để trống.")
            .Must(s => !string.IsNullOrWhiteSpace(s) && AllowedStatuses.Contains(s.Trim()))
            .WithMessage("Trạng thái danh mục chỉ chấp nhận 'Active' hoặc 'Inactive'.");
    }
}

public class UpsertSortingRuleValidator : AbstractValidator<UpsertSortingRuleRequest>
{
    public UpsertSortingRuleValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề quy tắc phân loại không được để trống.")
            .MaximumLength(150).WithMessage("Tiêu đề quy tắc tối đa 150 ký tự.");

        RuleFor(x => x.PreparationSteps)
            .NotEmpty().WithMessage("Các bước sơ chế sạch không được để trống.")
            .MaximumLength(2000).WithMessage("Nội dung sơ chế tối đa 2000 ký tự.");

        RuleFor(x => x.SortingInstruction)
            .NotEmpty().WithMessage("Hướng dẫn phân loại chi tiết không được để trống.")
            .MaximumLength(2000).WithMessage("Hướng dẫn phân loại tối đa 2000 ký tự.");

        RuleFor(x => x.HandlingAction)
            .NotEmpty().WithMessage("Hành động xử lý đề xuất không được để trống.")
            .MaximumLength(1000).WithMessage("Hành động xử lý tối đa 1000 ký tự.");

        RuleFor(x => x.HazardWarning)
            .MaximumLength(1000).WithMessage("Cảnh báo an toàn tối đa 1000 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.HazardWarning));

        RuleFor(x => x.ApplicableRegion)
            .MaximumLength(100).WithMessage("Khu vực áp dụng tối đa 100 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.ApplicableRegion));
    }
}
