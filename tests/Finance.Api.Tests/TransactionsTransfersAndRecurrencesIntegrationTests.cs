using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Finance.Api.Tests.Infrastructure;

namespace Finance.Api.Tests;

public sealed class TransactionsTransfersAndRecurrencesIntegrationTests
{
    [Fact]
    public async Task AccountTransaction_PendingConfirmEditDelete_AndLargeAuditLog_Work()
    {
        using var factory = new FinanceApiFactory();
        using HttpClient alice = await factory.CreateAuthenticatedMobileClientAsync("alice-tx@exemplo.com", "Alice");

        Guid accountId = await alice.CreateAccountAsync("Conta Principal", "CHECKING", 1000.00m);

        // Criar PENDING -> não altera saldo
        HttpResponseMessage pendingRes = await alice.PostAsJsonAsync("/api/v1/transactions", new
        {
            accountId,
            type = "EXPENSE",
            status = "PENDING",
            description = "Aluguel",
            amount = 5000.00m,
            transactionDate = "2026-11-24"
        });
        Assert.Equal(HttpStatusCode.Created, pendingRes.StatusCode);
        using JsonDocument pendingDoc = await pendingRes.ReadJsonAsync();
        Guid txId = pendingDoc.RootElement[0].GetProperty("id").GetGuid();

        Assert.Equal(1000.00m, await GetAccountBalanceAsync(alice, accountId));

        // Confirmar -> saldo cai para -4000
        HttpResponseMessage confirmRes = await alice.PatchAsync($"/api/v1/transactions/{txId}/confirm", null);
        Assert.Equal(HttpStatusCode.OK, confirmRes.StatusCode);
        Assert.Equal(-4000.00m, await GetAccountBalanceAsync(alice, accountId));

        // Editar valor para 500 -> saldo vai para 500
        HttpResponseMessage editRes = await alice.PutAsJsonAsync($"/api/v1/transactions/{txId}", new
        {
            description = "Aluguel Ajustado",
            amount = 500.00m,
            transactionDate = "2026-11-24"
        });
        Assert.Equal(HttpStatusCode.OK, editRes.StatusCode);
        Assert.Equal(500.00m, await GetAccountBalanceAsync(alice, accountId));

        // Excluir -> saldo volta para 1000
        HttpResponseMessage deleteRes = await alice.DeleteAsync($"/api/v1/transactions/{txId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteRes.StatusCode);
        Assert.Equal(1000.00m, await GetAccountBalanceAsync(alice, accountId));

        // Audit log registrou LARGE_TRANSACTION_CREATED
        HttpResponseMessage auditRes = await alice.GetAsync("/api/v1/security/audit-logs");
        using JsonDocument auditDoc = await auditRes.ReadJsonAsync();
        Assert.Contains(auditDoc.RootElement.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("eventType").GetString() == "LARGE_TRANSACTION_CREATED");
    }

    [Fact]
    public async Task Transfer_CreateEditDelete_UpdatesBothAccountsCleanly()
    {
        using var factory = new FinanceApiFactory();
        using HttpClient alice = await factory.CreateAuthenticatedMobileClientAsync("alice-tr@exemplo.com", "Alice");

        Guid originId = await alice.CreateAccountAsync("Origem", "CHECKING", 1000.00m);
        Guid destId = await alice.CreateAccountAsync("Poupança", "SAVINGS", 200.00m);

        HttpResponseMessage trRes = await alice.PostAsJsonAsync("/api/v1/transactions/transfer", new
        {
            sourceAccountId = originId,
            destinationAccountId = destId,
            description = "Reserva mensal",
            amount = 300.00m,
            transferDate = "2026-11-24"
        });
        Assert.Equal(HttpStatusCode.Created, trRes.StatusCode);
        using JsonDocument trDoc = await trRes.ReadJsonAsync();
        Guid outId = trDoc.RootElement.GetProperty("outgoingTransaction").GetProperty("id").GetGuid();

        Assert.Equal(700.00m, await GetAccountBalanceAsync(alice, originId));
        Assert.Equal(500.00m, await GetAccountBalanceAsync(alice, destId));

        HttpResponseMessage editTrRes = await alice.PutAsJsonAsync($"/api/v1/transactions/{outId}", new
        {
            description = "Reserva atualizada",
            amount = 450.00m,
            transactionDate = "2026-11-24"
        });
        Assert.Equal(HttpStatusCode.OK, editTrRes.StatusCode);
        Assert.Equal(550.00m, await GetAccountBalanceAsync(alice, originId));
        Assert.Equal(650.00m, await GetAccountBalanceAsync(alice, destId));

        HttpResponseMessage deleteTrRes = await alice.DeleteAsync($"/api/v1/transactions/{outId}");
        Assert.Equal(HttpStatusCode.NoContent, deleteTrRes.StatusCode);
        Assert.Equal(1000.00m, await GetAccountBalanceAsync(alice, originId));
        Assert.Equal(200.00m, await GetAccountBalanceAsync(alice, destId));
    }

    [Fact]
    public async Task Recurrences_MaterializeIsIdempotent_AndDeactivatePreventsFutureMaterializations()
    {
        using var factory = new FinanceApiFactory();
        using HttpClient alice = await factory.CreateAuthenticatedMobileClientAsync("alice-rec@exemplo.com", "Alice");

        Guid accountId = await alice.CreateAccountAsync("Conta", "CHECKING", 1000m);
        Guid cardId = await alice.CreateCreditCardAsync("Cartão", 3000m, closingDay: 20, dueDay: 28);

        HttpResponseMessage catRes = await alice.GetAsync("/api/v1/categories?type=EXPENSE");
        using JsonDocument catDoc = await catRes.ReadJsonAsync();
        Guid expenseCatId = catDoc.RootElement[0].GetProperty("id").GetGuid();

        HttpResponseMessage recAccRes = await alice.PostAsJsonAsync("/api/v1/recurrences", new
        {
            type = "EXPENSE",
            frequency = "MONTHLY",
            description = "Academia",
            amount = 120.00m,
            categoryId = expenseCatId,
            accountId,
            startDate = "2026-11-05"
        });
        Assert.Equal(HttpStatusCode.Created, recAccRes.StatusCode);
        using JsonDocument recAccDoc = await recAccRes.ReadJsonAsync();
        Guid recAccId = recAccDoc.RootElement.GetProperty("id").GetGuid();

        HttpResponseMessage recCardRes = await alice.PostAsJsonAsync("/api/v1/recurrences", new
        {
            type = "EXPENSE",
            frequency = "MONTHLY",
            description = "Streaming",
            amount = 49.90m,
            categoryId = expenseCatId,
            creditCardId = cardId,
            startDate = "2026-11-10"
        });
        Assert.Equal(HttpStatusCode.Created, recCardRes.StatusCode);

        // 1ª materialização em Nov/2026 -> 2 lançamentos criados
        HttpResponseMessage mat1 = await alice.PostAsJsonAsync("/api/v1/recurrences/materialize", new
        {
            year = 2026,
            month = 11
        });
        Assert.Equal(HttpStatusCode.OK, mat1.StatusCode);
        using JsonDocument mat1Doc = await mat1.ReadJsonAsync();
        Assert.Equal(2, mat1Doc.RootElement.GetProperty("createdCount").GetInt32());

        // 2ª materialização no mesmo mês -> 0 criados (idempotente)
        HttpResponseMessage mat2 = await alice.PostAsJsonAsync("/api/v1/recurrences/materialize", new
        {
            year = 2026,
            month = 11
        });
        Assert.Equal(HttpStatusCode.OK, mat2.StatusCode);
        using JsonDocument mat2Doc = await mat2.ReadJsonAsync();
        Assert.Equal(0, mat2Doc.RootElement.GetProperty("createdCount").GetInt32());

        // Desativar a recorrência da conta -> só a do cartão materializa em Dez/2026
        HttpResponseMessage deactRes = await alice.PatchAsync($"/api/v1/recurrences/{recAccId}/deactivate", null);
        Assert.Equal(HttpStatusCode.OK, deactRes.StatusCode);

        HttpResponseMessage matDec = await alice.PostAsJsonAsync("/api/v1/recurrences/materialize", new
        {
            year = 2026,
            month = 12
        });
        using JsonDocument matDecDoc = await matDec.ReadJsonAsync();
        Assert.Equal(1, matDecDoc.RootElement.GetProperty("createdCount").GetInt32());
    }

    private static async Task<decimal> GetAccountBalanceAsync(HttpClient client, Guid accountId)
    {
        HttpResponseMessage res = await client.GetAsync("/api/v1/accounts");
        using JsonDocument doc = await res.ReadJsonAsync();
        JsonElement account = doc.RootElement.EnumerateArray().Single(a => a.GetProperty("id").GetGuid() == accountId);
        return account.GetProperty("currentBalance").GetDecimal();
    }
}
