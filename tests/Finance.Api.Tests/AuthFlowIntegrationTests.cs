using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Finance.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace Finance.Api.Tests;

public sealed class AuthFlowIntegrationTests
{
    private const string RefreshCookieName = "finance_refresh_token";

    private static HttpClient CreateRawClient(FinanceApiFactory factory)
        => factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

    [Fact]
    public async Task Register_Web_CreatesUserAndDefaultCategories_SetsHttpOnlyCookie_AndOmitsRefreshToken()
    {
        using var factory = new FinanceApiFactory();
        using HttpClient client = CreateRawClient(factory);

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            name = "Lucas Dev",
            email = "  Lucas.Register@Exemplo.COM ",
            password = "SenhaForte123!",
            clientType = "web",
            deviceName = "Chrome Windows"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        string json = await response.Content.ReadAsStringAsync();
        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        Assert.Equal("lucas.register@exemplo.com", root.GetProperty("user").GetProperty("email").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("accessToken").GetString()));
        Assert.False(root.TryGetProperty("refreshToken", out _));
        Assert.False(root.TryGetProperty("refreshTokenExpiresAtUtc", out _));

        string setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains($"{RefreshCookieName}=", setCookie, StringComparison.Ordinal);
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/api/v1/auth", setCookie, StringComparison.OrdinalIgnoreCase);

