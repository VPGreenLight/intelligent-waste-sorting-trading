using System.Text.Json;

namespace EcoLink.ApiTests;

/// <summary>Test cho nhóm Auth (+ /health).</summary>
[Trait("Category", "Auth")]
public class AuthTests
{
    // ======================= Health =======================
    [Fact]
    public async Task Health_ReturnsOk() =>
        (await Api.GetAsync(Routes.Health)).ShouldBe(200);

    // ======================= Register =======================
    [Fact]
    public async Task Register_WithValidData_Succeeds()
    {
        var payload = TestData.ValidRegister();
        var result = (await Api.PostJsonAsync(Routes.Register, payload)).ShouldBeIn(200, 201);

        Assert.True(result.Body.GetProperty("success").GetBoolean());
        if (Api.Unwrap(result.Body).TryGetProperty("user", out var user) && user.ValueKind == JsonValueKind.Object)
        {
            Assert.Equal(payload.Str("username"), user.GetProperty("username").GetString());
            Assert.Equal(payload.Str("email"), user.GetProperty("email").GetString(), ignoreCase: true);
            Assert.True(Guid.TryParse(user.GetProperty("id").GetString(), out _));
        }
    }

    [Fact]
    public async Task Register_ResponseDoesNotLeakPassword()
    {
        var payload = TestData.ValidRegister();
        var result = (await Api.PostJsonAsync(Routes.Register, payload)).ShouldBeIn(200, 201);
        Assert.DoesNotContain(payload.Str("password"), result.Raw);
    }

