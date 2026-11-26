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

    [Fact]
    public async Task CategoryBudgets_And_SixMonthProjection_ComputeAlertsInstallmentsAndRecurrencesAccurately()
    {
        using var factory = new FinanceApiFactory();
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        using HttpClient alice = await factory.CreateAuthenticatedMobileClientAsync("alice-plan@exemplo.com", "Alice");

        Guid accId = await alice.CreateAccountAsync("Conta Principal", "CHECKING", 10000.00m);
        Guid cardId = await alice.CreateCreditCardAsync("Cartão Razo", 12000.00m, closingDay: 20, dueDay: 27, defaultAccountId: accId);

        HttpResponseMessage expCatRes = await alice.GetAsync("/api/v1/categories?type=EXPENSE");
        using JsonDocument expCatDoc = await expCatRes.ReadJsonAsync();
        Guid foodCatId = expCatDoc.RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == "Alimentação").GetProperty("id").GetGuid();
        Guid housingCatId = expCatDoc.RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == "Moradia").GetProperty("id").GetGuid();
        Guid leisureCatId = expCatDoc.RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == "Lazer").GetProperty("id").GetGuid();
        Guid subsCatId = expCatDoc.RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == "Assinaturas").GetProperty("id").GetGuid();

        HttpResponseMessage incCatRes = await alice.GetAsync("/api/v1/categories?type=INCOME");
        using JsonDocument incCatDoc = await incCatRes.ReadJsonAsync();
        Guid salaryCatId = incCatDoc.RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == "Salário").GetProperty("id").GetGuid();

        // Income category cannot have a monthly budget
        HttpResponseMessage invalidBudgetRes = await alice.PatchAsJsonAsync(
            $"/api/v1/categories/{salaryCatId}/budget",
            new { monthlyBudget = 5000.00m });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidBudgetRes.StatusCode);

        // Set budgets: Food = 1000 (will exceed), Housing = 2000 (will hit 85% warning), Leisure = 500 (0 spent -> OK)
        Assert.Equal(HttpStatusCode.OK, (await alice.PatchAsJsonAsync($"/api/v1/categories/{foodCatId}/budget", new { monthlyBudget = 1000.00m })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.PatchAsJsonAsync($"/api/v1/categories/{housingCatId}/budget", new { monthlyBudget = 2000.00m })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await alice.PatchAsJsonAsync($"/api/v1/categories/{leisureCatId}/budget", new { monthlyBudget = 500.00m })).StatusCode);

        // Expenses in Nov/2026: Food 1200 (120% EXCEEDED), Housing 1700 (85% WARNING)
        await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = accId,
            categoryId = foodCatId,
            type = "EXPENSE",
            status = "CONFIRMED",
            description = "Supermercado",
            amount = 1200.00m,
            transactionDate = "2026-11-08"
        });
        await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId = accId,
            categoryId = housingCatId,
            type = "EXPENSE",
            status = "CONFIRMED",
            description = "Aluguel",
            amount = 1700.00m,
            transactionDate = "2026-11-05"
        });

        // 4x installment on credit card (300/month across Nov/2026 .. Feb/2027)
        await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            creditCardId = cardId,
            categoryId = leisureCatId,
            type = "EXPENSE",
            description = "Console",
            amount = 1200.00m,
            installments = 4,
            transactionDate = "2026-11-10"
        });

        // Active monthly recurrence: Internet 150/month starting Nov/2026 (materialize Nov only -> Dec..Apr stay projected!)
        await alice.PostAsJsonAsync("/api/v1/recurrences", new
        {
            type = "EXPENSE",
            frequency = "MONTHLY",
            description = "Internet Fibra",
            amount = 150.00m,
            categoryId = subsCatId,
            accountId = accId,
            startDate = "2026-11-15"
        });
        await alice.PostAsJsonAsync("/api/v1/recurrences/materialize", new { year = 2026, month = 11 });

        // Verify monthly-summary categoryBudgets
        HttpResponseMessage dashRes = await alice.GetAsync("/api/v1/dashboard/monthly-summary?year=2026&month=11");
        Assert.Equal(HttpStatusCode.OK, dashRes.StatusCode);
        using JsonDocument dashDoc = await dashRes.ReadJsonAsync();
        JsonElement[] budgets = dashDoc.RootElement.GetProperty("categoryBudgets").EnumerateArray().ToArray();

        Assert.Equal(3, budgets.Length);
        Assert.Equal("Alimentação", budgets[0].GetProperty("categoryName").GetString());
        Assert.Equal("EXCEEDED", budgets[0].GetProperty("status").GetString());
        Assert.Equal(120.00m, budgets[0].GetProperty("usagePercentage").GetDecimal());
        Assert.Equal(-200.00m, budgets[0].GetProperty("remainingAmount").GetDecimal());

        Assert.Equal("Moradia", budgets[1].GetProperty("categoryName").GetString());
        Assert.Equal("WARNING", budgets[1].GetProperty("status").GetString());
        Assert.Equal(85.00m, budgets[1].GetProperty("usagePercentage").GetDecimal());

        Assert.Equal("Lazer", budgets[2].GetProperty("categoryName").GetString());
        Assert.Equal("OK", budgets[2].GetProperty("status").GetString());
        Assert.Equal(300.00m, budgets[2].GetProperty("spentAmount").GetDecimal()); // 1st installment of Console!

        // Verify 6-month projection (Nov/2026 .. Apr/2027)
        HttpResponseMessage projRes = await alice.GetAsync("/api/v1/dashboard/projection?year=2026&month=11&months=6");
        Assert.Equal(HttpStatusCode.OK, projRes.StatusCode);
        using JsonDocument projDoc = await projRes.ReadJsonAsync();
        JsonElement proj = projDoc.RootElement;

        Assert.Equal(6, proj.GetProperty("monthsCount").GetInt32());
        Assert.Equal(1200.00m, proj.GetProperty("totalInstallmentsCommitted").GetDecimal()); // 4 * 300
        Assert.Equal(900.00m, proj.GetProperty("totalRecurrencesCommitted").GetDecimal());   // 6 * 150 (no double count in Nov!)

        JsonElement[] months = proj.GetProperty("months").EnumerateArray().ToArray();
        Assert.Equal(6, months.Length);

        // Nov/2026: 300 installment + 150 materialized recurrence (not doubled!) + 2900 other expenses
        Assert.Equal(300.00m, months[0].GetProperty("installmentExpenses").GetDecimal());
        Assert.Equal(150.00m, months[0].GetProperty("recurringExpenses").GetDecimal());
        Assert.Equal(2900.00m, months[0].GetProperty("otherExpenses").GetDecimal());

        // Dec/2026: 300 installment + 150 projected recurrence
        Assert.Equal(300.00m, months[1].GetProperty("installmentExpenses").GetDecimal());
        Assert.Equal(150.00m, months[1].GetProperty("recurringExpenses").GetDecimal());

        // Mar/2027 (index 4): installments finished (0), recurrence continues (150)
        Assert.Equal(0.00m, months[4].GetProperty("installmentExpenses").GetDecimal());
        Assert.Equal(150.00m, months[4].GetProperty("recurringExpenses").GetDecimal());
    }
}
