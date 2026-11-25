using Finance.Application.Common;
using Finance.Application.Common.Exceptions;
using Finance.Application.Common.Interfaces;
using Finance.Domain.Common;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using FluentValidation;

namespace Finance.Application.Accounts;

public sealed class AccountService(
    IAccountRepository accountRepository,
    IAuthRepository authRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IValidator<CreateAccountCommand> createValidator,
    IValidator<UpdateAccountCommand> updateValidator) : IAccountService
{
    public async Task<IReadOnlyList<AccountDto>> GetAllAsync(bool includeArchived, CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        IReadOnlyList<Account> accounts = await accountRepository.GetByUserAsync(userId, includeArchived, ct);
        return accounts.Select(MapDto).ToArray();
    }

    public async Task<AccountDto> CreateAsync(CreateAccountCommand command, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(command, ct);

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        var account = new Account(
            userId,
            command.Name,
            command.Type,
            command.InitialBalance,
            command.Color ?? "#ff5a36",
            dateTimeProvider.UtcNow);

        await accountRepository.AddAsync(account, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return MapDto(account);
    }

    public async Task<AccountDto> UpdateAsync(Guid id, UpdateAccountCommand command, CancellationToken ct = default)
    {
        await updateValidator.ValidateAndThrowAsync(command, ct);

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        Account account = await accountRepository.GetByIdAsync(userId, id, ct)
            ?? throw AppException.NotFound("account_not_found", "Conta não encontrada.");

        account.Update(command.Name, command.Type, command.Color ?? string.Empty);
        await unitOfWork.SaveChangesAsync(ct);

        return MapDto(account);
    }

    public async Task<AccountDto> SetArchivedAsync(Guid id, bool isArchived, CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        Account account = await accountRepository.GetByIdAsync(userId, id, ct)
            ?? throw AppException.NotFound("account_not_found", "Conta não encontrada.");

        account.SetArchived(isArchived);

        if (isArchived)
        {
            await authRepository.AddAuditLogAsync(
                ApplicationContextHelper.CreateAuditLog(
                    userId,
                    SecurityAuditEventType.AccountArchived,
                    currentUserService,
                    dateTimeProvider.UtcNow,
                    new { accountId = account.Id, accountName = account.Name }),
                ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return MapDto(account);
    }

    private static AccountDto MapDto(Account account)
        => new(
            account.Id,
            account.Name,
            account.Type.ToScreamingSnakeCase(),
            account.InitialBalance,
            account.CurrentBalance,
            account.Color,
            account.IsArchived,
            account.CreatedAtUtc);
}
