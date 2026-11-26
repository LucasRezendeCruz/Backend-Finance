using Finance.Application.Common;
using Finance.Application.Common.Exceptions;
using Finance.Application.Common.Interfaces;
using Finance.Application.CreditCards;
using Finance.Application.Recurrences;
using Finance.Domain.Common;
using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Application.Dashboard;

public sealed class DashboardService(
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    ICreditCardRepository creditCardRepository,
    ITransactionRepository transactionRepository,
    IRecurrenceRepository recurrenceRepository,
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

        Dictionary<Guid, decimal> spentByCategoryId = expenseTransactions
            .Where(t => t.CategoryId.HasValue)
            .GroupBy(t => t.CategoryId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));

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

                decimal? budget = category?.MonthlyBudget;
                decimal? budgetUsage = budget.HasValue && budget.Value > 0m
                    ? decimal.Round((total / budget.Value) * 100m, 2, MidpointRounding.AwayFromZero)
                    : null;

                return new CategoryExpenseSummaryDto(
                    group.Key,
                    category?.Name ?? "Sem categoria",
                    category?.Color ?? "#64748b",
                    category?.Icon ?? "tag",
                    total,
                    pct,
                    budget,
                    budgetUsage);
            })
            .OrderByDescending(c => c.TotalAmount)
            .ToArray();

        IReadOnlyList<CategoryBudgetStatusDto> categoryBudgets = categories
            .Where(c => !c.IsArchived && c.Type == CategoryType.Expense && c.MonthlyBudget.HasValue && c.MonthlyBudget.Value > 0m)
            .Select(c =>
            {
                decimal budget = c.MonthlyBudget!.Value;
                decimal spent = spentByCategoryId.GetValueOrDefault(c.Id, 0m);
                decimal remaining = budget - spent;
                decimal usagePct = decimal.Round((spent / budget) * 100m, 2, MidpointRounding.AwayFromZero);
                string status = usagePct >= 100m ? "EXCEEDED" : usagePct >= 80m ? "WARNING" : "OK";

                return new CategoryBudgetStatusDto(
                    c.Id,
                    c.Name,
                    c.Color,
                    c.Icon,
                    budget,
                    spent,
                    remaining,
                    usagePct,
                    status);
            })
            .OrderByDescending(b => b.UsagePercentage)
            .ThenBy(b => b.CategoryName)
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
            upcomingDueItems.OrderBy(u => u.DueDate).ToArray(),
            categoryBudgets);
    }

    public async Task<CashflowProjectionDto> GetProjectionAsync(
        int startYear,
        int startMonth,
        int months = 6,
        CancellationToken ct = default)
    {
        if (startYear is < 1900 or > 9999 || startMonth is < 1 or > 12)
        {
            throw AppException.Unprocessable("invalid_period", "Informe um mês (1..12) e ano válidos.");
        }

        if (months is < 1 or > 12)
        {
            throw AppException.Unprocessable("invalid_projection_months", "Informe entre 1 e 12 meses para a projeção.");
        }

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);

        var periodSlots = new List<(int Year, int Month)>(months);
        var cursor = new DateOnly(startYear, startMonth, 1);
        for (int i = 0; i < months; i++)
        {
            periodSlots.Add((cursor.Year, cursor.Month));
            cursor = cursor.AddMonths(1);
        }

        (int endYear, int endMonth) = periodSlots[^1];
        var rangeStart = new DateOnly(startYear, startMonth, 1);
        var rangeEnd = new DateOnly(endYear, endMonth, DateTime.DaysInMonth(endYear, endMonth));

        IReadOnlyList<Account> activeAccounts = await accountRepository.GetByUserAsync(userId, includeArchived: false, ct);
        IReadOnlyList<Category> categories = await categoryRepository.GetByUserAsync(userId, null, includeArchived: true, ct);
        IReadOnlyList<CreditCard> cards = await creditCardRepository.GetByUserAsync(userId, includeArchived: true, ct);
        IReadOnlyList<RecurrenceRule> activeRecurrences = await recurrenceRepository.GetByUserAsync(userId, includeInactive: false, ct);
        IReadOnlyList<CreditCardInvoice> rangeInvoices =
            await creditCardRepository.GetInvoicesForRangeAsync(userId, startYear, startMonth, endYear, endMonth, ct);

        Guid[] rangeInvoiceIds = rangeInvoices.Select(i => i.Id).ToArray();
        IReadOnlyList<Transaction> rangeTransactions =
            await transactionRepository.GetForProjectionRangeAsync(userId, rangeStart, rangeEnd, rangeInvoiceIds, ct);

        Dictionary<Guid, Category> categoriesById = categories.ToDictionary(c => c.Id);
        Dictionary<Guid, CreditCard> cardsById = cards.ToDictionary(c => c.Id);
        Dictionary<Guid, Account> activeAccountsById = activeAccounts.ToDictionary(a => a.Id);
        Dictionary<Guid, CreditCardInvoice> invoicesById = rangeInvoices.ToDictionary(i => i.Id);

        // Index materialized recurrence occurrences by (RuleId, Date) to avoid double counting
        var materializedRecurrenceKeys = new HashSet<(Guid RuleId, DateOnly Date)>();
        foreach (Transaction tx in rangeTransactions)
        {
            if (tx.RecurrenceRuleId.HasValue)
            {
                materializedRecurrenceKeys.Add((tx.RecurrenceRuleId.Value, tx.TransactionDate));
            }
        }

        // Preload existing occurrence counts only for bounded recurrences
        var recurrenceExistingCounts = new Dictionary<Guid, int>();
        foreach (RecurrenceRule rule in activeRecurrences)
        {
            if (rule.TotalInstallments.HasValue)
            {
                recurrenceExistingCounts[rule.Id] = await recurrenceRepository.CountOccurrencesAsync(userId, rule.Id, ct);
            }
        }

        // Group transactions into their billing/cashflow month key (Year, Month)
        var txsByPeriod = periodSlots.ToDictionary(p => p, _ => new List<Transaction>());
        foreach (Transaction tx in rangeTransactions)
        {
            if (tx.Type is TransactionType.TransferIn or TransactionType.TransferOut or TransactionType.InvoicePayment)
                continue;

            (int Year, int Month) targetKey;
            if (tx.CreditCardInvoiceId.HasValue && invoicesById.TryGetValue(tx.CreditCardInvoiceId.Value, out CreditCardInvoice? inv))
            {
                targetKey = (inv.ReferenceYear, inv.ReferenceMonth);
            }
            else
            {
                targetKey = (tx.TransactionDate.Year, tx.TransactionDate.Month);
            }

            if (txsByPeriod.TryGetValue(targetKey, out List<Transaction>? bucket))
            {
                bucket.Add(tx);
            }
        }

        decimal startingBalance = activeAccounts.Sum(a => a.CurrentBalance);
        decimal runningBalance = startingBalance;
        decimal grandInstallments = 0m;
        decimal grandRecurrences = 0m;
        decimal grandCommittedExpenses = 0m;

        var monthPoints = new List<MonthlyProjectionPointDto>(months);

        foreach ((int year, int month) in periodSlots)
        {
            decimal confirmedIncome = 0m;
            decimal pendingIncome = 0m;
            decimal recurringIncome = 0m;
            decimal installmentExpenses = 0m;
            decimal recurringExpenses = 0m;
            decimal otherExpenses = 0m;
            decimal balanceDelta = 0m;
            int installmentCount = 0;
            int recurrenceCount = 0;

            var commitments = new List<ProjectionCommitmentDto>();

            foreach (Transaction tx in txsByPeriod[(year, month)])
            {
                Category? cat = tx.CategoryId.HasValue ? categoriesById.GetValueOrDefault(tx.CategoryId.Value) : null;
                string catName = cat?.Name ?? "Sem categoria";
                string catColor = cat?.Color ?? "#64748b";

                if (tx.Type == TransactionType.Income)
                {
                    if (tx.Status == TransactionStatus.Confirmed)
                    {
                        confirmedIncome += tx.Amount;
                    }
                    else
                    {
                        pendingIncome += tx.Amount;
                        balanceDelta += tx.Amount;
                    }
                }
                else if (tx.Type == TransactionType.Expense)
                {
                    bool isInstallment = tx.InstallmentGroupId.HasValue || (tx.TotalInstallments.HasValue && tx.TotalInstallments.Value > 1);
                    bool isMaterializedRecurrence = tx.RecurrenceRuleId.HasValue;

                    if (isInstallment)
                    {
                        installmentExpenses += tx.Amount;
                        installmentCount++;
                        commitments.Add(new ProjectionCommitmentDto(
                            "INSTALLMENT",
                            tx.Description,
                            catName,
                            catColor,
                            tx.Amount,
                            tx.TransactionDate,
                            tx.InstallmentNumber,
                            tx.TotalInstallments));
                    }
                    else if (isMaterializedRecurrence)
                    {
                        recurringExpenses += tx.Amount;
                        recurrenceCount++;
                        commitments.Add(new ProjectionCommitmentDto(
                            "RECURRENCE",
                            tx.Description,
                            catName,
                            catColor,
                            tx.Amount,
                            tx.TransactionDate,
                            tx.InstallmentNumber,
                            tx.TotalInstallments));
                    }
                    else
                    {
                        otherExpenses += tx.Amount;
                        if (tx.Status == TransactionStatus.Pending)
                        {
                            commitments.Add(new ProjectionCommitmentDto(
                                "PENDING",
                                tx.Description,
                                catName,
                                catColor,
                                tx.Amount,
                                tx.TransactionDate,
                                null,
                                null));
                        }
                    }

                    // Determine if this expense has NOT hit ConsolidatedBalance yet
                    bool impactsFutureBalance;
                    if (tx.CreditCardInvoiceId.HasValue && invoicesById.TryGetValue(tx.CreditCardInvoiceId.Value, out CreditCardInvoice? inv))
                    {
                        impactsFutureBalance = inv.Status != InvoiceStatus.Paid;
                    }
                    else
                    {
                        impactsFutureBalance = tx.Status != TransactionStatus.Confirmed;
                    }

                    if (impactsFutureBalance)
                    {
                        balanceDelta -= tx.Amount;
                    }
                }
            }

            // Project active recurrence rules that haven't been materialized yet in this month
            foreach (RecurrenceRule rule in activeRecurrences)
            {
                if (rule.AccountId.HasValue && !activeAccountsById.ContainsKey(rule.AccountId.Value))
                    continue;
                if (rule.CreditCardId.HasValue && (!cardsById.TryGetValue(rule.CreditCardId.Value, out CreditCard? card) || card.IsArchived))
                    continue;

                List<DateOnly> occurrences = RecurrenceService.GetOccurrencesInMonth(rule, year, month);
                if (occurrences.Count == 0)
                    continue;

                Category? cat = categoriesById.GetValueOrDefault(rule.CategoryId);
                string catName = cat?.Name ?? "Sem categoria";
                string catColor = cat?.Color ?? "#64748b";

                foreach (DateOnly occDate in occurrences)
                {
                    if (materializedRecurrenceKeys.Contains((rule.Id, occDate)))
                        continue;

                    if (rule.TotalInstallments.HasValue)
                    {
                        int used = recurrenceExistingCounts.GetValueOrDefault(rule.Id, 0);
                        if (used >= rule.TotalInstallments.Value)
                            break;
                        recurrenceExistingCounts[rule.Id] = used + 1;
                    }

                    if (rule.Type == RecurrenceType.Income)
                    {
                        recurringIncome += rule.Amount;
                        balanceDelta += rule.Amount;
                    }
                    else
                    {
                        recurringExpenses += rule.Amount;
                        recurrenceCount++;
                        balanceDelta -= rule.Amount;
                        commitments.Add(new ProjectionCommitmentDto(
                            "RECURRENCE",
                            rule.Description,
                            catName,
                            catColor,
                            rule.Amount,
                            occDate,
                            null,
                            rule.TotalInstallments));
                    }
                }
            }

            decimal totalProjectedIncome = confirmedIncome + pendingIncome + recurringIncome;
            decimal totalCommittedExpenses = installmentExpenses + recurringExpenses + otherExpenses;
            decimal projectedNet = totalProjectedIncome - totalCommittedExpenses;
            runningBalance += balanceDelta;

            grandInstallments += installmentExpenses;
            grandRecurrences += recurringExpenses;
            grandCommittedExpenses += totalCommittedExpenses;

            monthPoints.Add(new MonthlyProjectionPointDto(
                year,
                month,
                confirmedIncome,
                pendingIncome,
                recurringIncome,
                totalProjectedIncome,
                installmentExpenses,
                recurringExpenses,
                otherExpenses,
                totalCommittedExpenses,
                projectedNet,
                runningBalance,
                installmentCount,
                recurrenceCount,
                commitments.OrderByDescending(c => c.Amount).Take(8).ToArray()));
        }

        return new CashflowProjectionDto(
            startYear,
            startMonth,
            months,
            startingBalance,
            grandInstallments,
            grandRecurrences,
            grandCommittedExpenses,
            monthPoints);
    }
}
