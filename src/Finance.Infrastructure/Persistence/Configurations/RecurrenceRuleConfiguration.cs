using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Infrastructure.Persistence.Configurations;

internal sealed class RecurrenceRuleConfiguration : IEntityTypeConfiguration<RecurrenceRule>
{
    public void Configure(EntityTypeBuilder<RecurrenceRule> builder)
    {
        builder.ToTable("recurrence_rules", t =>
        {
            t.HasCheckConstraint("CK_recurrence_rules_amount", "amount > 0");
        });

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(r => r.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(r => r.Type).HasColumnName("type").HasScreamingSnakeCaseConversion(20).IsRequired();
        builder.Property(r => r.Frequency).HasColumnName("frequency").HasScreamingSnakeCaseConversion(20).IsRequired();
        builder.Property(r => r.TotalInstallments).HasColumnName("total_installments");
        builder.Property(r => r.Description).HasColumnName("description").HasMaxLength(200).IsRequired();
        builder.Property(r => r.Amount).HasColumnName("amount").HasPrecision(18, 2).IsRequired();
        builder.Property(r => r.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(r => r.AccountId).HasColumnName("account_id");
        builder.Property(r => r.CreditCardId).HasColumnName("credit_card_id");
        builder.Property(r => r.StartDate).HasColumnName("start_date").IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("is_active").IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(r => r.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(r => r.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<CreditCard>()
            .WithMany()
            .HasForeignKey(r => r.CreditCardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.UserId, r.IsActive });
    }
}
