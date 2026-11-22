using Ecolink.Application.Dtos;
using FluentValidation;

namespace Ecolink.Application.Validators;

/// <summary>Kiểm tra đầu vào Blog Content Management (M-14: S-08, A-09) và Gamification (M-10: U-14, S-09, S-10, A-07).</summary>
public class SaveBlogPostRequestValidator : AbstractValidator<SaveBlogPostRequest>
{
    public SaveBlogPostRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Vui lòng nhập tiêu đề bài viết.")
            .Must(t => !string.IsNullOrWhiteSpace(t) && t.Trim().Length is >= 10 and <= 150)
            .WithMessage("Tiêu đề phải từ 10 đến 150 ký tự.");

        RuleFor(x => x.Summary)
            .MaximumLength(500).WithMessage("Tóm tắt tối đa 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Summary));

        RuleFor(x => x.Content)
            .MaximumLength(200000).WithMessage("Nội dung quá dài.")
            .When(x => !string.IsNullOrEmpty(x.Content));

        RuleFor(x => x.CoverImageUrl)
            .MaximumLength(1000).WithMessage("URL ảnh bìa quá dài.")
            .When(x => !string.IsNullOrEmpty(x.CoverImageUrl));

        RuleFor(x => x.Tags)
            .Must(tags => tags == null || tags.Count <= 5)
            .WithMessage("Tối đa 5 thẻ phân loại.");

        When(x => x.SubmitForApproval, () =>
        {
            RuleFor(x => x.CoverImageUrl)
                .Must(u => !string.IsNullOrWhiteSpace(u))
                .WithMessage("Vui lòng tải lên ảnh bìa trước khi nộp duyệt.");

            RuleFor(x => x.Content)
                .Must(c => !string.IsNullOrWhiteSpace(c))
                .WithMessage("Vui lòng nhập nội dung bài viết trước khi nộp duyệt.");
        });
    }
}

public class RejectBlogPostRequestValidator : AbstractValidator<RejectBlogPostRequest>
{
    public RejectBlogPostRequestValidator()
    {
        RuleFor(x => x.Feedback)
            .Must(f => !string.IsNullOrWhiteSpace(f))
            .WithMessage("Vui lòng nhập nội dung góp ý chỉnh sửa.")
            .MaximumLength(2000).WithMessage("Góp ý tối đa 2000 ký tự.");
    }
}

public class ReviewBlogPostRequestValidator : AbstractValidator<ReviewBlogPostRequest>
{
    private static readonly string[] ValidActions = { "approve", "approved", "publish", "published", "reject", "rejected" };

    public ReviewBlogPostRequestValidator()
    {
        RuleFor(x => x)
            .Must(r =>
            {
                var act = (r.Action ?? string.Empty).Trim().ToLowerInvariant();
                return (r.Approve.HasValue && act.Length == 0) || ValidActions.Contains(act);
            })
            .WithMessage("Quyết định duyệt không hợp lệ (Approve hoặc Reject).");

        RuleFor(x => x.Feedback)
            .NotEmpty().WithMessage("Vui lòng nhập góp ý chỉnh sửa khi từ chối bài viết.")
            .MaximumLength(2000).WithMessage("Góp ý tối đa 2000 ký tự.")
            .When(x => x.Approve == false
                       || string.Equals(x.Action?.Trim(), "reject", StringComparison.OrdinalIgnoreCase)
                       || string.Equals(x.Action?.Trim(), "rejected", StringComparison.OrdinalIgnoreCase));
    }
}

public class SaveGiftRequestValidator : AbstractValidator<SaveGiftRequest>
{
    private static readonly string[] ValidTypes = { "Physical", "Voucher" };

    public SaveGiftRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length <= 150)
            .WithMessage("Tên quà bắt buộc nhập và tối đa 150 ký tự.");

        RuleFor(x => x.Type)
            .Must(t => !string.IsNullOrWhiteSpace(t) && ValidTypes.Contains(t.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Loại quà phải là Physical hoặc Voucher.");

        RuleFor(x => x.PointsRequired)
            .GreaterThanOrEqualTo(0).WithMessage("Điểm quy đổi phải là số không âm.");

        RuleFor(x => x.StockQuantity)
            .InclusiveBetween(0, 1000000).WithMessage("Số lượng tồn kho phải từ 0 đến 1.000.000.");
    }
}

public class RedeemGiftRequestValidator : AbstractValidator<RedeemGiftRequest>
{
    public RedeemGiftRequestValidator()
    {
        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, 10).WithMessage("Số lượng đổi phải từ 1 đến 10.");

