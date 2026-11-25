using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Finance.Api.Tests.Infrastructure;

namespace Finance.Api.Tests;

public sealed class InstallmentAnticipationIntegrationTests
{
    [Fact]
    public async Task CardInstallments_MoveToOpenInvoice_WithCountFromLastAndDiscount_Works()
    {
        using var factory = new FinanceApiFactory();
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        using HttpClient alice = await factory.CreateAuthenticatedMobileClientAsync("alice-ant-inv@exemplo.com", "Alice");

        Guid cardId = await alice.CreateCreditCardAsync("Cartão Principal", 10000m, closingDay: 18, dueDay: 25);

        HttpResponseMessage createRes = await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            creditCardId = cardId,
            type = "EXPENSE",
            description = "Notebook Pro",
            amount = 3000.00m,
            transactionDate = "2026-11-10",
            installments = 6
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        using JsonDocument createDoc = await createRes.ReadJsonAsync();
        Guid groupId = createDoc.RootElement[0].GetProperty("installmentGroupId").GetGuid();

        // Antecipa as 2 últimas parcelas (#5 e #6 = R$ 1000,00) para a fatura aberta atual (11/2026) com R$ 15,00 de desconto
        HttpResponseMessage antRes = await alice.PostAsJsonAsync(
            $"/api/v1/transactions/installment-groups/{groupId}/anticipate",
            new
            {
                count = 2,
                selectionOrder = "FROM_LAST",
                mode = "MOVE_TO_INVOICE",
                discountAmount = 15.00m
            });
        Assert.Equal(HttpStatusCode.OK, antRes.StatusCode);

        using JsonDocument antDoc = await antRes.ReadJsonAsync();
        Assert.Equal("MOVE_TO_INVOICE", antDoc.RootElement.GetProperty("mode").GetString());
        Assert.Equal(2, antDoc.RootElement.GetProperty("anticipatedCount").GetInt32());
        Assert.Equal(1000.00m, antDoc.RootElement.GetProperty("originalTotal").GetDecimal());
        Assert.Equal(15.00m, antDoc.RootElement.GetProperty("discountAmount").GetDecimal());
        Assert.Equal(985.00m, antDoc.RootElement.GetProperty("netTotal").GetDecimal());

        JsonElement[] updated = antDoc.RootElement.GetProperty("updatedInstallments").EnumerateArray().ToArray();
        Assert.Equal([5, 6], updated.Select(x => x.GetProperty("installmentNumber").GetInt32()).ToArray());
        Assert.All(updated, x =>
        {
            Assert.Equal(500.00m, x.GetProperty("originalAmount").GetDecimal());
            Assert.Equal(492.50m, x.GetProperty("amount").GetDecimal());
            Assert.NotEqual(JsonValueKind.Null, x.GetProperty("anticipatedAtUtc").ValueKind);
        });

        // Conferir totais das faturas: Nov/2026 = 500 + 985 = 1485; Mar/2027 e Abr/2027 = 0
        HttpResponseMessage invoicesRes = await alice.GetAsync($"/api/v1/credit-cards/{cardId}/invoices");
        using JsonDocument invDoc = await invoicesRes.ReadJsonAsync();
        JsonElement[] invoices = invDoc.RootElement.EnumerateArray().ToArray();

        Assert.Equal(1485.00m, invoices.Single(i => i.GetProperty("referenceYear").GetInt32() == 2026 && i.GetProperty("referenceMonth").GetInt32() == 11).GetProperty("totalAmount").GetDecimal());
        Assert.Equal(0.00m, invoices.Single(i => i.GetProperty("referenceYear").GetInt32() == 2027 && i.GetProperty("referenceMonth").GetInt32() == 3).GetProperty("totalAmount").GetDecimal());
        Assert.Equal(0.00m, invoices.Single(i => i.GetProperty("referenceYear").GetInt32() == 2027 && i.GetProperty("referenceMonth").GetInt32() == 4).GetProperty("totalAmount").GetDecimal());

        // Tentar antecipar as mesmas parcelas novamente -> 422
        HttpResponseMessage secondAttempt = await alice.PostAsJsonAsync(
            $"/api/v1/transactions/installment-groups/{groupId}/anticipate",
            new
            {
                installmentNumbers = new[] { 5, 6 },
                mode = "MOVE_TO_INVOICE"
            });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, secondAttempt.StatusCode);
    }

    [Fact]
    public async Task CardInstallments_PayFromAccount_WithSpecificNumbersAndDiscount_Works()
    {
        using var factory = new FinanceApiFactory();
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        using HttpClient alice = await factory.CreateAuthenticatedMobileClientAsync("alice-ant-acc@exemplo.com", "Alice");

        Guid accountId = await alice.CreateAccountAsync("Conta Corrente", "CHECKING", 1000.00m);
        Guid cardId = await alice.CreateCreditCardAsync("Pulse", 5000m, closingDay: 18, dueDay: 25, defaultAccountId: accountId);

        HttpResponseMessage createRes = await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            creditCardId = cardId,
            type = "EXPENSE",
            description = "Monitor 4K",
            amount = 400.00m,
            transactionDate = "2026-11-10",
            installments = 4
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        using JsonDocument createDoc = await createRes.ReadJsonAsync();
        Guid groupId = createDoc.RootElement[0].GetProperty("installmentGroupId").GetGuid();

        HttpResponseMessage antRes = await alice.PostAsJsonAsync(
            $"/api/v1/transactions/installment-groups/{groupId}/anticipate",
            new
            {
                installmentNumbers = new[] { 3, 4 },
                mode = "PAY_FROM_ACCOUNT",
                paymentAccountId = accountId,
                discountAmount = 10.00m
            });
        Assert.Equal(HttpStatusCode.OK, antRes.StatusCode);

        using JsonDocument antDoc = await antRes.ReadJsonAsync();
        Assert.Equal("PAY_FROM_ACCOUNT", antDoc.RootElement.GetProperty("mode").GetString());
        Assert.Equal(190.00m, antDoc.RootElement.GetProperty("netTotal").GetDecimal());
        Assert.All(antDoc.RootElement.GetProperty("updatedInstallments").EnumerateArray(), item =>
        {
            Assert.Equal("CONFIRMED", item.GetProperty("status").GetString());
            Assert.Equal(accountId, item.GetProperty("accountId").GetGuid());
            Assert.Equal(JsonValueKind.Null, item.GetProperty("creditCardInvoiceId").ValueKind);
            Assert.Equal(95.00m, item.GetProperty("amount").GetDecimal());
            Assert.Equal(100.00m, item.GetProperty("originalAmount").GetDecimal());
        });

        // Saldo: 1000 - 190 = 810
        HttpResponseMessage accountsRes = await alice.GetAsync("/api/v1/accounts");
        using JsonDocument accDoc = await accountsRes.ReadJsonAsync();
        Assert.Equal(810.00m, accDoc.RootElement[0].GetProperty("currentBalance").GetDecimal());

        // Faturas Jan/2027 e Fev/2027 zeradas
        HttpResponseMessage invoicesRes = await alice.GetAsync($"/api/v1/credit-cards/{cardId}/invoices?year=2027");
        using JsonDocument invDoc = await invoicesRes.ReadJsonAsync();
        Assert.All(invDoc.RootElement.EnumerateArray(), i => Assert.Equal(0.00m, i.GetProperty("totalAmount").GetDecimal()));
    }

    [Fact]
    public async Task AccountInstallments_AnticipateByTransactionIds_WithDiscount_AndIdor_Work()
    {
        using var factory = new FinanceApiFactory();
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        using HttpClient alice = await factory.CreateAuthenticatedMobileClientAsync("alice-boleto@exemplo.com", "Alice");
        using HttpClient bob = await factory.CreateAuthenticatedMobileClientAsync("bob-boleto@exemplo.com", "Bob");

        Guid accountId = await alice.CreateAccountAsync("Caixa Boleto", "CHECKING", 2000.00m);

        // 5x de R$ 200,00 = R$ 1000,00; 1ª parcela já confirmada (saldo vai a 1800) e 2..5 pendentes
        HttpResponseMessage createRes = await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId,
            type = "EXPENSE",
            status = "CONFIRMED",
            description = "Curso Finanças",
            amount = 1000.00m,
            transactionDate = "2026-11-10",
            installments = 5
        });
        Assert.Equal(HttpStatusCode.Created, createRes.StatusCode);
        using JsonDocument createDoc = await createRes.ReadJsonAsync();
        JsonElement[] parts = createDoc.RootElement.EnumerateArray().ToArray();
        Guid groupId = parts[0].GetProperty("installmentGroupId").GetGuid();
        Guid tx4Id = parts[3].GetProperty("id").GetGuid();
        Guid tx5Id = parts[4].GetProperty("id").GetGuid();

        // IDOR check
        HttpResponseMessage bobDetail = await bob.GetAsync($"/api/v1/transactions/installment-groups/{groupId}");
        Assert.Equal(HttpStatusCode.NotFound, bobDetail.StatusCode);

        // Antecipar #4 e #5 por TransactionIds com desconto de R$ 20,00 (400 - 20 = 380)
        HttpResponseMessage antRes = await alice.PostAsJsonAsync(
            $"/api/v1/transactions/installment-groups/{groupId}/anticipate",
            new
            {
                transactionIds = new[] { tx4Id, tx5Id },
                discountAmount = 20.00m
            });
        Assert.Equal(HttpStatusCode.OK, antRes.StatusCode);

        // Saldo final: 1800 - 380 = 1420
        HttpResponseMessage accountsRes = await alice.GetAsync("/api/v1/accounts");
        using JsonDocument accDoc = await accountsRes.ReadJsonAsync();
        Assert.Equal(1420.00m, accDoc.RootElement[0].GetProperty("currentBalance").GetDecimal());

        HttpResponseMessage groupRes = await alice.GetAsync($"/api/v1/transactions/installment-groups/{groupId}");
        Assert.Equal(HttpStatusCode.OK, groupRes.StatusCode);
        using JsonDocument groupDoc = await groupRes.ReadJsonAsync();
        Assert.Equal("ACCOUNT", groupDoc.RootElement.GetProperty("sourceKind").GetString());
        Assert.Equal(3, groupDoc.RootElement.GetProperty("settledCount").GetInt32());
        Assert.Equal(2, groupDoc.RootElement.GetProperty("remainingCount").GetInt32());
        Assert.Equal(400.00m, groupDoc.RootElement.GetProperty("remainingAmount").GetDecimal());
        Assert.Equal(20.00m, groupDoc.RootElement.GetProperty("totalDiscountSaved").GetDecimal());
    }
}
