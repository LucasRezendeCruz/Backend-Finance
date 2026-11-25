using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Finance.Api.Tests.Infrastructure;

internal static class ApiTestClientExtensions
{
    public static async Task<HttpClient> CreateAuthenticatedMobileClientAsync(
        this FinanceApiFactory factory,
        string email,
        string name = "Usuário Teste")
    {
        HttpClient client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            name,
            email,
            password = "SenhaForte123!",
            clientType = "mobile",
            deviceName = "Test Runner"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        string accessToken = doc.RootElement.GetProperty("accessToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return client;
    }

    public static async Task<Guid> CreateAccountAsync(
        this HttpClient client,
        string name,
        string type,
        decimal initialBalance,
        string color = "#ff5a36")
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name,
            type,
            initialBalance,
            color
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        using JsonDocument doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    public static async Task<Guid> CreateCreditCardAsync(
        this HttpClient client,
        string name,
        decimal creditLimit,
        int closingDay,
        int dueDay,
        Guid? defaultAccountId = null)
    {
        HttpResponseMessage res = await client.PostAsJsonAsync("/api/v1/credit-cards", new
        {
            name,
            creditLimit,
            closingDay,
            dueDay,
            defaultAccountId,
            color = "#18181b"
        });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        using JsonDocument doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    public static async Task<JsonDocument> ReadJsonAsync(this HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync());
}
