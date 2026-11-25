using Finance.Domain.Common;
using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Domain.Tests;

public sealed class InstallmentAnticipationDomainTests
{
    [Fact]
    public void ApplyDiscount_ZeroDiscount_ReturnsNormalizedAmounts()
    {
        IReadOnlyList<decimal> result = InstallmentMath.ApplyDiscount([100m, 100m, 100m], 0m);

        Assert.Equal([100m, 100m, 100m], result);
    }

    [Fact]
    public void ApplyDiscount_ProportionalWithCentsRemainder_PreservesExactTotal()
    {
        IReadOnlyList<decimal> result = InstallmentMath.ApplyDiscount([33.34m, 33.33m, 33.33m], 10.00m);

        Assert.Equal(90.00m, result.Sum());
        Assert.All(result, amount => Assert.True(amount >= 0.01m));
        Assert.Equal([30.00m, 30.00m, 30.00m], result);
    }

    [Fact]
    public void ApplyDiscount_MaximumAllowedDiscount_LeavesOneCentPerInstallment()
    {
        IReadOnlyList<decimal> result = InstallmentMath.ApplyDiscount([50m, 50m], 99.98m);

        Assert.Equal([0.01m, 0.01m], result);
        Assert.Equal(0.02m, result.Sum());
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(99.99)]
    [InlineData(120.00)]
    public void ApplyDiscount_InvalidDiscount_ThrowsDomainException(decimal discount)
    {
        Assert.Throws<DomainException>(() => InstallmentMath.ApplyDiscount([50m, 50m], discount));
    }

    [Fact]
    public void ApplyDiscount_EmptyList_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => InstallmentMath.ApplyDiscount([], 0m));
    }

    [Fact]
    public void AnticipateToInvoice_UpdatesInvoiceDateNetAmount_AndBlocksDoubleAnticipation()
    {
        var now = new DateTimeOffset(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);
        var originalInvoiceId = Guid.NewGuid();
        var targetInvoiceId = Guid.NewGuid();
        var tx = new Transaction(
            Guid.NewGuid(),
            null,
            originalInvoiceId,
            null,
            TransactionType.Expense,
            TransactionStatus.Confirmed,
            "Notebook",
            250m,
            new DateOnly(2027, 2, 10),
            now,
            installmentNumber: 4,
            totalInstallments: 6,
            installmentGroupId: Guid.NewGuid());

        DateTimeOffset anticipatedAt = now.AddMinutes(5);
        tx.AnticipateToInvoice(targetInvoiceId, new DateOnly(2026, 11, 24), 235m, anticipatedAt);

        Assert.Equal(targetInvoiceId, tx.CreditCardInvoiceId);
        Assert.Equal(new DateOnly(2026, 11, 24), tx.TransactionDate);
        Assert.Equal(235m, tx.Amount);
        Assert.Equal(250m, tx.OriginalAmount);
        Assert.Equal(anticipatedAt, tx.AnticipatedAtUtc);

        Assert.Throws<DomainException>(() =>
            tx.AnticipateToInvoice(Guid.NewGuid(), new DateOnly(2026, 11, 25), 200m, anticipatedAt.AddMinutes(1)));
    }

    [Fact]
    public void AnticipateFromAccount_MovesFromInvoiceOrPendingToConfirmedAccount()
    {
        var now = new DateTimeOffset(2026, 11, 24, 18, 0, 0, TimeSpan.Zero);
        var accountId = Guid.NewGuid();
        var tx = new Transaction(
            Guid.NewGuid(),
            null,
            Guid.NewGuid(),
            null,
            TransactionType.Expense,
            TransactionStatus.Confirmed,
            "Curso",
            150m,
            new DateOnly(2027, 1, 10),
            now,
            installmentNumber: 3,
            totalInstallments: 4,
            installmentGroupId: Guid.NewGuid());

        tx.AnticipateFromAccount(accountId, new DateOnly(2026, 11, 24), 140m, now.AddMinutes(2));

        Assert.Null(tx.CreditCardInvoiceId);
        Assert.Equal(accountId, tx.AccountId);
        Assert.Equal(TransactionStatus.Confirmed, tx.Status);
        Assert.Equal(140m, tx.Amount);
        Assert.Equal(150m, tx.OriginalAmount);
        Assert.NotNull(tx.AnticipatedAtUtc);
    }

    [Fact]
    public void CreditCardInvoice_UpdateBillingDates_UpdatesOpenInvoice_AndBlocksPaidInvoice()
    {
        var invoice = new CreditCardInvoice(
            Guid.NewGuid(),
            2026,
            12,
            new DateOnly(2026, 12, 10),
            new DateOnly(2026, 12, 17));

        invoice.UpdateBillingDates(new DateOnly(2026, 12, 15), new DateOnly(2026, 12, 22));
        Assert.Equal(new DateOnly(2026, 12, 15), invoice.ClosingDate);
        Assert.Equal(new DateOnly(2026, 12, 22), invoice.DueDate);

        invoice.MarkAsPaid(Guid.NewGuid(), DateTimeOffset.UtcNow);
        Assert.Throws<DomainException>(() =>
            invoice.UpdateBillingDates(new DateOnly(2026, 12, 18), new DateOnly(2026, 12, 25)));
    }
}
