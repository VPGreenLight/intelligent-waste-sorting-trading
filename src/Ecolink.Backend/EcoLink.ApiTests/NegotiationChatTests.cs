using System.Net.Http.Headers;
using System.Text.Json;

namespace EcoLink.ApiTests;

/// <summary>
/// Đường dẫn và hàm tiện ích dùng cho kiểm thử tích hợp Module 8:
/// Chat Thương lượng Thời gian thực trong Ứng dụng (In-App Real-Time Negotiation Chatbox: U-08, R-05)
/// tích hợp liên module với Module 6 (Handover Request Management) và Module 7 (Handover Receipt).
/// </summary>
public static class NegotiationRoutes
{
    public static string Messages(Guid orderId) => $"/api/v1/trade/orders/{orderId}/messages";
    public static string Attachments(Guid orderId) => $"/api/v1/trade/orders/{orderId}/messages/attachments";
    public static string Proposals(Guid orderId) => $"/api/v1/trade/orders/{orderId}/proposals";
    public static string RespondProposal(Guid orderId, Guid messageId) =>
        $"/api/v1/trade/orders/{orderId}/proposals/{messageId}/respond";
    public static string AcceptTerms(Guid orderId) => $"/api/v1/trade/orders/{orderId}/accept-terms";
    public static string MarkRead(Guid orderId) => $"/api/v1/trade/orders/{orderId}/messages/read";
}

/// <summary>
/// Bộ kiểm thử tích hợp (Integration Tests) cho Module 8: In-App Real-Time Negotiation Chatbox (U-08, R-05).
/// Kiểm chứng luồng tích hợp xuyên suốt: Khởi tạo đơn bàn giao (M6) -> Mở luồng chat &amp; kiểm soát quyền (BR-NEG-01)
/// -> Gửi tin nhắn văn bản &amp; đính kèm ảnh R2 -> Gửi Đề xuất giá/giờ &amp; Phản đề xuất ghi đè (`Superseded` - BR-NEG-03/04)
/// -> Chấp nhận Đề xuất khóa cứng `AgreedPricePerKg` &amp; `ConfirmedTimeSlot` vào đơn (BR-NEG-05) -> Đánh dấu đã đọc.
/// </summary>
[Trait("Category", "NegotiationChat")]
public class NegotiationChatTests
{
    private static async Task<Guid> CreateFreshPendingOrderAsync()
    {
        await HandoverHelper.EnsureRecyclerReadyAsync();
        await HandoverHelper.ClearCitizenActiveAsync();
        return await HandoverHelper.CreateAsync();
    }

    // ======================= NEG-01: Quyền truy cập phòng chat (BR-NEG-01) =======================

    [Fact]
    public async Task NEG01_GetChatThread_ParticipantsAllowed_OutsiderBlockedWithBrNeg01()
    {
        var orderId = await CreateFreshPendingOrderAsync();
        var citizenToken = await HandoverHelper.Citizen();
        var collectorToken = await HandoverHelper.Collector();

        // 1. Citizen (chủ đơn) xem thread -> 200 OK (MSG-NEG-THREAD-OK)
        var citizenThread = (await Api.GetAsync(NegotiationRoutes.Messages(orderId), citizenToken)).ShouldBe(200);
        Assert.Equal("MSG-NEG-THREAD-OK", citizenThread.MessageCode());
        Assert.Equal(orderId, citizenThread.Data().GetProperty("orderId").GetGuid());

        // 2. Collector (cơ sở nhận đơn) xem thread -> 200 OK
        var collectorThread = (await Api.GetAsync(NegotiationRoutes.Messages(orderId), collectorToken)).ShouldBe(200);
        Assert.Equal(orderId, collectorThread.Data().GetProperty("orderId").GetGuid());

        // 3. Một Citizen thứ ba không liên quan cố truy cập -> bị chặn (400/403)
        var (_, _, _, outsiderToken) = await OnboardingHelper.RegisterFreshCitizenAsync();
        var outsiderRes = await Api.GetAsync(NegotiationRoutes.Messages(orderId), outsiderToken);
        outsiderRes.ShouldBeIn(400, 403);
        Assert.False(outsiderRes.Body.GetProperty("success").GetBoolean());
    }

    // ======================= NEG-02: Gửi tin nhắn Text & Ảnh đính kèm =======================

