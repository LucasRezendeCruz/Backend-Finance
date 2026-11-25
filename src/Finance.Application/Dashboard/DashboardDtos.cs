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
    decimal Percentage);

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
    IReadOnlyList<UpcomingDueItemDto> UpcomingDueItems);
