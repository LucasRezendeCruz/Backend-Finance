using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(a => a.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(a => a.Name).HasColumnName("name").HasMaxLength(120).IsRequired();
        builder.Property(a => a.Type).HasColumnName("type").HasScreamingSnakeCaseConversion(30).IsRequired();
        builder.Property(a => a.InitialBalance).HasColumnName("initial_balance").HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.CurrentBalance).HasColumnName("current_balance").HasPrecision(18, 2).IsRequired();
        builder.Property(a => a.Color).HasColumnName("color").HasMaxLength(20).IsRequired();
        builder.Property(a => a.IsArchived).HasColumnName("is_archived").IsRequired();
        builder.Property(a => a.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(a => new { a.UserId, a.IsArchived });
    }
}
