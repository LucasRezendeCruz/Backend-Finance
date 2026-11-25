using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Application.Common.Interfaces;

public sealed record TransactionFilter(
    int? Month,
    int? Year,
    Guid? AccountId,
    Guid? CreditCardId,
    Guid? CategoryId,
    TransactionType? Type,
    TransactionStatus? Status,
    Guid? InstallmentGroupId,
    int Page,
    int PageSize);

public interface ITransactionRepository
{
    Task<(IReadOnlyList<Transaction> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        TransactionFilter filter,
        CancellationToken ct = default);

    Task<Transaction?> GetByIdAsync(Guid userId, Guid transactionId, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByInvoiceIdAsync(Guid userId, Guid invoiceId, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetByInstallmentGroupAsync(Guid userId, Guid installmentGroupId, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetAllInstallmentTransactionsAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetForDashboardMonthAsync(
        Guid userId,
        int year,
        int month,
        IReadOnlyCollection<Guid> monthInvoiceIds,
        CancellationToken ct = default);
    Task<IReadOnlyList<Transaction>> GetUpcomingPendingAccountTransactionsAsync(
        Guid userId,
        DateOnly fromDate,
        CancellationToken ct = default);

    Task AddAsync(Transaction transaction, CancellationToken ct = default);
    Task AddRangeAsync(IReadOnlyCollection<Transaction> transactions, CancellationToken ct = default);
    void Remove(Transaction transaction);
    void RemoveRange(IReadOnlyCollection<Transaction> transactions);
}
