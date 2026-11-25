using Finance.Application.Common.Interfaces;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Finance.Infrastructure.Persistence.Repositories;

internal sealed class CreditCardRepository(FinanceDbContext dbContext) : ICreditCardRepository
{
    public async Task<IReadOnlyList<CreditCard>> GetByUserAsync(
        Guid userId,
        bool includeArchived,
        CancellationToken ct = default)
    {
        IQueryable<CreditCard> query = dbContext.CreditCards
            .AsNoTracking()
            .Where(c => c.UserId == userId);

        if (!includeArchived)
            query = query.Where(c => !c.IsArchived);

        return await query
            .OrderBy(c => c.IsArchived)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);
    }

    public Task<CreditCard?> GetByIdAsync(Guid userId, Guid creditCardId, CancellationToken ct = default)
        => dbContext.CreditCards.FirstOrDefaultAsync(c => c.UserId == userId && c.Id == creditCardId, ct);

    public async Task AddAsync(CreditCard card, CancellationToken ct = default)
        => await dbContext.CreditCards.AddAsync(card, ct);

    public async Task<IReadOnlyList<CreditCardInvoice>> GetUnpaidInvoicesByUserAsync(
        Guid userId,
        CancellationToken ct = default)
        => await dbContext.CreditCardInvoices
            .AsNoTracking()
            .Where(inv => inv.Status != InvoiceStatus.Paid &&
                dbContext.CreditCards.Any(c => c.Id == inv.CreditCardId && c.UserId == userId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CreditCardInvoice>> GetInvoicesByIdsAsync(
        IReadOnlyCollection<Guid> invoiceIds,
        CancellationToken ct = default)
    {
        if (invoiceIds.Count == 0)
            return [];

        return await dbContext.CreditCardInvoices
            .Where(inv => invoiceIds.Contains(inv.Id))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CreditCardInvoice>> GetInvoicesByCardAsync(
        Guid userId,
        Guid creditCardId,
        int? year,
        CancellationToken ct = default)
    {
        IQueryable<CreditCardInvoice> query = dbContext.CreditCardInvoices
            .AsNoTracking()
            .Where(inv => inv.CreditCardId == creditCardId &&
                dbContext.CreditCards.Any(c => c.Id == creditCardId && c.UserId == userId));

        if (year.HasValue)
            query = query.Where(inv => inv.ReferenceYear == year.Value);

        return await query
            .OrderByDescending(inv => inv.ReferenceYear)
            .ThenByDescending(inv => inv.ReferenceMonth)
            .ToListAsync(ct);
    }

    public Task<CreditCardInvoice?> GetInvoiceByIdAsync(
        Guid userId,
        Guid creditCardId,
        Guid invoiceId,
        CancellationToken ct = default)
        => dbContext.CreditCardInvoices
            .FirstOrDefaultAsync(inv =>
                inv.Id == invoiceId &&
                inv.CreditCardId == creditCardId &&
                dbContext.CreditCards.Any(c => c.Id == creditCardId && c.UserId == userId), ct);

    public Task<CreditCardInvoice?> GetInvoiceByIdDirectAsync(Guid invoiceId, CancellationToken ct = default)
        => dbContext.CreditCardInvoices.FirstOrDefaultAsync(inv => inv.Id == invoiceId, ct);

    public Task<CreditCardInvoice?> GetInvoiceByReferenceAsync(
        Guid creditCardId,
        int referenceYear,
        int referenceMonth,
        CancellationToken ct = default)
        => dbContext.CreditCardInvoices.FirstOrDefaultAsync(
            inv => inv.CreditCardId == creditCardId &&
                   inv.ReferenceYear == referenceYear &&
                   inv.ReferenceMonth == referenceMonth,
            ct);

    public async Task<IReadOnlyList<CreditCardInvoice>> GetOpenInvoicesByCardAsync(
        Guid creditCardId,
        CancellationToken ct = default)
        => await dbContext.CreditCardInvoices
            .Where(inv => inv.CreditCardId == creditCardId && inv.Status == InvoiceStatus.Open)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CreditCardInvoice>> GetUnpaidInvoicesByCardAsync(
        Guid creditCardId,
        CancellationToken ct = default)
        => await dbContext.CreditCardInvoices
            .AsNoTracking()
            .Where(inv => inv.CreditCardId == creditCardId && inv.Status != InvoiceStatus.Paid)
            .ToListAsync(ct);

    public async Task AddInvoiceAsync(CreditCardInvoice invoice, CancellationToken ct = default)
        => await dbContext.CreditCardInvoices.AddAsync(invoice, ct);

    public async Task<IReadOnlyList<CreditCardInvoice>> GetInvoicesForMonthAsync(
        Guid userId,
        int year,
        int month,
        CancellationToken ct = default)
        => await dbContext.CreditCardInvoices
            .AsNoTracking()
            .Where(inv => inv.ReferenceYear == year &&
                inv.ReferenceMonth == month &&
                dbContext.CreditCards.Any(c => c.Id == inv.CreditCardId && c.UserId == userId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<CreditCardInvoice>> GetUpcomingUnpaidInvoicesAsync(
        Guid userId,
        DateOnly fromDate,
        CancellationToken ct = default)
        => await dbContext.CreditCardInvoices
            .AsNoTracking()
            .Where(inv => inv.Status != InvoiceStatus.Paid &&
                inv.DueDate >= fromDate &&
                dbContext.CreditCards.Any(c => c.Id == inv.CreditCardId && c.UserId == userId))
            .OrderBy(inv => inv.DueDate)
            .ToListAsync(ct);
}
