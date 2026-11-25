using Finance.Domain.Common;
using Finance.Domain.Enums;

namespace Finance.Domain.Entities;

public sealed class Transaction : Entity
{
    public Guid UserId { get; private set; }
    public Guid? AccountId { get; private set; }
    public Guid? CreditCardInvoiceId { get; private set; }
    public Guid? TransferPairId { get; private set; }
    public Guid? RecurrenceRuleId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public Guid? InstallmentGroupId { get; private set; }
    public TransactionType Type { get; private set; }
    public TransactionStatus Status { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public decimal? OriginalAmount { get; private set; }
    public DateOnly TransactionDate { get; private set; }
    public int? InstallmentNumber { get; private set; }
    public int? TotalInstallments { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset? AnticipatedAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Transaction() { }

    public Transaction(
        Guid userId,
        Guid? accountId,
        Guid? creditCardInvoiceId,
        Guid? categoryId,
        TransactionType type,
        TransactionStatus status,
        string description,
        decimal amount,
        DateOnly transactionDate,
        DateTimeOffset createdAtUtc,
        Guid? recurrenceRuleId = null,
        int? installmentNumber = null,
        int? totalInstallments = null,
        string? notes = null,
        Guid? installmentGroupId = null)
    {
        if (userId == Guid.Empty)
            throw new DomainException("invalid_user_id", "Usuário inválido.");
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("invalid_description", "A descrição do lançamento é obrigatória.");
        if (amount <= 0)
            throw new DomainException("invalid_amount", "O valor do lançamento deve ser maior que zero.");

        if (installmentNumber.HasValue && totalInstallments.HasValue &&
            (installmentNumber.Value < 1 || installmentNumber.Value > totalInstallments.Value))
        {
            throw new DomainException("invalid_installment_number", "Número de parcela inválido.");
        }

        UserId = userId;
        AccountId = accountId;
        CreditCardInvoiceId = creditCardInvoiceId;
        CategoryId = categoryId;
        InstallmentGroupId = installmentGroupId;
        Type = type;
        Status = status;
        Description = description.Trim();
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        TransactionDate = transactionDate;
        CreatedAtUtc = createdAtUtc;
        RecurrenceRuleId = recurrenceRuleId;
        InstallmentNumber = installmentNumber;
        TotalInstallments = totalInstallments;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void Confirm()
    {
        if (Status == TransactionStatus.Confirmed)
            throw new DomainException("transaction_already_confirmed", "Este lançamento já está confirmado.");

        Status = TransactionStatus.Confirmed;
    }

    public void LinkTransferPair(Guid pairId)
    {
        if (pairId == Guid.Empty)
            throw new DomainException("invalid_transfer_pair", "Identificador de transferência inválido.");

        TransferPairId = pairId;
    }

    public void UnlinkTransferPair()
    {
        TransferPairId = null;
    }

    public void UpdateDetails(string description, decimal amount, DateOnly transactionDate, Guid? categoryId, string? notes)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("invalid_description", "A descrição do lançamento é obrigatória.");
        if (amount <= 0)
            throw new DomainException("invalid_amount", "O valor do lançamento deve ser maior que zero.");

        Description = description.Trim();
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        TransactionDate = transactionDate;
        CategoryId = categoryId;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    public void AnticipateToInvoice(
        Guid targetInvoiceId,
        DateOnly anticipatedDate,
        decimal netAmount,
        DateTimeOffset anticipatedAtUtc)
    {
        if (AnticipatedAtUtc.HasValue)
            throw new DomainException("installment_already_anticipated", "Esta parcela já foi antecipada.");
        if (targetInvoiceId == Guid.Empty)
            throw new DomainException("invalid_invoice", "Fatura de destino inválida.");

        decimal roundedNet = decimal.Round(netAmount, 2, MidpointRounding.AwayFromZero);
        if (roundedNet <= 0m || roundedNet > Amount)
            throw new DomainException("invalid_amount", "Valor líquido da antecipação inválido para esta parcela.");

        if (roundedNet < Amount)
        {
            OriginalAmount ??= Amount;
            Amount = roundedNet;
        }

        CreditCardInvoiceId = targetInvoiceId;
        TransactionDate = anticipatedDate;
        AnticipatedAtUtc = anticipatedAtUtc;
    }

    public void AnticipateFromAccount(
        Guid paymentAccountId,
        DateOnly paymentDate,
        decimal netAmount,
        DateTimeOffset anticipatedAtUtc)
    {
        if (AnticipatedAtUtc.HasValue)
            throw new DomainException("installment_already_anticipated", "Esta parcela já foi antecipada.");
        if (paymentAccountId == Guid.Empty)
            throw new DomainException("invalid_payment_account", "Conta pagadora inválida.");

        decimal roundedNet = decimal.Round(netAmount, 2, MidpointRounding.AwayFromZero);
        if (roundedNet <= 0m || roundedNet > Amount)
            throw new DomainException("invalid_amount", "Valor líquido da antecipação inválido para esta parcela.");

        if (roundedNet < Amount)
        {
            OriginalAmount ??= Amount;
            Amount = roundedNet;
        }

        CreditCardInvoiceId = null;
        AccountId = paymentAccountId;
        Status = TransactionStatus.Confirmed;
        TransactionDate = paymentDate;
        AnticipatedAtUtc = anticipatedAtUtc;
    }
}