    [Fact]
    public async Task NEG02_SendMessage_TextAndUploadedAttachment_PersistsInThreadAndIncrementsUnread()
    {
        var orderId = await CreateFreshPendingOrderAsync();
        var citizenToken = await HandoverHelper.Citizen();
        var collectorToken = await HandoverHelper.Collector();

        // 1. Upload ảnh đính kèm phế liệu qua POST /attachments -> 200 OK (MSG-NEG-UPLOAD-OK)
        using var form = new MultipartFormDataContent();
        var pngBytes = TestImages.Png(64, 64);
        var fileContent = new ByteArrayContent(pngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "file", "anh-phe-lieu-chat.png");

        var uploadRes = (await Api.SendAsync(HttpMethod.Post, NegotiationRoutes.Attachments(orderId), form, citizenToken))
            .ShouldBe(200);
        Assert.Equal("MSG-NEG-UPLOAD-OK", uploadRes.MessageCode());
        var attachmentUrl = uploadRes.Data().Str("attachmentUrl");
        Assert.False(string.IsNullOrWhiteSpace(attachmentUrl));

        // 2. Citizen gửi tin nhắn Text -> 201 Created (MSG-NEG-02)
        var textRes = (await ApiJson.PostAsync(NegotiationRoutes.Messages(orderId), new
        {
            messageType = "Text",
            messageContent = "Chào cơ sở, mình có khoảng 12kg nhựa PET đã ép phẳng.",
            attachmentUrl = (string?)null
        }, citizenToken)).ShouldBe(201);

        Assert.Equal("MSG-NEG-02", textRes.MessageCode());
        Assert.Equal("Text", textRes.Data().Str("messageType"));

        // 3. Citizen gửi tiếp tin nhắn Image đính kèm URL vừa tải lên -> 201 Created
        var imgRes = (await ApiJson.PostAsync(NegotiationRoutes.Messages(orderId), new
        {
            messageType = "Image",
            messageContent = "Ảnh thực tế bao nhựa của mình đây ạ",
            attachmentUrl
        }, citizenToken)).ShouldBe(201);

        Assert.Equal("Image", imgRes.Data().Str("messageType"));
        Assert.Equal(attachmentUrl, imgRes.Data().Str("attachmentUrl"));

        // 4. Collector mở phòng chat kiểm tra UnreadCount >= 2
        var collectorThread = (await Api.GetAsync(NegotiationRoutes.Messages(orderId), collectorToken)).ShouldBe(200);
        Assert.True(collectorThread.Data().GetProperty("unreadCount").GetInt32() >= 2);
    }

    // ======================= NEG-03: Validation nội dung tin nhắn =======================

    [Theory]
    [InlineData("empty-text")]
    [InlineData("too-long-text")]
    [InlineData("image-without-url")]
    [InlineData("unsupported-type")]
    public async Task NEG03_SendMessage_InvalidPayload_Returns400BadRequest(string scenario)
    {
        var orderId = await CreateFreshPendingOrderAsync();
        var citizenToken = await HandoverHelper.Citizen();

        object payload = scenario switch
        {
            "empty-text" => new { messageType = "Text", messageContent = "   ", attachmentUrl = (string?)null },
            "too-long-text" => new { messageType = "Text", messageContent = new string('x', 2005), attachmentUrl = (string?)null },
            "image-without-url" => new { messageType = "Image", messageContent = "Ảnh", attachmentUrl = (string?)"" },
            "unsupported-type" => new { messageType = "Audio", messageContent = "Voice", attachmentUrl = (string?)null },
            _ => throw new ArgumentException(scenario)
        };

        (await ApiJson.PostAsync(NegotiationRoutes.Messages(orderId), payload, citizenToken))
            .ShouldFailWith(400);
    }

    // ======================= NEG-04: Validation gửi Đề xuất (BR-NEG-03 & BR-NEG-04) =======================

