namespace Finance.Application.Transactions;

public interface ITransactionService
{
    Task<PagedTransactionsDto> GetPagedAsync(TransactionFilterQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<TransactionDto>> CreateAsync(CreateTransactionCommand command, CancellationToken ct = default);
    Task<TransferResultDto> TransferAsync(CreateTransferCommand command, CancellationToken ct = default);
    Task<TransactionDto> UpdateAsync(Guid id, UpdateTransactionCommand command, CancellationToken ct = default);
    Task<TransactionDto> ConfirmAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<InstallmentGroupSummaryDto>> GetInstallmentGroupsAsync(
        bool includeCompleted,
        CancellationToken ct = default);

    Task<InstallmentGroupDetailDto> GetInstallmentGroupDetailAsync(
        Guid groupId,
        CancellationToken ct = default);

    Task<AnticipationResultDto> AnticipateInstallmentsAsync(
        Guid groupId,
        AnticipateInstallmentsCommand command,
        CancellationToken ct = default);
}
