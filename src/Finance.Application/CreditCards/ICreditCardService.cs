namespace Finance.Application.CreditCards;

public interface ICreditCardService
{
    Task<IReadOnlyList<CreditCardDto>> GetAllAsync(bool includeArchived, CancellationToken ct = default);
    Task<CreditCardDto> CreateAsync(CreateCreditCardCommand command, CancellationToken ct = default);
    Task<CreditCardDto> UpdateAsync(Guid id, UpdateCreditCardCommand command, CancellationToken ct = default);
    Task<IReadOnlyList<CreditCardInvoiceDto>> GetInvoicesAsync(Guid cardId, int? year, CancellationToken ct = default);
    Task<CreditCardInvoiceDetailDto> GetInvoiceDetailAsync(Guid cardId, Guid invoiceId, CancellationToken ct = default);
    Task<CreditCardInvoiceDto> PayInvoiceAsync(Guid cardId, Guid invoiceId, PayInvoiceCommand command, CancellationToken ct = default);
}