    [Theory]
    [InlineData("zero-price")]
    [InlineData("over-max-price")]
    [InlineData("tiny-weight")]
    [InlineData("past-timeslot")]
    public async Task NEG04_SendProposal_InvalidPriceWeightOrPastTimeSlot_Returns400BadRequest(string scenario)
    {
        var orderId = await CreateFreshPendingOrderAsync();
        var citizenToken = await HandoverHelper.Citizen();

        var futureSlot = DateTime.UtcNow.AddHours(6).ToString("O");

        object payload = scenario switch
        {
            "zero-price" => new { proposedPricePerKg = 0m, proposedWeightKg = 5m, proposedTimeSlot = futureSlot, note = "Giá 0đ" },
            "over-max-price" => new { proposedPricePerKg = 15_000_000m, proposedWeightKg = 5m, proposedTimeSlot = futureSlot, note = "Giá vượt trần 10M" },
            "tiny-weight" => new { proposedPricePerKg = 8000m, proposedWeightKg = 0m, proposedTimeSlot = futureSlot, note = "Khối lượng <= 0kg" },
            "past-timeslot" => new { proposedPricePerKg = 8000m, proposedWeightKg = 5m, proposedTimeSlot = "   ", note = "Thiếu khung giờ đề xuất" },
            _ => throw new ArgumentException(scenario)
        };

        (await ApiJson.PostAsync(NegotiationRoutes.Proposals(orderId), payload, citizenToken))
            .ShouldFailWith(400);
    }

    // ======================= NEG-05: Phản đề xuất ghi đè đề xuất cũ (BR-NEG-03: Superseded) =======================

    [Fact]
    public async Task NEG05_SendProposal_AndCounterProposal_SupersedesPreviousActiveProposal_BrNeg04()
    {
        var orderId = await CreateFreshPendingOrderAsync();
        var citizenToken = await HandoverHelper.Citizen();
        var collectorToken = await HandoverHelper.Collector();

        // 1. Citizen gửi Đề xuất A (9.000đ/kg) -> 201 Created (MSG-NEG-01), ProposalStatus = Pending
        var slotA = DateTime.UtcNow.AddHours(5).ToString("O");
        var propARes = (await ApiJson.PostAsync(NegotiationRoutes.Proposals(orderId), new
        {
            proposedPricePerKg = 9000m,
            proposedWeightKg = 10m,
            proposedTimeSlot = slotA,
            note = "Mình đề xuất 9.000đ/kg vì nhựa đã bóc nhãn sạch."
        }, citizenToken)).ShouldBe(201);

        Assert.Equal("MSG-NEG-01", propARes.MessageCode());
        var propAId = propARes.Data().GetProperty("id").GetGuid();
        Assert.Equal("Pending", propARes.Data().GetProperty("proposal").Str("proposalStatus"));

        // 2. Collector gửi Phản đề xuất B (8.500đ/kg) -> Đề xuất A tự động chuyển sang Superseded
        var slotB = DateTime.UtcNow.AddHours(6).ToString("O");
        var propBRes = (await ApiJson.PostAsync(NegotiationRoutes.Proposals(orderId), new
        {
            proposedPricePerKg = 8500m,
            proposedWeightKg = 10m,
            proposedTimeSlot = slotB,
            note = "Cơ sở gửi lại mức giá 8.500đ/kg thu gom tận nơi ạ."
        }, collectorToken)).ShouldBe(201);

        var propBId = propBRes.Data().GetProperty("id").GetGuid();
        Assert.Equal("Pending", propBRes.Data().GetProperty("proposal").Str("proposalStatus"));

        // 3. Kiểm tra trên Thread: ActiveProposal hiện tại là B, còn A đã là Superseded
        var thread = (await Api.GetAsync(NegotiationRoutes.Messages(orderId), citizenToken)).ShouldBe(200).Data();
        Assert.Equal("Negotiating", thread.Str("orderStatus"));
        Assert.Equal(propBId, thread.GetProperty("activeProposal").GetProperty("id").GetGuid());

        var messages = thread.GetProperty("messages").GetProperty("items").EnumerateArray().ToList();
        var msgA = messages.First(m => m.GetProperty("id").GetGuid() == propAId);
        Assert.Equal("Superseded", msgA.GetProperty("proposal").Str("proposalStatus"));
    }

    // ======================= NEG-06: Chặn tự chấp nhận đề xuất của chính mình (BR-NEG-03) =======================

