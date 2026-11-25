using Finance.Domain.Common;
using Finance.Domain.Entities;
using Finance.Domain.Enums;

namespace Finance.Domain.Tests;

public sealed class CreditCardInvoiceTests
{
    private static CreditCardInvoice CreateOpenInvoice()
        => new(
            Guid.NewGuid(),
            2026,
            11,
            new DateOnly(2026, 11, 18),
            new DateOnly(2026, 11, 25));

    [Fact]
    public void AddAndRemoveCharge_UpdatesTotalAmountCorrectly()
    {
        var invoice = CreateOpenInvoice();

        invoice.AddCharge(120.50m);
        invoice.AddCharge(79.50m);
        invoice.RemoveCharge(50.00m);

        Assert.Equal(150.00m, invoice.TotalAmount);
        Assert.Equal(InvoiceStatus.Open, invoice.Status);
    }

    [Fact]
    public void MarkAsPaid_SetsStatusAccountAndTimestamp()
    {
        var invoice = CreateOpenInvoice();
        invoice.AddCharge(2340.90m);
        invoice.Close();
        var accountId = Guid.NewGuid();
        var paidAt = new DateTimeOffset(2026, 11, 23, 18, 0, 0, TimeSpan.Zero);

        invoice.MarkAsPaid(accountId, paidAt);

        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(accountId, invoice.PaidWithAccountId);
        Assert.Equal(paidAt, invoice.PaidAtUtc);
    }

    [Fact]
    public void PaidInvoice_BlocksFurtherMutations()
    {
        var invoice = CreateOpenInvoice();
        invoice.AddCharge(500m);
        invoice.MarkAsPaid(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Throws<DomainException>(() => invoice.AddCharge(10m));
        Assert.Throws<DomainException>(() => invoice.RemoveCharge(10m));
        Assert.Throws<DomainException>(() => invoice.Close());
        Assert.Throws<DomainException>(() => invoice.MarkAsPaid(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }
}
