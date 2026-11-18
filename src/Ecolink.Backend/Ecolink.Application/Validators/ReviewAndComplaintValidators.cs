using Ecolink.Application.Dtos;
using Ecolink.Domain.Enums;
using FluentValidation;

namespace Ecolink.Application.Validators;

public class CreateReviewRequestValidator : AbstractValidator<CreateReviewRequest>
{
    public CreateReviewRequestValidator()
    {
        RuleFor(x => x.RatingStars)
            .InclusiveBetween(1, 5)
            .WithMessage("Số sao đánh giá phải nằm trong khoảng từ 1 đến 5 sao.");

        RuleFor(x => x.Comment)
            .MaximumLength(1000)
            .WithMessage("Nhận xét đánh giá tối đa 1000 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Comment));
    }
}

public class ReportReviewRequestValidator : AbstractValidator<ReportReviewRequest>
{
    public ReportReviewRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do báo cáo đánh giá vi phạm.")
            .Must(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length >= 5 && r.Trim().Length <= 500)
            .WithMessage("Lý do báo cáo đánh giá phải dài từ 5 đến 500 ký tự.");
    }
}

public class ModerateReviewRequestValidator : AbstractValidator<ModerateReviewRequest>
{
    private static readonly string[] AllowedActions = { "Hide", "Restore" };

    public ModerateReviewRequestValidator()
    {
        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Vui lòng chọn hành động kiểm duyệt (Hide hoặc Restore).")
            .Must(a => !string.IsNullOrWhiteSpace(a) && AllowedActions.Contains(a.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Hành động kiểm duyệt chỉ hỗ trợ: Hide hoặc Restore.");

        RuleFor(x => x.ModerationReason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do kiểm duyệt đánh giá.")
            .Must(r => !string.IsNullOrWhiteSpace(r) && r.Trim().Length >= 5 && r.Trim().Length <= 500)
            .WithMessage("Lý do kiểm duyệt phải dài từ 5 đến 500 ký tự.");
    }
}

public class SubmitComplaintRequestValidator : AbstractValidator<SubmitComplaintRequest>
{
    public SubmitComplaintRequestValidator()
    {
        RuleFor(x => x.HandoverRequestId)
            .NotEqual(Guid.Empty)
            .WithMessage("Vui lòng chọn yêu cầu bàn giao cần khiếu nại.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Vui lòng chọn nhóm nguyên nhân khiếu nại.")
            .Must(c => !string.IsNullOrWhiteSpace(c) && Enum.TryParse<ComplaintCategory>(c.Trim(), ignoreCase: true, out _))
            .WithMessage("Nhóm khiếu nại chỉ hỗ trợ: ScaleDiscrepancy, PaymentIssue, NoShow, Misconduct, Other.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Vui lòng mô tả chi tiết nội dung khiếu nại.")
            .Must(d => !string.IsNullOrWhiteSpace(d) && d.Trim().Length >= 15 && d.Trim().Length <= 2000)
            .WithMessage("Nội dung mô tả khiếu nại phải dài từ 15 đến 2000 ký tự.");

        RuleFor(x => x.EvidencePhotoUrls)
            .Must(urls => urls == null || urls.Count <= 5)
            .WithMessage("Tối đa chỉ được đính kèm 5 ảnh bằng chứng (BR-DSP-02).");

        RuleForEach(x => x.EvidencePhotoUrls)
            .Must(IsValidHttpUrl)
            .WithMessage("Mỗi URL ảnh bằng chứng phải là đường dẫn HTTP/HTTPS hợp lệ.")
            .When(x => x.EvidencePhotoUrls is { Count: > 0 });
    }

    private static bool IsValidHttpUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var trimmed = url.Trim();
        return trimmed.Length <= 1000 &&
               Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) &&
               (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

public class ResolveComplaintRequestValidator : AbstractValidator<ResolveComplaintRequest>
{
    public ResolveComplaintRequestValidator()
    {
        RuleFor(x => x.ResolutionOutcome)
            .NotEmpty().WithMessage("Vui lòng chọn kết quả phán quyết trọng tài.")
            .Must(o => !string.IsNullOrWhiteSpace(o) && Enum.TryParse<ComplaintResolutionOutcome>(o.Trim(), ignoreCase: true, out _))
            .WithMessage("Phán quyết chỉ hỗ trợ: ConfirmReceiptAsIs, CompensateUser, UpholdComplainant, CancelDisputedOrder, Dismissed.");

        RuleFor(x => x.ResolutionNote)
            .NotEmpty().WithMessage("Biên bản giải trình phán quyết là bắt buộc (BR-DSP-04).")
            .Must(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length >= 10 && n.Trim().Length <= 2000)
            .WithMessage("Biên bản giải trình phán quyết phải dài từ 10 đến 2000 ký tự (BR-DSP-04).");

        RuleFor(x => x.WarningReason)
            .MaximumLength(500)
            .WithMessage("Lý do cảnh cáo tài khoản tối đa 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.WarningReason));
    }
}
