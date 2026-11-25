using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Infrastructure.Persistence.Configurations;

internal sealed class SecurityAuditLogConfiguration : IEntityTypeConfiguration<SecurityAuditLog>
{
    public void Configure(EntityTypeBuilder<SecurityAuditLog> builder)
    {
        builder.ToTable("security_audit_logs");
        builder.HasKey(log => log.Id);

        builder.Property(log => log.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(log => log.UserId).HasColumnName("user_id");
        builder.Property(log => log.EventType).HasColumnName("event_type").HasScreamingSnakeCaseConversion(60).IsRequired();
        builder.Property(log => log.IpAddress).HasColumnName("ip_address").HasMaxLength(64).IsRequired();
        builder.Property(log => log.UserAgent).HasColumnName("user_agent").HasMaxLength(1024).IsRequired();
        builder.Property(log => log.MetadataJson).HasColumnName("metadata_json").HasColumnType("jsonb");
        builder.Property(log => log.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(log => log.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(log => new { log.UserId, log.CreatedAtUtc });
    }
}
