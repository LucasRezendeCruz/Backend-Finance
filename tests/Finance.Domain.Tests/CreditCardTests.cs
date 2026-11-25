using Finance.Domain.Common;
using Finance.Domain.Entities;

namespace Finance.Domain.Tests;

public sealed class CreditCardTests
{
    private static CreditCard CreateCard(int closingDay = 3, int dueDay = 10)
        => new(Guid.NewGuid(), null, "Cartão Black", 5000m, closingDay, dueDay, "#ff5a36");

    [Fact]
    public void CalculateInvoiceReference_PurchaseBeforeClosingDay_ReturnsCurrentMonth()
    {
        var card = CreateCard(closingDay: 18, dueDay: 25);

        var reference = card.CalculateInvoiceReference(new DateOnly(2026, 11, 17));

        Assert.Equal((2026, 11), reference);
    }

    [Fact]
    public void CalculateInvoiceReference_PurchaseOnClosingDay_ReturnsNextMonth()
    {
        var card = CreateCard(closingDay: 18, dueDay: 25);

        var reference = card.CalculateInvoiceReference(new DateOnly(2026, 11, 18));

        Assert.Equal((2026, 12), reference);
    }

    [Fact]
    public void CalculateInvoiceReference_DecemberAfterClosing_RollsOverToJanuaryNextYear()
    {
        var card = CreateCard(closingDay: 18, dueDay: 25);

        var reference = card.CalculateInvoiceReference(new DateOnly(2026, 12, 20));

        Assert.Equal((2027, 1), reference);
    }

    [Fact]
    public void CalculateInvoiceReference_ClosingDay31InFebruary_ClampsToLastDayOfMonth()
    {
        var card = CreateCard(closingDay: 31, dueDay: 10);

        var beforeClose = card.CalculateInvoiceReference(new DateOnly(2026, 2, 27));
        var onClampedClose = card.CalculateInvoiceReference(new DateOnly(2026, 2, 28));
        var leapYearBefore = card.CalculateInvoiceReference(new DateOnly(2024, 2, 28));
        var leapYearOnClose = card.CalculateInvoiceReference(new DateOnly(2024, 2, 29));

        Assert.Equal((2026, 2), beforeClose);
        Assert.Equal((2026, 3), onClampedClose);
        Assert.Equal((2024, 2), leapYearBefore);
        Assert.Equal((2024, 3), leapYearOnClose);
    }

    [Fact]
    public void CalculateInvoiceDates_DueDayLessOrEqualClosingDay_DueDateIsNextMonth()
    {
        var card = CreateCard(closingDay: 25, dueDay: 5);

        var (closingDate, dueDate) = card.CalculateInvoiceDates(2026, 12);

        Assert.Equal(new DateOnly(2026, 12, 25), closingDate);
        Assert.Equal(new DateOnly(2027, 1, 5), dueDate);
    }

    [Fact]
    public void CalculateInvoiceDates_DueDayAfterClosingDay_DueDateIsSameMonth()
    {
        var card = CreateCard(closingDay: 3, dueDay: 10);

        var (closingDate, dueDate) = card.CalculateInvoiceDates(2026, 11);

        Assert.Equal(new DateOnly(2026, 11, 3), closingDate);
        Assert.Equal(new DateOnly(2026, 11, 10), dueDate);
    }

    [Fact]
    public void SplitInstallments_DividesExactAndPutsRemainderOnFirstInstallment()
    {
        var parts = CreditCard.SplitInstallments(100m, 3);

        Assert.Equal([33.34m, 33.33m, 33.33m], parts);
        Assert.Equal(100m, parts.Sum());
    }

    [Fact]
    public void SplitInstallments_SixInstallmentsOf100_ExactSum()
    {
        var parts = CreditCard.SplitInstallments(100m, 6);

        Assert.Equal([16.70m, 16.66m, 16.66m, 16.66m, 16.66m, 16.66m], parts);
        Assert.Equal(100m, parts.Sum());
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(32, 10)]
    [InlineData(10, 0)]
    [InlineData(10, 32)]
    public void Constructor_InvalidBillingDays_ThrowsDomainException(int closingDay, int dueDay)
    {
        Assert.Throws<DomainException>(() => CreateCard(closingDay, dueDay));
    }

    [Fact]
    public void SplitInstallments_InvalidAmountOrInstallments_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => CreditCard.SplitInstallments(0m, 3));
        Assert.Throws<DomainException>(() => CreditCard.SplitInstallments(100m, 0));
    }
}
