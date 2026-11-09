using Ecolink.Application.Dtos;
using FluentValidation;

namespace Ecolink.Application.Validators;

/// <summary>
/// Kiểm tra hợp lệ cho Request gửi tin nhắn văn bản hoặc hình ảnh (U-08 &amp; R-05).
/// </summary>
public class SendChatMessageRequestValidator : AbstractValidator<SendChatMessageRequest>
{
    private static readonly string[] AllowedMessageTypes = { "text", "image" };

    public SendChatMessageRequestValidator()
    {
        RuleFor(x => x.MessageType)
            .NotEmpty().WithMessage("Loại tin nhắn không được để trống.")
            .Must(t => AllowedMessageTypes.Contains(t.Trim().ToLowerInvariant()))
            .WithMessage("Loại tin nhắn thông thường chỉ chấp nhận 'Text' hoặc 'Image'. Để gửi đề xuất giá/khung giờ, vui lòng gọi endpoint '/proposals'.");

        When(x => string.Equals(x.MessageType?.Trim(), "Text", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.MessageContent)
                .NotEmpty().WithMessage("Nội dung tin nhắn văn bản không được để trống.")
                .MaximumLength(2000).WithMessage("Nội dung tin nhắn không được vượt quá 2000 ký tự.");
        });

        When(x => string.Equals(x.MessageType?.Trim(), "Image", StringComparison.OrdinalIgnoreCase), () =>
        {
            RuleFor(x => x.AttachmentUrl)
                .NotEmpty().WithMessage("Đường dẫn ảnh đính kèm (AttachmentUrl) là bắt buộc đối với tin nhắn hình ảnh.")
                .MaximumLength(1000).WithMessage("Đường dẫn ảnh đính kèm không được vượt quá 1000 ký tự.");

            RuleFor(x => x.MessageContent)
                .MaximumLength(500).WithMessage("Chú thích ảnh không được vượt quá 500 ký tự.")
                .When(x => !string.IsNullOrWhiteSpace(x.MessageContent));
        });
    }
}

/// <summary>
/// Kiểm tra hợp lệ cho Request gửi bản đề xuất / phản đề xuất Giá, Khối lượng và Khung giờ thu gom (BR-NEG-02, BR-NEG-03, BR-NEG-04).
/// </summary>
public class SendProposalRequestValidator : AbstractValidator<SendProposalRequest>
{
    public SendProposalRequestValidator()
    {
        RuleFor(x => x.ProposedPricePerKg)
            .GreaterThan(0m).WithMessage("Đơn giá đề xuất (VNĐ/kg) phải lớn hơn 0.")
            .LessThanOrEqualTo(10_000_000m).WithMessage("Đơn giá đề xuất không được vượt quá 10.000.000 VNĐ/kg.");

        RuleFor(x => x.ProposedWeightKg)
            .GreaterThan(0m).WithMessage("Khối lượng ước tính đề xuất (kg) phải lớn hơn 0.")
            .LessThanOrEqualTo(100_000m).WithMessage("Khối lượng ước tính không được vượt quá 100.000 kg.");

        RuleFor(x => x.ProposedTimeSlot)
            .NotEmpty().WithMessage("Khung giờ thu gom đề xuất không được để trống (BR-NEG-04).")
            .MaximumLength(150).WithMessage("Khung giờ thu gom đề xuất không được vượt quá 150 ký tự.");

        RuleFor(x => x.Note)
            .MaximumLength(1000).WithMessage("Ghi chú đề xuất không được vượt quá 1000 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.Note));
    }
}

/// <summary>
/// Kiểm tra hợp lệ cho Request phản hồi đề xuất (Chấp nhận / Từ chối).
/// </summary>
public class RespondProposalRequestValidator : AbstractValidator<RespondProposalRequest>
{
    public RespondProposalRequestValidator()
    {
        RuleFor(x => x.ResponseNote)
            .MaximumLength(500).WithMessage("Ghi chú phản hồi đề xuất không được vượt quá 500 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.ResponseNote));
    }
}

