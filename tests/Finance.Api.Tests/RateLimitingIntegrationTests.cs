using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Finance.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Finance.Api.Tests;

public sealed class RateLimitingIntegrationTests
{
    [Fact]
    public async Task AuthStrictPolicy_ExceedingLimit_Returns429WithRetryAfterAndProblemDetails()
    {
        using var factory = new FinanceApiFactory(authStrictPermitLimit: 3, authStrictWindowSeconds: 60);
        using HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        for (int i = 0; i < 3; i++)
        {
            HttpResponseMessage attempt = await client.PostAsJsonAsync("/api/v1/auth/login", new
            {
                email = "nobody@exemplo.com",
                password = "Password123!",
                clientType = "mobile"
            });

            Assert.Equal(HttpStatusCode.Unauthorized, attempt.StatusCode);
        }

        HttpResponseMessage throttled = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "nobody@exemplo.com",
            password = "Password123!",
            clientType = "mobile"
        });

        Assert.Equal(HttpStatusCode.TooManyRequests, throttled.StatusCode);
        Assert.True(throttled.Headers.Contains("Retry-After"));

        using JsonDocument doc = JsonDocument.Parse(await throttled.Content.ReadAsStringAsync());
        Assert.Equal(429, doc.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("rate_limit_exceeded", doc.RootElement.GetProperty("code").GetString());
    }
}
