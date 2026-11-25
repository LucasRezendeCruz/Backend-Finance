using Finance.Domain.Enums;

namespace Finance.Application.Accounts;

public sealed record CreateAccountCommand(
    string Name,
    AccountType Type,
    decimal InitialBalance,
    string? Color);

public sealed record UpdateAccountCommand(
    string Name,
    AccountType Type,
    string? Color);

public sealed record AccountDto(
    Guid Id,
    string Name,
    string Type,
    decimal InitialBalance,
    decimal CurrentBalance,
    string Color,
    bool IsArchived,
    DateTimeOffset CreatedAtUtc);
