using Finance.Domain.Common;
using Finance.Domain.Enums;

namespace Finance.Domain.Entities;

public sealed class CreditCardInvoice : Entity
{
    public Guid CreditCardId { get; private set; }
    public int ReferenceYear { get; private set; }
    public int ReferenceMonth { get; private set; }
    public DateOnly ClosingDate { get; private set; }
    public DateOnly DueDate { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public decimal TotalAmount { get; private set; }
    public DateTimeOffset? PaidAtUtc { get; private set; }
    public Guid? PaidWithAccountId { get; private set; }

    private CreditCardInvoice() { }

    public CreditCardInvoice(
        Guid creditCardId,
        int referenceYear,
        int referenceMonth,
        DateOnly closingDate,
        DateOnly dueDate)
    {
        if (creditCardId == Guid.Empty)
            throw new DomainException("invalid_credit_card", "Cartão inválido.");
        if (referenceYear < 1 || referenceMonth is < 1 or > 12)
            throw new DomainException("invalid_invoice_reference", "Referência de fatura inválida.");

        CreditCardId = creditCardId;
        ReferenceYear = referenceYear;
        ReferenceMonth = referenceMonth;
        ClosingDate = closingDate;
        DueDate = dueDate;
        Status = InvoiceStatus.Open;
        TotalAmount = 0m;
    }

    public void AddCharge(decimal amount)
    {
        if (Status == InvoiceStatus.Paid)
            throw new DomainException("invoice_already_paid", "Não é possível lançar compras em uma fatura já paga.");
        if (amount <= 0)
            throw new DomainException("invalid_amount", "O valor do lançamento deve ser maior que zero.");

        TotalAmount = decimal.Round(TotalAmount + amount, 2, MidpointRounding.AwayFromZero);
    }

    public void RemoveCharge(decimal amount)
    {
        if (Status == InvoiceStatus.Paid)
            throw new DomainException("invoice_already_paid", "Não é possível alterar ou estornar compras de uma fatura já paga.");
        if (amount <= 0)
            throw new DomainException("invalid_amount", "O valor a estornar deve ser maior que zero.");

        TotalAmount = Math.Max(0m, decimal.Round(TotalAmount - amount, 2, MidpointRounding.AwayFromZero));
    }

    public void Close()
    {
        if (Status == InvoiceStatus.Paid)
            throw new DomainException("invoice_already_paid", "A fatura já está paga.");

        Status = InvoiceStatus.Closed;
    }

    public void UpdateBillingDates(DateOnly closingDate, DateOnly dueDate)
    {
        if (Status == InvoiceStatus.Paid)
            throw new DomainException("invoice_already_paid", "Não é possível alterar datas de uma fatura já paga.");

        ClosingDate = closingDate;
        DueDate = dueDate;
    }

    public void MarkAsPaid(Guid accountId, DateTimeOffset paidAtUtc)
    {
        if (Status == InvoiceStatus.Paid)
            throw new DomainException("invoice_already_paid", "Esta fatura já foi paga.");
        if (accountId == Guid.Empty)
            throw new DomainException("invalid_payment_account", "Selecione uma conta válida para pagar a fatura.");

        Status = InvoiceStatus.Paid;
        PaidWithAccountId = accountId;
        PaidAtUtc = paidAtUtc;
    }
}
