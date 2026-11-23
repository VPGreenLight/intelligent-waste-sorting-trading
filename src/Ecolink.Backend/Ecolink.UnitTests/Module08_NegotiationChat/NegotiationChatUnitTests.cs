using System.Globalization;
using Ecolink.Application.Dtos;
using Ecolink.Application.Validators;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Ecolink.Infrastructure.Services;
using Ecolink.UnitTests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Ecolink.UnitTests.Module08_NegotiationChat;

[Trait("Module", "Module08_NegotiationChat")]
public class NegotiationChatUnitTests
{
    private static string FutureSlotDate =>
        (DateTime.UtcNow + TimeSpan.FromHours(7)).Date.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private readonly SendChatMessageRequestValidator _msgValidator = new();
    private readonly SendProposalRequestValidator _proposalValidator = new();
    private readonly RespondProposalRequestValidator _respondValidator = new();

    private static async Task<(
        NegotiationChatService Sut,
        ApplicationDbContext Db,
        User Citizen,
        User RecyclerUser,
        User StaffUser,
        TradeOrder Order)> CreateSeededChatSutAsync(OrderStatus initialStatus = OrderStatus.Pending)
    {
        var db = TestDbContextFactory.Create();

        var citizen = new User
        {
            Id = Guid.NewGuid(),
            Username = "citizen_chat",
            Email = "citizen_chat@ecolink.vn",
            FullName = "Nguyễn Văn Dân",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        var recyclerUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "recycler_chat",
            Email = "recycler_chat@ecolink.vn",
            FullName = "Đỗ Văn Hùng",
            Role = UserRole.Recycler,
            Status = UserStatus.Active
        };
        var staffUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "staff_chat",
            Email = "staff_chat@ecolink.vn",
            FullName = "Nhân Viên Phân Xử",
            Role = UserRole.Staff,
            Status = UserStatus.Active
        };

        var profile = new RecyclerProfile
        {
            UserId = recyclerUser.Id,
            User = recyclerUser,
            FacilityName = "Trạm Tái Chế Hòa Lạc",
            RepresentativeName = "Đỗ Văn Hùng",
            BusinessAddress = "Tân Xã, Thạch Thất, Hà Nội",
            ContactPhone = "0968112233",
            Latitude = 21.0182m,
            Longitude = 105.5345m,
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online
        };

        var partner = new CollectionPartner
        {
            Id = recyclerUser.Id,
            UserId = recyclerUser.Id,
            FacilityName = "Trạm Tái Chế Hòa Lạc",
            Address = "Tân Xã, Thạch Thất, Hà Nội",
            Latitude = 21.0182,
            Longitude = 105.5345,
            OperatingHours = "07:30 - 19:00",
            ContactPhone = "0968112233",
            IsVerified = true
        };

        var order = new TradeOrder
        {
            Id = Guid.NewGuid(),
            UserId = citizen.Id,
            User = citizen,
            PartnerId = partner.Id,
            Partner = partner,
            RecyclerUserId = recyclerUser.Id,
            RecyclerProfile = profile,
            Status = initialStatus,
            CategoryCode = "plastic",
            EstimatedWeightKg = 5.0m,
            InitialPricePerKg = 7000m,
            ProposedTimeSlot = $"{FutureSlotDate} (09:00 - 11:00)",
            PickupAddress = "KTX Khu B, ĐH FPT Hòa Lạc",
            ScheduledAt = DateTime.UtcNow.AddDays(1)
        };

        db.Users.AddRange(citizen, recyclerUser, staffUser);
        db.RecyclerProfiles.Add(profile);
        db.CollectionPartners.Add(partner);
        db.TradeOrders.Add(order);
        await db.SaveChangesAsync();

