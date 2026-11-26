using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Infrastructure.Persistence.Configurations;

internal sealed class TelegramLinkTokenConfiguration : IEntityTypeConfiguration<TelegramLinkToken>
{
    public void Configure(EntityTypeBuilder<TelegramLinkToken> builder)
    {
        builder.ToTable("telegram_link_tokens");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.ConsumedAtUtc).HasColumnName("consumed_at_utc");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.ExpiresAtUtc });
    }
}
