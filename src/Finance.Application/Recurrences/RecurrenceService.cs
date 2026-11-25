using Finance.Application.Common;
using Finance.Application.Common.Exceptions;
using Finance.Application.Common.Interfaces;
using Finance.Domain.Common;
using Finance.Domain.Entities;
using Finance.Domain.Enums;
using FluentValidation;

namespace Finance.Application.Recurrences;

public sealed class RecurrenceService(
    IRecurrenceRepository recurrenceRepository,
    IAccountRepository accountRepository,
    ICategoryRepository categoryRepository,
    ICreditCardRepository creditCardRepository,
    ITransactionRepository transactionRepository,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IValidator<CreateRecurrenceCommand> createValidator,
    IValidator<MaterializeRecurrencesCommand> materializeValidator) : IRecurrenceService
{
    public async Task<IReadOnlyList<RecurrenceRuleDto>> GetAllAsync(
        bool includeInactive,
        CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        IReadOnlyList<RecurrenceRule> rules = await recurrenceRepository.GetByUserAsync(userId, includeInactive, ct);
        return rules.Select(MapDto).ToArray();
    }

    public async Task<RecurrenceRuleDto> CreateAsync(CreateRecurrenceCommand command, CancellationToken ct = default)
    {
        await createValidator.ValidateAndThrowAsync(command, ct);

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);

        Category category = await categoryRepository.GetByIdAsync(userId, command.CategoryId, ct)
            ?? throw AppException.NotFound("category_not_found", "Categoria não encontrada.");

        if (category.IsArchived)
        {
            throw AppException.Unprocessable("category_archived", "A categoria selecionada está arquivada.");
        }

        CategoryType expectedCategoryType = command.Type == RecurrenceType.Income
            ? CategoryType.Income
            : CategoryType.Expense;

        if (category.Type != expectedCategoryType)
        {
            throw AppException.Unprocessable(
                "category_type_mismatch",
                "O tipo da categoria não é compatível com o tipo da recorrência.");
        }

        if (command.AccountId.HasValue)
        {
            Account account = await accountRepository.GetByIdAsync(userId, command.AccountId.Value, ct)
                ?? throw AppException.NotFound("account_not_found", "Conta não encontrada.");

            if (account.IsArchived)
            {
                throw AppException.Unprocessable("account_archived", "A conta selecionada está arquivada.");
            }
        }
        else if (command.CreditCardId.HasValue)
        {
            CreditCard card = await creditCardRepository.GetByIdAsync(userId, command.CreditCardId.Value, ct)
                ?? throw AppException.NotFound("credit_card_not_found", "Cartão de crédito não encontrado.");

            if (card.IsArchived)
            {
                throw AppException.Unprocessable("credit_card_archived", "O cartão selecionado está arquivado.");
            }
        }

        var rule = new RecurrenceRule(
            userId,
            command.Type,
            command.Frequency,
            command.TotalInstallments,
            command.Description,
            command.Amount,
            command.CategoryId,
            command.AccountId,
            command.CreditCardId,
            command.StartDate);

        await recurrenceRepository.AddAsync(rule, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return MapDto(rule);
    }

    public async Task<MaterializeResultDto> MaterializeAsync(
        MaterializeRecurrencesCommand command,
        CancellationToken ct = default)
    {
        await materializeValidator.ValidateAndThrowAsync(command, ct);

        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        DateTimeOffset utcNow = dateTimeProvider.UtcNow;

        IReadOnlyList<RecurrenceRule> activeRules =
            await recurrenceRepository.GetByUserAsync(userId, includeInactive: false, ct);

        var createdIds = new List<Guid>();
        var createdTransactions = new List<Transaction>();
        var invoiceCache = new Dictionary<(Guid CardId, int Year, int Month), CreditCardInvoice>();

        await using IUnitOfWorkTransaction dbTx = await unitOfWork.BeginTransactionAsync(ct);

        foreach (RecurrenceRule rule in activeRules)
        {
            List<DateOnly> occurrences = GetOccurrencesInMonth(rule, command.Year, command.Month);
            if (occurrences.Count == 0)
                continue;

            int existingCount = await recurrenceRepository.CountOccurrencesAsync(userId, rule.Id, ct);

            CreditCard? card = null;
            if (rule.CreditCardId.HasValue)
            {
                card = await creditCardRepository.GetByIdAsync(userId, rule.CreditCardId.Value, ct);
                if (card is null || card.IsArchived)
                    continue;
            }
            else if (rule.AccountId.HasValue)
            {
                Account? account = await accountRepository.GetByIdAsync(userId, rule.AccountId.Value, ct);
                if (account is null || account.IsArchived)
                    continue;
            }

            foreach (DateOnly occurrenceDate in occurrences)
            {
                if (rule.TotalInstallments.HasValue && existingCount >= rule.TotalInstallments.Value)
                    break;

                if (await recurrenceRepository.OccurrenceExistsAsync(userId, rule.Id, occurrenceDate, ct))
                    continue;

                int? installmentNum = rule.TotalInstallments.HasValue ? existingCount + 1 : null;

                if (card is not null)
                {
                    (int refYear, int refMonth) = card.CalculateInvoiceReference(occurrenceDate);
                    CreditCardInvoice invoice = await GetOrCreateInvoiceAsync(
                        card,
                        refYear,
                        refMonth,
                        invoiceCache,
                        ct);

                    if (invoice.Status == InvoiceStatus.Paid)
                        continue;

                    invoice.AddCharge(rule.Amount);

                    var cardTx = new Transaction(
                        userId,
                        null,
                        invoice.Id,
                        rule.CategoryId,
                        TransactionType.Expense,
                        TransactionStatus.Confirmed,
                        rule.Description,
                        rule.Amount,
                        occurrenceDate,
                        utcNow,
                        recurrenceRuleId: rule.Id,
                        installmentNumber: installmentNum,
                        totalInstallments: rule.TotalInstallments);

                    createdTransactions.Add(cardTx);
                    createdIds.Add(cardTx.Id);
                    existingCount++;
                }
                else if (rule.AccountId.HasValue)
                {
                    TransactionType txType = rule.Type == RecurrenceType.Income
                        ? TransactionType.Income
                        : TransactionType.Expense;

                    var accountTx = new Transaction(
                        userId,
                        rule.AccountId.Value,
                        null,
                        rule.CategoryId,
                        txType,
                        TransactionStatus.Pending,
                        rule.Description,
                        rule.Amount,
                        occurrenceDate,
                        utcNow,
                        recurrenceRuleId: rule.Id,
                        installmentNumber: installmentNum,
                        totalInstallments: rule.TotalInstallments);

                    createdTransactions.Add(accountTx);
                    createdIds.Add(accountTx.Id);
                    existingCount++;
                }
            }
        }

        if (createdTransactions.Count > 0)
        {
            await transactionRepository.AddRangeAsync(createdTransactions, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        await dbTx.CommitAsync(ct);

        return new MaterializeResultDto(command.Year, command.Month, createdIds.Count, createdIds);
    }

    public async Task<RecurrenceRuleDto> DeactivateAsync(Guid id, CancellationToken ct = default)
    {
        Guid userId = ApplicationContextHelper.RequireUserId(currentUserService);
        RecurrenceRule rule = await recurrenceRepository.GetByIdAsync(userId, id, ct)
            ?? throw AppException.NotFound("recurrence_not_found", "Regra de recorrência não encontrada.");

        rule.Deactivate();
        await unitOfWork.SaveChangesAsync(ct);

        return MapDto(rule);
    }

    private async Task<CreditCardInvoice> GetOrCreateInvoiceAsync(
        CreditCard card,
        int year,
        int month,
        IDictionary<(Guid CardId, int Year, int Month), CreditCardInvoice> cache,
        CancellationToken ct)
    {
        if (cache.TryGetValue((card.Id, year, month), out CreditCardInvoice? cached))
            return cached;

        CreditCardInvoice? existing = await creditCardRepository.GetInvoiceByReferenceAsync(card.Id, year, month, ct);
        if (existing is not null)
        {
            cache[(card.Id, year, month)] = existing;
            return existing;
        }

        (DateOnly closingDate, DateOnly dueDate) = card.CalculateInvoiceDates(year, month);
        var created = new CreditCardInvoice(card.Id, year, month, closingDate, dueDate);
        await creditCardRepository.AddInvoiceAsync(created, ct);
        cache[(card.Id, year, month)] = created;
        return created;
    }

    private static List<DateOnly> GetOccurrencesInMonth(RecurrenceRule rule, int year, int month)
    {
        var result = new List<DateOnly>();
        int daysInMonth = DateTime.DaysInMonth(year, month);
        var firstDay = new DateOnly(year, month, 1);
        var lastDay = new DateOnly(year, month, daysInMonth);

        switch (rule.Frequency)
        {
            case RecurrenceFrequency.Monthly:
            {
                var startMonth = new DateOnly(rule.StartDate.Year, rule.StartDate.Month, 1);
                if (firstDay >= startMonth)
                {
                    var candidate = new DateOnly(year, month, Math.Min(rule.StartDate.Day, daysInMonth));
                    if (candidate >= rule.StartDate)
                        result.Add(candidate);
                }
                break;
            }
            case RecurrenceFrequency.Yearly:
            {
                if (year >= rule.StartDate.Year && month == rule.StartDate.Month)
                {
                    var candidate = new DateOnly(year, month, Math.Min(rule.StartDate.Day, daysInMonth));
                    if (candidate >= rule.StartDate)
                        result.Add(candidate);
                }
                break;
            }
            case RecurrenceFrequency.Weekly:
            {
                DateOnly cursor = rule.StartDate;
                if (cursor < firstDay)
                {
                    int daysDiff = firstDay.DayNumber - cursor.DayNumber;
                    int weeksToSkip = (daysDiff + 6) / 7;
                    cursor = cursor.AddDays(weeksToSkip * 7);
                }

                while (cursor <= lastDay)
                {
                    result.Add(cursor);
                    cursor = cursor.AddDays(7);
                }
                break;
            }
        }

        return result;
    }

    private static RecurrenceRuleDto MapDto(RecurrenceRule rule)
        => new(
            rule.Id,
            rule.Type.ToScreamingSnakeCase(),
            rule.Frequency.ToScreamingSnakeCase(),
            rule.TotalInstallments,
            rule.Description,
            rule.Amount,
            rule.CategoryId,
            rule.AccountId,
            rule.CreditCardId,
            rule.StartDate,
            rule.IsActive);
}
