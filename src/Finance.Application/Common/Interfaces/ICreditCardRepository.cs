using Finance.Domain.Entities;

namespace Finance.Application.Common.Interfaces;

public interface ICreditCardRepository
{
    Task<IReadOnlyList<CreditCard>> GetByUserAsync(Guid userId, bool includeArchived, CancellationToken ct = default);
    Task<CreditCard?> GetByIdAsync(Guid userId, Guid creditCardId, CancellationToken ct = default);
    Task AddAsync(CreditCard card, CancellationToken ct = default);

    Task<IReadOnlyList<CreditCardInvoice>> GetUnpaidInvoicesByUserAsync(Guid userId, CancellationToken ct = default);
    Task<IReadOnlyList<CreditCardInvoice>> GetInvoicesByIdsAsync(IReadOnlyCollection<Guid> invoiceIds, CancellationToken ct = default);
    Task<IReadOnlyList<CreditCardInvoice>> GetInvoicesByCardAsync(Guid userId, Guid creditCardId, int? year, CancellationToken ct = default);
    Task<CreditCardInvoice?> GetInvoiceByIdAsync(Guid userId, Guid creditCardId, Guid invoiceId, CancellationToken ct = default);
    Task<CreditCardInvoice?> GetInvoiceByIdDirectAsync(Guid invoiceId, CancellationToken ct = default);
    Task<CreditCardInvoice?> GetInvoiceByReferenceAsync(Guid creditCardId, int referenceYear, int referenceMonth, CancellationToken ct = default);
    Task<IReadOnlyList<CreditCardInvoice>> GetOpenInvoicesByCardAsync(Guid creditCardId, CancellationToken ct = default);
    Task<IReadOnlyList<CreditCardInvoice>> GetUnpaidInvoicesByCardAsync(Guid creditCardId, CancellationToken ct = default);
    Task AddInvoiceAsync(CreditCardInvoice invoice, CancellationToken ct = default);
    Task<IReadOnlyList<CreditCardInvoice>> GetInvoicesForMonthAsync(Guid userId, int year, int month, CancellationToken ct = default);
    Task<IReadOnlyList<CreditCardInvoice>> GetInvoicesForRangeAsync(
        Guid userId,
        int startYear,
        int startMonth,
        int endYear,
        int endMonth,
        CancellationToken ct = default);
    Task<IReadOnlyList<CreditCardInvoice>> GetUpcomingUnpaidInvoicesAsync(Guid userId, DateOnly fromDate, CancellationToken ct = default);
}
