using Ecolink.Application.Dtos;
using Ecolink.Application.Validators;
using Ecolink.Domain.Constants;
using FluentAssertions;
using Xunit;
using static Ecolink.UnitTests.Validators.ValidatorAssert;

namespace Ecolink.UnitTests.Validators;

[Trait("Layer", "Unit")]
[Trait("Area", "Validators")]
public class GovernanceChatValidatorTests
{
    // ═════════ Governance: lý do (BR-ADM-03) ═════════
    [Theory]
    [InlineData(0, false)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void LockOrUnlock_Reason_Length10To500(int len, bool valid)
        => IsValid(new LockOrUnlockAccountValidator(), new LockOrUnlockAccountRequest { IsLocked = true, Reason = Str(len) }).Should().Be(valid);

    [Theory]
    [InlineData(0, false)]
    [InlineData(9, false)]
    [InlineData(10, true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Deactivate_Reason_Length10To500(int len, bool valid)
        => IsValid(new DeactivateAccountValidator(), new DeactivateAccountRequest { Reason = Str(len) }).Should().Be(valid);

    private readonly UpdateStaffStatusValidator _status = new();

    [Theory]
    [InlineData("Active", true)]
    [InlineData("suspended", true)]
    [InlineData(" Deactivated ", true)]
    [InlineData("Locked", false)]
    [InlineData("", false)]
    public void StaffStatus_Status_Rules(string status, bool valid)
    {
        var m = new UpdateStaffStatusRequest { Status = status, Reason = "Ly do du dai" };
        Has(_status, m, nameof(m.Status)).Should().Be(!valid);
    }

    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    [InlineData(500, false)]
    [InlineData(501, true)]
    public void StaffStatus_Reason_Boundary(int len, bool expectError)
    {
        var m = new UpdateStaffStatusRequest { Status = "Active", Reason = Str(len) };
        Has(_status, m, nameof(m.Reason)).Should().Be(expectError);
    }

    // ───── CreateStaffAccountValidator ─────
    private readonly CreateStaffAccountValidator _create = new();
    private static CreateStaffAccountRequest NewStaff() => new() { FullName = "Nguyen Van A", Email = "staff@ecolink.vn" };

    [Fact] public void CreateStaff_Valid_Passes() => IsValid(_create, NewStaff()).Should().BeTrue();

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void CreateStaff_FullName_Length3To100(int len, bool expectError)
    {
        var m = NewStaff(); m.FullName = Str(len);
        Has(_create, m, nameof(m.FullName)).Should().Be(expectError);
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("abc", true)]
    [InlineData("a@b.vn", false)]
    public void CreateStaff_Email_Rules(string email, bool expectError)
    {
        var m = NewStaff(); m.Email = email;
        Has(_create, m, nameof(m.Email)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("1234567", true)]
    [InlineData("12345678", false)]
    public void CreateStaff_InitialPassword_Min8WhenProvided(string? pw, bool expectError)
    {
        var m = NewStaff(); m.InitialPassword = pw;
        Has(_create, m, nameof(m.InitialPassword)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(51, true)]
    public void CreateStaff_Username_MaxLength(int len, bool expectError)
    {
        var m = NewStaff(); m.Username = Str(len);
        Has(_create, m, nameof(m.Username)).Should().Be(expectError);
    }

    [Fact]
    public void CreateStaff_InitialPermissions_ValidCodesAndAliases_Pass()
    {
        var m = NewStaff();
        m.InitialPermissions = new List<string> { StaffPermissions.ReviewAi, "blog.write", "gift.manage" };
        IsValid(_create, m).Should().BeTrue();
    }

    [Fact]
    public void CreateStaff_InitialPermissions_UnknownCode_Fails()
    {
        var m = NewStaff();
        m.InitialPermissions = new List<string> { StaffPermissions.ReviewAi, "HACK_EVERYTHING" };
        Has(_create, m, nameof(m.InitialPermissions)).Should().BeTrue();
    }

    [Fact]
    public void CreateStaff_NullPermissions_IsAllowed()
    {
        var m = NewStaff(); m.InitialPermissions = null;
        IsValid(_create, m).Should().BeTrue();
    }

    // ───── UpdateStaffAccountValidator ─────
    private readonly UpdateStaffAccountValidator _upd = new();

    [Theory]
    [InlineData(2, true)]
    [InlineData(3, false)]
    [InlineData(100, false)]
    [InlineData(101, true)]
    public void UpdateStaff_FullName_Length(int len, bool expectError)
    {
        var m = new UpdateStaffAccountRequest { FullName = Str(len) };
        Has(_upd, m, nameof(m.FullName)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("   ", false)]
    [InlineData("1234567", true)]
    [InlineData("12345678", false)]
    public void UpdateStaff_ResetPassword_Min8WhenProvided(string? pw, bool expectError)
    {
        var m = new UpdateStaffAccountRequest { FullName = "Nguyen A", ResetTemporaryPassword = pw };
        Has(_upd, m, nameof(m.ResetTemporaryPassword)).Should().Be(expectError);
    }

    // ───── UpdateStaffPermissionsValidator ─────
    private readonly UpdateStaffPermissionsValidator _perm = new();

    [Fact]
    public void UpdatePermissions_AllValid_Passes()
        => IsValid(_perm, new UpdateStaffPermissionsRequest { Permissions = StaffPermissions.AllCodes.ToList() }).Should().BeTrue();

    [Fact]
    public void UpdatePermissions_Empty_IsAllowed()
        => IsValid(_perm, new UpdateStaffPermissionsRequest { Permissions = new() }).Should().BeTrue();

    [Fact]
    public void UpdatePermissions_UnknownCode_Fails()
        => IsValid(_perm, new UpdateStaffPermissionsRequest { Permissions = new() { "NOPE" } }).Should().BeFalse();

    /// <summary>
    /// Permissions = null (JSON "permissions": null) phải trả lỗi validation chứ không được ném NullReferenceException.
    /// Rule hiện tại: .NotNull().Must(perms => perms.All(...)) – với CascadeMode mặc định (Continue) thì Must vẫn chạy khi null.
    /// Nếu test này FAIL bằng NullReferenceException thì đó là bug thật trong validator (xem ghi chú bàn giao).
    /// </summary>
    [Fact]
    public void UpdatePermissions_Null_ReturnsValidationError_NotException()
    {
        var m = new UpdateStaffPermissionsRequest { Permissions = null! };
        var act = () => _perm.Validate(m);
        act.Should().NotThrow();
        act().IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(500, false)]
    [InlineData(501, true)]
    public void UpdatePermissions_Reason_MaxLength(int len, bool expectError)
    {
        var m = new UpdateStaffPermissionsRequest { Permissions = new(), Reason = Str(len) };
        Has(_perm, m, nameof(m.Reason)).Should().Be(expectError);
    }

    // ───── Check-in / out ─────
    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void StaffCheckIn_ShiftNote(object? note, bool valid)
    {
        var text = note is int n ? Str(n) : (string?)note;
        IsValid(new StaffCheckInValidator(), new StaffCheckInRequest { ShiftNote = text }).Should().Be(valid);
        IsValid(new StaffCheckOutValidator(), new StaffCheckOutRequest { ShiftNote = text }).Should().Be(valid);
    }

    // ═════════ Negotiation chat ═════════
    private readonly SendChatMessageRequestValidator _chat = new();

    [Theory]
    [InlineData("Text", "Xin chào", null, true)]
    [InlineData("text", "Xin chào", null, true)]
    [InlineData("Text", "", null, false)]
    [InlineData("Text", null, null, false)]
    [InlineData("Image", null, "https://cdn/x.jpg", true)]
    [InlineData("Image", "Chú thích", "https://cdn/x.jpg", true)]
    [InlineData("Image", null, "", false)]
    [InlineData("Image", null, null, false)]
    [InlineData("Proposal", "x", null, false)]       // đề xuất giá phải đi qua /proposals
    [InlineData("", "x", null, false)]
    [InlineData(null, "x", null, false)]
    public void Chat_MessageType_Matrix(string? type, string? content, string? url, bool valid)
    {
        var m = new SendChatMessageRequest { MessageType = type!, MessageContent = content, AttachmentUrl = url };
        IsValid(_chat, m).Should().Be(valid);
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void Chat_Text_MaxLength2000(int len, bool valid)
        => IsValid(_chat, new SendChatMessageRequest { MessageType = "Text", MessageContent = Str(len) }).Should().Be(valid);

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Chat_ImageCaption_MaxLength500(int len, bool valid)
        => IsValid(_chat, new SendChatMessageRequest { MessageType = "Image", AttachmentUrl = "https://x/y.png", MessageContent = Str(len) }).Should().Be(valid);

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void Chat_ImageUrl_MaxLength1000(int len, bool valid)
        => IsValid(_chat, new SendChatMessageRequest { MessageType = "Image", AttachmentUrl = Str(len) }).Should().Be(valid);

    // ───── SendProposalRequestValidator ─────
    private readonly SendProposalRequestValidator _prop = new();
    private static SendProposalRequest Prop() => new() { ProposedPricePerKg = 5000m, ProposedWeightKg = 10m, ProposedTimeSlot = "2026-12-01 (08:00 - 10:00)" };

    [Fact] public void Proposal_Valid_Passes() => IsValid(_prop, Prop()).Should().BeTrue();

    [Theory]
    [InlineData("0", true)]
    [InlineData("-1", true)]
    [InlineData("0.01", false)]
    [InlineData("10000000", false)]
    [InlineData("10000000.01", true)]
    public void Proposal_Price_Boundary(string price, bool expectError)
    {
        var m = Prop(); m.ProposedPricePerKg = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);
        Has(_prop, m, nameof(m.ProposedPricePerKg)).Should().Be(expectError);
    }

    [Theory]
    [InlineData("0", true)]
    [InlineData("0.1", false)]
    [InlineData("100000", false)]
    [InlineData("100000.1", true)]
    public void Proposal_Weight_Boundary(string kg, bool expectError)
    {
        var m = Prop(); m.ProposedWeightKg = decimal.Parse(kg, System.Globalization.CultureInfo.InvariantCulture);
        Has(_prop, m, nameof(m.ProposedWeightKg)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(150, false)]
    [InlineData(151, true)]
    public void Proposal_TimeSlot_Length(int len, bool expectError)
    {
        var m = Prop(); m.ProposedTimeSlot = Str(len);
        Has(_prop, m, nameof(m.ProposedTimeSlot)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(50, false)]
    [InlineData(51, true)]
    public void Proposal_CategoryCode_MaxLength(int len, bool expectError)
    {
        var m = Prop(); m.CategoryCode = Str(len);
        Has(_prop, m, nameof(m.CategoryCode)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(300, false)]
    [InlineData(301, true)]
    public void Proposal_PickupAddress_MaxLength(int len, bool expectError)
    {
        var m = Prop(); m.PickupAddress = Str(len);
        Has(_prop, m, nameof(m.PickupAddress)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(1000, false)]
    [InlineData(1001, true)]
    public void Proposal_Note_MaxLength(int len, bool expectError)
    {
        var m = Prop(); m.Note = Str(len);
        Has(_prop, m, nameof(m.Note)).Should().Be(expectError);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void RespondProposal_Note_MaxLength(int len, bool valid)
        => IsValid(new RespondProposalRequestValidator(), new RespondProposalRequest { IsAccepted = true, ResponseNote = Str(len) }).Should().Be(valid);
}
