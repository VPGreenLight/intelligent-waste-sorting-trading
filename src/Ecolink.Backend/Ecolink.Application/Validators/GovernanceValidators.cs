using Ecolink.Application.Dtos;
using Ecolink.Domain.Constants;
using FluentValidation;

namespace Ecolink.Application.Validators;

public class LockOrUnlockAccountValidator : AbstractValidator<LockOrUnlockAccountRequest>
{
    public LockOrUnlockAccountValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Vui lòng nhập lý do khóa hoặc mở khóa tài khoản (BR-ADM-03).")
            .MinimumLength(10).WithMessage("Lý do kiểm duyệt phải rõ ràng, tối thiểu 10 ký tự (BR-ADM-03).")
            .MaximumLength(500).WithMessage("Lý do kiểm duyệt không vượt quá 500 ký tự.");
    }
}

public class DeactivateAccountValidator : AbstractValidator<DeactivateAccountRequest>
{
    public DeactivateAccountValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Bắt buộc phải nhập biên bản lý do vô hiệu hóa tài khoản (BR-ADM-03, BR-ADM-04).")
            .MinimumLength(10).WithMessage("Lý do vô hiệu hóa phải tối thiểu 10 ký tự.")
            .MaximumLength(500).WithMessage("Lý do vô hiệu hóa tối đa 500 ký tự.");
    }
}

public class CreateStaffAccountValidator : AbstractValidator<CreateStaffAccountRequest>
{
    public CreateStaffAccountValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên nhân viên không được để trống.")
            .MinimumLength(3).WithMessage("Họ và tên nhân viên tối thiểu 3 ký tự.")
            .MaximumLength(100).WithMessage("Họ và tên nhân viên tối đa 100 ký tự.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email công việc không được để trống.")
            .EmailAddress().WithMessage("Email công việc không đúng định dạng.")
            .MaximumLength(255);

        RuleFor(x => x.Username)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.Username));

        RuleFor(x => x.InitialPassword)
            .MinimumLength(8).WithMessage("Mật khẩu khởi tạo phải có ít nhất 8 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.InitialPassword));

        RuleFor(x => x.InitialPermissions)
            .Must(perms => perms == null || perms.All(StaffPermissions.IsValid))
            .WithMessage("Danh sách quyền khởi tạo chứa mã quyền không hợp lệ.");
    }
}

public class UpdateStaffAccountValidator : AbstractValidator<UpdateStaffAccountRequest>
{
    public UpdateStaffAccountValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ và tên nhân viên không được để trống.")
            .MinimumLength(3).WithMessage("Họ và tên nhân viên tối thiểu 3 ký tự.")
            .MaximumLength(100);

        RuleFor(x => x.ResetTemporaryPassword)
            .MinimumLength(8).WithMessage("Mật khẩu tạm cấp lại phải có tối thiểu 8 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.ResetTemporaryPassword));
    }
}

public class UpdateStaffStatusValidator : AbstractValidator<UpdateStaffStatusRequest>
{
    private static readonly string[] AllowedStatuses = ["Active", "Suspended", "Deactivated"];

    public UpdateStaffStatusValidator()
    {
        RuleFor(x => x.Status)
            .NotEmpty().WithMessage("Trạng thái tài khoản nhân viên không được để trống.")
            .Must(s => AllowedStatuses.Any(a => a.Equals(s?.Trim(), StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Trạng thái hợp lệ gồm: Active, Suspended, Deactivated.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Bắt buộc ghi rõ lý do thay đổi trạng thái nhân sự (BR-ADM-03).")
            .MinimumLength(10).WithMessage("Lý do phải rõ ràng, tối thiểu 10 ký tự.")
            .MaximumLength(500);
    }
}

public class UpdateStaffPermissionsValidator : AbstractValidator<UpdateStaffPermissionsRequest>
{
    public UpdateStaffPermissionsValidator()
    {
        RuleFor(x => x.Permissions)
            .NotNull().WithMessage("Danh sách quyền không được null.")
            .Must(perms => perms.All(StaffPermissions.IsValid))
            .WithMessage("Tồn tại mã quyền không nằm trong danh mục chuẩn RBAC của hệ thống (BR-ADM-02).");

        RuleFor(x => x.Reason)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}

public class StaffCheckInValidator : AbstractValidator<StaffCheckInRequest>
{
    public StaffCheckInValidator()
    {
        RuleFor(x => x.ShiftNote)
            .MaximumLength(500).WithMessage("Ghi chú ca trực tối đa 500 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.ShiftNote));
    }
}

public class StaffCheckOutValidator : AbstractValidator<StaffCheckOutRequest>
{
    public StaffCheckOutValidator()
    {
        RuleFor(x => x.ShiftNote)
            .MaximumLength(500).WithMessage("Ghi chú bàn giao ca tối đa 500 ký tự.")
            .When(x => !string.IsNullOrWhiteSpace(x.ShiftNote));
    }
}
