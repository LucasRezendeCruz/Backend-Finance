using Finance.Application.Common.Interfaces;
using Finance.Infrastructure.Persistence;
using Finance.Infrastructure.Persistence.Repositories;
using Finance.Infrastructure.Security;
using Finance.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string? connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'DefaultConnection' não configurada.");
        }

        services.AddDbContext<FinanceDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsAssembly(typeof(FinanceDbContext).Assembly.FullName);
                npgsql.MigrationsHistoryTable(
                    FinanceDbContext.MigrationsHistoryTable,
                    FinanceDbContext.Schema);
            }));

        services.Configure<Argon2Options>(configuration.GetSection(Argon2Options.SectionName));
        services.Configure<JwtOptions>(options =>
        {
            configuration.GetSection(JwtOptions.SectionName).Bind(options);
            if (string.IsNullOrWhiteSpace(options.SecretKey))
            {
                options.SecretKey = JwtOptions.ResolveSecretKey(configuration) ?? string.Empty;
            }
        });

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<FinanceDbContext>());
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, Argon2PasswordHasher>();
        services.AddSingleton<ITokenService, JwtTokenService>();
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<ISecurityAuditRepository, SecurityAuditRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ICreditCardRepository, CreditCardRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IRecurrenceRepository, RecurrenceRepository>();

        return services;
    }
}