        RuleFor(x => x.DeliveryAddress)
            .MaximumLength(500).WithMessage("Địa chỉ giao hàng tối đa 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.DeliveryAddress));
    }
}

public class CreateRedemptionRequestValidator : AbstractValidator<CreateRedemptionRequest>
{
    public CreateRedemptionRequestValidator()
    {
        Include(new RedeemGiftRequestValidator());

        RuleFor(x => x.GiftId)
            .NotEmpty().WithMessage("Vui lòng chọn quà tặng muốn đổi.");
    }
}

public class SaveEcoFactRequestValidator : AbstractValidator<SaveEcoFactRequest>
{
    public SaveEcoFactRequestValidator()
    {
        RuleFor(x => x.Title)
            .Must(t => !string.IsNullOrWhiteSpace(t) && t.Trim().Length <= 200)
            .WithMessage("Tiêu đề Eco Fact bắt buộc và tối đa 200 ký tự.");

        RuleFor(x => x.Content)
            .Must(c => !string.IsNullOrWhiteSpace(c) && c.Trim().Length <= 300)
            .WithMessage("Nội dung Eco Fact bắt buộc và tối đa 300 ký tự.");

        RuleFor(x => x.UnlockTreeLevel)
            .GreaterThanOrEqualTo(1).WithMessage("Cấp cây mở khóa phải từ 1 trở lên.");
    }
}

public class GamificationConfigDtoValidator : AbstractValidator<GamificationConfigDto>
{
    public GamificationConfigDtoValidator()
    {
        RuleFor(x => x.DailyNonTransactionCap).GreaterThanOrEqualTo(0);
        RuleFor(x => x.CheckinBasePoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StreakBonusIncrement).GreaterThanOrEqualTo(0);
        RuleFor(x => x.StreakBonusCap).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DefaultPointsPerKg).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DailyTreeCareLimit).InclusiveBetween(1, 100);
        RuleFor(x => x.VoucherValidityDays).InclusiveBetween(1, 3650);

        RuleFor(x => x.TreeLevels)
            .NotEmpty().WithMessage("Bảng cấp cây không được để trống.")
            .Must(levels =>
            {
                if (levels == null || levels.Count is 0 or > 10) return false;
                var ordered = levels.OrderBy(l => l.Level).ToList();
                for (var i = 0; i < ordered.Count; i++)
                {
                    if (ordered[i].Level != i + 1) return false;
                    if (string.IsNullOrWhiteSpace(ordered[i].Title) || ordered[i].Title.Trim().Length > 100) return false;
                    if (ordered[i].RequiredPoints < 0) return false;
                    if (i == 0 && ordered[i].RequiredPoints != 0) return false;
                    if (i > 0 && ordered[i].RequiredPoints <= ordered[i - 1].RequiredPoints) return false;
                }
                return true;
            })
            .WithMessage("Các cấp cây phải liên tục từ 1 (ngưỡng 0 điểm) và tăng dần nghiêm ngặt.");
    }
}

public class SaveLeaderboardCycleRequestValidator : AbstractValidator<SaveLeaderboardCycleRequest>
{
    private static readonly string[] AllowedPeriods = { "Weekly", "Monthly", "Quarterly", "week", "month", "quarter" };

    public SaveLeaderboardCycleRequestValidator()
    {
        RuleFor(x => x.Name)
            .Must(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length <= 150)
            .WithMessage("Tên chu kỳ bắt buộc nhập và tối đa 150 ký tự.");

        RuleFor(x => x.PeriodType)
            .Must(p => !string.IsNullOrWhiteSpace(p) && AllowedPeriods.Contains(p.Trim(), StringComparer.OrdinalIgnoreCase))
            .WithMessage("Chu kỳ phải là Weekly, Monthly hoặc Quarterly.");

        RuleFor(x => x.EndsAt)
            .GreaterThan(x => x.StartsAt)
            .WithMessage("Thời gian kết thúc chu kỳ phải lớn hơn thời gian bắt đầu.");

        RuleFor(x => x.Top1BonusPoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Top2BonusPoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Top3BonusPoints).GreaterThanOrEqualTo(0);
        RuleFor(x => x)
            .Must(x => x.Top1BonusPoints >= x.Top2BonusPoints && x.Top2BonusPoints >= x.Top3BonusPoints)
            .WithMessage("Điểm thưởng Top 1/2/3 phải không âm và giảm dần theo thứ hạng.");
    }
}
