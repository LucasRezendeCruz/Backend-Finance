using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Finance.Api.Tests.Infrastructure;

namespace Finance.Api.Tests;

public sealed class TelegramBotIntegrationTests
{
    [Fact]
    public async Task Webhook_RejectsInvalidSecret_And_FullLinkQuickTransactionsUndoAndUnlinkFlowWorks()
    {
        using var factory = new FinanceApiFactory();
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero));
        using HttpClient alice = await factory.CreateAuthenticatedMobileClientAsync("alice-tg@exemplo.com", "Alice");
        using HttpClient anon = factory.CreateClient();

        Guid accId = await alice.CreateAccountAsync("Nubank Conta", "CHECKING", 1000.00m);
        Guid cardId = await alice.CreateCreditCardAsync("Ultravioleta", 8000.00m, closingDay: 25, dueDay: 5, defaultAccountId: accId);

        // Set budget on Alimentação so /orcamentos returns a real line
        HttpResponseMessage expCatRes = await alice.GetAsync("/api/v1/categories?type=EXPENSE");
        using JsonDocument expCatDoc = await expCatRes.ReadJsonAsync();
        Guid foodCatId = expCatDoc.RootElement.EnumerateArray().First(c => c.GetProperty("name").GetString() == "Alimentação").GetProperty("id").GetGuid();
        await alice.PatchAsJsonAsync($"/api/v1/categories/{foodCatId}/budget", new { monthlyBudget = 500.00m });

        // 1. Webhook without valid secret header -> 401
        HttpResponseMessage badWebhook = await anon.PostAsJsonAsync("/api/v1/telegram/webhook", new
        {
            update_id = 1L,
            message = new { message_id = 1L, chat = new { id = 555111L }, text = "/saldo" }
        });
        Assert.Equal(HttpStatusCode.Unauthorized, badWebhook.StatusCode);

        // Helper to send signed webhook update
        async Task<HttpResponseMessage> PostWebhookAsync(object payload)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/telegram/webhook")
            {
                Content = JsonContent.Create(payload)
            };
            req.Headers.Add("X-Telegram-Bot-Api-Secret-Token", FinanceApiFactory.TestTelegramWebhookSecret);
            return await anon.SendAsync(req);
        }

        // 2. Unlinked chat is blocked by strict whitelist
        HttpResponseMessage unlinkedRes = await PostWebhookAsync(new
        {
            update_id = 2L,
            message = new { message_id = 2L, chat = new { id = 555111L }, from = new { username = "alice_tg", first_name = "Alice" }, text = "/saldo" }
        });
        Assert.Equal(HttpStatusCode.OK, unlinkedRes.StatusCode);
        Assert.Contains("ainda não está vinculado", factory.TelegramBot.SentMessages[^1].Text);

        // 3. Generate ephemeral link token in web app
        HttpResponseMessage tokenRes = await alice.PostAsync("/api/v1/telegram/link-token", null);
        Assert.Equal(HttpStatusCode.OK, tokenRes.StatusCode);
        using JsonDocument tokenDoc = await tokenRes.ReadJsonAsync();
        string rawToken = tokenDoc.RootElement.GetProperty("token").GetString()!;
        Assert.Contains($"https://t.me/RazoTestBot?start={rawToken}", tokenDoc.RootElement.GetProperty("deepLink").GetString());

        // 4. Consume link token via /start <token>
        HttpResponseMessage linkRes = await PostWebhookAsync(new
        {
            update_id = 3L,
            message = new { message_id = 3L, chat = new { id = 555111L }, from = new { username = "alice_tg", first_name = "Alice" }, text = $"/start {rawToken}" }
        });
        Assert.Equal(HttpStatusCode.OK, linkRes.StatusCode);
        Assert.Contains("vinculada com sucesso", factory.TelegramBot.SentMessages[^1].Text);

        // Replay of the same token by another chat must fail
        await PostWebhookAsync(new
        {
            update_id = 4L,
            message = new { message_id = 4L, chat = new { id = 999999L }, from = new { username = "attacker", first_name = "Eve" }, text = $"/start {rawToken}" }
        });
        Assert.Contains("Token inválido ou expirado", factory.TelegramBot.SentMessages[^1].Text);

        // Verify status reflects active link
        HttpResponseMessage statusRes = await alice.GetAsync("/api/v1/telegram/status");
        using JsonDocument statusDoc = await statusRes.ReadJsonAsync();
        Assert.True(statusDoc.RootElement.GetProperty("isLinked").GetBoolean());
        Assert.Equal("alice_tg", statusDoc.RootElement.GetProperty("link").GetProperty("telegramUsername").GetString());

        // 5. Quick expense: "Mercado 142,50" -> confirmed expense on Nubank Conta + inline Desfazer button
        await PostWebhookAsync(new
        {
            update_id = 5L,
            message = new { message_id = 5L, chat = new { id = 555111L }, text = "Mercado 142,50" }
        });
        var expenseReply = factory.TelegramBot.SentMessages[^1];
        Assert.Contains("Lançamento registrado", expenseReply.Text);
        Assert.Contains("Alimentação", expenseReply.Text);
        Assert.NotNull(expenseReply.InlineButtons);
        string undoCallback = expenseReply.InlineButtons![0].CallbackData;
        Assert.StartsWith("undo:", undoCallback);

        // 6. Quick 4x card installment: "Notebook 1200 4x cartão"
        await PostWebhookAsync(new
        {
            update_id = 6L,
            message = new { message_id = 6L, chat = new { id = 555111L }, text = "Notebook 1200 4x cartão" }
        });
        Assert.Contains("4x de", factory.TelegramBot.SentMessages[^1].Text);
        Assert.Contains("Ultravioleta", factory.TelegramBot.SentMessages[^1].Text);

        // 7. Quick income: "+ Freelance 900"
        await PostWebhookAsync(new
        {
            update_id = 7L,
            message = new { message_id = 7L, chat = new { id = 555111L }, text = "+ Freelance 900" }
        });
        Assert.Contains("+ R$", factory.TelegramBot.SentMessages[^1].Text);

        // 8. /saldo, /resumo and /orcamentos commands
        await PostWebhookAsync(new
        {
            update_id = 8L,
            message = new { message_id = 8L, chat = new { id = 555111L }, text = "/saldo" }
        });
        // Balance = 1000 - 142.50 + 900 = 1757.50
        Assert.Contains("1.757,50", factory.TelegramBot.SentMessages[^1].Text);

        await PostWebhookAsync(new
        {
            update_id = 9L,
            message = new { message_id = 9L, chat = new { id = 555111L }, text = "/orcamentos" }
        });
        Assert.Contains("Alimentação", factory.TelegramBot.SentMessages[^1].Text);
        Assert.Contains("142,50", factory.TelegramBot.SentMessages[^1].Text);

        // 9. Undo via inline button callback reverses the R$ 142,50 expense -> balance back to R$ 1.900,00
        await PostWebhookAsync(new
        {
            update_id = 10L,
            callback_query = new
            {
                id = "cb-1",
                from = new { username = "alice_tg", first_name = "Alice" },
                message = new { message_id = 5L, chat = new { id = 555111L }, text = "" },
                data = undoCallback
            }
        });
        Assert.Contains("estornado com sucesso", factory.TelegramBot.SentMessages[^1].Text);

        await PostWebhookAsync(new
        {
            update_id = 11L,
            message = new { message_id = 11L, chat = new { id = 555111L }, text = "/saldo" }
        });
        Assert.Contains("1.900,00", factory.TelegramBot.SentMessages[^1].Text);

        // 10. Unlink from web app & check Audit Log records TELEGRAM_LINKED + TELEGRAM_UNLINKED
        HttpResponseMessage unlinkRes = await alice.DeleteAsync("/api/v1/telegram/link");
        Assert.Equal(HttpStatusCode.NoContent, unlinkRes.StatusCode);

        HttpResponseMessage auditRes = await alice.GetAsync("/api/v1/security/audit-logs?page=1&pageSize=20");
        using JsonDocument auditDoc = await auditRes.ReadJsonAsync();
        string[] events = auditDoc.RootElement.GetProperty("items")
            .EnumerateArray()
            .Select(e => e.GetProperty("eventType").GetString()!)
            .ToArray();
        Assert.Contains("TELEGRAM_LINKED", events);
        Assert.Contains("TELEGRAM_UNLINKED", events);
    }

    [Fact]
    public async Task QuickTransaction_DetectsExplicitAccountAndCardNames_AndDisambiguatesPixVsCard()
    {
        using var factory = new FinanceApiFactory();
        factory.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 26, 17, 5, 0, TimeSpan.Zero));
        using HttpClient user = await factory.CreateAuthenticatedMobileClientAsync("lucas-tg@exemplo.com", "Lucas");
        using HttpClient anon = factory.CreateClient();

        // Create accounts: Carteira (default), Itaú Reserva, Nubank Conta
        Guid carteiraId = await user.CreateAccountAsync("Carteira", "CASH", 400.00m);
        Guid itauId = await user.CreateAccountAsync("Itaú Reserva", "SAVINGS", 5880.00m);
        Guid nubankAccId = await user.CreateAccountAsync("Nubank Conta", "CHECKING", 12152.00m);

        // Create cards: Inter Black (default), Nubank Ultravioleta (shares "Nubank" with account!)
        Guid interCardId = await user.CreateCreditCardAsync("Inter Black", 10000.00m, closingDay: 25, dueDay: 5, defaultAccountId: nubankAccId);
        Guid nuCardId = await user.CreateCreditCardAsync("Nubank Ultravioleta", 15000.00m, closingDay: 25, dueDay: 5, defaultAccountId: nubankAccId);

        async Task PostWebhookAsync(object payload)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/telegram/webhook")
            {
                Content = JsonContent.Create(payload)
            };
            req.Headers.Add("X-Telegram-Bot-Api-Secret-Token", FinanceApiFactory.TestTelegramWebhookSecret);
            HttpResponseMessage res = await anon.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        }

        // Link account and explicitly set Carteira + Inter Black as defaults
        HttpResponseMessage tokenRes = await user.PostAsync("/api/v1/telegram/link-token", null);
        using JsonDocument tokenDoc = await tokenRes.ReadJsonAsync();
        string rawToken = tokenDoc.RootElement.GetProperty("token").GetString()!;

        await PostWebhookAsync(new
        {
            update_id = 100L,
            message = new { message_id = 1L, chat = new { id = 777111L }, from = new { username = "lucas", first_name = "Lucas" }, text = $"/start {rawToken}" }
        });

        HttpResponseMessage prefRes = await user.PutAsJsonAsync("/api/v1/telegram/preferences", new
        {
            defaultAccountId = carteiraId,
            defaultCreditCardId = interCardId
        });
        Assert.Equal(HttpStatusCode.OK, prefRes.StatusCode);

        // 1. Exact user scenario: "Mercado 152,00 pix conta nubank" -> MUST debit Nubank Conta (NOT Carteira!) with clean description "Mercado"
        await PostWebhookAsync(new
        {
            update_id = 101L,
            message = new { message_id = 2L, chat = new { id = 777111L }, text = "Mercado 152,00 pix conta nubank" }
        });
        string reply1 = factory.TelegramBot.SentMessages[^1].Text;
        Assert.Contains("Mercado: - R$ 152,00", reply1);
        Assert.Contains("Nubank Conta (PIX)", reply1);
        Assert.DoesNotContain("Carteira", reply1);

        // Verify balances: Carteira stays R$ 400,00; Nubank Conta drops to R$ 12.000,00
        await PostWebhookAsync(new
        {
            update_id = 102L,
            message = new { message_id = 3L, chat = new { id = 777111L }, text = "/saldo" }
        });
        string balanceReply = factory.TelegramBot.SentMessages[^1].Text;
        Assert.Contains("Carteira: R$ 400,00", balanceReply);
        Assert.Contains("Nubank Conta: R$ 12.000,00", balanceReply);

        // 2. Explicit card name without the word "cartão": "Uber 32 no inter black" -> Inter Black card, clean description "Uber"
        await PostWebhookAsync(new
        {
            update_id = 103L,
            message = new { message_id = 4L, chat = new { id = 777111L }, text = "Uber 32 no inter black" }
        });
        string reply2 = factory.TelegramBot.SentMessages[^1].Text;
        Assert.Contains("Uber: - R$ 32,00", reply2);
        Assert.Contains("Inter Black", reply2);

        // 3. Disambiguate "nubank" when "cartão" is mentioned -> Nubank Ultravioleta (NOT Nubank Conta!)
        await PostWebhookAsync(new
        {
            update_id = 104L,
            message = new { message_id = 5L, chat = new { id = 777111L }, text = "Jantar 100 cartao nubank" }
        });
        string reply3 = factory.TelegramBot.SentMessages[^1].Text;
        Assert.Contains("Jantar: - R$ 100,00", reply3);
        Assert.Contains("Nubank Ultravioleta", reply3);

        // 4. Product noun guard: "Conta de luz 180" must NOT falsely match "Nubank Conta" -> uses default Carteira
        await PostWebhookAsync(new
        {
            update_id = 105L,
            message = new { message_id = 6L, chat = new { id = 777111L }, text = "Conta de luz 180" }
        });
        string reply4 = factory.TelegramBot.SentMessages[^1].Text;
        Assert.Contains("Conta de luz: - R$ 180,00", reply4);
        Assert.Contains("Carteira", reply4);

        // 5. Product noun guard: "Carteira de couro 120" must preserve "Carteira de couro" as description
        await PostWebhookAsync(new
        {
            update_id = 106L,
            message = new { message_id = 7L, chat = new { id = 777111L }, text = "Carteira de couro 120" }
        });
        string reply5 = factory.TelegramBot.SentMessages[^1].Text;
        Assert.Contains("Carteira de couro: - R$ 120,00", reply5);

        // 6. Income with explicit account (diacritic-insensitive): "+ Freelance 900 pix itau" -> Itaú Reserva
        await PostWebhookAsync(new
        {
            update_id = 107L,
            message = new { message_id = 8L, chat = new { id = 777111L }, text = "+ Freelance 900 pix itau" }
        });
        string reply6 = factory.TelegramBot.SentMessages[^1].Text;
        Assert.Contains("Freelance: + R$ 900,00", reply6);
        Assert.Contains("Itaú Reserva (PIX)", reply6);
    }
}
