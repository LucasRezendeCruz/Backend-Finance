using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Infrastructure.Persistence.Configurations;

internal sealed class CreditCardInvoiceConfiguration : IEntityTypeConfiguration<CreditCardInvoice>
{
    public void Configure(EntityTypeBuilder<CreditCardInvoice> builder)
    {
        builder.ToTable("credit_card_invoices");
        builder.HasKey(inv => inv.Id);

        builder.Property(inv => inv.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(inv => inv.CreditCardId).HasColumnName("credit_card_id").IsRequired();
        builder.Property(inv => inv.ReferenceYear).HasColumnName("reference_year").IsRequired();
        builder.Property(inv => inv.ReferenceMonth).HasColumnName("reference_month").IsRequired();
        builder.Property(inv => inv.ClosingDate).HasColumnName("closing_date").IsRequired();
        builder.Property(inv => inv.DueDate).HasColumnName("due_date").IsRequired();
        builder.Property(inv => inv.Status).HasColumnName("status").HasScreamingSnakeCaseConversion(20).IsRequired();
        builder.Property(inv => inv.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2).IsRequired();
        builder.Property(inv => inv.PaidAtUtc).HasColumnName("paid_at_utc");
        builder.Property(inv => inv.PaidWithAccountId).HasColumnName("paid_with_account_id");

        builder.HasOne<CreditCard>()
            .WithMany()
            .HasForeignKey(inv => inv.CreditCardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(inv => inv.PaidWithAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(inv => new { inv.CreditCardId, inv.ReferenceYear, inv.ReferenceMonth }).IsUnique();
    }
}