        var sut = new NegotiationChatService(db, NullLogger<NegotiationChatService>.Instance);
        return (sut, db, citizen, recyclerUser, staffUser, order);
    }

    [Fact]
    public void UTC001_SendChatMessageRequestValidator_ValidTextAndImageMessages_PassValidation()
    {
        var textOk = _msgValidator.Validate(new SendChatMessageRequest
        {
            MessageType = "Text",
            MessageContent = "Chào anh, em có 5kg chai nhựa PET sạch."
        });

        var imageOk = _msgValidator.Validate(new SendChatMessageRequest
        {
            MessageType = "Image",
            AttachmentUrl = "https://cdn.ecolink.test/chat/photo.jpg",
            MessageContent = "Ảnh chụp thực tế"
        });

        textOk.IsValid.Should().BeTrue();
        imageOk.IsValid.Should().BeTrue();
    }

    [Fact]
    public void UTC002_SendChatMessageRequestValidator_ProposalTypeOrEmptyTextOrOver2000CharsOrMissingImageUrl_FailsValidation()
    {
        var proposalType = _msgValidator.Validate(new SendChatMessageRequest { MessageType = "Proposal", MessageContent = "Test" });
        var emptyText = _msgValidator.Validate(new SendChatMessageRequest { MessageType = "Text", MessageContent = "   " });
        var longText = _msgValidator.Validate(new SendChatMessageRequest { MessageType = "Text", MessageContent = new string('A', 2001) });
        var missingUrl = _msgValidator.Validate(new SendChatMessageRequest { MessageType = "Image", AttachmentUrl = "" });

        proposalType.IsValid.Should().BeFalse();
        emptyText.IsValid.Should().BeFalse();
        longText.IsValid.Should().BeFalse();
        missingUrl.IsValid.Should().BeFalse();
    }

    [Fact]
    public void UTC003_SendProposalAndRespondValidators_BoundaryPriceWeightAndTimeSlot_ValidatesAccurately_BrNeg04()
    {
        var validProposal = _proposalValidator.Validate(new SendProposalRequest
        {
            ProposedPricePerKg = 7500m,
            ProposedWeightKg = 6.0m,
            ProposedTimeSlot = $"{FutureSlotDate} (14:00 - 16:00)",
            Note = "Tăng thêm 1kg"
        });
        validProposal.IsValid.Should().BeTrue();

        var zeroPrice = _proposalValidator.Validate(new SendProposalRequest { ProposedPricePerKg = 0m, ProposedWeightKg = 5m, ProposedTimeSlot = "14:00" });
        var overMaxPrice = _proposalValidator.Validate(new SendProposalRequest { ProposedPricePerKg = 10_000_001m, ProposedWeightKg = 5m, ProposedTimeSlot = "14:00" });
        var emptySlot = _proposalValidator.Validate(new SendProposalRequest { ProposedPricePerKg = 7000m, ProposedWeightKg = 5m, ProposedTimeSlot = "   " });
        var over500Note = _respondValidator.Validate(new RespondProposalRequest { IsAccepted = false, ResponseNote = new string('N', 501) });

        zeroPrice.IsValid.Should().BeFalse();
        overMaxPrice.IsValid.Should().BeFalse();
        emptySlot.IsValid.Should().BeFalse();
        over500Note.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task UTC004_GetOrderChatThreadAsync_AuthorizedCitizenOrRecycler_ReturnsThreadAndOpenStatus()
    {
        var (sut, _, citizen, _, _, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);

        var thread = await sut.GetOrderChatThreadAsync(order.Id, citizen.Id, "Citizen", 1, 50);

        thread.OrderId.Should().Be(order.Id);
        thread.CitizenUserId.Should().Be(citizen.Id);
        thread.FacilityName.Should().Be("Trạm Tái Chế Hòa Lạc");
        thread.IsChatOpen.Should().BeTrue();
        thread.IsProposalLocked.Should().BeFalse();
    }

    [Fact]
    public async Task UTC005_GetOrderChatThreadAndVerifyAccess_UnauthorizedThirdPartyUser_ThrowsBrNeg01Forbidden()
    {
        var (sut, _, _, _, _, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);
        var strangerId = Guid.NewGuid();

        var act = () => sut.GetOrderChatThreadAsync(order.Id, strangerId, "Citizen");

        var ex = await act.Should().ThrowAsync<ForbiddenAccessException>();
        ex.Which.RuleCode.Should().Be("BR-NEG-01");
        ex.Which.MessageCode.Should().Be("MSG-NEG-FORBIDDEN");

        var hasAccess = await sut.VerifyChatRoomAccessAsync(order.Id, strangerId, "Citizen");
        hasAccess.Should().BeFalse();
    }

    [Fact]
    public async Task UTC006_GetOrderChatThreadAsync_StaffAccessAllowedOnlyWhenOrderIsDisputed_BrNeg01_BrDsp06()
    {
        var (sut, db, _, _, staffUser, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);

        // 1. Khi đơn đang Pending -> Staff bị chặn truy cập tin nhắn riêng tư
        var actPending = () => sut.GetOrderChatThreadAsync(order.Id, staffUser.Id, "Staff");
        (await actPending.Should().ThrowAsync<ForbiddenAccessException>()).Which.RuleCode.Should().Be("BR-NEG-01");

        // 2. Khi đơn chuyển sang Disputed -> Staff được phép xem lịch sử thương lượng để phân xử (BR-DSP-06)
        order.Status = OrderStatus.Disputed;
        await db.SaveChangesAsync();

        var disputedThread = await sut.GetOrderChatThreadAsync(order.Id, staffUser.Id, "Staff");
        disputedThread.OrderStatus.Should().Be(OrderStatus.Disputed.ToString());
    }

    [Fact]
    public async Task UTC007_SendMessageAsync_GeneralTextOrImageAllowedOnPendingAcceptedAndCompleted_ReturnsMsgNeg02_BrNeg02()
    {
        var (sut, _, citizen, _, _, order) = await CreateSeededChatSutAsync(OrderStatus.Accepted);

        var (msg, notification) = await sut.SendMessageAsync(order.Id, citizen.Id, "Citizen", new SendChatMessageRequest
        {
            MessageType = "Text",
            MessageContent = "  Anh đến cổng phụ KTX nhé!  "
        });

        msg.MessageType.Should().Be(ChatMessageType.Text.ToString());
        msg.MessageContent.Should().Be("Anh đến cổng phụ KTX nhé!");
        msg.SenderName.Should().Be("Nguyễn Văn Dân");
        notification.Should().Be("Bạn có tin nhắn mới từ Nguyễn Văn Dân."); // MSG-NEG-02
    }

    [Fact]
    public async Task UTC008_SendMessageAsync_WhenOrderCancelledRejectedOrExpired_ThrowsBrNeg02ChatClosed()
    {
        var (sut, _, citizen, _, _, order) = await CreateSeededChatSutAsync(OrderStatus.Cancelled);

        var act = () => sut.SendMessageAsync(order.Id, citizen.Id, "Citizen", new SendChatMessageRequest
        {
            MessageType = "Text",
            MessageContent = "Tin nhắn sau khi hủy"
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-NEG-02");
        ex.Which.MessageCode.Should().Be("MSG-NEG-CHAT-CLOSED");
    }

    [Fact]
    public async Task UTC009_SendProposalAsync_FirstProposalOnPendingOrder_MovesToNegotiatingAndReturnsMsgNeg01_BrHnd02_BrNeg04()
    {
        var (sut, db, citizen, _, _, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);

        var (proposalMsg, superseded, toast, notification) = await sut.SendProposalAsync(
            order.Id,
            citizen.Id,
            "Citizen",
            new SendProposalRequest
            {
                ProposedPricePerKg = 7500m,
                ProposedWeightKg = 8.0m,
                ProposedTimeSlot = $"{FutureSlotDate} (15:00 - 17:00)",
                Note = "Em có tới 8kg chai sạch"
            });

        superseded.Should().BeEmpty();
        proposalMsg.MessageType.Should().Be(ChatMessageType.Proposal.ToString());
        proposalMsg.Proposal.Should().NotBeNull();
        proposalMsg.Proposal!.ProposalStatus.Should().Be(ProposalStatus.Pending.ToString());
        toast.Should().Be("Yêu cầu trao đổi của bạn đã được gửi đến Trạm Tái Chế Hòa Lạc."); // MSG-NEG-01
        notification.Should().Contain("Nguyễn Văn Dân");

        var updatedOrder = await db.TradeOrders.FirstAsync(o => o.Id == order.Id);
        updatedOrder.Status.Should().Be(OrderStatus.Negotiating);
        updatedOrder.ProposedTimeSlot.Should().Be($"{FutureSlotDate} (15:00 - 17:00)");
    }

    [Fact]
    public async Task UTC010_SendProposalAsync_CounterProposalSupersedesPreviousPendingProposal_BrNeg03()
    {
        var (sut, db, citizen, recyclerUser, _, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);

        var (firstProposal, _, _, _) = await sut.SendProposalAsync(order.Id, citizen.Id, "Citizen", new SendProposalRequest
        {
            ProposedPricePerKg = 8000m,
            ProposedWeightKg = 8.0m,
            ProposedTimeSlot = $"{FutureSlotDate} (15:00 - 17:00)"
        });

        var (counterProposal, supersededIds, _, _) = await sut.SendProposalAsync(order.Id, recyclerUser.Id, "Recycler", new SendProposalRequest
        {
            ProposedPricePerKg = 7400m,
            ProposedWeightKg = 8.0m,
            ProposedTimeSlot = $"{FutureSlotDate} (16:00 - 18:00)",
            Note = "Cơ sở hỗ trợ giá 7.400đ/kg"
        });

        supersededIds.Should().ContainSingle().Which.Should().Be(firstProposal.Id);
        counterProposal.Proposal!.ProposalStatus.Should().Be(ProposalStatus.Pending.ToString());

        var firstInDb = await db.ChatMessages.FirstAsync(m => m.Id == firstProposal.Id);
        firstInDb.ProposalStatus.Should().Be(ProposalStatus.Superseded);
    }

    [Fact]
    public async Task UTC011_SendProposalAsync_WhenOrderAlreadyAcceptedOrCompleted_ThrowsBrNeg02ProposalLocked()
    {
        var (sut, _, citizen, _, _, order) = await CreateSeededChatSutAsync(OrderStatus.Accepted);

        var act = () => sut.SendProposalAsync(order.Id, citizen.Id, "Citizen", new SendProposalRequest
        {
            ProposedPricePerKg = 8000m,
            ProposedWeightKg = 5.0m,
            ProposedTimeSlot = $"{FutureSlotDate} (15:00 - 17:00)"
        });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-NEG-02");
        ex.Which.MessageCode.Should().Be("MSG-NEG-PROPOSAL-LOCKED");
    }

    [Fact]
    public async Task UTC012_RespondToProposalAsync_CounterpartyAcceptsLatestProposal_SnapshotsTermsAndLocksOrderAccepted_BrNeg03_BrNeg05()
    {
        var (sut, db, citizen, recyclerUser, _, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);
        var (proposalMsg, _, _, _) = await sut.SendProposalAsync(order.Id, recyclerUser.Id, "Recycler", new SendProposalRequest
        {
            ProposedPricePerKg = 7300m,
            ProposedWeightKg = 6.5m,
            ProposedTimeSlot = $"{FutureSlotDate} (14:00 - 16:00)"
        });

        var (res, toast) = await sut.RespondToProposalAsync(
            order.Id,
            proposalMsg.Id,
            citizen.Id,
            "Citizen",
            new RespondProposalRequest { IsAccepted = true });

        res.ProposalStatus.Should().Be(ProposalStatus.Accepted.ToString());
        res.OrderStatus.Should().Be(OrderStatus.Accepted.ToString());
        res.AgreedPricePerKg.Should().Be(7300m);
        res.ConfirmedWeightKg.Should().Be(6.5m);
        res.ConfirmedTimeSlot.Should().Be($"{FutureSlotDate} (14:00 - 16:00)");
        res.IsProposalLocked.Should().BeTrue();
        res.SystemMessage.MessageType.Should().Be(ChatMessageType.System.ToString());
        toast.Should().Be("Nguyễn Văn Dân đã chấp nhận đề nghị của bạn."); // MSG-NEG-03

        var dbOrder = await db.TradeOrders.FirstAsync(o => o.Id == order.Id);
        dbOrder.Status.Should().Be(OrderStatus.Accepted);
        dbOrder.AcceptedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task UTC013_RespondToProposalAsync_CounterpartyRejectsProposal_KeepsOrderInNegotiatingAndLogsReason()
    {
        var (sut, db, citizen, recyclerUser, _, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);
        var (proposalMsg, _, _, _) = await sut.SendProposalAsync(order.Id, citizen.Id, "Citizen", new SendProposalRequest
        {
            ProposedPricePerKg = 9000m,
            ProposedWeightKg = 5.0m,
            ProposedTimeSlot = $"{FutureSlotDate} (14:00 - 16:00)"
        });

        var (res, _) = await sut.RespondToProposalAsync(
            order.Id,
            proposalMsg.Id,
            recyclerUser.Id,
            "Recycler",
            new RespondProposalRequest
            {
                IsAccepted = false,
                ResponseNote = "Giá trần nhựa PET hôm nay là 7.500đ/kg"
            });

        res.ProposalStatus.Should().Be(ProposalStatus.Rejected.ToString());
        res.OrderStatus.Should().Be(OrderStatus.Negotiating.ToString());
        res.IsProposalLocked.Should().BeFalse();
        res.SystemMessage.MessageContent.Should().Contain("7.500đ/kg");

        var dbOrder = await db.TradeOrders.FirstAsync(o => o.Id == order.Id);
        dbOrder.Status.Should().Be(OrderStatus.Negotiating);
    }

    [Fact]
    public async Task UTC014_RespondToProposalAsync_SenderAttemptsToRespondToOwnProposal_ThrowsBrNeg03SelfResponse()
    {
        var (sut, _, citizen, _, _, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);
        var (proposalMsg, _, _, _) = await sut.SendProposalAsync(order.Id, citizen.Id, "Citizen", new SendProposalRequest
        {
            ProposedPricePerKg = 7500m,
            ProposedWeightKg = 5.0m,
            ProposedTimeSlot = $"{FutureSlotDate} (14:00 - 16:00)"
        });

        var act = () => sut.RespondToProposalAsync(
            order.Id,
            proposalMsg.Id,
            citizen.Id,
            "Citizen",
            new RespondProposalRequest { IsAccepted = true });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-NEG-03");
        ex.Which.MessageCode.Should().Be("MSG-NEG-SELF-RESPONSE");
    }

    [Fact]
    public async Task UTC015_RespondToProposalAsync_AttemptingToAcceptSupersededProposal_ThrowsBrNeg03Superseded()
    {
        var (sut, _, citizen, recyclerUser, _, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);
        var (oldProposal, _, _, _) = await sut.SendProposalAsync(order.Id, citizen.Id, "Citizen", new SendProposalRequest
        {
            ProposedPricePerKg = 7500m,
            ProposedWeightKg = 5.0m,
            ProposedTimeSlot = $"{FutureSlotDate} (14:00 - 16:00)"
        });
        await sut.SendProposalAsync(order.Id, recyclerUser.Id, "Recycler", new SendProposalRequest
        {
            ProposedPricePerKg = 7200m,
            ProposedWeightKg = 5.0m,
            ProposedTimeSlot = $"{FutureSlotDate} (15:00 - 17:00)"
        });

        var act = () => sut.RespondToProposalAsync(
            order.Id,
            oldProposal.Id,
            recyclerUser.Id,
            "Recycler",
            new RespondProposalRequest { IsAccepted = true });

        var ex = await act.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-NEG-03");
        ex.Which.MessageCode.Should().Be("MSG-NEG-SUPERSEDED");
    }

    [Fact]
    public async Task UTC016_RespondToProposalAsync_ProposalAlreadyRejected_ThrowsBrNeg03AlreadyResponded()
    {
        var (sut, _, citizen, recyclerUser, _, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);
        var (proposalMsg, _, _, _) = await sut.SendProposalAsync(order.Id, citizen.Id, "Citizen", new SendProposalRequest
        {
            ProposedPricePerKg = 8000m,
            ProposedWeightKg = 5.0m,
            ProposedTimeSlot = $"{FutureSlotDate} (14:00 - 16:00)"
        });
        await sut.RespondToProposalAsync(order.Id, proposalMsg.Id, recyclerUser.Id, "Recycler", new RespondProposalRequest { IsAccepted = false });

        var actAgain = () => sut.RespondToProposalAsync(
            order.Id,
            proposalMsg.Id,
            recyclerUser.Id,
            "Recycler",
            new RespondProposalRequest { IsAccepted = true });

        var ex = await actAgain.Should().ThrowAsync<BusinessRuleException>();
        ex.Which.RuleCode.Should().Be("BR-NEG-03");
        ex.Which.MessageCode.Should().Be("MSG-NEG-ALREADY-RESPONDED");
    }

    [Fact]
    public async Task UTC017_AcceptOrderInitialTermsAsync_RecyclerAcceptsInitialOrder_LocksInitialPriceAndTimeSlot_BrNeg05()
    {
        var (sut, _, _, recyclerUser, _, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);

        var (res, toast) = await sut.AcceptOrderInitialTermsAsync(order.Id, recyclerUser.Id, "Recycler");

        res.OrderStatus.Should().Be(OrderStatus.Accepted.ToString());
        res.AgreedPricePerKg.Should().Be(7000m);
        res.ConfirmedTimeSlot.Should().Be($"{FutureSlotDate} (09:00 - 11:00)");
        res.IsProposalLocked.Should().BeTrue();
        toast.Should().Be("Trạm Tái Chế Hòa Lạc đã chấp nhận đề nghị của bạn."); // MSG-NEG-03
    }

    [Fact]
    public async Task UTC018_AcceptOrderInitialTermsAsync_CitizenAttemptsToCall_ThrowsBrNeg05Forbidden()
    {
        var (sut, _, citizen, _, _, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);

        var act = () => sut.AcceptOrderInitialTermsAsync(order.Id, citizen.Id, "Citizen");

        var ex = await act.Should().ThrowAsync<ForbiddenAccessException>();
        ex.Which.RuleCode.Should().Be("BR-NEG-05");
        ex.Which.MessageCode.Should().Be("MSG-NEG-ONLY-RECYCLER");
    }

    [Fact]
    public async Task UTC019_AcceptOrderInitialTermsAsync_WithPendingCitizenProposal_AutoAcceptsLatestCitizenProposal()
    {
        var (sut, _, citizen, recyclerUser, _, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);
        var (citizenProposal, _, _, _) = await sut.SendProposalAsync(order.Id, citizen.Id, "Citizen", new SendProposalRequest
        {
            ProposedPricePerKg = 7400m,
            ProposedWeightKg = 6.0m,
            ProposedTimeSlot = $"{FutureSlotDate} (16:00 - 18:00)"
        });

        var (res, _) = await sut.AcceptOrderInitialTermsAsync(order.Id, recyclerUser.Id, "Recycler");

        res.ProposalMessageId.Should().Be(citizenProposal.Id);
        res.OrderStatus.Should().Be(OrderStatus.Accepted.ToString());
        res.AgreedPricePerKg.Should().Be(7400m);
        res.ConfirmedWeightKg.Should().Be(6.0m);
        res.ConfirmedTimeSlot.Should().Be($"{FutureSlotDate} (16:00 - 18:00)");
    }

    [Fact]
    public async Task UTC020_MarkMessagesAsReadAsync_MarksOnlyCounterpartyUnreadMessagesAsRead()
    {
        var (sut, db, citizen, recyclerUser, _, order) = await CreateSeededChatSutAsync(OrderStatus.Pending);
        await sut.SendMessageAsync(order.Id, citizen.Id, "Citizen", new SendChatMessageRequest { MessageType = "Text", MessageContent = "Tin 1 từ Citizen" });
        await sut.SendMessageAsync(order.Id, citizen.Id, "Citizen", new SendChatMessageRequest { MessageType = "Text", MessageContent = "Tin 2 từ Citizen" });
        await sut.SendMessageAsync(order.Id, recyclerUser.Id, "Recycler", new SendChatMessageRequest { MessageType = "Text", MessageContent = "Tin 1 từ Recycler" });

        // Recycler đọc tin nhắn -> chỉ đánh dấu đã đọc 2 tin của Citizen
        var markedForRecycler = await sut.MarkMessagesAsReadAsync(order.Id, recyclerUser.Id, "Recycler");
        markedForRecycler.Should().Be(2);

        // Tin của Recycler gửi cho Citizen vẫn chưa được Citizen đọc
        var unreadForCitizen = await db.ChatMessages.CountAsync(m => m.TradeOrderId == order.Id && m.SenderUserId == recyclerUser.Id && !m.IsRead);
        unreadForCitizen.Should().Be(1);
    }

    [Fact]
    public async Task UTC021_CreateTradeOrderAsync_WithRecyclerProfileLookup_PersistsOrderForCitizenAndRecycler()
    {
        var db = TestDbContextFactory.Create();
        var citizen = new User
        {
            Id = Guid.NewGuid(),
            Username = "citizen_hl",
            Email = "citizen_hl@ecolink.vn",
            FullName = "Nguyễn Văn Dân",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        var recyclerUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "collector_hl1",
            Email = "collector_hl1@ecolink.vn",
            FullName = "Đỗ Văn Hùng",
            Role = UserRole.Recycler,
            Status = UserStatus.Active
        };
        var profile = new RecyclerProfile
        {
            UserId = recyclerUser.Id,
            User = recyclerUser,
            FacilityName = "Trạm Thu Gom Tái Chế Xanh Hòa Lạc",
            RepresentativeName = "Đỗ Văn Hùng",
            BusinessAddress = "Thôn 3, Xã Tân Xã, Thạch Thất, Hà Nội",
            ContactPhone = "0968112233",
            Latitude = 21.0182m,
            Longitude = 105.5345m,
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online
        };
        var partner = new CollectionPartner
        {
            Id = Guid.NewGuid(),
            UserId = recyclerUser.Id,
            FacilityName = profile.FacilityName,
            Address = profile.BusinessAddress,
            Latitude = 21.0182,
            Longitude = 105.5345,
            OperatingHours = "07:30 - 19:00",
            ContactPhone = "0968112233",
            IsVerified = true
        };

        db.Users.AddRange(citizen, recyclerUser);
        db.RecyclerProfiles.Add(profile);
        db.CollectionPartners.Add(partner);
        await db.SaveChangesAsync();

        var tradeService = new TradeTransactionService(
            NullLogger<TradeTransactionService>.Instance,
            new GamificationService(NullLogger<GamificationService>.Instance),
            db);

        var created = await tradeService.CreateTradeOrderAsync(new CreateTradeOrderRequest
        {
            UserId = citizen.Id,
            PartnerId = recyclerUser.Id,
            EstimatedWeightKg = 10m,
            CategoryCode = "plastic",
            InitialPricePerKg = 5500m,
            ProposedTimeSlot = "10:00 - 12:00",
            PickupAddress = "Cổng phụ Khu CNC Hòa Lạc"
        });

        created.PartnerName.Should().Be("Trạm Thu Gom Tái Chế Xanh Hòa Lạc");
        created.RecyclerUserId.Should().Be(recyclerUser.Id);
        created.Status.Should().Be(OrderStatus.Pending.ToString());

        var citizenOrders = await tradeService.GetUserOrdersAsync(citizen.Id);
        citizenOrders.Should().Contain(o => o.Id == created.Id);

        var recyclerOrders = await tradeService.GetUserOrdersAsync(recyclerUser.Id);
        recyclerOrders.Should().Contain(o => o.Id == created.Id);
    }

    [Fact]
    public async Task UTC022_CreateTradeOrderAsync_IsInquiryDraft_CreatesAndReusesNegotiatingOrder()
    {
        var db = TestDbContextFactory.Create();
        var citizen = new User
        {
            Id = Guid.NewGuid(),
            Username = "citizen_draft",
            Email = "citizen_draft@ecolink.vn",
            FullName = "Nguyễn Văn Dân",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        var recyclerUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "collector_hl1_draft",
            Email = "collector_hl1_draft@ecolink.vn",
            FullName = "Đỗ Văn Hùng",
            Role = UserRole.Recycler,
            Status = UserStatus.Active
        };
        var partner = new CollectionPartner
        {
            Id = recyclerUser.Id,
            UserId = recyclerUser.Id,
            FacilityName = "Trạm Thu Gom Tái Chế Xanh Hòa Lạc",
            Address = "Tân Xã, Thạch Thất, Hà Nội",
            Latitude = 21.0182,
            Longitude = 105.5345,
            OperatingHours = "07:30 - 19:00",
            ContactPhone = "0968112233",
            IsVerified = true
        };

        db.Users.AddRange(citizen, recyclerUser);
        db.CollectionPartners.Add(partner);
        await db.SaveChangesAsync();

        var tradeService = new TradeTransactionService(
            NullLogger<TradeTransactionService>.Instance,
            new GamificationService(NullLogger<GamificationService>.Instance),
            db);

        var first = await tradeService.CreateTradeOrderAsync(new CreateTradeOrderRequest
        {
            UserId = citizen.Id,
            PartnerId = recyclerUser.Id,
            IsInquiryDraft = true
        });

        first.Status.Should().Be(OrderStatus.Negotiating.ToString());
        first.IsInquiryOnly.Should().BeTrue();
        first.EstimatedWeightKg.Should().Be(0m);
        first.CategoryCode.Should().BeNull();
        first.AgreedPricePerKg.Should().BeNull();
        (await db.HandoverRequests.AnyAsync(h => h.Id == first.Id)).Should().BeFalse();

        var second = await tradeService.CreateTradeOrderAsync(new CreateTradeOrderRequest
        {
            UserId = citizen.Id,
            PartnerId = recyclerUser.Id,
            IsInquiryDraft = true
        });

        second.Id.Should().Be(first.Id);
        (await db.TradeOrders.CountAsync(o => o.UserId == citizen.Id)).Should().Be(1);
    }

    [Fact]
    public async Task UTC023_SendProposalAsync_WithCategoryAndPickupAddress_UpdatesOrderAndProposalPayload()
    {
        var (sut, db, citizen, _, _, order) = await CreateSeededChatSutAsync(OrderStatus.Negotiating);

        var (proposalMsg, _, _, _) = await sut.SendProposalAsync(
            order.Id,
            citizen.Id,
            "Citizen",
            new SendProposalRequest
            {
                ProposedPricePerKg = 24000m,
                ProposedWeightKg = 6.5m,
                ProposedTimeSlot = "14:00 - 16:00",
                CategoryCode = "metal",
                PickupAddress = "Cổng phụ Khu CNC Hòa Lạc, Thạch Thất, Hà Nội",
                Note = "Vỏ lon nhôm đã ép dẹp"
            });

        proposalMsg.Proposal.Should().NotBeNull();
        proposalMsg.Proposal!.CategoryCode.Should().Be("metal");
        proposalMsg.Proposal!.PickupAddress.Should().Be("Cổng phụ Khu CNC Hòa Lạc, Thạch Thất, Hà Nội");

        var updatedOrder = await db.TradeOrders.FirstAsync(o => o.Id == order.Id);
        updatedOrder.CategoryCode.Should().Be("metal");
        updatedOrder.EstimatedWeightKg.Should().Be(6.5m);
        updatedOrder.PickupAddress.Should().Be("Cổng phụ Khu CNC Hòa Lạc, Thạch Thất, Hà Nội");
    }

    [Fact]
    public async Task UTC024_InquiryOrder_UpgradesToOfficialHandoverWithoutLockingAgreedPriceEarly()
    {
        var db = TestDbContextFactory.Create();
        var citizen = new User
        {
            Id = Guid.NewGuid(),
            Username = "citizen_upg",
            Email = "citizen_upg@ecolink.vn",
            FullName = "Nguyễn Văn Dân",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        var recyclerUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "collector_upg",
            Email = "collector_upg@ecolink.vn",
            FullName = "Đỗ Văn Hùng",
            Role = UserRole.Recycler,
            Status = UserStatus.Active
        };
        var plasticCat = new WasteCategory
        {
            Id = Guid.NewGuid(),
            Code = "plastic",
            Name = "Nhựa tái chế",
            IsRecyclable = true,
            Status = "ACTIVE"
        };
        var profile = new RecyclerProfile
        {
            UserId = recyclerUser.Id,
            User = recyclerUser,
            FacilityName = "Trạm Thu Gom Tái Chế Xanh Hòa Lạc",
            RepresentativeName = "Đỗ Văn Hùng",
            BusinessAddress = "Thôn 3, Tân Xã",
            ContactPhone = "0968112233",
            Latitude = 21.0182m,
            Longitude = 105.5345m,
            VerificationStatus = VerificationStatus.Verified,
            ActiveStatus = RecyclerActiveStatus.Online
        };
        var partner = new CollectionPartner
        {
            Id = recyclerUser.Id,
            UserId = recyclerUser.Id,
            FacilityName = profile.FacilityName,
            Address = profile.BusinessAddress,
            Latitude = 21.0182,
            Longitude = 105.5345,
            OperatingHours = "07:30 - 19:00",
            ContactPhone = "0968112233",
            IsVerified = true
        };
        var price = new RecyclerPrice
        {
            Id = Guid.NewGuid(),
            RecyclerId = recyclerUser.Id,
            WasteCategoryId = plasticCat.Id,
            PricePerKg = 7200m,
            MinQuantityKg = 1m,
            Status = PriceStatus.Active
        };

        db.Users.AddRange(citizen, recyclerUser);
        db.WasteCategories.Add(plasticCat);
        db.RecyclerProfiles.Add(profile);
        db.CollectionPartners.Add(partner);
        db.RecyclerPrices.Add(price);
        await db.SaveChangesAsync();

        var tradeService = new TradeTransactionService(
            NullLogger<TradeTransactionService>.Instance,
            new GamificationService(NullLogger<GamificationService>.Instance),
            db);
        var chatService = new NegotiationChatService(db, NullLogger<NegotiationChatService>.Instance);

        // 1. Nhắn hỏi giá trước
        var inquiry = await tradeService.CreateTradeOrderAsync(new CreateTradeOrderRequest
        {
            UserId = citizen.Id,
            PartnerId = recyclerUser.Id,
            IsInquiryDraft = true
        });
        inquiry.IsInquiryOnly.Should().BeTrue();

        var threadBefore = await chatService.GetOrderChatThreadAsync(inquiry.Id, citizen.Id, "Citizen");
        threadBefore.IsInquiryOnly.Should().BeTrue();
        threadBefore.AgreedPricePerKg.Should().BeNull();

        // 2. Nâng cấp lên đơn thật qua form Đặt lịch bàn giao
        var upgraded = await tradeService.CreateTradeOrderAsync(new CreateTradeOrderRequest
        {
            UserId = citizen.Id,
            PartnerId = recyclerUser.Id,
            EstimatedWeightKg = 10m,
            CategoryCode = "plastic",
            InitialPricePerKg = 7200m,
            ProposedTimeSlot = "10:00 - 12:00",
            PickupAddress = "Cổng phụ Khu CNC Hòa Lạc",
            IsInquiryDraft = false
        });

        upgraded.Id.Should().Be(inquiry.Id);
        upgraded.IsInquiryOnly.Should().BeFalse();
        upgraded.Status.Should().Be(OrderStatus.Pending.ToString());
        upgraded.AgreedPricePerKg.Should().BeNull();

        var handoverItem = await db.HandoverRequestItems.FirstAsync(i => i.RequestId == upgraded.Id);
        handoverItem.AgreedPricePerKg.Should().BeNull();

        var threadAfter = await chatService.GetOrderChatThreadAsync(upgraded.Id, citizen.Id, "Citizen");
        threadAfter.IsInquiryOnly.Should().BeFalse();
        threadAfter.AgreedPricePerKg.Should().BeNull();
        threadAfter.IsProposalLocked.Should().BeFalse();
    }

    [Fact]
    public async Task UTC025_CreateTradeOrderAndUpdateTimeSlot_ValidatesTodayTo30DaysAndRejectsPastOrOver30Days()
    {
        var db = TestDbContextFactory.Create();
        var citizen = new User
        {
            Id = Guid.NewGuid(),
            Username = "citizen_slot_check",
            Email = "citizen_slot@ecolink.vn",
            FullName = "Nguyễn Văn Tùng",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        var recyclerUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "collector_slot_check",
            Email = "collector_slot@ecolink.vn",
            FullName = "Vựa Thanh Xuân",
            Role = UserRole.Recycler,
            Status = UserStatus.Active
        };
        db.Users.AddRange(citizen, recyclerUser);
        db.CollectionPartners.Add(new CollectionPartner
        {
            Id = recyclerUser.Id,
            UserId = recyclerUser.Id,
            FacilityName = "Vựa Thanh Xuân",
            Address = "Thanh Xuân, Hà Nội",
            Latitude = 21.0,
            Longitude = 105.8,
            OperatingHours = "08:00 - 19:00",
            ContactPhone = "0988000111",
            IsVerified = true
        });
        await db.SaveChangesAsync();

        var tradeService = new TradeTransactionService(
            NullLogger<TradeTransactionService>.Instance,
            new GamificationService(NullLogger<GamificationService>.Instance),
            db);

        var todayLocal = (DateTime.UtcNow + TimeSpan.FromHours(7)).Date;
        var yesterdayStr = todayLocal.AddDays(-1).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var tooFarStr = todayLocal.AddDays(31).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var validDay30Str = todayLocal.AddDays(30).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var validDay15Str = todayLocal.AddDays(15).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        // 1. Chặn tạo đơn với ngày hẹn trong quá khứ
        var pastAct = () => tradeService.CreateTradeOrderAsync(new CreateTradeOrderRequest
        {
            UserId = citizen.Id,
            PartnerId = recyclerUser.Id,
            EstimatedWeightKg = 5m,
            CategoryCode = "plastic",
            ProposedTimeSlot = $"{yesterdayStr} (10:00 - 12:00)",
            PickupAddress = "Cổng phụ Hòa Lạc"
        });
        (await pastAct.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-NEG-04");

        // 2. Chặn tạo đơn vượt quá +30 ngày
        var overAct = () => tradeService.CreateTradeOrderAsync(new CreateTradeOrderRequest
        {
            UserId = citizen.Id,
            PartnerId = recyclerUser.Id,
            EstimatedWeightKg = 5m,
            CategoryCode = "plastic",
            ProposedTimeSlot = $"{tooFarStr} (10:00 - 12:00)",
            PickupAddress = "Cổng phụ Hòa Lạc"
        });
        (await overAct.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-NEG-04");

        // 3. Chấp nhận ngày +30 và đồng bộ ScheduledAt
        var created = await tradeService.CreateTradeOrderAsync(new CreateTradeOrderRequest
        {
            UserId = citizen.Id,
            PartnerId = recyclerUser.Id,
            EstimatedWeightKg = 5m,
            CategoryCode = "plastic",
            ProposedTimeSlot = $"{validDay30Str} (13:30 - 15:30)",
            PickupAddress = "Cổng phụ Hòa Lạc"
        });
        (created.ScheduledAt + TimeSpan.FromHours(7)).Date.Should().Be(todayLocal.AddDays(30));

        // 4. UpdateOrderTimeSlotAsync chặn quá khứ và đồng bộ ScheduledAt khi đổi sang ngày +15
        var pastUpdateAct = () => tradeService.UpdateOrderTimeSlotAsync(created.Id, citizen.Id, $"{yesterdayStr} (10:00 - 12:00)");
        (await pastUpdateAct.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-NEG-04");

        var updated = await tradeService.UpdateOrderTimeSlotAsync(created.Id, citizen.Id, $"{validDay15Str} (15:30 - 17:30)");
        (updated.ScheduledAt + TimeSpan.FromHours(7)).Date.Should().Be(todayLocal.AddDays(15));
    }

    [Fact]
    public async Task UTC026_SendProposalAsync_RejectsPastOrOver30Days_AndAllowsDay30()
    {
        var (sut, db, citizen, _, _, order) = await CreateSeededChatSutAsync(OrderStatus.Negotiating);
        var todayLocal = (DateTime.UtcNow + TimeSpan.FromHours(7)).Date;
        var yesterdayStr = todayLocal.AddDays(-1).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var day31Str = todayLocal.AddDays(31).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var day30Str = todayLocal.AddDays(30).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        var pastAct = () => sut.SendProposalAsync(order.Id, citizen.Id, "Citizen", new SendProposalRequest
        {
            ProposedPricePerKg = 7500m,
            ProposedWeightKg = 5m,
            ProposedTimeSlot = $"{yesterdayStr} (10:00 - 12:00)"
        });
        (await pastAct.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-NEG-04");

        var tooFarAct = () => sut.SendProposalAsync(order.Id, citizen.Id, "Citizen", new SendProposalRequest
        {
            ProposedPricePerKg = 7500m,
            ProposedWeightKg = 5m,
            ProposedTimeSlot = $"{day31Str} (10:00 - 12:00)"
        });
        (await tooFarAct.Should().ThrowAsync<BusinessRuleException>()).Which.RuleCode.Should().Be("BR-NEG-04");

        var (okMsg, _, _, _) = await sut.SendProposalAsync(order.Id, citizen.Id, "Citizen", new SendProposalRequest
        {
            ProposedPricePerKg = 7500m,
            ProposedWeightKg = 5m,
            ProposedTimeSlot = $"{day30Str} (13:30 - 15:30)"
        });
        okMsg.Proposal!.ProposedTimeSlot.Should().Be($"{day30Str} (13:30 - 15:30)");
        var refreshedOrder = await db.TradeOrders.FirstAsync(o => o.Id == order.Id);
        (refreshedOrder.ScheduledAt + TimeSpan.FromHours(7)).Date.Should().Be(todayLocal.AddDays(30));
    }

    [Fact]
    public async Task UTC027_GetUserOrdersAsync_EnrichesLastMessagePreviewUnreadCountAndOrdering()
    {
        var db = TestDbContextFactory.Create();
        var citizen = new User
        {
            Id = Guid.NewGuid(),
            Username = "citizen_list",
            Email = "citizen_list@ecolink.vn",
            FullName = "Nguyễn Văn Dân",
            Role = UserRole.Citizen,
            Status = UserStatus.Active
        };
        var recyclerUser = new User
        {
            Id = Guid.NewGuid(),
            Username = "recycler_list",
            Email = "recycler_list@ecolink.vn",
            FullName = "Cơ sở Minh Tâm",
            Role = UserRole.Recycler,
            Status = UserStatus.Active
        };
        var partner = new CollectionPartner
        {
            Id = recyclerUser.Id,
            UserId = recyclerUser.Id,
            FacilityName = "Cơ Sở Minh Tâm",
            Address = "Ngã Tư Hòa Lạc",
            Latitude = 21.0182,
            Longitude = 105.5345,
            OperatingHours = "07:30 - 19:00",
            ContactPhone = "0968112233",
            IsVerified = true
        };

        var now = DateTime.UtcNow;
        var quietOrder = new TradeOrder
        {
            Id = Guid.NewGuid(),
            UserId = citizen.Id,
            PartnerId = partner.Id,
            RecyclerUserId = recyclerUser.Id,
            Status = OrderStatus.Pending,
            ScheduledAt = now.AddDays(1),
            EstimatedWeightKg = 5m,
            CategoryCode = "plastic",
            InitialPricePerKg = 5000m,
            ProposedTimeSlot = "10:00 - 12:00",
            PickupAddress = "Điểm hẹn A",
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddHours(-2)
        };
        var activeOldOrder = new TradeOrder
        {
            Id = Guid.NewGuid(),
            UserId = citizen.Id,
            PartnerId = partner.Id,
            RecyclerUserId = recyclerUser.Id,
            Status = OrderStatus.Negotiating,
            ScheduledAt = now.AddDays(1),
            EstimatedWeightKg = 8m,
            CategoryCode = "paper",
            InitialPricePerKg = 4000m,
            ProposedTimeSlot = "14:00 - 16:00",
            PickupAddress = "Điểm hẹn B",
            CreatedAt = now.AddHours(-4),
            UpdatedAt = now.AddHours(-4)
        };

        db.Users.AddRange(citizen, recyclerUser);
        db.CollectionPartners.Add(partner);
        db.TradeOrders.AddRange(quietOrder, activeOldOrder);
        db.ChatMessages.AddRange(
            // quietOrder: tin của chính Citizen & tin System chưa đọc => UnreadCount cho Citizen vẫn là 0
            new ChatMessage
            {
                TradeOrderId = quietOrder.Id,
                SenderUserId = citizen.Id,
                SenderRole = "Citizen",
                MessageType = ChatMessageType.Text,
                MessageContent = "Chào vựa",
                IsRead = false,
                SentAt = now.AddHours(-2)
            },
            new ChatMessage
            {
                TradeOrderId = quietOrder.Id,
                SenderUserId = recyclerUser.Id,
                SenderRole = "System",
                MessageType = ChatMessageType.System,
                MessageContent = "Thông báo hệ thống",
                IsRead = false,
                SentAt = now.AddHours(-1)
            },
            // activeOldOrder: tạo cũ hơn nhưng có tin nhắn mới nhất cách đây 5 phút + 2 tin chưa đọc từ Recycler
            new ChatMessage
            {
                TradeOrderId = activeOldOrder.Id,
                SenderUserId = recyclerUser.Id,
                SenderRole = "Recycler",
                MessageType = ChatMessageType.Text,
                MessageContent = "Đã đọc trước đó",
                IsRead = true,
                SentAt = now.AddMinutes(-20)
            },
            new ChatMessage
            {
                TradeOrderId = activeOldOrder.Id,
                SenderUserId = recyclerUser.Id,
                SenderRole = "Recycler",
                MessageType = ChatMessageType.Image,
                AttachmentUrl = "https://cdn.ecolink.vn/waste.jpg",
                IsRead = false,
                SentAt = now.AddMinutes(-10)
            },
            new ChatMessage
            {
                TradeOrderId = activeOldOrder.Id,
                SenderUserId = recyclerUser.Id,
                SenderRole = "Recycler",
                MessageType = ChatMessageType.Text,
                MessageContent = "Hẹn bạn 14h nhé",
                IsRead = false,
                SentAt = now.AddMinutes(-5)
            }
        );
        await db.SaveChangesAsync();

        var tradeService = new TradeTransactionService(
            NullLogger<TradeTransactionService>.Instance,
            new GamificationService(NullLogger<GamificationService>.Instance),
            db);

        var result = (await tradeService.GetUserOrdersAsync(citizen.Id)).ToList();

        result.Should().HaveCount(2);
        result[0].Id.Should().Be(activeOldOrder.Id);
        result[0].UnreadCount.Should().Be(2);
        result[0].LastMessagePreview.Should().Be("Hẹn bạn 14h nhé");
        result[0].LastMessageSenderId.Should().Be(recyclerUser.Id);
        result[0].LastMessageSenderRole.Should().Be("Recycler");

        result[1].Id.Should().Be(quietOrder.Id);
        result[1].UnreadCount.Should().Be(0);
        result[1].LastMessagePreview.Should().Be("Thông báo hệ thống");
    }
}

