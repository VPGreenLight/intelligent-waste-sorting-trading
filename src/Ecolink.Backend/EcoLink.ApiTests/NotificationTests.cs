using System.Text.Json;

namespace EcoLink.ApiTests;

/// <summary>
/// Kiểm thử tích hợp (Integration Tests) cho Module M-15 Notifications (STT 169-171, SU-04).
/// </summary>
[Trait("Category", "Notifications")]
public class NotificationTests
{
    private static Task<string> CitizenToken() => Tokens.GetAsync("citizen");
    private static Task<string> AdminToken() => Tokens.GetAsync("admin");

    [Fact]
    public async Task NTF01_GetNotifications_Unauthenticated_Returns401()
    {
        var result = await Api.GetAsync(Routes.Notifications);
        result.ShouldBe(401);
    }

    [Fact]
    public async Task NTF02_GetNotifications_CitizenToken_ReturnsPagedEnvelopeWithUnreadCount()
    {
        var token = await CitizenToken();
        var result = (await Api.GetAsync(Routes.Notifications, token)).ShouldBe(200);

        Assert.True(result.Body.GetProperty("success").GetBoolean());
        Assert.Equal("MSG-NTF-LIST-OK", result.Body.GetProperty("messageCode").GetString());

        var data = result.Body.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.GetProperty("items").ValueKind);
        Assert.True(data.GetProperty("unreadCount").GetInt32() >= 0);
    }

    [Fact]
    public async Task NTF03_MarkNotificationRead_NonExistentId_Returns404()
    {
        var token = await CitizenToken();
        var missingId = Guid.NewGuid();
        var result = await Api.PatchJsonAsync($"{Routes.Notifications}/{missingId}/read", null, token);
        result.ShouldBe(404);
    }

    [Fact]
    public async Task NTF04_MarkAllNotificationsRead_Authenticated_Returns200AndZeroUnread()
    {
        var token = await AdminToken();
        var result = (await Api.PutJsonAsync(Routes.NotificationsReadAll, new { }, token)).ShouldBe(200);

        Assert.True(result.Body.GetProperty("success").GetBoolean());
        Assert.Equal("MSG-NTF-READ-ALL-OK", result.Body.GetProperty("messageCode").GetString());
        Assert.Equal(0, result.Body.GetProperty("data").GetProperty("unreadCount").GetInt32());
    }
}
