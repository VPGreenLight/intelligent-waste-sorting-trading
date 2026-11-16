using Ecolink.Application.Dtos;
using Ecolink.Domain.Enums;
using FluentValidation;

namespace Ecolink.Application.Validators;

public class UpsertServicePackageRequestValidator : AbstractValidator<UpsertServicePackageRequest>
{
    private static readonly string[] AllowedTargetRoles = ["User", "Recycler", "All"];

    public UpsertServicePackageRequestValidator()
    {
        RuleFor(x => x.PackageCode)
            .NotEmpty().WithMessage("Mã gói dịch vụ không được để trống.")
            .Matches("^[A-Z0-9_]{3,50}$").WithMessage("Mã gói chỉ gồm chữ in hoa, số và dấu gạch dưới (3–50 ký tự).");

        RuleFor(x => x.PackageName)
            .NotEmpty().WithMessage("Tên gói dịch vụ không được để trống.")
            .MinimumLength(2).WithMessage("Tên gói tối thiểu 2 ký tự.")
            .MaximumLength(150).WithMessage("Tên gói tối đa 150 ký tự.");

        RuleFor(x => x.TargetRole)
            .NotEmpty()
            .Must(r => AllowedTargetRoles.Any(a => a.Equals(r?.Trim(), StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Vai trò áp dụng phải là User, Recycler hoặc All.");

        RuleFor(x => x.PriceVnd)
            .GreaterThanOrEqualTo(2000m)
            .When(x => !string.Equals(x.PackageCode?.Trim(), "BASIC", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Gói dịch vụ trả phí phải có đơn giá tối thiểu 2.000 VNĐ theo quy định cổng PayOS/VietQR.");

        RuleFor(x => x.DurationDays)
            .InclusiveBetween(1, 730)
            .When(x => !string.Equals(x.PackageCode?.Trim(), "BASIC", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Chu kỳ hiệu lực của gói trả phí phải từ 1 đến 730 ngày.");

        RuleFor(x => x.PriceVnd)
            .Equal(0m)
            .When(x => string.Equals(x.PackageCode?.Trim(), "BASIC", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Gói mặc định BASIC bắt buộc có giá 0 VNĐ.");

        RuleFor(x => x.DurationDays)
            .Equal(0)
            .When(x => string.Equals(x.PackageCode?.Trim(), "BASIC", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Gói mặc định BASIC bắt buộc có chu kỳ vĩnh viễn (0 ngày).");
    }
}

public class ResolvePaymentAnomalyRequestValidator : AbstractValidator<ResolvePaymentAnomalyRequest>
{
    public ResolvePaymentAnomalyRequestValidator()
    {
        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Hành động đối soát không được để trống.")
            .Must(a => Enum.TryParse<ReconciliationAction>(a, ignoreCase: true, out _))
            .WithMessage("Hành động đối soát không hợp lệ.");

        RuleFor(x => x.ResolutionNote)
            .NotEmpty().WithMessage("Bắt buộc nhập biên bản lý do đối soát (BR-PAY-06).")
            .MinimumLength(10).WithMessage("Lý do đối soát phải rõ ràng, tối thiểu 10 ký tự.")
            .MaximumLength(500).WithMessage("Lý do đối soát tối đa 500 ký tự.");

        RuleFor(x => x.TargetOrderCode)
            .NotEmpty().WithMessage("Bắt buộc chỉ định mã đơn hàng cần khớp lệnh (targetOrderCode).")
            .When(x => Enum.TryParse<ReconciliationAction>(x.Action, true, out var act) && act == ReconciliationAction.ManualMatchOrder);

        RuleFor(x => x.ResolutionBankRef)
            .NotEmpty().WithMessage("Bắt buộc nhập mã tham chiếu giao dịch hoàn tiền của ngân hàng.")
            .When(x => Enum.TryParse<ReconciliationAction>(x.Action, true, out var act)
                       && (act == ReconciliationAction.MarkRefunded || act == ReconciliationAction.MarkExcessRefunded));
    }
}
