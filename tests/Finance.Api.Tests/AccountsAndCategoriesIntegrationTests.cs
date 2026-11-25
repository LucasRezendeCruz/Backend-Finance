using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Finance.Api.Tests.Infrastructure;

namespace Finance.Api.Tests;

public sealed class AccountsAndCategoriesIntegrationTests
{
    [Fact]
    public async Task Accounts_Crud_Archive_AuditLog_AndIdorProtection_Work()
    {
        using var factory = new FinanceApiFactory();
        using HttpClient alice = await factory.CreateAuthenticatedMobileClientAsync("alice-acc@exemplo.com", "Alice");
        using HttpClient bob = await factory.CreateAuthenticatedMobileClientAsync("bob-acc@exemplo.com", "Bob");

        HttpResponseMessage createRes = await alice.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "Conta Corrente",
            type = "CHECKING",
            initialBalance = 1500.50m,
            color = "#ff5a36"
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        using JsonDocument createdDoc = await createRes.ReadJsonAsync();
        Guid accountId = createdDoc.RootElement.GetProperty("id").GetGuid();
        Assert.Equal(1500.50m, createdDoc.RootElement.GetProperty("currentBalance").GetDecimal());
        Assert.Equal("CHECKING", createdDoc.RootElement.GetProperty("type").GetString());

        HttpResponseMessage updateRes = await alice.PutAsJsonAsync($"/api/v1/accounts/{accountId}", new
        {
            name = "Reserva Principal",
            type = "SAVINGS",
            color = "#22c55e"
        });
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);
        using JsonDocument updatedDoc = await updateRes.ReadJsonAsync();
        Assert.Equal("Reserva Principal", updatedDoc.RootElement.GetProperty("name").GetString());
        Assert.Equal("SAVINGS", updatedDoc.RootElement.GetProperty("type").GetString());
        Assert.Equal(1500.50m, updatedDoc.RootElement.GetProperty("currentBalance").GetDecimal());

        HttpResponseMessage idorUpdate = await bob.PutAsJsonAsync($"/api/v1/accounts/{accountId}", new
        {
            name = "Hack",
            type = "CASH"
        });
        Assert.Equal(HttpStatusCode.NotFound, idorUpdate.StatusCode);

        HttpResponseMessage archiveRes = await alice.PatchAsJsonAsync($"/api/v1/accounts/{accountId}/archive", new
        {
            isArchived = true
        });
        Assert.Equal(HttpStatusCode.OK, archiveRes.StatusCode);

        HttpResponseMessage activeListRes = await alice.GetAsync("/api/v1/accounts");
        using JsonDocument activeListDoc = await activeListRes.ReadJsonAsync();
        Assert.Empty(activeListDoc.RootElement.EnumerateArray());

        HttpResponseMessage allListRes = await alice.GetAsync("/api/v1/accounts?includeArchived=true");
        using JsonDocument allListDoc = await allListRes.ReadJsonAsync();
        Assert.Single(allListDoc.RootElement.EnumerateArray());

        HttpResponseMessage auditRes = await alice.GetAsync("/api/v1/security/audit-logs");
        using JsonDocument auditDoc = await auditRes.ReadJsonAsync();
        Assert.Contains(auditDoc.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("eventType").GetString() == "ACCOUNT_ARCHIVED");
    }

    [Fact]
    public async Task Categories_Crud_DuplicateConflict_AndIdorProtection_Work()
    {
        using var factory = new FinanceApiFactory();
        using HttpClient alice = await factory.CreateAuthenticatedMobileClientAsync("alice-cat@exemplo.com", "Alice");
        using HttpClient bob = await factory.CreateAuthenticatedMobileClientAsync("bob-cat@exemplo.com", "Bob");

        HttpResponseMessage incomeListRes = await alice.GetAsync("/api/v1/categories?type=INCOME");
        Assert.Equal(HttpStatusCode.OK, incomeListRes.StatusCode);
        using JsonDocument incomeDoc = await incomeListRes.ReadJsonAsync();
        Assert.Equal(4, incomeDoc.RootElement.GetArrayLength());

        HttpResponseMessage createRes = await alice.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "Viagens",
            type = "EXPENSE",
            icon = "plane",
            color = "#3b82f6"
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        using JsonDocument createdDoc = await createRes.ReadJsonAsync();
        Guid catId = createdDoc.RootElement.GetProperty("id").GetGuid();

        HttpResponseMessage duplicateRes = await alice.PostAsJsonAsync("/api/v1/categories", new
        {
            name = "viagens",
            type = "EXPENSE"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateRes.StatusCode);

        HttpResponseMessage idorRes = await bob.PatchAsJsonAsync($"/api/v1/categories/{catId}/archive", new
        {
            isArchived = true
        });
        Assert.Equal(HttpStatusCode.NotFound, idorRes.StatusCode);

        HttpResponseMessage archiveRes = await alice.PatchAsJsonAsync($"/api/v1/categories/{catId}/archive", new
        {
            isArchived = true
        });
        Assert.Equal(HttpStatusCode.OK, archiveRes.StatusCode);
    }
}
