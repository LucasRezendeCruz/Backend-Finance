using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Finance.Application.Common;
using Finance.Application.Common.Exceptions;
using Finance.Application.Common.Interfaces;
using Finance.Application.Dashboard;
using Finance.Application.Transactions;
using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Application.Telegram;

public sealed class TelegramBotService(
    ITelegramRepository telegramRepository,
    ITelegramBotClient botClient,
    IAccountRepository accountRepository,
    ICreditCardRepository creditCardRepository,
    ICategoryRepository categoryRepository,
    ISecurityAuditRepository auditRepository,
    ITransactionService transactionService,
    IDashboardService dashboardService,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    ICurrentUserOverride currentUserOverride,
    IDateTimeProvider dateTimeProvider) : ITelegramBotService
{
    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");
    private const int LinkTokenTtlMinutes = 10;

    public async Task<TelegramStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        TelegramLink? link = await telegramRepository.GetLinkByUserIdAsync(userId, ct);
        string? botUsername = await ResolveBotUsernameSafeAsync(ct);

        return new TelegramStatusDto(
            botClient.IsConfigured,
            botClient.Mode,
            botUsername,
            link is not null,
            link is null
                ? null
                : new TelegramLinkInfoDto(
                    link.TelegramUsername,
                    link.TelegramFirstName,
                    link.DefaultAccountId,
                    link.DefaultCreditCardId,
                    link.LinkedAtUtc,
                    link.LastInteractionAtUtc));
    }

    public async Task<TelegramLinkTokenResultDto> GenerateLinkTokenAsync(CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        DateTimeOffset utcNow = dateTimeProvider.UtcNow;

        // Invalidate any previously unconsumed tokens for this user
        IReadOnlyList<TelegramLinkToken> pending = await telegramRepository.GetUnconsumedTokensByUserAsync(userId, ct);
        foreach (TelegramLinkToken oldToken in pending)
        {
            oldToken.MarkConsumed(utcNow);
        }

        byte[] rawBytes = RandomNumberGenerator.GetBytes(24);
        string rawToken = Base64Url.EncodeToString(rawBytes);
        string tokenHash = HashToken(rawToken);
        DateTimeOffset expiresAtUtc = utcNow.AddMinutes(LinkTokenTtlMinutes);

        var linkToken = new TelegramLinkToken(userId, tokenHash, expiresAtUtc, utcNow);
        await telegramRepository.AddTokenAsync(linkToken, ct);

        await auditRepository.AddAsync(
            new SecurityAuditLog(
                userId,
                SecurityAuditEventType.TelegramLinkRequested,
                currentUserService.IpAddress,
                currentUserService.UserAgent,
                utcNow),
            ct);

        await unitOfWork.SaveChangesAsync(ct);

        string? botUsername = await ResolveBotUsernameSafeAsync(ct);
        string? deepLink = !string.IsNullOrWhiteSpace(botUsername)
            ? $"https://t.me/{botUsername.TrimStart('@')}?start={rawToken}"
            : null;

        return new TelegramLinkTokenResultDto(
            rawToken,
            $"/vincular {rawToken}",
            deepLink,
            expiresAtUtc);
    }

    public async Task<TelegramStatusDto> UpdatePreferencesAsync(
        UpdateTelegramPreferencesCommand command,
        CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        TelegramLink link = await telegramRepository.GetLinkByUserIdAsync(userId, ct)
            ?? throw AppException.NotFound("telegram_not_linked", "Nenhuma conta do Telegram vinculada.");

        if (command.DefaultAccountId.HasValue)
        {
            Account account = await accountRepository.GetByIdAsync(userId, command.DefaultAccountId.Value, ct)
                ?? throw AppException.NotFound("account_not_found", "Conta padrão não encontrada.");
            if (account.IsArchived)
                throw AppException.Unprocessable("account_archived", "A conta selecionada está arquivada.");
        }

        if (command.DefaultCreditCardId.HasValue)
        {
            CreditCard card = await creditCardRepository.GetByIdAsync(userId, command.DefaultCreditCardId.Value, ct)
                ?? throw AppException.NotFound("credit_card_not_found", "Cartão padrão não encontrado.");
            if (card.IsArchived)
                throw AppException.Unprocessable("credit_card_archived", "O cartão selecionado está arquivado.");
        }

        link.UpdatePreferences(command.DefaultAccountId, command.DefaultCreditCardId);
        await unitOfWork.SaveChangesAsync(ct);

        return await GetStatusAsync(ct);
    }

    public async Task UnlinkAsync(CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        TelegramLink? link = await telegramRepository.GetLinkByUserIdAsync(userId, ct);
        if (link is null)
            return;

        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        long chatId = link.TelegramChatId;
        telegramRepository.RemoveLink(link);

        await auditRepository.AddAsync(
            new SecurityAuditLog(
                userId,
                SecurityAuditEventType.TelegramUnlinked,
                currentUserService.IpAddress,
                currentUserService.UserAgent,
                utcNow,
                $"{{\"chatId\":{chatId}}}"),
            ct);

        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task HandleUpdateAsync(TelegramBotUpdate update, CancellationToken ct = default)
    {
        if (update.CallbackQuery is not null)
        {
            await HandleCallbackAsync(update.CallbackQuery, ct);
            return;
        }

        if (update.Message is not null && !string.IsNullOrWhiteSpace(update.Message.Text))
        {
            await HandleMessageAsync(update.Message, ct);
        }
    }

    private async Task HandleMessageAsync(TelegramIncomingMessage message, CancellationToken ct)
    {
        ParsedTelegramInput parsed = TelegramMessageParser.Parse(message.Text);

        if (parsed.Kind == TelegramCommandKind.Link)
        {
            await HandleLinkCommandAsync(message, parsed.LinkToken, ct);
            return;
        }

        TelegramLink? link = await telegramRepository.GetLinkByChatIdAsync(message.ChatId, ct);
        if (link is null)
        {
            await botClient.SendMessageAsync(
                message.ChatId,
                "🔒 Este chat ainda não está vinculado a uma conta Razo.\n\nAcesse Segurança → Bot do Telegram no site para gerar seu link seguro de conexão.",
                ct: ct);
            return;
        }

        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        link.RecordInteraction(message.Username, message.FirstName, utcNow);
        await unitOfWork.SaveChangesAsync(ct);

        using IDisposable _ = currentUserOverride.RunAs(link.UserId, $"TelegramBot/chat:{message.ChatId}");

        switch (parsed.Kind)
        {
            case TelegramCommandKind.Help:
                await botClient.SendMessageAsync(message.ChatId, BuildHelpText(), ct: ct);
                break;

            case TelegramCommandKind.Balance:
                await HandleBalanceAsync(message.ChatId, ct);
                break;

            case TelegramCommandKind.Summary:
                await HandleSummaryAsync(message.ChatId, ct);
                break;

            case TelegramCommandKind.Budgets:
                await HandleBudgetsAsync(message.ChatId, ct);
                break;

            case TelegramCommandKind.Undo:
                await HandleUndoAsync(link, message.ChatId, null, ct);
                break;

            case TelegramCommandKind.Unlink:
                await HandleUnlinkFromChatAsync(link, message.ChatId, ct);
                break;

            case TelegramCommandKind.QuickTransaction:
                await HandleQuickTransactionAsync(link, message.ChatId, parsed, ct);
                break;

            default:
                await botClient.SendMessageAsync(
                    message.ChatId,
                    "Não entendi o comando. Exemplos:\n• Mercado 84,50\n• Uber 32 cartão\n• Notebook 3000 6x\n• + Salário 11200\n• /saldo · /resumo · /orcamentos · /ajuda",
                    ct: ct);
                break;
        }
    }

    private async Task HandleCallbackAsync(TelegramIncomingCallbackQuery callback, CancellationToken ct)
    {
        TelegramLink? link = await telegramRepository.GetLinkByChatIdAsync(callback.ChatId, ct);
        if (link is null)
        {
            await botClient.AnswerCallbackQueryAsync(callback.CallbackQueryId, "Conta não vinculada.", ct);
            return;
        }

        if (callback.Data is not null && callback.Data.StartsWith("undo:", StringComparison.Ordinal))
        {
            string rawId = callback.Data["undo:".Length..];
            Guid? requestedTxId = Guid.TryParse(rawId, out Guid parsedId) ? parsedId : null;

            using IDisposable _ = currentUserOverride.RunAs(link.UserId, $"TelegramBot/chat:{callback.ChatId}");
            await HandleUndoAsync(link, callback.ChatId, requestedTxId, ct);
            await botClient.AnswerCallbackQueryAsync(callback.CallbackQueryId, "Lançamento desfeito.", ct);
            return;
        }

        await botClient.AnswerCallbackQueryAsync(callback.CallbackQueryId, null, ct);
    }

    private async Task HandleLinkCommandAsync(
        TelegramIncomingMessage message,
        string? rawToken,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            await botClient.SendMessageAsync(
                message.ChatId,
                "Envie o código gerado no site Razo:\n/vincular <seu-codigo>",
                ct: ct);
            return;
        }

        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        string hash = HashToken(rawToken.Trim());
        TelegramLinkToken? tokenEntity = await telegramRepository.GetTokenByHashAsync(hash, ct);

        if (tokenEntity is null || !tokenEntity.IsUsable(utcNow))
        {
            await botClient.SendMessageAsync(
                message.ChatId,
                "⚠️ Token inválido ou expirado. Gere um novo link em Segurança → Bot do Telegram no Razo.",
                ct: ct);
            return;
        }

        TelegramLink? existingByChat = await telegramRepository.GetLinkByChatIdAsync(message.ChatId, ct);
        if (existingByChat is not null && existingByChat.UserId != tokenEntity.UserId)
        {
            await botClient.SendMessageAsync(
                message.ChatId,
                "⚠️ Este chat já está vinculado a outra conta. Use /desconectar antes de vincular novamente.",
                ct: ct);
            return;
        }

        TelegramLink? existingByUser = await telegramRepository.GetLinkByUserIdAsync(tokenEntity.UserId, ct);
        if (existingByUser is not null)
        {
            telegramRepository.RemoveLink(existingByUser);
        }

        IReadOnlyList<Account> accounts = await accountRepository.GetByUserAsync(tokenEntity.UserId, includeArchived: false, ct);
        IReadOnlyList<CreditCard> cards = await creditCardRepository.GetByUserAsync(tokenEntity.UserId, includeArchived: false, ct);

        Guid? defaultAccountId = existingByUser?.DefaultAccountId ?? accounts.FirstOrDefault()?.Id;
        Guid? defaultCardId = existingByUser?.DefaultCreditCardId ?? cards.FirstOrDefault()?.Id;

        var newLink = new TelegramLink(
            tokenEntity.UserId,
            message.ChatId,
            message.Username,
            message.FirstName,
            defaultAccountId,
            defaultCardId,
            utcNow);

        tokenEntity.MarkConsumed(utcNow);
        await telegramRepository.AddLinkAsync(newLink, ct);

        await auditRepository.AddAsync(
            new SecurityAuditLog(
                tokenEntity.UserId,
                SecurityAuditEventType.TelegramLinked,
                currentUserService.IpAddress,
                $"TelegramBot/chat:{message.ChatId}",
                utcNow,
                $"{{\"chatId\":{message.ChatId}}}"),
            ct);

        await unitOfWork.SaveChangesAsync(ct);

        await botClient.SendMessageAsync(
            message.ChatId,
            "✅ Conta Razo vinculada com sucesso!\n\nExemplos rápidos:\n• Mercado 84,50\n• Uber 32 cartão\n• Notebook 3000 6x\n• + Salário 11200\n• /saldo · /resumo · /orcamentos",
            ct: ct);
    }

    private async Task HandleQuickTransactionAsync(
        TelegramLink link,
        long chatId,
        ParsedTelegramInput parsed,
        CancellationToken ct)
    {
        if (!parsed.Amount.HasValue || !parsed.TransactionType.HasValue || string.IsNullOrWhiteSpace(parsed.Description))
            return;

        Guid userId = link.UserId;
        IReadOnlyList<Account> accounts = await accountRepository.GetByUserAsync(userId, includeArchived: false, ct);
        IReadOnlyList<CreditCard> cards = await creditCardRepository.GetByUserAsync(userId, includeArchived: false, ct);
        IReadOnlyList<Category> categories = await categoryRepository.GetByUserAsync(userId, null, includeArchived: false, ct);

        ResolvedTelegramSource resolved = TelegramMessageParser.ResolvePaymentSource(
            parsed,
            accounts,
            cards,
            link.DefaultAccountId,
            link.DefaultCreditCardId);

        if (!resolved.AccountId.HasValue && !resolved.CreditCardId.HasValue)
        {
            await botClient.SendMessageAsync(
                chatId,
                "⚠️ Cadastre pelo menos uma conta ativa no Razo antes de lançar pelo Telegram.",
                ct: ct);
            return;
        }

        CategoryType catType = parsed.TransactionType.Value == TransactionType.Income
            ? CategoryType.Income
            : CategoryType.Expense;
        Category? category = TelegramMessageParser.MatchCategory(resolved.CleanDescription, catType, categories);

        string notes = string.IsNullOrWhiteSpace(resolved.PaymentMethodTag)
            ? "Lançado via Bot Telegram"
            : $"Lançado via Bot Telegram · {resolved.PaymentMethodTag}";

        DateOnly today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        var command = new CreateTransactionCommand(
            resolved.AccountId,
            resolved.CreditCardId,
            category?.Id,
            parsed.TransactionType.Value,
            TransactionStatus.Confirmed,
            resolved.CleanDescription,
            parsed.Amount.Value,
            today,
            parsed.Installments,
            notes);

        IReadOnlyList<TransactionDto> created = await transactionService.CreateAsync(command, ct);
        if (created.Count > 0)
        {
            link.SetLastBotTransaction(created[0].Id);
            await unitOfWork.SaveChangesAsync(ct);
        }

        string sign = parsed.TransactionType.Value == TransactionType.Income ? "+" : "-";
        string instText = parsed.Installments > 1 ? $" ({parsed.Installments}x de {FormatBrl(created[0].Amount)})" : string.Empty;
        string catLabel = category?.Name ?? "Sem categoria";
        string methodSuffix = string.IsNullOrWhiteSpace(resolved.PaymentMethodTag) ? string.Empty : $" ({resolved.PaymentMethodTag})";

        string reply = $"✅ Lançamento registrado\n{resolved.CleanDescription}: {sign} {FormatBrl(parsed.Amount.Value)}{instText}\n{catLabel} · {resolved.SourceName}{methodSuffix}";

        IReadOnlyList<TelegramInlineButton>? buttons = created.Count == 1
            ? [new TelegramInlineButton("↩️ Desfazer", $"undo:{created[0].Id}")]
            : null;

        await botClient.SendMessageAsync(chatId, reply, buttons, ct);
    }

    private async Task HandleBalanceAsync(long chatId, CancellationToken ct)
    {
        DateTime utcNow = dateTimeProvider.UtcNow.UtcDateTime;
        MonthlyDashboardSummaryDto summary = await dashboardService.GetMonthlySummaryAsync(utcNow.Year, utcNow.Month, ct);

        var sb = new StringBuilder();
        sb.AppendLine($"💰 Saldo consolidado: {FormatBrl(summary.ConsolidatedBalance)}");
        sb.AppendLine();
        if (summary.Accounts.Count == 0)
        {
            sb.AppendLine("Nenhuma conta ativa cadastrada.");
        }
        else
        {
            foreach (AccountBalanceSummaryDto acc in summary.Accounts)
            {
                sb.AppendLine($"• {acc.Name}: {FormatBrl(acc.CurrentBalance)}");
            }
        }

        await botClient.SendMessageAsync(chatId, sb.ToString().TrimEnd(), ct: ct);
    }

    private async Task HandleSummaryAsync(long chatId, CancellationToken ct)
    {
        DateTime utcNow = dateTimeProvider.UtcNow.UtcDateTime;
        MonthlyDashboardSummaryDto summary = await dashboardService.GetMonthlySummaryAsync(utcNow.Year, utcNow.Month, ct);

        decimal totalIncome = summary.ConfirmedIncome + summary.PendingIncome;
        decimal totalExpense = summary.ConfirmedExpense + summary.PendingExpense;

        var sb = new StringBuilder();
        sb.AppendLine($"📊 Resumo · {utcNow.Month:D2}/{utcNow.Year}");
        sb.AppendLine($"Saldo consolidado: {FormatBrl(summary.ConsolidatedBalance)}");
        sb.AppendLine($"Receitas: + {FormatBrl(totalIncome)}");
        sb.AppendLine($"Despesas: - {FormatBrl(totalExpense)}");
        sb.AppendLine($"Líquido previsto: {FormatBrl(summary.NetProjected)}");

        if (summary.CreditCardInvoices.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Cartões:");
            foreach (MonthlyInvoiceSummaryDto inv in summary.CreditCardInvoices)
            {
                sb.AppendLine($"• {inv.CreditCardName} ({inv.Status}): {FormatBrl(inv.TotalAmount)}");
            }
        }

        await botClient.SendMessageAsync(chatId, sb.ToString().TrimEnd(), ct: ct);
    }

    private async Task HandleBudgetsAsync(long chatId, CancellationToken ct)
    {
        DateTime utcNow = dateTimeProvider.UtcNow.UtcDateTime;
        MonthlyDashboardSummaryDto summary = await dashboardService.GetMonthlySummaryAsync(utcNow.Year, utcNow.Month, ct);

        if (summary.CategoryBudgets.Count == 0)
        {
            await botClient.SendMessageAsync(
                chatId,
                "Nenhum orçamento configurado ainda. Defina tetos por categoria na aba Planejamento do Razo.",
                ct: ct);
            return;
        }

        var sb = new StringBuilder();
        sb.AppendLine($"🎯 Orçamentos · {utcNow.Month:D2}/{utcNow.Year}");
        foreach (CategoryBudgetStatusDto b in summary.CategoryBudgets)
        {
            string icon = b.Status == "EXCEEDED" ? "🚨" : b.Status == "WARNING" ? "⚠️" : "✅";
            sb.AppendLine($"{icon} {b.CategoryName}: {FormatBrl(b.SpentAmount)} / {FormatBrl(b.MonthlyBudget)} ({b.UsagePercentage:0}%)");
        }

        await botClient.SendMessageAsync(chatId, sb.ToString().TrimEnd(), ct: ct);
    }

    private async Task HandleUndoAsync(
        TelegramLink link,
        long chatId,
        Guid? explicitTransactionId,
        CancellationToken ct)
    {
        Guid? txId = explicitTransactionId ?? link.LastBotTransactionId;
        if (!txId.HasValue)
        {
            await botClient.SendMessageAsync(chatId, "Nenhum lançamento recente do bot para desfazer.", ct: ct);
            return;
        }

        try
        {
            await transactionService.DeleteAsync(txId.Value, ct);
            if (link.LastBotTransactionId == txId.Value)
            {
                link.SetLastBotTransaction(null);
                await unitOfWork.SaveChangesAsync(ct);
            }

            await botClient.SendMessageAsync(chatId, "↩️ Último lançamento estornado com sucesso.", ct: ct);
        }
        catch (AppException ex)
        {
            await botClient.SendMessageAsync(chatId, $"⚠️ Não foi possível desfazer: {ex.Message}", ct: ct);
        }
    }

    private async Task HandleUnlinkFromChatAsync(TelegramLink link, long chatId, CancellationToken ct)
    {
        DateTimeOffset utcNow = dateTimeProvider.UtcNow;
        Guid userId = link.UserId;
        telegramRepository.RemoveLink(link);

        await auditRepository.AddAsync(
            new SecurityAuditLog(
                userId,
                SecurityAuditEventType.TelegramUnlinked,
                currentUserService.IpAddress,
                $"TelegramBot/chat:{chatId}",
                utcNow,
                $"{{\"chatId\":{chatId},\"origin\":\"telegram\"}}"),
            ct);

        await unitOfWork.SaveChangesAsync(ct);
        await botClient.SendMessageAsync(chatId, "🔌 Telegram desconectado da sua conta Razo.", ct: ct);
    }

    private async Task<string?> ResolveBotUsernameSafeAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(botClient.ConfiguredBotUsername))
            return botClient.ConfiguredBotUsername.TrimStart('@');

        if (!botClient.IsConfigured)
            return null;

        try
        {
            return await botClient.ResolveBotUsernameAsync(ct);
        }
        catch
        {
            return null;
        }
    }

    private static string HashToken(string rawToken)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(hash);
    }

    private static string FormatBrl(decimal value)
        => value.ToString("C2", PtBr);

    private static string BuildHelpText() =>
        """
        Razo · Comandos disponíveis

        Lançamentos rápidos:
        • Mercado 84,50 (despesa na conta padrão)
        • Mercado 152 pix nubank (escolhe a conta pelo nome)
        • Uber 32 cartão (ou Uber 32 inter black)
        • Notebook 3000 6x (parcelado no cartão)
        • + Salário 11200 (ou + Freelance 1300 itau)

        Consultas e controle:
        • /saldo — saldo consolidado e por conta
        • /resumo — entradas, saídas e faturas do mês
        • /orcamentos — status dos tetos por categoria
        • /desfazer — estorna o último lançamento do bot
        • /desconectar — revoga o vínculo deste chat
        """;
}
