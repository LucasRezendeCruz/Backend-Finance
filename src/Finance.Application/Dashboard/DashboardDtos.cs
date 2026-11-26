namespace Finance.Application.Dashboard;

public sealed record AccountBalanceSummaryDto(
    Guid Id,
    string Name,
    string Type,
    decimal CurrentBalance,
    string Color);

public sealed record CategoryExpenseSummaryDto(
    Guid? CategoryId,
    string CategoryName,
    string Color,
    string Icon,
    decimal TotalAmount,
    decimal Percentage,
    decimal? MonthlyBudget = null,
    decimal? BudgetUsagePercentage = null);

public sealed record CategoryBudgetStatusDto(
    Guid CategoryId,
    string CategoryName,
    string Color,
    string Icon,
    decimal MonthlyBudget,
    decimal SpentAmount,
    decimal RemainingAmount,
    decimal UsagePercentage,
    string Status);

public sealed record MonthlyInvoiceSummaryDto(
    Guid InvoiceId,
    Guid CreditCardId,
    string CreditCardName,
    int ReferenceYear,
    int ReferenceMonth,
    DateOnly ClosingDate,
    DateOnly DueDate,
    string Status,
    decimal TotalAmount);

public sealed record UpcomingDueItemDto(
    string Kind,
    Guid ReferenceId,
    string Title,
    decimal Amount,
    DateOnly DueDate,
    string Type);

public sealed record MonthlyDashboardSummaryDto(
    int Year,
    int Month,
    decimal ConsolidatedBalance,
    decimal ConfirmedIncome,
    decimal PendingIncome,
    decimal ConfirmedExpense,
    decimal PendingExpense,
    decimal NetConfirmed,
    decimal NetProjected,
    IReadOnlyList<AccountBalanceSummaryDto> Accounts,
    IReadOnlyList<CategoryExpenseSummaryDto> ExpensesByCategory,
    IReadOnlyList<MonthlyInvoiceSummaryDto> CreditCardInvoices,
    IReadOnlyList<UpcomingDueItemDto> UpcomingDueItems,
    IReadOnlyList<CategoryBudgetStatusDto> CategoryBudgets);

public sealed record ProjectionCommitmentDto(
    string Kind,
    string Description,
    string CategoryName,
    string Color,
    decimal Amount,
    DateOnly ReferenceDate,
    int? InstallmentNumber,
    int? TotalInstallments);

public sealed record MonthlyProjectionPointDto(
    int Year,
    int Month,
    decimal ConfirmedIncome,
    decimal PendingIncome,
    decimal RecurringIncome,
    decimal TotalProjectedIncome,
    decimal InstallmentExpenses,
    decimal RecurringExpenses,
    decimal OtherExpenses,
    decimal TotalCommittedExpenses,
    decimal ProjectedNet,
    decimal RunningProjectedBalance,
    int InstallmentCount,
    int RecurrenceCount,
    IReadOnlyList<ProjectionCommitmentDto> TopCommitments);

public sealed record CashflowProjectionDto(
    int StartYear,
    int StartMonth,
    int MonthsCount,
    decimal StartingConsolidatedBalance,
    decimal TotalInstallmentsCommitted,
    decimal TotalRecurrencesCommitted,
    decimal TotalCommittedExpenses,
    IReadOnlyList<MonthlyProjectionPointDto> Months);
