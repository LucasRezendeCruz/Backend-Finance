using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Finance.Api.Tests.Infrastructure;

namespace Finance.Api.Tests;

public sealed class DashboardIntegrationTests
{
    [Fact]
    public async Task MonthlySummary_ExcludesTransfersAndInvoicePayments_IncludesCardExpenses_AndGroupsCategories()
    {
        using var factory = new FinanceApiFactory();
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        using HttpClient alice = await factory.CreateAuthenticatedMobileClientAsync("alice-dash@exemplo.com", "Alice");

        Guid acc1 = await alice.CreateAccountAsync("Conta A", "CHECKING", 1000.00m);
        Guid acc2 = await alice.CreateAccountAsync("Conta B", "SAVINGS", 500.00m);
        Guid archivedAcc = await alice.CreateAccountAsync("Conta Antiga", "CASH", 9999.00m);
        await alice.PatchAsJsonAsync($"/api/v1/accounts/{archivedAcc}/archive", new { isArchived = true });

        Guid cardId = await alice.CreateCreditCardAsync("Cartão", 5000.00m, closingDay: 18, dueDay: 25, defaultAccountId: acc1);

        HttpResponseMessage expCatRes = await alice.GetAsync("/api/v1/categories?type=EXPENSE");
        using JsonDocument expCatDoc = await expCatRes.ReadJsonAsync();
        Guid foodCatId = expCatDoc.RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == "Alimentação").GetProperty("id").GetGuid();
        Guid leisureCatId = expCatDoc.RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == "Lazer").GetProperty("id").GetGuid();

        HttpResponseMessage incCatRes = await alice.GetAsync("/api/v1/categories?type=INCOME");
        using JsonDocument incCatDoc = await incCatRes.ReadJsonAsync();
        Guid salaryCatId = incCatDoc.RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == "Salário").GetProperty("id").GetGuid();

        // Receita confirmada R$ 4000 + pendente R$ 500
        await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = acc1,
            categoryId = salaryCatId,
            type = "INCOME",
            status = "CONFIRMED",
            description = "Salário",
            amount = 4000.00m,
            transactionDate = "2026-11-05"
        });
        await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = acc1,
            categoryId = salaryCatId,
            type = "INCOME",
            status = "PENDING",
            description = "Bônus",
            amount = 500.00m,
            transactionDate = "2026-11-25"
        });

        // Despesa em conta confirmada R$ 300 + pendente R$ 100
        await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = acc1,
            categoryId = foodCatId,
            type = "EXPENSE",
            status = "CONFIRMED",
            description = "Mercado",
            amount = 300.00m,
            transactionDate = "2026-11-08"
        });
        await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = acc1,
            categoryId = leisureCatId,
            type = "EXPENSE",
            status = "PENDING",
            description = "Cinema",
            amount = 100.00m,
            transactionDate = "2026-11-22"
        });

        // Compra no cartão R$ 200 (fatura Nov/2026) e pagamento da fatura (não deve contar em dobro!)
        await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            creditCardId = cardId,
            categoryId = leisureCatId,
            type = "EXPENSE",
            description = "Show",
            amount = 200.00m,
            transactionDate = "2026-11-10"
        });

        HttpResponseMessage invRes = await alice.GetAsync($"/api/v1/credit-cards/{cardId}/invoices?year=2026");
        using JsonDocument invDoc = await invRes.ReadJsonAsync();
        Guid novInvoiceId = invDoc.RootElement[0].GetProperty("id").GetGuid();
        await alice.PostAsJsonAsync($"/api/v1/credit-cards/{cardId}/invoices/{novInvoiceId}/pay", new { paymentDate = "2026-11-25" });

        // Transferência entre contas R$ 250 (não deve entrar em receitas/despesas)
        await alice.PostAsJsonAsync("/api/v1/transactions/transfer", new
        {
            sourceAccountId = acc1,
            destinationAccountId = acc2,
            description = "Transferência",
            amount = 250.00m,
            transferDate = "2026-11-09"
        });

        HttpResponseMessage dashRes = await alice.GetAsync("/api/v1/dashboard/monthly-summary?year=2026&month=11");
        Assert.Equal(HttpStatusCode.OK, dashRes.StatusCode);

        using JsonDocument dashDoc = await dashRes.ReadJsonAsync();
        JsonElement root = dashDoc.RootElement;

        // Saldo consolidado apenas contas ativas:
        // acc1 = 1000 + 4000 - 300 - 200 (fatura paga) - 250 = 4250; acc2 = 500 + 250 = 750 => total = 5000
        Assert.Equal(5000.00m, root.GetProperty("consolidatedBalance").GetDecimal());
        Assert.Equal(4000.00m, root.GetProperty("confirmedIncome").GetDecimal());
        Assert.Equal(500.00m, root.GetProperty("pendingIncome").GetDecimal());
        Assert.Equal(500.00m, root.GetProperty("confirmedExpense").GetDecimal()); // 300 conta + 200 cartão
        Assert.Equal(100.00m, root.GetProperty("pendingExpense").GetDecimal());
        Assert.Equal(3500.00m, root.GetProperty("netConfirmed").GetDecimal());
        Assert.Equal(3900.00m, root.GetProperty("netProjected").GetDecimal());

        JsonElement[] cats = root.GetProperty("expensesByCategory").EnumerateArray().ToArray();
        Assert.Equal(2, cats.Length);
        Assert.Equal(600.00m, cats.Sum(c => c.GetProperty("totalAmount").GetDecimal()));
        Assert.Equal(100.00m, cats.Sum(c => c.GetProperty("percentage").GetDecimal()));

        // Próximos vencimentos deve conter Bônus e Cinema (ambos pendentes em Nov/2026 >= 10/11)
        JsonElement[] upcoming = root.GetProperty("upcomingDueItems").EnumerateArray().ToArray();
        Assert.Equal(2, upcoming.Length);
    }
}
