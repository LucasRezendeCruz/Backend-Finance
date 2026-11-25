using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Infrastructure.Persistence.Configurations;

internal sealed class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("transactions", t =>
        {
            t.HasCheckConstraint("CK_transactions_amount", "amount > 0");
            t.HasCheckConstraint(
                "CK_transactions_original_amount",
                "original_amount IS NULL OR original_amount > 0");
        });

        builder.HasKey(tx => tx.Id);

        builder.Property(tx => tx.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(tx => tx.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(tx => tx.AccountId).HasColumnName("account_id");
        builder.Property(tx => tx.CreditCardInvoiceId).HasColumnName("credit_card_invoice_id");
        builder.Property(tx => tx.TransferPairId).HasColumnName("transfer_pair_id");
        builder.Property(tx => tx.RecurrenceRuleId).HasColumnName("recurrence_rule_id");
        builder.Property(tx => tx.CategoryId).HasColumnName("category_id");
        builder.Property(tx => tx.InstallmentGroupId).HasColumnName("installment_group_id");
        builder.Property(tx => tx.Type).HasColumnName("type").HasScreamingSnakeCaseConversion(30).IsRequired();
        builder.Property(tx => tx.Status).HasColumnName("status").HasScreamingSnakeCaseConversion(20).IsRequired();
        builder.Property(tx => tx.Description).HasColumnName("description").HasMaxLength(250).IsRequired();
        builder.Property(tx => tx.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(tx => tx.OriginalAmount).HasColumnName("original_amount").HasPrecision(18, 2);
        builder.Property(tx => tx.TransactionDate).HasColumnName("transaction_date").IsRequired();
        builder.Property(tx => tx.InstallmentNumber).HasColumnName("installment_number");
        builder.Property(tx => tx.TotalInstallments).HasColumnName("total_installments");
        builder.Property(tx => tx.Notes).HasColumnName("notes").HasMaxLength(1000);
        builder.Property(tx => tx.AnticipatedAtUtc).HasColumnName("anticipated_at_utc");
        builder.Property(tx => tx.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(tx => tx.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(tx => tx.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CreditCardInvoice>()
            .WithMany()
            .HasForeignKey(tx => tx.CreditCardInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Transaction>()
            .WithMany()
            .HasForeignKey(tx => tx.TransferPairId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<RecurrenceRule>()
            .WithMany()
            .HasForeignKey(tx => tx.RecurrenceRuleId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(tx => tx.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(tx => new { tx.UserId, tx.TransactionDate });
        builder.HasIndex(tx => new { tx.UserId, tx.InstallmentGroupId });
        builder.HasIndex(tx => tx.CreditCardInvoiceId);
    }
}