    [Fact]
    public async Task NEG06_RespondToProposal_SenderCannotRespondToOwnProposal_Returns400WithBrNeg04()
    {
        var orderId = await CreateFreshPendingOrderAsync();
        var citizenToken = await HandoverHelper.Citizen();

        var propRes = (await ApiJson.PostAsync(NegotiationRoutes.Proposals(orderId), new
        {
            proposedPricePerKg = 8500m,
            proposedWeightKg = 8m,
            proposedTimeSlot = DateTime.UtcNow.AddHours(4).ToString("O"),
            note = "Đề xuất của Citizen"
        }, citizenToken)).ShouldBe(201);

        var propId = propRes.Data().GetProperty("id").GetGuid();

        // Citizen tự bấm chấp nhận đề xuất do chính mình vừa gửi -> bị chặn bởi BR-NEG-03 (MSG-NEG-SELF-RESPONSE)
        (await ApiJson.PostAsync(NegotiationRoutes.RespondProposal(orderId, propId), new
        {
            isAccepted = true,
            responseNote = "Tự duyệt"
        }, citizenToken)).ShouldFailWith(400, "MSG-NEG-SELF-RESPONSE");
    }

    // ======================= NEG-07: Từ chối đề xuất & gửi lại đề xuất mới =======================

    [Fact]
    public async Task NEG07_RespondToProposal_CounterpartyRejects_MarksProposalRejectedAndAllowsNewProposal()
    {
        var orderId = await CreateFreshPendingOrderAsync();
        var citizenToken = await HandoverHelper.Citizen();
        var collectorToken = await HandoverHelper.Collector();

        var prop1 = (await ApiJson.PostAsync(NegotiationRoutes.Proposals(orderId), new
        {
            proposedPricePerKg = 9500m,
            proposedWeightKg = 10m,
            proposedTimeSlot = DateTime.UtcNow.AddHours(4).ToString("O"),
            note = "Đề xuất lần 1"
        }, citizenToken)).ShouldBe(201);
        var prop1Id = prop1.Data().GetProperty("id").GetGuid();

        // Collector từ chối Đề xuất 1 -> 200 OK (MSG-NEG-03), ProposalStatus = Rejected
        var rejectRes = (await ApiJson.PostAsync(NegotiationRoutes.RespondProposal(orderId, prop1Id), new
        {
            isAccepted = false,
            responseNote = "Mức giá này cao hơn trần thu mua hôm nay của cơ sở."
        }, collectorToken)).ShouldBe(200);

        Assert.Equal("MSG-NEG-03", rejectRes.MessageCode());
        Assert.Equal("Rejected", rejectRes.Data().Str("proposalStatus"));
        Assert.False(rejectRes.Data().GetProperty("isProposalLocked").GetBoolean());

        // Sau khi bị từ chối, Citizen gửi lại Đề xuất 2 hợp lý hơn -> 201 Created
        var prop2 = (await ApiJson.PostAsync(NegotiationRoutes.Proposals(orderId), new
        {
            proposedPricePerKg = 8200m,
            proposedWeightKg = 10m,
            proposedTimeSlot = DateTime.UtcNow.AddHours(5).ToString("O"),
            note = "Đề xuất lần 2 điều chỉnh giá"
        }, citizenToken)).ShouldBe(201);

        Assert.Equal("Pending", prop2.Data().GetProperty("proposal").Str("proposalStatus"));
    }

    // ======================= NEG-08: Chấp nhận đề xuất -> Khóa giá & giờ vào Đơn (BR-NEG-05) =======================

