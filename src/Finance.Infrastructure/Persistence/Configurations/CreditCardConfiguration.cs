using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Infrastructure.Persistence.Configurations;

internal sealed class CreditCardConfiguration : IEntityTypeConfiguration<CreditCard>
{
    public void Configure(EntityTypeBuilder<CreditCard> builder)
    {
        builder.ToTable("credit_cards", t =>
        {
            t.HasCheckConstraint("CK_credit_cards_closing_day", "closing_day >= 1 AND closing_day <= 31");
            t.HasCheckConstraint("CK_credit_cards_due_day", "due_day >= 1 AND due_day <= 31");
            t.HasCheckConstraint("CK_credit_cards_limit", "credit_limit > 0");
        });

        builder.HasKey(cc => cc.Id);

        builder.Property(cc => cc.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(cc => cc.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(cc => cc.DefaultAccountId).HasColumnName("default_account_id");
        builder.Property(cc => cc.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(cc => cc.CreditLimit).HasColumnName("credit_limit").HasPrecision(18, 2).IsRequired();
        builder.Property(cc => cc.ClosingDay).HasColumnName("closing_day").IsRequired();
        builder.Property(cc => cc.DueDay).HasColumnName("due_day").IsRequired();
        builder.Property(cc => cc.Color).HasColumnName("color").HasMaxLength(20).IsRequired();
        builder.Property(cc => cc.IsArchived).HasColumnName("is_archived").IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(cc => cc.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(cc => cc.DefaultAccountId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
