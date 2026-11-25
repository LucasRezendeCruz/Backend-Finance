using Finance.Application.Common;
using Finance.Application.Common.Exceptions;
using Finance.Application.Common.Interfaces;
using Finance.Application.CreditCards;
using Finance.Domain.Common;
using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Application.Dashboard;

public sealed class DashboardService(
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    ICreditCardRepository creditCardRepository,
    ITransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider) : IDashboardService
{
    public async Task<MonthlyDashboardSummaryDto> GetMonthlySummaryAsync(
        int year,
        int month,
        CancellationToken ct = default)
    {
        if (year is < 1900 or > 9999 || month is < 1 or > 12)
        {
            throw AppException.Unprocessable("invalid_period", "Informe um mês (1..12) e ano válidos.");
        }

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        DateOnly today = DateOnly.FromDateTime(dateTimeProvider.UtcNow.UtcDateTime);
        DateOnly upcomingCutoff = today.AddDays(30);

        IReadOnlyList<Account> activeAccounts = await accountRepository.GetByUserAsync(userId, includeArchived: false, ct);
        IReadOnlyList<Category> categories = await categoryRepository.GetByUserAsync(userId, null, includeArchived: true, ct);
        IReadOnlyList<CreditCard> cards = await creditCardRepository.GetByUserAsync(userId, includeArchived: true, ct);
        IReadOnlyList<CreditCardInvoice> monthInvoices = await creditCardRepository.GetInvoicesForMonthAsync(userId, year, month, ct);

        Guid[] monthInvoiceIds = monthInvoices.Select(i => i.Id).ToArray();
        IReadOnlyList<Transaction> monthTransactions =
            await transactionRepository.GetForDashboardMonthAsync(userId, year, month, monthInvoiceIds, ct);

        decimal confirmedIncome = 0m;
        decimal pendingIncome = 0m;
        decimal confirmedExpense = 0m;
        decimal pendingExpense = 0m;

        var expenseTransactions = new List<Transaction>();

        foreach (Transaction tx in monthTransactions)
        {
            if (tx.Type is TransactionType.TransferIn or TransactionType.TransferOut or TransactionType.InvoicePayment)
                continue;

            if (tx.Type == TransactionType.Income)
            {
                if (tx.Status == TransactionStatus.Confirmed)
                    confirmedIncome += tx.Amount;
                else
                    pendingIncome += tx.Amount;
            }
            else if (tx.Type == TransactionType.Expense)
            {
                expenseTransactions.Add(tx);
                if (tx.Status == TransactionStatus.Confirmed)
                    confirmedExpense += tx.Amount;
                else
                    pendingExpense += tx.Amount;
            }
        }

        Dictionary<Guid, Category> categoriesById = categories.ToDictionary(c => c.Id);
        Dictionary<Guid, CreditCard> cardsById = cards.ToDictionary(c => c.Id);

        decimal totalExpenses = expenseTransactions.Sum(t => t.Amount);
        IReadOnlyList<CategoryExpenseSummaryDto> expensesByCategory = expenseTransactions
            .GroupBy(t => t.CategoryId)
            .Select(group =>
            {
                Category? category = group.Key.HasValue
                    ? categoriesById.GetValueOrDefault(group.Key.Value)
                    : null;

                decimal total = group.Sum(t => t.Amount);
                decimal pct = totalExpenses > 0m
                    ? decimal.Round((total / totalExpenses) * 100m, 2, MidpointRounding.AwayFromZero)
                    : 0m;

                return new CategoryExpenseSummaryDto(
                    group.Key,
                    category?.Name ?? "Sem categoria",
                    category?.Color ?? "#64748b",
                    category?.Icon ?? "tag",
                    total,
                    pct);
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToArray();

        IReadOnlyList<MonthlyInvoiceSummaryDto> invoiceSummaries = monthInvoices
            .OrderBy(inv => inv.DueDate)
            .Select(inv =>
            {
                string cardName = cardsById.TryGetValue(inv.CreditCardId, out CreditCard? card)
                    ? card.Name
                    : "Cartão";

                return new MonthlyInvoiceSummaryDto(
                    inv.Id,
                    inv.CreditCardId,
                    cardName,
                    inv.ReferenceYear,
                    inv.ReferenceMonth,
                    inv.ClosingDate,
                    inv.DueDate,
                    CreditCardService.ComputeEffectiveStatus(inv, today).ToScreamingSnakeCase(),
                    inv.TotalAmount);
            })
            .ToArray();

        IReadOnlyList<CreditCardInvoice> upcomingInvoices =
            await creditCardRepository.GetUpcomingUnpaidInvoicesAsync(userId, today, ct);
        IReadOnlyList<Transaction> upcomingAccountTxs =
            await transactionRepository.GetUpcomingPendingAccountTransactionsAsync(userId, today, ct);

        var upcomingDueItems = new List<UpcomingDueItemDto>();

        foreach (CreditCardInvoice inv in upcomingInvoices)
        {
            if (inv.DueDate > upcomingCutoff || inv.TotalAmount <= 0m)
                continue;

            string cardName = cardsById.TryGetValue(inv.CreditCardId, out CreditCard? card)
                ? card.Name
                : "Cartão";

            upcomingDueItems.Add(new UpcomingDueItemDto(
                "CREDIT_CARD_INVOICE",
                inv.Id,
                $"Fatura {cardName} ({inv.ReferenceMonth:D2}/{inv.ReferenceYear})",
                inv.TotalAmount,
                inv.DueDate,
                TransactionType.Expense.ToScreamingSnakeCase()));
        }

        foreach (Transaction tx in upcomingAccountTxs)
        {
            if (tx.TransactionDate > upcomingCutoff)
                continue;

            upcomingDueItems.Add(new UpcomingDueItemDto(
                "PENDING_TRANSACTION",
                tx.Id,
                tx.Description,
                tx.Amount,
                tx.TransactionDate,
                tx.Type.ToScreamingSnakeCase()));
        }

        IReadOnlyList<AccountBalanceSummaryDto> accountSummaries = activeAccounts
            .OrderBy(a => a.Name)
            .Select(a => new AccountBalanceSummaryDto(
                a.Id,
                a.Name,
                a.Type.ToScreamingSnakeCase(),
                a.CurrentBalance,
                a.Color))
            .ToArray();

        decimal consolidatedBalance = activeAccounts.Sum(a => a.CurrentBalance);
        decimal netConfirmed = confirmedIncome - confirmedExpense;
        decimal netProjected = (confirmedIncome + pendingIncome) - (confirmedExpense + pendingExpense);

        return new MonthlyDashboardSummaryDto(
            year,
            month,
            consolidatedBalance,
            confirmedIncome,
            pendingIncome,
            confirmedExpense,
            pendingExpense,
            netConfirmed,
            netProjected,
            accountSummaries,
            expensesByCategory,
            invoiceSummaries,
            upcomingDueItems.OrderBy(u => u.DueDate).ToArray());
    }
}
