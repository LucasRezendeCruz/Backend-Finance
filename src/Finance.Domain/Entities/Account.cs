using Finance.Domain.Common;
using Finance.Domain.Enums;

namespace Finance.Domain.Entities;

public sealed class Account : Entity
{
    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public AccountType Type { get; private set; }
    public decimal InitialBalance { get; private set; }
    public decimal CurrentBalance { get; private set; }
    public string Color { get; private set; } = string.Empty;
    public bool IsArchived { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private Account() { }

    public Account(
        Guid userId,
        string name,
        AccountType type,
        decimal initialBalance,
        string color,
        DateTimeOffset createdAtUtc)
    {
        if (userId == Guid.Empty)
            throw new DomainException("invalid_user_id", "Usuário inválido.");
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("invalid_account_name", "O nome da conta é obrigatório.");

        UserId = userId;
        Name = name.Trim();
        Type = type;
        InitialBalance = decimal.Round(initialBalance, 2, MidpointRounding.AwayFromZero);
        CurrentBalance = InitialBalance;
        Color = string.IsNullOrWhiteSpace(color) ? "#ff5a36" : color.Trim();
        IsArchived = false;
        CreatedAtUtc = createdAtUtc;
    }

    public void Update(string name, AccountType type, string color)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("invalid_account_name", "O nome da conta é obrigatório.");

        Name = name.Trim();
        Type = type;
        if (!string.IsNullOrWhiteSpace(color))
            Color = color.Trim();
    }

    public void ApplyBalanceChange(decimal signedDelta)
    {
        CurrentBalance = decimal.Round(CurrentBalance + signedDelta, 2, MidpointRounding.AwayFromZero);
    }

    public void SetArchived(bool isArchived)
    {
        IsArchived = isArchived;
    }
}
