namespace Finance.Application.Accounts;

public interface IAccountService
{
    Task<IReadOnlyList<AccountDto>> GetAllAsync(bool includeArchived, CancellationToken ct = default);
    Task<AccountDto> CreateAsync(CreateAccountCommand command, CancellationToken ct = default);
    Task<AccountDto> UpdateAsync(Guid id, UpdateAccountCommand command, CancellationToken ct = default);
    Task<AccountDto> SetArchivedAsync(Guid id, bool isArchived, CancellationToken ct = default);
}
