using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(u => u.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(256).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").IsRequired();
        builder.Property(u => u.TwoFactorEnabled).HasColumnName("two_factor_enabled").IsRequired();
        builder.Property(u => u.TwoFactorSecret).HasColumnName("two_factor_secret").HasMaxLength(512);
        builder.Property(u => u.FailedLoginAttempts).HasColumnName("failed_login_attempts").IsRequired();
        builder.Property(u => u.LockoutEndUtc).HasColumnName("lockout_end_utc");
        builder.Property(u => u.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();
    }
}
