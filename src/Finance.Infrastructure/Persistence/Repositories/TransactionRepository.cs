using Finance.Application.Common.Interfaces;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Finance.Infrastructure.Persistence.Repositories;

internal sealed class TransactionRepository(FinanceDbContext dbContext) : ITransactionRepository
{
    public async Task<(IReadOnlyList<Transaction> Items, int TotalCount)> GetPagedAsync(
        Guid userId,
        TransactionFilter filter,
        CancellationToken ct = default)
    {
        IQueryable<Transaction> query = dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId);

        if (filter.Year.HasValue && filter.Month.HasValue)
        {
            int days = DateTime.DaysInMonth(filter.Year.Value, filter.Month.Value);
            var start = new DateOnly(filter.Year.Value, filter.Month.Value, 1);
            var end = new DateOnly(filter.Year.Value, filter.Month.Value, days);
            query = query.Where(t => t.TransactionDate >= start && t.TransactionDate <= end);
        }
        else if (filter.Year.HasValue)
        {
            var start = new DateOnly(filter.Year.Value, 1, 1);
            var end = new DateOnly(filter.Year.Value, 12, 31);
            query = query.Where(t => t.TransactionDate >= start && t.TransactionDate <= end);
        }
        else if (filter.Month.HasValue)
        {
            int month = filter.Month.Value;
            query = query.Where(t => t.TransactionDate.Month == month);
        }

        if (filter.AccountId.HasValue)
            query = query.Where(t => t.AccountId == filter.AccountId.Value);

        if (filter.CreditCardId.HasValue)
        {
            Guid cardId = filter.CreditCardId.Value;
            query = query.Where(t =>
                t.CreditCardInvoiceId != null &&
                dbContext.CreditCardInvoices.Any(inv => inv.Id == t.CreditCardInvoiceId && inv.CreditCardId == cardId));
        }

        if (filter.CategoryId.HasValue)
            query = query.Where(t => t.CategoryId == filter.CategoryId.Value);

        if (filter.Type.HasValue)
            query = query.Where(t => t.Type == filter.Type.Value);

        if (filter.Status.HasValue)
            query = query.Where(t => t.Status == filter.Status.Value);

        if (filter.InstallmentGroupId.HasValue)
            query = query.Where(t => t.InstallmentGroupId == filter.InstallmentGroupId.Value);

        int totalCount = await query.CountAsync(ct);

        List<Transaction> items = await query
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAtUtc)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    public Task<Transaction?> GetByIdAsync(Guid userId, Guid transactionId, CancellationToken ct = default)
        => dbContext.Transactions.FirstOrDefaultAsync(t => t.UserId == userId && t.Id == transactionId, ct);

    public async Task<IReadOnlyList<Transaction>> GetByInvoiceIdAsync(
        Guid userId,
        Guid invoiceId,
        CancellationToken ct = default)
        => await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId &&
                t.CreditCardInvoiceId == invoiceId &&
                t.Type != TransactionType.InvoicePayment)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.InstallmentNumber ?? 0)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Transaction>> GetByInstallmentGroupAsync(
        Guid userId,
        Guid installmentGroupId,
        CancellationToken ct = default)
        => await dbContext.Transactions
            .Where(t => t.UserId == userId && t.InstallmentGroupId == installmentGroupId)
            .OrderBy(t => t.InstallmentNumber ?? 0)
            .ThenBy(t => t.TransactionDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Transaction>> GetAllInstallmentTransactionsAsync(
        Guid userId,
        CancellationToken ct = default)
        => await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.InstallmentGroupId != null)
            .OrderBy(t => t.InstallmentNumber ?? 0)
            .ThenBy(t => t.TransactionDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Transaction>> GetForDashboardMonthAsync(
        Guid userId,
        int year,
        int month,
        IReadOnlyCollection<Guid> monthInvoiceIds,
        CancellationToken ct = default)
    {
        int days = DateTime.DaysInMonth(year, month);
        var start = new DateOnly(year, month, 1);
        var end = new DateOnly(year, month, days);

        if (monthInvoiceIds.Count == 0)
        {
            return await dbContext.Transactions
                .AsNoTracking()
                .Where(t => t.UserId == userId &&
                    t.CreditCardInvoiceId == null &&
                    t.TransactionDate >= start &&
                    t.TransactionDate <= end)
                .ToListAsync(ct);
        }

        return await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId &&
                ((t.CreditCardInvoiceId != null && monthInvoiceIds.Contains(t.CreditCardInvoiceId.Value)) ||
                 (t.CreditCardInvoiceId == null && t.TransactionDate >= start && t.TransactionDate <= end)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Transaction>> GetUpcomingPendingAccountTransactionsAsync(
        Guid userId,
        DateOnly fromDate,
        CancellationToken ct = default)
        => await dbContext.Transactions
            .AsNoTracking()
            .Where(t => t.UserId == userId &&
                t.CreditCardInvoiceId == null &&
                t.AccountId != null &&
                t.Status == TransactionStatus.Pending &&
                t.TransactionDate >= fromDate)
            .OrderBy(t => t.TransactionDate)
            .ToListAsync(ct);

    public async Task AddAsync(Transaction transaction, CancellationToken ct = default)
        => await dbContext.Transactions.AddAsync(transaction, ct);

    public async Task AddRangeAsync(IReadOnlyCollection<Transaction> transactions, CancellationToken ct = default)
        => await dbContext.Transactions.AddRangeAsync(transactions, ct);

    public void Remove(Transaction transaction)
        => dbContext.Transactions.Remove(transaction);

    public void RemoveRange(IReadOnlyCollection<Transaction> transactions)
        => dbContext.Transactions.RemoveRange(transactions);
}
