using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Finance.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories", table =>
        {
            table.HasCheckConstraint(
                "CK_categories_monthly_budget",
                "monthly_budget IS NULL OR monthly_budget > 0");
        });
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(c => c.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(c => c.Type).HasScreamingSnakeCaseConversion(20).HasColumnName("type").IsRequired();
        builder.Property(c => c.Icon).HasColumnName("icon").HasMaxLength(60).IsRequired();
        builder.Property(c => c.Color).HasColumnName("color").HasMaxLength(20).IsRequired();
        builder.Property(c => c.MonthlyBudget).HasColumnName("monthly_budget").HasPrecision(18, 2);
        builder.Property(c => c.IsArchived).HasColumnName("is_archived").IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.UserId, c.Name, c.Type }).IsUnique();
    }
}
