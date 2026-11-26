using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Infrastructure.Persistence.Configurations;

internal sealed class TelegramLinkConfiguration : IEntityTypeConfiguration<TelegramLink>
{
    public void Configure(EntityTypeBuilder<TelegramLink> builder)
    {
        builder.ToTable("telegram_links");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.TelegramChatId).HasColumnName("telegram_chat_id").IsRequired();
        builder.Property(x => x.TelegramUsername).HasColumnName("telegram_username").HasMaxLength(100);
        builder.Property(x => x.TelegramFirstName).HasColumnName("telegram_first_name").HasMaxLength(100);
        builder.Property(x => x.DefaultAccountId).HasColumnName("default_account_id");
        builder.Property(x => x.DefaultCreditCardId).HasColumnName("default_credit_card_id");
        builder.Property(x => x.LastBotTransactionId).HasColumnName("last_bot_transaction_id");
        builder.Property(x => x.LinkedAtUtc).HasColumnName("linked_at_utc").IsRequired();
        builder.Property(x => x.LastInteractionAtUtc).HasColumnName("last_interaction_at_utc");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.DefaultAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<CreditCard>()
            .WithMany()
            .HasForeignKey(x => x.DefaultCreditCardId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasIndex(x => x.TelegramChatId).IsUnique();
    }
}
