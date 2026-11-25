using Finance.Domain.Common;
using Finance.Domain.Enums;

namespace Finance.Domain.Entities;

public sealed class RecurrenceRule : Entity
{
    public Guid UserId { get; private set; }
    public RecurrenceType Type { get; private set; }
    public RecurrenceFrequency Frequency { get; private set; }
    public int? TotalInstallments { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid? AccountId { get; private set; }
    public Guid? CreditCardId { get; private set; }
    public DateOnly StartDate { get; private set; }
    public bool IsActive { get; private set; }

    private RecurrenceRule() { }

    public RecurrenceRule(
        Guid userId,
        RecurrenceType type,
        RecurrenceFrequency frequency,
        int? totalInstallments,
        string description,
        decimal amount,
        Guid categoryId,
        Guid? accountId,
        Guid? creditCardId,
        DateOnly startDate)
    {
        if (userId == Guid.Empty)
            throw new DomainException("invalid_user_id", "Usuário inválido.");
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("invalid_description", "A descrição da recorrência é obrigatória.");
        if (amount <= 0)
            throw new DomainException("invalid_amount", "O valor da recorrência deve ser maior que zero.");
        if (categoryId == Guid.Empty)
            throw new DomainException("invalid_category", "A categoria é obrigatória.");
        if (accountId.HasValue == creditCardId.HasValue)
            throw new DomainException("invalid_recurrence_source", "Informe exatamente uma conta ou um cartão de crédito.");
        if (totalInstallments.HasValue && totalInstallments.Value < 1)
            throw new DomainException("invalid_installments", "O total de parcelas deve ser pelo menos 1.");

        UserId = userId;
        Type = type;
        Frequency = frequency;
        TotalInstallments = totalInstallments;
        Description = description.Trim();
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        CategoryId = categoryId;
        AccountId = accountId;
        CreditCardId = creditCardId;
        StartDate = startDate;
        IsActive = true;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
