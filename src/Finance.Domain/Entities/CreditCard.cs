using Finance.Domain.Common;

namespace Finance.Domain.Entities;

public sealed class CreditCard : Entity
{
    public Guid UserId { get; private set; }
    public Guid? DefaultAccountId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal CreditLimit { get; private set; }
    public int ClosingDay { get; private set; }
    public int DueDay { get; private set; }
    public string Color { get; private set; } = string.Empty;
    public bool IsArchived { get; private set; }

    private CreditCard() { }

    public CreditCard(
        Guid userId,
        Guid? defaultAccountId,
        string name,
        decimal creditLimit,
        int closingDay,
        int dueDay,
        string color)
    {
        if (userId == Guid.Empty)
            throw new DomainException("invalid_user_id", "Usuário inválido.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("invalid_card_name", "O nome do cartão é obrigatório.");
        if (creditLimit <= 0)
            throw new DomainException("invalid_credit_limit", "O limite do cartão deve ser maior que zero.");

        ValidateBillingDays(closingDay, dueDay);

        UserId = userId;
        DefaultAccountId = defaultAccountId;
        Name = name.Trim();
        CreditLimit = decimal.Round(creditLimit, 2, MidpointRounding.AwayFromZero);
        ClosingDay = closingDay;
        DueDay = dueDay;
        Color = string.IsNullOrWhiteSpace(color) ? "#ff5a36" : color.Trim();
        IsArchived = false;
    }

    public void Update(Guid? defaultAccountId, string name, decimal creditLimit, int closingDay, int dueDay, string color)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("invalid_card_name", "O nome do cartão é obrigatório.");
        if (creditLimit <= 0)
            throw new DomainException("invalid_credit_limit", "O limite do cartão deve ser maior que zero.");

        ValidateBillingDays(closingDay, dueDay);

        DefaultAccountId = defaultAccountId;
        Name = name.Trim();
        CreditLimit = decimal.Round(creditLimit, 2, MidpointRounding.AwayFromZero);
        ClosingDay = closingDay;
        DueDay = dueDay;
        if (!string.IsNullOrWhiteSpace(color))
            Color = color.Trim();
    }

    public void SetArchived(bool isArchived) => IsArchived = isArchived;

    /// <summary>
    /// Calcula o ano e mês de referência da fatura com base na data da compra e no dia de fechamento.
    /// Compras realizadas no dia de fechamento ou depois entram na fatura do mês seguinte.
    /// </summary>
    public (int Year, int Month) CalculateInvoiceReference(DateOnly purchaseDate)
    {
        int daysInMonth = DateTime.DaysInMonth(purchaseDate.Year, purchaseDate.Month);
        int effectiveClosingDay = Math.Min(ClosingDay, daysInMonth);

        if (purchaseDate.Day >= effectiveClosingDay)
        {
            var nextMonth = new DateOnly(purchaseDate.Year, purchaseDate.Month, 1).AddMonths(1);
            return (nextMonth.Year, nextMonth.Month);
        }

        return (purchaseDate.Year, purchaseDate.Month);
    }

    /// <summary>
    /// Calcula as datas reais de fechamento e vencimento para uma fatura de referência (Ano/Mês).
    /// Se DueDay &lt;= ClosingDay, o vencimento ocorre no mês seguinte ao fechamento.
    /// </summary>
    public (DateOnly ClosingDate, DateOnly DueDate) CalculateInvoiceDates(int referenceYear, int referenceMonth)
    {
        if (referenceYear < 1 || referenceMonth is < 1 or > 12)
            throw new DomainException("invalid_invoice_reference", "Mês ou ano de referência inválido.");

        int closingDays = DateTime.DaysInMonth(referenceYear, referenceMonth);
        var closingDate = new DateOnly(referenceYear, referenceMonth, Math.Min(ClosingDay, closingDays));

        int dueYear = referenceYear;
        int dueMonth = referenceMonth;

        if (DueDay <= ClosingDay)
        {
            var nextMonth = new DateOnly(referenceYear, referenceMonth, 1).AddMonths(1);
            dueYear = nextMonth.Year;
            dueMonth = nextMonth.Month;
        }

        int dueDays = DateTime.DaysInMonth(dueYear, dueMonth);
        var dueDate = new DateOnly(dueYear, dueMonth, Math.Min(DueDay, dueDays));

        return (closingDate, dueDate);
    }

    /// <summary>
    /// Divide um valor total em N parcelas com 2 casas decimais, jogando eventual resto de centavos na 1ª parcela.
    /// </summary>
    public static IReadOnlyList<decimal> SplitInstallments(decimal totalAmount, int installments)
    {
        if (totalAmount <= 0)
            throw new DomainException("invalid_amount", "O valor total da compra deve ser maior que zero.");
        if (installments < 1)
            throw new DomainException("invalid_installments", "O número de parcelas deve ser pelo menos 1.");

        decimal roundedTotal = decimal.Round(totalAmount, 2, MidpointRounding.AwayFromZero);
        if (installments == 1)
            return [roundedTotal];

        decimal baseInstallment = Math.Round(roundedTotal / installments, 2, MidpointRounding.ToZero);
        if (baseInstallment <= 0)
            throw new DomainException("invalid_installment_amount", "Cada parcela deve ter valor mínimo de R$ 0,01.");

        decimal remainder = roundedTotal - (baseInstallment * installments);
        var result = new decimal[installments];
        result[0] = baseInstallment + remainder;

        for (int i = 1; i < installments; i++)
            result[i] = baseInstallment;

        return result;
    }

    private static void ValidateBillingDays(int closingDay, int dueDay)
    {
        if (closingDay is < 1 or > 31)
            throw new DomainException("invalid_closing_day", "O dia de fechamento deve estar entre 1 e 31.");
        if (dueDay is < 1 or > 31)
            throw new DomainException("invalid_due_day", "O dia de vencimento deve estar entre 1 e 31.");
    }
}
