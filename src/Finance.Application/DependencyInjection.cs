using Finance.Application.Accounts;
using Finance.Application.Auth;
using Finance.Application.Categories;
using Finance.Application.CreditCards;
using Finance.Application.Dashboard;
using Finance.Application.Recurrences;
using Finance.Application.Security;
using Finance.Application.Telegram;
using Finance.Application.Transactions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Finance.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISecurityAuditService, SecurityAuditService>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICreditCardService, CreditCardService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IRecurrenceService, RecurrenceService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ITelegramBotService, TelegramBotService>();

        return services;
    }
}