    [Fact]
    public async Task NEG08_RespondToProposal_CounterpartyAccepts_LocksAgreedPriceAndTimeSlotIntoOrder_BrNeg06()
    {
        var orderId = await CreateFreshPendingOrderAsync();
        var citizenToken = await HandoverHelper.Citizen();
        var collectorToken = await HandoverHelper.Collector();

        var proposedSlot = DateTime.UtcNow.AddHours(5);
        var propRes = (await ApiJson.PostAsync(NegotiationRoutes.Proposals(orderId), new
        {
            proposedPricePerKg = 8800m,
            proposedWeightKg = 12m,
            proposedTimeSlot = proposedSlot.ToString("O"),
            note = "Chốt giá 8.800đ/kg cho 12kg nhựa"
        }, citizenToken)).ShouldBe(201);
        var propId = propRes.Data().GetProperty("id").GetGuid();

        // 1. Collector chấp nhận Đề xuất -> 200 OK (MSG-NEG-03)
        var acceptRes = (await ApiJson.PostAsync(NegotiationRoutes.RespondProposal(orderId, propId), new
        {
            isAccepted = true,
            responseNote = "Đồng ý mức giá 8.800đ/kg, cơ sở sẽ qua đúng giờ hẹn."
        }, collectorToken)).ShouldBe(200);

        Assert.Equal("MSG-NEG-03", acceptRes.MessageCode());
        var acceptData = acceptRes.Data();
        Assert.Equal("Accepted", acceptData.Str("proposalStatus"));
        Assert.Equal("Accepted", acceptData.Str("orderStatus"));
        Assert.True(acceptData.GetProperty("isProposalLocked").GetBoolean());
        Assert.Equal(8800m, acceptData.GetProperty("agreedPricePerKg").GetDecimal());
        Assert.Equal(12m, acceptData.GetProperty("confirmedWeightKg").GetDecimal());

        // 2. Sau khi đã Accepted, mọi nỗ lực gửi thêm Proposal mới đều bị khóa bởi BR-NEG-02
        (await ApiJson.PostAsync(NegotiationRoutes.Proposals(orderId), new
        {
            proposedPricePerKg = 9000m,
            proposedWeightKg = 12m,
            proposedTimeSlot = DateTime.UtcNow.AddHours(6).ToString("O"),
            note = "Đổi ý sau khi đã chốt"
        }, citizenToken)).ShouldFailWith(400, "MSG-NEG-PROPOSAL-LOCKED");

        // 3. Kiểm tra liên Module 6 & 7: Đơn bàn giao đã chuyển sang Accepted và cho phép xem trước biên nhận
        var orderDetail = (await Api.GetAsync(HandoverRoutes.One(orderId), citizenToken)).ShouldBe(200).Data();
        Assert.Equal("Accepted", orderDetail.Str("status"));
    }

    // ======================= NEG-09: Cơ sở chấp nhận trực tiếp điều khoản ban đầu (accept-terms) =======================

    [Fact]
    public async Task NEG09_AcceptOrderTerms_DirectAcceptByRecyclerWithoutProposal_LocksNegotiationStatusAccepted()
    {
        var orderId = await CreateFreshPendingOrderAsync();
        var collectorToken = await HandoverHelper.Collector();

        var res = (await ApiJson.PostAsync(NegotiationRoutes.AcceptTerms(orderId), null, collectorToken))
            .ShouldBe(200);

        Assert.Equal("MSG-NEG-03", res.MessageCode());
        Assert.Equal("Accepted", res.Data().Str("orderStatus"));
        Assert.True(res.Data().GetProperty("isProposalLocked").GetBoolean());
        Assert.True(res.Data().GetProperty("agreedPricePerKg").GetDecimal() > 0m);
    }

    // ======================= NEG-10: Đánh dấu đã đọc toàn bộ tin nhắn (PUT /messages/read) =======================

    [Fact]
    public async Task NEG10_MarkMessagesAsRead_UpdatesUnreadCountToZeroForRecipient()
    {
        var orderId = await CreateFreshPendingOrderAsync();
        var citizenToken = await HandoverHelper.Citizen();
        var collectorToken = await HandoverHelper.Collector();

        // Citizen gửi 2 tin nhắn cho Collector
        (await ApiJson.PostAsync(NegotiationRoutes.Messages(orderId), new
        {
            messageType = "Text",
            messageContent = "Tin nhắn số 1",
            attachmentUrl = (string?)null
        }, citizenToken)).ShouldBe(201);

        (await ApiJson.PostAsync(NegotiationRoutes.Messages(orderId), new
        {
            messageType = "Text",
            messageContent = "Tin nhắn số 2",
            attachmentUrl = (string?)null
        }, citizenToken)).ShouldBe(201);

        // Collector gọi PUT /messages/read -> 200 OK (MSG-NEG-READ-OK)
        var readRes = (await ApiJson.PutAsync(NegotiationRoutes.MarkRead(orderId), null, collectorToken))
            .ShouldBe(200);
        Assert.Equal("MSG-NEG-READ-OK", readRes.MessageCode());
        Assert.True(readRes.Data().GetProperty("markedCount").GetInt32() >= 2);

        // Kiểm tra lại Thread của Collector -> UnreadCount == 0
        var afterRead = (await Api.GetAsync(NegotiationRoutes.Messages(orderId), collectorToken)).ShouldBe(200).Data();
        Assert.Equal(0, afterRead.GetProperty("unreadCount").GetInt32());
    }
}