        await factory.WithDbContextAsync(async db =>
        {
            int categoryCount = await db.Categories.CountAsync();
            Assert.Equal(12, categoryCount);
        });
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409_AndWeakPassword_Returns400()
    {
        using var factory = new FinanceApiFactory();
        using HttpClient client = CreateRawClient(factory);

        HttpResponseMessage weak = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            name = "Lucas",
            email = "weak@exemplo.com",
            password = "curta",
            clientType = "web"
        });
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        HttpResponseMessage first = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            name = "Lucas",
            email = "dup@exemplo.com",
            password = "SenhaForte123!",
            clientType = "web"
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        HttpResponseMessage duplicate = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            name = "Lucas 2",
            email = "DUP@EXEMPLO.COM",
            password = "SenhaForte123!",
            clientType = "web"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using JsonDocument dupDoc = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        Assert.Equal("email_already_registered", dupDoc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_ReturnIdentical401_WhileMobileAndWebDeliverProperly()
    {
        using var factory = new FinanceApiFactory();
        using HttpClient client = CreateRawClient(factory);

        await RegisterAsync(client, "user@exemplo.com", "SenhaForte123!", "mobile", "iPhone");

        HttpResponseMessage unknownEmail = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "naoexiste@exemplo.com",
            password = "SenhaForte123!",
            clientType = "web"
        });
        HttpResponseMessage wrongPassword = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "user@exemplo.com",
            password = "SenhaErrada999!",
            clientType = "web"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);

        using JsonDocument unknownDoc = JsonDocument.Parse(await unknownEmail.Content.ReadAsStringAsync());
        using JsonDocument wrongDoc = JsonDocument.Parse(await wrongPassword.Content.ReadAsStringAsync());
        Assert.Equal("invalid_credentials", unknownDoc.RootElement.GetProperty("code").GetString());
        Assert.Equal("invalid_credentials", wrongDoc.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            unknownDoc.RootElement.GetProperty("detail").GetString(),
            wrongDoc.RootElement.GetProperty("detail").GetString());

        HttpResponseMessage mobileLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "user@exemplo.com",
            password = "SenhaForte123!",
            clientType = "mobile",
            deviceName = "Expo Pixel"
        });
        Assert.Equal(HttpStatusCode.OK, mobileLogin.StatusCode);
        Assert.False(mobileLogin.Headers.Contains("Set-Cookie"));

        using JsonDocument mobileDoc = JsonDocument.Parse(await mobileLogin.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrWhiteSpace(mobileDoc.RootElement.GetProperty("accessToken").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(mobileDoc.RootElement.GetProperty("refreshToken").GetString()));
    }

    [Fact]
    public async Task Refresh_WebCsrfValidation_BlocksMissingHeaderOrDisallowedOrigin_AndAllowsValidWebRefresh()
    {
        using var factory = new FinanceApiFactory();
        using HttpClient client = CreateRawClient(factory);

        HttpResponseMessage reg = await RegisterAsync(client, "csrf@exemplo.com", "SenhaForte123!", "web", "Chrome");
        string cookie1 = ExtractRefreshCookie(reg);

        using var missingHeaderReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        missingHeaderReq.Headers.Add("Cookie", $"{RefreshCookieName}={cookie1}");
        missingHeaderReq.Headers.Add("Origin", FinanceApiFactory.AllowedWebOrigin);
        HttpResponseMessage missingHeaderRes = await client.SendAsync(missingHeaderReq);
        Assert.Equal(HttpStatusCode.Forbidden, missingHeaderRes.StatusCode);
        using (JsonDocument err = JsonDocument.Parse(await missingHeaderRes.Content.ReadAsStringAsync()))
        {
            Assert.Equal("csrf_validation_failed", err.RootElement.GetProperty("code").GetString());
        }

        using var evilOriginReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        evilOriginReq.Headers.Add("Cookie", $"{RefreshCookieName}={cookie1}");
        evilOriginReq.Headers.Add("X-Client-Type", "web");
        evilOriginReq.Headers.Add("Origin", "https://evil.example");
        HttpResponseMessage evilOriginRes = await client.SendAsync(evilOriginReq);
        Assert.Equal(HttpStatusCode.Forbidden, evilOriginRes.StatusCode);

        using var validReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        validReq.Headers.Add("Cookie", $"{RefreshCookieName}={cookie1}");
        validReq.Headers.Add("X-Client-Type", "web");
        validReq.Headers.Add("Origin", FinanceApiFactory.AllowedWebOrigin);
        HttpResponseMessage validRes = await client.SendAsync(validReq);
        Assert.Equal(HttpStatusCode.OK, validRes.StatusCode);

        string cookie2 = ExtractRefreshCookie(validRes);
        Assert.NotEqual(cookie1, cookie2);
    }

    [Fact]
    public async Task Refresh_ReusedMobileToken_RevokesEntireFamily_AndLogsReuseDetected()
    {
        using var factory = new FinanceApiFactory();
        using HttpClient client = CreateRawClient(factory);

        HttpResponseMessage reg = await RegisterAsync(client, "reuse@exemplo.com", "SenhaForte123!", "mobile", "Android");
        using JsonDocument regDoc = JsonDocument.Parse(await reg.Content.ReadAsStringAsync());
        string token1 = regDoc.RootElement.GetProperty("refreshToken").GetString()!;

        HttpResponseMessage rotated = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = token1,
            clientType = "mobile"
        });
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        using JsonDocument rotDoc = JsonDocument.Parse(await rotated.Content.ReadAsStringAsync());
        string token2 = rotDoc.RootElement.GetProperty("refreshToken").GetString()!;
        Assert.NotEqual(token1, token2);

        HttpResponseMessage replay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = token1,
            clientType = "mobile"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        using (JsonDocument replayDoc = JsonDocument.Parse(await replay.Content.ReadAsStringAsync()))
        {
            Assert.Equal("refresh_token_reuse_detected", replayDoc.RootElement.GetProperty("code").GetString());
        }

        HttpResponseMessage legitimateAfterReplay = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = token2,
            clientType = "mobile"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, legitimateAfterReplay.StatusCode);

        HttpResponseMessage freshLogin = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "reuse@exemplo.com",
            password = "SenhaForte123!",
            clientType = "mobile"
        });
        Assert.Equal(HttpStatusCode.OK, freshLogin.StatusCode);
        using JsonDocument freshDoc = JsonDocument.Parse(await freshLogin.Content.ReadAsStringAsync());
        string accessToken = freshDoc.RootElement.GetProperty("accessToken").GetString()!;

        using var auditReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/security/audit-logs?alertsOnly=true");
        auditReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage auditRes = await client.SendAsync(auditReq);
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);

        using JsonDocument auditDoc = JsonDocument.Parse(await auditRes.Content.ReadAsStringAsync());
        JsonElement items = auditDoc.RootElement.GetProperty("items");
        Assert.Contains(items.EnumerateArray(), item =>
            item.GetProperty("eventType").GetString() == "REFRESH_TOKEN_REUSE_DETECTED");
    }

    [Fact]
    public async Task Login_FiveFailures_LocksFor15Minutes_AndUnlocksAfterWindowExpires()
    {
        using var factory = new FinanceApiFactory();
        using HttpClient client = CreateRawClient(factory);

        await RegisterAsync(client, "lockout@exemplo.com", "SenhaForte123!", "mobile", "iPhone");

        for (int i = 0; i < 5; i++)
        {
            HttpResponseMessage fail = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = "lockout@exemplo.com",
                password = "SenhaErrada!",
                clientType = "mobile"
            });
            Assert.Equal(HttpStatusCode.Unauthorized, fail.StatusCode);
        }

        HttpResponseMessage duringLockout = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "lockout@exemplo.com",
            password = "SenhaForte123!",
            clientType = "mobile"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, duringLockout.StatusCode);

        factory.Clock.Advance(TimeSpan.FromMinutes(16));

        HttpResponseMessage afterLockout = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "lockout@exemplo.com",
            password = "SenhaForte123!",
            clientType = "mobile"
        });
        Assert.Equal(HttpStatusCode.OK, afterLockout.StatusCode);

        using JsonDocument okDoc = JsonDocument.Parse(await afterLockout.Content.ReadAsStringAsync());
        string accessToken = okDoc.RootElement.GetProperty("accessToken").GetString()!;

        await factory.WithDbContextAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == "lockout@exemplo.com");
            Assert.Equal(0, user.FailedLoginAttempts);
            Assert.Null(user.LockoutEndUtc);
        });

        using var auditReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/security/audit-logs?alertsOnly=true");
        auditReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        HttpResponseMessage auditRes = await client.SendAsync(auditReq);
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);

        using JsonDocument auditDoc = JsonDocument.Parse(await auditRes.Content.ReadAsStringAsync());
        Assert.Contains(auditDoc.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("eventType").GetString() == "LOGIN_LOCKED");
    }

    [Fact]
    public async Task Sessions_IdorProtection_Revocation_AndWebLogout_WorkEndToEnd()
    {
        using var factory = new FinanceApiFactory();
        using HttpClient client = CreateRawClient(factory);

        HttpResponseMessage userAWeb = await RegisterAsync(client, "alice@exemplo.com", "SenhaForte123!", "web", "Chrome Desktop");
        string aliceWebCookie = ExtractRefreshCookie(userAWeb);
        using JsonDocument aliceWebDoc = JsonDocument.Parse(await userAWeb.Content.ReadAsStringAsync());
        string aliceWebJwt = aliceWebDoc.RootElement.GetProperty("accessToken").GetString()!;

        HttpResponseMessage userAMobile = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "alice@exemplo.com",
            password = "SenhaForte123!",
            clientType = "mobile",
            deviceName = "iPhone 16"
        });
        Assert.Equal(HttpStatusCode.OK, userAMobile.StatusCode);
        using JsonDocument aliceMobDoc = JsonDocument.Parse(await userAMobile.Content.ReadAsStringAsync());
        string aliceMobileRefresh = aliceMobDoc.RootElement.GetProperty("refreshToken").GetString()!;

        HttpResponseMessage userBReg = await RegisterAsync(client, "bob@exemplo.com", "SenhaForte123!", "mobile", "Pixel");
        using JsonDocument bobDoc = JsonDocument.Parse(await userBReg.Content.ReadAsStringAsync());
        string bobJwt = bobDoc.RootElement.GetProperty("accessToken").GetString()!;

        using var listAliceReq = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/sessions");
        listAliceReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aliceWebJwt);
        HttpResponseMessage listAliceRes = await client.SendAsync(listAliceReq);
        Assert.Equal(HttpStatusCode.OK, listAliceRes.StatusCode);

        using JsonDocument sessionsDoc = JsonDocument.Parse(await listAliceRes.Content.ReadAsStringAsync());
        JsonElement[] sessions = sessionsDoc.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, sessions.Length);

        JsonElement webSession = Assert.Single(sessions, s => s.GetProperty("isCurrent").GetBoolean());
        JsonElement mobileSession = Assert.Single(sessions, s => !s.GetProperty("isCurrent").GetBoolean());
        Guid mobileSessionId = mobileSession.GetProperty("id").GetGuid();

        using var idorReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/auth/sessions/{mobileSessionId}");
        idorReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bobJwt);
        HttpResponseMessage idorRes = await client.SendAsync(idorReq);
        Assert.Equal(HttpStatusCode.NotFound, idorRes.StatusCode);

        using var revokeMobReq = new HttpRequestMessage(HttpMethod.Delete, $"/api/v1/auth/sessions/{mobileSessionId}");
        revokeMobReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", aliceWebJwt);
        HttpResponseMessage revokeMobRes = await client.SendAsync(revokeMobReq);
        Assert.Equal(HttpStatusCode.NoContent, revokeMobRes.StatusCode);

        HttpResponseMessage mobileRefreshAfterRevoke = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = aliceMobileRefresh,
            clientType = "mobile"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, mobileRefreshAfterRevoke.StatusCode);

        using var logoutReq = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        logoutReq.Headers.Add("Cookie", $"{RefreshCookieName}={aliceWebCookie}");
        logoutReq.Headers.Add("X-Client-Type", "web");
        logoutReq.Headers.Add("Origin", FinanceApiFactory.AllowedWebOrigin);
        HttpResponseMessage logoutRes = await client.SendAsync(logoutReq);
        Assert.Equal(HttpStatusCode.NoContent, logoutRes.StatusCode);

        string clearCookie = Assert.Single(logoutRes.Headers.GetValues("Set-Cookie"));
        Assert.Contains($"{RefreshCookieName}=;", clearCookie, StringComparison.Ordinal);

        using var refreshAfterLogout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        refreshAfterLogout.Headers.Add("Cookie", $"{RefreshCookieName}={aliceWebCookie}");
        refreshAfterLogout.Headers.Add("X-Client-Type", "web");
        refreshAfterLogout.Headers.Add("Origin", FinanceApiFactory.AllowedWebOrigin);
        HttpResponseMessage refreshAfterLogoutRes = await client.SendAsync(refreshAfterLogout);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshAfterLogoutRes.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoints_WithoutJwt_Return401_AndResponsesIncludeSecurityHeaders()
    {
        using var factory = new FinanceApiFactory();
        using HttpClient client = CreateRawClient(factory);

        HttpResponseMessage sessionsRes = await client.GetAsync("/api/v1/auth/sessions");
        HttpResponseMessage auditRes = await client.GetAsync("/api/v1/security/audit-logs");

        Assert.Equal(HttpStatusCode.Unauthorized, sessionsRes.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, auditRes.StatusCode);

        Assert.Equal("nosniff", Assert.Single(sessionsRes.Headers.GetValues("X-Content-Type-Options")));
        Assert.Equal("DENY", Assert.Single(sessionsRes.Headers.GetValues("X-Frame-Options")));
        Assert.Equal("no-referrer", Assert.Single(sessionsRes.Headers.GetValues("Referrer-Policy")));
        Assert.Contains("no-store", sessionsRes.Headers.CacheControl?.ToString() ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpResponseMessage> RegisterAsync(
        HttpClient client,
        string email,
        string password,
        string clientType,
        string deviceName)
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            name = "Usuário Teste",
            email,
            password,
            clientType,
            deviceName
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return response;
    }

    private static string ExtractRefreshCookie(HttpResponseMessage response)
    {
        string header = response.Headers.GetValues("Set-Cookie")
            .First(v => v.StartsWith($"{RefreshCookieName}=", StringComparison.Ordinal));

        int start = RefreshCookieName.Length + 1;
        int end = header.IndexOf(';', start);
        return end < 0 ? header[start..] : header[start..end];
    }
}
