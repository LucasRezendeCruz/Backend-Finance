namespace Finance.Application.CreditCards;

public sealed record CreateCreditCardCommand(
    string Name,
    decimal CreditLimit,
    int ClosingDay,
    int DueDay,
    Guid? DefaultAccountId,
    string? Color);

public sealed record UpdateCreditCardCommand(
    string Name,
    decimal CreditLimit,
    int ClosingDay,
    int DueDay,
    Guid? DefaultAccountId,
    string? Color);

public sealed record PayInvoiceCommand(
    Guid? AccountId,
    DateOnly? PaymentDate);

public sealed record CreditCardDto(
    Guid Id,
    Guid? DefaultAccountId,
    string Name,
    decimal CreditLimit,
    decimal UsedLimit,
    decimal AvailableLimit,
    int ClosingDay,
    int DueDay,
    string Color,
    bool IsArchived);

public sealed record CreditCardInvoiceDto(
    Guid Id,
    Guid CreditCardId,
    int ReferenceYear,
    int ReferenceMonth,
    DateOnly ClosingDate,
    DateOnly DueDate,
    string Status,
    decimal TotalAmount,
    DateTimeOffset? PaidAtUtc,
    Guid? PaidWithAccountId);

public sealed record InvoiceTransactionItemDto(
    Guid Id,
    Guid? CategoryId,
    Guid? InstallmentGroupId,
    string Description,
    decimal Amount,
    decimal? OriginalAmount,
    DateOnly TransactionDate,
    int? InstallmentNumber,
    int? TotalInstallments,
    string? Notes,
    DateTimeOffset? AnticipatedAtUtc);

public sealed record CreditCardInvoiceDetailDto(
    Guid Id,
    Guid CreditCardId,
    string CreditCardName,
    int ReferenceYear,
    int ReferenceMonth,
    DateOnly ClosingDate,
    DateOnly DueDate,
    string Status,
    decimal TotalAmount,
    DateTimeOffset? PaidAtUtc,
    Guid? PaidWithAccountId,
    IReadOnlyList<InvoiceTransactionItemDto> Transactions);