    [Fact]
    public async Task Register_WithoutOptionalPhoneNumber_Succeeds()
    {
        var payload = TestData.ValidRegister();
        payload.Remove("phoneNumber");
        var result = (await Api.PostJsonAsync(Routes.Register, payload)).ShouldBeIn(200, 201);
        Assert.True(result.Body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Register_WithMaxLengthFields_Succeeds()
    {
        // username 50, password 100, fullName 100 ký tự đều nằm trong giới hạn cho phép
        var payload = TestData.ValidRegister();
        payload["username"] = $"qa_{TestData.Unique()}".PadRight(50, 'x');
        payload["password"] = "Aa1!" + new string('x', 96);
        payload["fullName"] = new string('N', 100);

        Assert.Equal(50, payload.Str("username").Length);
        Assert.Equal(100, payload.Str("password").Length);
        (await Api.PostJsonAsync(Routes.Register, payload)).ShouldBeIn(200, 201);
    }

    [Fact]
    public async Task Register_WithMinLengthPassword_Succeeds()
    {
        var payload = TestData.ValidRegister();
        payload["password"] = "abc123";
        (await Api.PostJsonAsync(Routes.Register, payload)).ShouldBeIn(200, 201);
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_Fails()
    {
        var existing = await TestData.RegisteredUserAsync();
        var payload = TestData.ValidRegister();
        payload["username"] = existing.Str("username");

        var result = (await Api.PostJsonAsync(Routes.Register, payload)).ShouldBe(400);
        Assert.False(result.Body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Fails()
    {
        var existing = await TestData.RegisteredUserAsync();
        var payload = TestData.ValidRegister();
        payload["email"] = existing.Str("email");

        var result = (await Api.PostJsonAsync(Routes.Register, payload)).ShouldBe(400);
        Assert.False(result.Body.GetProperty("success").GetBoolean());
    }

    [Theory]
    [InlineData("username-too-short-2")]
    [InlineData("username-too-long-51")]
    [InlineData("email-invalid-format")]
    [InlineData("email-empty")]
    [InlineData("password-too-short-5")]
    [InlineData("password-too-long-101")]
    [InlineData("fullname-too-long-101")]
    [InlineData("missing-username")]
    [InlineData("missing-email")]
    [InlineData("missing-password")]
    [InlineData("missing-fullname")]
    public async Task Register_WithInvalidData_Returns400(string scenario)
    {
        var p = TestData.ValidRegister();
        switch (scenario)
        {
            case "username-too-short-2": p["username"] = "ab"; break;
            case "username-too-long-51": p["username"] = new string('a', 51); break;
            case "email-invalid-format": p["email"] = "not-an-email"; break;
            case "email-empty": p["email"] = ""; break;
            case "password-too-short-5": p["password"] = "12345"; break;
            case "password-too-long-101": p["password"] = new string('p', 101); break;
            case "fullname-too-long-101": p["fullName"] = new string('x', 101); break;
            case "missing-username": p.Remove("username"); break;
            case "missing-email": p.Remove("email"); break;
            case "missing-password": p.Remove("password"); break;
            case "missing-fullname": p.Remove("fullName"); break;
            default: throw new ArgumentException(scenario);
        }

        (await Api.PostJsonAsync(Routes.Register, p)).ShouldBe(400);
    }

    [Fact]
    public async Task Register_WithEmptyJsonObject_Returns400() =>
        (await Api.PostRawJsonAsync(Routes.Register, "{}")).ShouldBe(400);

    [Fact]
    public async Task Register_WithoutBody_Returns400Or415() =>
        (await Api.PostEmptyAsync(Routes.Register)).ShouldBeIn(400, 415);

    [Fact]
    public async Task Register_ThenLogin_WorksWithUsernameAndEmail()
    {
        var payload = TestData.ValidRegister();
        (await Api.PostJsonAsync(Routes.Register, payload)).ShouldBeIn(200, 201);

        foreach (var identifier in new[] { payload.Str("username"), payload.Str("email") })
        {
            var login = (await Api.PostJsonAsync(Routes.Login,
                new { usernameOrEmail = identifier, password = payload.Str("password") })).ShouldBe(200);
            Assert.NotNull(Api.ExtractToken(login.Body));
        }
    }

    // ======================= Login =======================
    [Theory]
    [MemberData(nameof(RoleData))]
    public async Task Login_WithDemoAccount_Succeeds(string role)
    {
        var (user, password) = Tokens.Accounts[role];
        var result = (await Api.PostJsonAsync(Routes.Login,
            new { usernameOrEmail = user, password })).ShouldBe(200);

        Assert.True(result.Body.GetProperty("success").GetBoolean());
        var body = Api.Unwrap(result.Body); // user, token nằm trong "data"
        Assert.NotNull(Api.ExtractToken(result.Body));
        Assert.Equal(user, body.GetProperty("user").GetProperty("username").GetString());
        Assert.True(Guid.TryParse(body.GetProperty("user").GetProperty("id").GetString(), out _));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("user").GetProperty("role").GetString()));
    }

    [Fact]
    public async Task Login_ReturnsExpectedResponseShape()
    {
        var (user, password) = Tokens.Accounts["citizen"];
        var result = (await Api.PostJsonAsync(Routes.Login,
            new { usernameOrEmail = user, password })).ShouldBe(200);

        var u = Api.Unwrap(result.Body).GetProperty("user");
        Assert.Equal(JsonValueKind.Number, u.GetProperty("currentGreenPoints").ValueKind);
        Assert.Equal(JsonValueKind.Number, u.GetProperty("treeLevel").ValueKind);
        Assert.Equal(JsonValueKind.Number, u.GetProperty("totalRecycledKg").ValueKind);
        Assert.True(u.GetProperty("isGoogleAuth").ValueKind is JsonValueKind.True or JsonValueKind.False);
        Assert.DoesNotContain(password, result.Raw);
    }

    [Fact]
    public async Task Login_TokenExpiry_IsInTheFuture()
    {
        var (user, password) = Tokens.Accounts["citizen"];
        var result = (await Api.PostJsonAsync(Routes.Login,
            new { usernameOrEmail = user, password })).ShouldBe(200);

        if (!Api.Unwrap(result.Body).TryGetProperty("expiresAt", out var exp) || exp.ValueKind != JsonValueKind.String)
            return; // response không có expiresAt -> bỏ qua

        Assert.True(DateTimeOffset.Parse(exp.GetString()!) > DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Fails()
    {
        var result = (await Api.PostJsonAsync(Routes.Login,
            new { usernameOrEmail = "citizen", password = "wrong-password" })).ShouldBe(400);

        Assert.False(result.Body.GetProperty("success").GetBoolean());
        Assert.Null(Api.ExtractToken(result.Body));
    }

    [Fact]
    public async Task Login_WithUnknownUser_Fails()
    {
        var result = (await Api.PostJsonAsync(Routes.Login,
            new { usernameOrEmail = $"nonexistent_{TestData.Unique()}", password = "whatever123" })).ShouldBe(400);
        Assert.False(result.Body.GetProperty("success").GetBoolean());
    }

    [Fact]
    public async Task Login_ErrorMessage_DoesNotRevealWhichAccountsExist()
    {
        // Thông báo lỗi của "sai mật khẩu" và "không tồn tại user" phải giống nhau
        var wrongPassword = await Api.PostJsonAsync(Routes.Login,
            new { usernameOrEmail = "citizen", password = "wrong" });
        var wrongUser = await Api.PostJsonAsync(Routes.Login,
            new { usernameOrEmail = $"nouser_{TestData.Unique()}", password = "wrong" });

        string? Msg(ApiResult r) =>
            r.Body.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;

        Assert.Equal(Msg(wrongPassword), Msg(wrongUser));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"usernameOrEmail\":\"citizen\"}")]
    [InlineData("{\"password\":\"citizen123\"}")]
    [InlineData("{\"usernameOrEmail\":\"\",\"password\":\"citizen123\"}")]
    [InlineData("{\"usernameOrEmail\":\"citizen\",\"password\":\"\"}")]
    public async Task Login_WithInvalidBody_Returns400(string rawJson) =>
        (await Api.PostRawJsonAsync(Routes.Login, rawJson)).ShouldBe(400);

    [Fact]
    public async Task Login_WithoutBody_Returns400Or415() =>
        (await Api.PostEmptyAsync(Routes.Login)).ShouldBeIn(400, 415);

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("\" OR \"\"=\"")]
    [InlineData("<script>alert(1)</script>")]
    public async Task Login_WithInjectionPayload_IsRejected(string identifier)
    {
        var result = (await Api.PostJsonAsync(Routes.Login,
            new { usernameOrEmail = identifier, password = "x' OR '1'='1" })).ShouldBe(400);
        Assert.Null(Api.ExtractToken(result.Body));
    }

    // ======================= Me =======================
    [Theory]
    [MemberData(nameof(RoleData))]
    public async Task Me_WithValidToken_ReturnsCurrentUser(string role)
    {
        var token = await Tokens.GetAsync(role);
        var result = (await Api.GetAsync(Routes.Me, query: new Dictionary<string, string> { ["token"] = token }))
            .ShouldBe(200);

        var body = Api.Unwrap(result.Body);
        Assert.Equal(Tokens.Accounts[role].User, body.GetProperty("username").GetString());
        Assert.True(Guid.TryParse(body.GetProperty("id").GetString(), out _));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("role").GetString()));
        Assert.Equal(JsonValueKind.Number, body.GetProperty("currentGreenPoints").ValueKind);
    }

    [Fact]
    public async Task Me_ForNewlyRegisteredUser_ReturnsThatUser()
    {
        var user = await TestData.RegisteredUserAsync();
        var login = (await Api.PostJsonAsync(Routes.Login,
            new { usernameOrEmail = user.Str("username"), password = user.Str("password") })).ShouldBe(200);

        var token = Api.ExtractToken(login.Body)!;
        var me = (await Api.GetAsync(Routes.Me, query: new Dictionary<string, string> { ["token"] = token }))
            .ShouldBe(200);
        Assert.Equal(user.Str("username"), Api.Unwrap(me.Body).GetProperty("username").GetString());
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401() =>
        (await Api.GetAsync(Routes.Me)).ShouldBe(401);

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("aaa.bbb.ccc")]
    public async Task Me_WithInvalidToken_Returns401(string token) =>
        (await Api.GetAsync(Routes.Me, query: new Dictionary<string, string> { ["token"] = token })).ShouldBe(401);

    [Fact]
    public async Task Me_WithTamperedToken_Returns401()
    {
        var token = await Tokens.GetAsync("citizen");
        var tampered = token[..^4] + (token.EndsWith("AAAA") ? "BBBB" : "AAAA");
        (await Api.GetAsync(Routes.Me, query: new Dictionary<string, string> { ["token"] = tampered }))
            .ShouldBe(401);
    }

    // ======================= demo-users, vietqr-token =======================
    [Fact]
    public async Task DemoUsers_ReturnsNonEmptyJson()
    {
        var result = (await Api.GetAsync(Routes.DemoUsers)).ShouldBe(200);
        Assert.True(TestData.IsNonEmpty(result.Body), "demo-users returned an empty result");
    }

    [Fact]
    public async Task DemoUsers_DoesNotExposePasswordHash()
    {
        var raw = (await Api.GetAsync(Routes.DemoUsers)).ShouldBe(200).Raw.ToLowerInvariant();
        Assert.DoesNotContain("passwordhash", raw);
        Assert.DoesNotContain("password_hash", raw);
    }

    // Endpoint này dành cho đối tác VietQR và dùng Basic Auth (Client ID + Client Secret),
    // không dùng JWT của người dùng -> Bearer token hoặc không có token đều phải bị từ chối.
    [Fact]
    public async Task VietQrToken_WithoutCredentials_Returns401() =>
        (await Api.PostEmptyAsync(Routes.VietQrToken)).ShouldBe(401);

    [Fact]
    public async Task VietQrToken_WithUserBearerToken_Returns401()
    {
        var token = await Tokens.GetAsync("citizen");
        (await Api.PostEmptyAsync(Routes.VietQrToken, token)).ShouldBe(401);
    }

    public static TheoryData<string> RoleData => Tokens.Roles;
}
