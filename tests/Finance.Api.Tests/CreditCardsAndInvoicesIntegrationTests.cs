using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Finance.Api.Tests.Infrastructure;

namespace Finance.Api.Tests;

public sealed class CreditCardsAndInvoicesIntegrationTests
{
    [Fact]
    public async Task CreditCard_BillingDates_InstallmentsSplit_LimitCalculation_AndPayInvoice_Work()
    {
        using var factory = new FinanceApiFactory();
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
        using HttpClient alice = await factory.CreateAuthenticatedMobileClientAsync("alice-card@exemplo.com", "Alice");
        using HttpClient bob = await factory.CreateAuthenticatedMobileClientAsync("bob-card@exemplo.com", "Bob");

        Guid accountId = await alice.CreateAccountAsync("Nubank", "CHECKING", 3000.00m);
        Guid cardId = await alice.CreateCreditCardAsync("Pulse Black", 2000.00m, closingDay: 18, dueDay: 25, defaultAccountId: accountId);

        // Compra antes do fechamento (dia 10 < 18) -> fatura 11/2026; 3x de R$ 100,00 = 33,34 + 33,33 + 33,33
        HttpResponseMessage beforeCloseRes = await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            creditCardId = cardId,
            type = "EXPENSE",
            description = "Fone Bluetooth",
            amount = 100.00m,
            transactionDate = "2026-11-10",
            installments = 3
        });
        Assert.Equal(HttpStatusCode.Created, beforeCloseRes.StatusCode);
        using JsonDocument beforeDoc = await beforeCloseRes.ReadJsonAsync();
        JsonElement[] parts = beforeDoc.RootElement.EnumerateArray().ToArray();
        Assert.Equal(3, parts.Length);
        Assert.Equal(33.34m, parts[0].GetProperty("amount").GetDecimal());
        Assert.Equal(33.33m, parts[1].GetProperty("amount").GetDecimal());
        Assert.Equal(33.33m, parts[2].GetProperty("amount").GetDecimal());

        // Compra NO dia do fechamento (dia 18 >= 18) -> fatura 12/2026
        HttpResponseMessage onCloseRes = await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            creditCardId = cardId,
            type = "EXPENSE",
            description = "Jantar",
            amount = 200.00m,
            transactionDate = "2026-11-18",
            installments = 1
        });
        Assert.Equal(HttpStatusCode.Created, onCloseRes.StatusCode);

        // Limite usado = 100 + 200 = 300; disponível = 1700
        HttpResponseMessage cardsRes = await alice.GetAsync("/api/v1/credit-cards");
        using JsonDocument cardsDoc = await cardsRes.ReadJsonAsync();
        JsonElement cardJson = Assert.Single(cardsDoc.RootElement.EnumerateArray());
        Assert.Equal(300.00m, cardJson.GetProperty("usedLimit").GetDecimal());
        Assert.Equal(1700.00m, cardJson.GetProperty("availableLimit").GetDecimal());

        // Editar closingDay/dueDay atualiza faturas ainda OPEN
        HttpResponseMessage updateCardRes = await alice.PutAsJsonAsync($"/api/v1/credit-cards/{cardId}", new
        {
            name = "Pulse Black",
            creditLimit = 2000.00m,
            closingDay = 20,
            dueDay = 28,
            defaultAccountId = accountId
        });
        Assert.Equal(HttpStatusCode.OK, updateCardRes.StatusCode);

        HttpResponseMessage invoicesRes = await alice.GetAsync($"/api/v1/credit-cards/{cardId}/invoices?year=2026");
        using JsonDocument invoicesDoc = await invoicesRes.ReadJsonAsync();
        JsonElement[] invoices = invoicesDoc.RootElement.EnumerateArray().ToArray();
        Assert.Equal(2, invoices.Length);

        JsonElement novInvoice = Assert.Single(invoices, i => i.GetProperty("referenceMonth").GetInt32() == 11);
        JsonElement decInvoice = Assert.Single(invoices, i => i.GetProperty("referenceMonth").GetInt32() == 12);
        Assert.Equal(33.34m, novInvoice.GetProperty("totalAmount").GetDecimal());
        Assert.Equal("2026-11-20", novInvoice.GetProperty("closingDate").GetString());
        Assert.Equal("2026-11-28", novInvoice.GetProperty("dueDate").GetString());
        Assert.Equal(233.33m, decInvoice.GetProperty("totalAmount").GetDecimal());

        Guid novInvoiceId = novInvoice.GetProperty("id").GetGuid();
        Guid novTxId = parts[0].GetProperty("id").GetGuid();

        // IDOR check
        HttpResponseMessage bobInvoiceRes = await bob.GetAsync($"/api/v1/credit-cards/{cardId}/invoices/{novInvoiceId}");
        Assert.Equal(HttpStatusCode.NotFound, bobInvoiceRes.StatusCode);

        // Detalhe da fatura 11/2026
        HttpResponseMessage detailRes = await alice.GetAsync($"/api/v1/credit-cards/{cardId}/invoices/{novInvoiceId}");
        Assert.Equal(HttpStatusCode.OK, detailRes.StatusCode);
        using JsonDocument detailDoc = await detailRes.ReadJsonAsync();
        Assert.Single(detailDoc.RootElement.GetProperty("transactions").EnumerateArray());

        // Pagar fatura 11/2026 (usa defaultAccountId)
        HttpResponseMessage payRes = await alice.PostAsJsonAsync(
            $"/api/v1/credit-cards/{cardId}/invoices/{novInvoiceId}/pay",
            new { paymentDate = "2026-11-28" });
        Assert.Equal(HttpStatusCode.OK, payRes.StatusCode);
        using JsonDocument paidDoc = await payRes.ReadJsonAsync();
        Assert.Equal("PAID", paidDoc.RootElement.GetProperty("status").GetString());

        // Saldo da conta descontado: 3000 - 33.34 = 2966.66
        HttpResponseMessage accountsRes = await alice.GetAsync("/api/v1/accounts");
        using JsonDocument accDoc = await accountsRes.ReadJsonAsync();
        Assert.Equal(2966.66m, accDoc.RootElement[0].GetProperty("currentBalance").GetDecimal());

        // Editar ou excluir item de fatura PAID é bloqueado com 422
        HttpResponseMessage editPaidTx = await alice.PutAsJsonAsync($"/api/v1/transactions/{novTxId}", new
        {
            description = "Alterar",
            amount = 50.00m,
            transactionDate = "2026-11-10"
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, editPaidTx.StatusCode);

        HttpResponseMessage deletePaidTx = await alice.DeleteAsync($"/api/v1/transactions/{novTxId}");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, deletePaidTx.StatusCode);
    }
}
