using System.Text.RegularExpressions;
using Ecolink.Application.Dtos;
using Ecolink.Domain.Enums;
using FluentValidation;

namespace Ecolink.Application.Validators;

public sealed class SubmitFeedbackDtoValidator : AbstractValidator<SubmitFeedbackDto>
{
    public SubmitFeedbackDtoValidator()
    {
        RuleFor(x => x.UserReportedLabel)
            .NotEmpty().WithMessage("Vui lòng chọn nhãn loại rác đúng.")
            .MaximumLength(50).WithMessage("Tên nhãn đề xuất không được vượt quá 50 ký tự.");

        RuleFor(x => x.ConfidenceScore)
            .InclusiveBetween(0d, 1d).When(x => x.ConfidenceScore != 0d)
            .WithMessage("Điểm tin cậy phải nằm trong khoảng [0, 1].");

        RuleFor(x => x.Note)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Note))
            .WithMessage("Ghi chú phản hồi không được vượt quá 500 ký tự.");
    }
}

public sealed class YoloBoundingBoxDtoValidator : AbstractValidator<YoloBoundingBoxDto>
{
    public YoloBoundingBoxDtoValidator()
    {
        RuleFor(b => b.CategoryCode)
            .NotEmpty().WithMessage("Mã danh mục trong hộp bao YOLO không được để trống.")
            .MaximumLength(50).WithMessage("Mã danh mục trong hộp bao YOLO không được vượt quá 50 ký tự.");

        RuleFor(b => b.XCenter)
            .InclusiveBetween(0d, 1d).WithMessage("Tọa độ xCenter phải nằm trong [0.0, 1.0].");

        RuleFor(b => b.YCenter)
            .InclusiveBetween(0d, 1d).WithMessage("Tọa độ yCenter phải nằm trong [0.0, 1.0].");

        RuleFor(b => b.Width)
            .GreaterThan(0d).WithMessage("Chiều rộng hộp bao width phải lớn hơn 0.")
            .LessThanOrEqualTo(1d).WithMessage("Chiều rộng hộp bao width không được vượt quá 1.0.");

        RuleFor(b => b.Height)
            .GreaterThan(0d).WithMessage("Chiều cao hộp bao height phải lớn hơn 0.")
            .LessThanOrEqualTo(1d).WithMessage("Chiều cao hộp bao height không được vượt quá 1.0.");
    }
}

public sealed class ReviewAiFeedbackRequestValidator : AbstractValidator<ReviewAiFeedbackRequest>
{
    public ReviewAiFeedbackRequestValidator()
    {
        RuleFor(x => x.Decision)
            .NotEmpty().WithMessage("Quyết định thẩm định không được để trống.")
            .Must(d => Enum.TryParse<AiFeedbackReviewDecision>(d?.Trim(), true, out _))
            .WithMessage("Quyết định thẩm định chỉ hỗ trợ: ApproveAndLabel, ApproveToQueue, Reject.");

        RuleFor(x => x.VerifiedLabel)
            .NotEmpty()
            .When(x => string.Equals(x.Decision?.Trim(), nameof(AiFeedbackReviewDecision.ApproveAndLabel), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Phải chỉ định nhãn chuẩn verifiedLabel khi chọn ApproveAndLabel.");

        RuleFor(x => x.ReviewNote)
            .Must(n => !string.IsNullOrWhiteSpace(n) && n.Trim().Length >= 5)
            .When(x => string.Equals(x.Decision?.Trim(), nameof(AiFeedbackReviewDecision.Reject), StringComparison.OrdinalIgnoreCase))
            .WithMessage("Khi bác bỏ ảnh (Reject), bắt buộc nhập lý do tối thiểu 5 ký tự (BR-CLS-06).");

        RuleForEach(x => x.YoloAnnotations!)
            .SetValidator(new YoloBoundingBoxDtoValidator())
            .When(x => x.YoloAnnotations is { Count: > 0 });
    }
}

public sealed class AssignGroundTruthLabelRequestValidator : AbstractValidator<AssignGroundTruthLabelRequest>
{
    public AssignGroundTruthLabelRequestValidator()
    {
        RuleFor(x => x.GroundTruthLabel)
            .NotEmpty()
            .When(x => !x.ExcludeFromDataset)
            .WithMessage("Phải chọn nhãn chuẩn groundTruthLabel khi xác nhận mẫu hợp lệ.");

        RuleFor(x => x.QualityNote)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.QualityNote))
            .WithMessage("Ghi chú chất lượng không được vượt quá 500 ký tự.");

        RuleForEach(x => x.YoloAnnotations!)
            .SetValidator(new YoloBoundingBoxDtoValidator())
            .When(x => x.YoloAnnotations is { Count: > 0 });
    }
}

public sealed class BatchAssignLabelsRequestValidator : AbstractValidator<BatchAssignLabelsRequest>
{
    public BatchAssignLabelsRequestValidator()
    {
        RuleFor(x => x.ItemIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách itemIds không được để trống.")
            .Must(ids => ids != null && ids.Count is >= 1 and <= 200)
            .WithMessage("Số lượng ảnh gán nhãn hàng loạt phải từ 1 đến 200 mục.");

        RuleFor(x => x.GroundTruthLabel)
            .NotEmpty().WithMessage("Phải chỉ định nhãn chuẩn groundTruthLabel cho lô gán nhãn.");
    }
}

public sealed class CreateDatasetVersionRequestValidator : AbstractValidator<CreateDatasetVersionRequest>
{
    private static readonly Regex VersionRegex = new(@"^v\d+\.\d+\.\d+$", RegexOptions.Compiled);

    public CreateDatasetVersionRequestValidator()
    {
        RuleFor(x => x.VersionCode)
            .NotEmpty().WithMessage("Mã phiên bản Dataset không được để trống.")
            .Must(code => code != null && VersionRegex.IsMatch(code.Trim()))
            .WithMessage("Mã phiên bản phải theo định dạng Semantic Versioning, ví dụ: v1.0.0.");

        RuleFor(x => x.VersionName)
            .NotEmpty().WithMessage("Tên phiên bản Dataset không được để trống.")
            .MaximumLength(150).WithMessage("Tên phiên bản không được vượt quá 150 ký tự.");

        RuleFor(x => x.TrainRatio)
            .GreaterThan(0).WithMessage("Tỷ lệ Train phải lớn hơn 0%.");

        RuleFor(x => x.ValRatio)
            .GreaterThanOrEqualTo(0).WithMessage("Tỷ lệ Validation phải >= 0%.");

        RuleFor(x => x.TestRatio)
            .GreaterThanOrEqualTo(0).WithMessage("Tỷ lệ Test phải >= 0%.");

        RuleFor(x => x)
            .Must(x => x.TrainRatio + x.ValRatio + x.TestRatio == 100)
            .WithMessage("Tổng tỷ lệ Train + Validation + Test bắt buộc bằng đúng 100%.");
    }
}
