namespace Finance.Domain.Common;

public static class InstallmentMath
{
    public static IReadOnlyList<decimal> ApplyDiscount(IReadOnlyList<decimal> originalAmounts, decimal totalDiscount)
    {
        ArgumentNullException.ThrowIfNull(originalAmounts);

        if (originalAmounts.Count == 0)
            throw new DomainException("invalid_installments", "Nenhuma parcela selecionada para antecipação.");

        var normalized = new decimal[originalAmounts.Count];
        decimal totalOriginal = 0m;

        for (int i = 0; i < originalAmounts.Count; i++)
        {
            decimal amount = decimal.Round(originalAmounts[i], 2, MidpointRounding.AwayFromZero);
            if (amount <= 0m)
                throw new DomainException("invalid_amount", "Todas as parcelas devem ter valor maior que zero.");

            normalized[i] = amount;
            totalOriginal += amount;
        }

        decimal roundedDiscount = decimal.Round(totalDiscount, 2, MidpointRounding.AwayFromZero);
        if (roundedDiscount < 0m)
            throw new DomainException("invalid_discount", "O desconto não pode ser negativo.");

        decimal maxDiscount = totalOriginal - (0.01m * normalized.Length);
        if (roundedDiscount > maxDiscount)
        {
            throw new DomainException(
                "invalid_discount",
                "O desconto excede o permitido mantendo ao menos R$ 0,01 em cada parcela.");
        }

        if (roundedDiscount == 0m)
            return normalized;

        var discounts = new decimal[normalized.Length];
        decimal appliedSum = 0m;

        for (int i = 0; i < normalized.Length; i++)
        {
            decimal maxItemDiscount = normalized[i] - 0.01m;
            decimal proportional = Math.Round(
                (normalized[i] / totalOriginal) * roundedDiscount,
                2,
                MidpointRounding.ToZero);

            decimal itemDiscount = Math.Min(maxItemDiscount, proportional);
            discounts[i] = itemDiscount;
            appliedSum += itemDiscount;
        }

        decimal remainingDiscount = roundedDiscount - appliedSum;
        for (int i = 0; i < normalized.Length && remainingDiscount > 0m; i++)
        {
            decimal headroom = (normalized[i] - 0.01m) - discounts[i];
            if (headroom <= 0m)
                continue;

            decimal step = Math.Min(headroom, remainingDiscount);
            discounts[i] += step;
            remainingDiscount -= step;
        }

        var result = new decimal[normalized.Length];
        for (int i = 0; i < normalized.Length; i++)
        {
            result[i] = normalized[i] - discounts[i];
        }

        return result;
    }
}
