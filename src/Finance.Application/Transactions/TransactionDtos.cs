using Finance.Domain.Enums;

namespace Finance.Application.Transactions;

public enum AnticipationMode
{
    MoveToInvoice,
    PayFromAccount
}

public enum InstallmentSelectionOrder
{
    FromLast,
    FromNext
}

public sealed record TransactionFilterQuery(
    int? Month = null,
    int? Year = null,
    Guid? AccountId = null,
    Guid? CreditCardId = null,
    Guid? CategoryId = null,
    TransactionType? Type = null,
    TransactionStatus? Status = null,
    Guid? InstallmentGroupId = null,
    int Page = 1,
    int PageSize = 20);

public sealed record CreateTransactionCommand(
    Guid? AccountId,
    Guid? CreditCardId,
    Guid? CategoryId,
    TransactionType Type,
    TransactionStatus? Status,
    string Description,
    decimal Amount,
    DateOnly TransactionDate,
    int Installments = 1,
    string? Notes = null);

public sealed record CreateTransferCommand(
    Guid SourceAccountId,
    Guid DestinationAccountId,
    string Description,
    decimal Amount,
    DateOnly TransferDate,
    string? Notes = null);

public sealed record UpdateTransactionCommand(
    Guid? CategoryId,
    string Description,
    decimal Amount,
    DateOnly TransactionDate,
    string? Notes = null);

public sealed record AnticipateInstallmentsCommand(
    IReadOnlyList<Guid>? TransactionIds = null,
    IReadOnlyList<int>? InstallmentNumbers = null,
    int? Count = null,
    InstallmentSelectionOrder SelectionOrder = InstallmentSelectionOrder.FromLast,
    AnticipationMode? Mode = null,
    Guid? TargetInvoiceId = null,
    Guid? PaymentAccountId = null,
    DateOnly? PaymentDate = null,
    decimal DiscountAmount = 0m);

public sealed record TransactionDto(
    Guid Id,
    Guid? AccountId,
    Guid? CreditCardInvoiceId,
    Guid? TransferPairId,
    Guid? RecurrenceRuleId,
    Guid? CategoryId,
    Guid? InstallmentGroupId,
    string Type,
    string Status,
    string Description,
    decimal Amount,
    decimal? OriginalAmount,
    DateOnly TransactionDate,
    int? InstallmentNumber,
    int? TotalInstallments,
    string? Notes,
    DateTimeOffset? AnticipatedAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record PagedTransactionsDto(
    IReadOnlyList<TransactionDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record TransferResultDto(
    TransactionDto OutgoingTransaction,
    TransactionDto IncomingTransaction);

public sealed record InstallmentItemDto(
    Guid TransactionId,
    int InstallmentNumber,
    int TotalInstallments,
    decimal Amount,
    decimal? OriginalAmount,
    DateOnly TransactionDate,
    string Status,
    Guid? AccountId,
    Guid? CreditCardInvoiceId,
    int? InvoiceReferenceYear,
    int? InvoiceReferenceMonth,
    string? InvoiceStatus,
    DateTimeOffset? AnticipatedAtUtc,
    bool CanAnticipate);

public sealed record InstallmentGroupSummaryDto(
    Guid InstallmentGroupId,
    string SourceKind,
    Guid? CreditCardId,
    Guid? AccountId,
    Guid? CategoryId,
    string Type,
    string Description,
    int TotalInstallments,
    int SettledCount,
    int RemainingCount,
    int CanAnticipateCount,
    decimal TotalOriginalAmount,
    decimal TotalCurrentAmount,
    decimal RemainingAmount,
    decimal TotalDiscountSaved,
    DateOnly? NextDueDate);

public sealed record InstallmentGroupDetailDto(
    Guid InstallmentGroupId,
    string SourceKind,
    Guid? CreditCardId,
    Guid? AccountId,
    Guid? CategoryId,
    string Type,
    string Description,
    int TotalInstallments,
    int SettledCount,
    int RemainingCount,
    int CanAnticipateCount,
    decimal TotalOriginalAmount,
    decimal TotalCurrentAmount,
    decimal RemainingAmount,
    decimal TotalDiscountSaved,
    DateOnly? NextDueDate,
    IReadOnlyList<InstallmentItemDto> Installments);

public sealed record AnticipationResultDto(
    Guid InstallmentGroupId,
    string Mode,
    int AnticipatedCount,
    decimal OriginalTotal,
    decimal DiscountAmount,
    decimal NetTotal,
    Guid? TargetInvoiceId,
    Guid? PaymentAccountId,
    IReadOnlyList<InstallmentItemDto> UpdatedInstallments);
