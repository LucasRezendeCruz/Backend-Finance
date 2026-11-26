using System.Globalization;
using Finance.Application.Common.Interfaces;
using Finance.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Finance.Infrastructure.Persistence;

public sealed class FinanceDbContext(DbContextOptions<FinanceDbContext> options) : DbContext(options), IUnitOfWork
{
    public const string Schema = "finance";
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<SecurityAuditLog> SecurityAuditLogs => Set<SecurityAuditLog>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<CreditCard> CreditCards => Set<CreditCard>();
    public DbSet<CreditCardInvoice> CreditCardInvoices => Set<CreditCardInvoice>();
    public DbSet<RecurrenceRule> RecurrenceRules => Set<RecurrenceRule>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TelegramLink> TelegramLinks => Set<TelegramLink>();
    public DbSet<TelegramLinkToken> TelegramLinkTokens => Set<TelegramLinkToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        bool isSqlite = string.Equals(
            Database.ProviderName,
            "Microsoft.EntityFrameworkCore.Sqlite",
            StringComparison.Ordinal);

        if (!isSqlite)
        {
            modelBuilder.HasDefaultSchema(Schema);
        }

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FinanceDbContext).Assembly);
        base.OnModelCreating(modelBuilder);

        if (isSqlite)
        {
            ConfigureSqliteCompatibility(modelBuilder);
        }
    }

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        IDbContextTransaction transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new EfUnitOfWorkTransaction(transaction);
    }

    private sealed class EfUnitOfWorkTransaction(IDbContextTransaction inner) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default)
            => inner.CommitAsync(cancellationToken);

        public Task RollbackAsync(CancellationToken cancellationToken = default)
            => inner.RollbackAsync(cancellationToken);

        public ValueTask DisposeAsync()
            => inner.DisposeAsync();
    }

    private static void ConfigureSqliteCompatibility(ModelBuilder modelBuilder)
    {
        var dateTimeOffsetConverter = new ValueConverter<DateTimeOffset, string>(
            v => v.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            v => DateTimeOffset.Parse(v, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

        var nullableDateTimeOffsetConverter = new ValueConverter<DateTimeOffset?, string?>(
            v => v.HasValue ? v.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture) : null,
            v => string.IsNullOrEmpty(v)
                ? null
                : DateTimeOffset.Parse(v, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind));

        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset))
                {
                    property.SetValueConverter(dateTimeOffsetConverter);
                }
                else if (property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(nullableDateTimeOffsetConverter);
                }

                if (string.Equals(property.GetColumnType(), "jsonb", StringComparison.OrdinalIgnoreCase))
                {
                    property.SetColumnType("TEXT");
                }
            }
        }
    }
}
