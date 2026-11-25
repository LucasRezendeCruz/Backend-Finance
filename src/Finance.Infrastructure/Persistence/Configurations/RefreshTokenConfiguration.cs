using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Infrastructure.Persistence.Configurations;

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(rt => rt.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(rt => rt.TokenHash).HasColumnName("token_hash").HasMaxLength(128).IsRequired();
        builder.Property(rt => rt.FamilyId).HasColumnName("family_id").IsRequired();
        builder.Property(rt => rt.ReplacedByTokenHash).HasColumnName("replaced_by_token_hash").HasMaxLength(128);
        builder.Property(rt => rt.ClientType).HasColumnName("client_type").HasScreamingSnakeCaseConversion(20).IsRequired();
        builder.Property(rt => rt.DeviceName).HasColumnName("device_name").HasMaxLength(150).IsRequired();
        builder.Property(rt => rt.IpAddress).HasColumnName("ip_address").HasMaxLength(64).IsRequired();
        builder.Property(rt => rt.UserAgent).HasColumnName("user_agent").HasMaxLength(1024).IsRequired();
        builder.Property(rt => rt.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(rt => rt.RevokedAtUtc).HasColumnName("revoked_at_utc");
        builder.Property(rt => rt.RevokedReason).HasColumnName("revoked_reason").HasMaxLength(100);
        builder.Property(rt => rt.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(rt => rt.TokenHash).IsUnique();
        builder.HasIndex(rt => new { rt.UserId, rt.FamilyId });
    }
}
