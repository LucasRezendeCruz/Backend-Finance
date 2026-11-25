using Finance.Domain.Enums;

namespace Finance.Application.Recurrences;

public sealed record CreateRecurrenceCommand(
    RecurrenceType Type,
    RecurrenceFrequency Frequency,
    int? TotalInstallments,
    string Description,
    decimal Amount,
    Guid CategoryId,
    Guid? AccountId,
    Guid? CreditCardId,
    DateOnly StartDate);

public sealed record MaterializeRecurrencesCommand(int Year, int Month);

public sealed record RecurrenceRuleDto(
    Guid Id,
    string Type,
    string Frequency,
    int? TotalInstallments,
    string Description,
    decimal Amount,
    Guid CategoryId,
    Guid? AccountId,
    Guid? CreditCardId,
    DateOnly StartDate,
    bool IsActive);

public sealed record MaterializeResultDto(
    int Year,
    int Month,
    int CreatedCount,
    IReadOnlyList<Guid> CreatedTransactionIds);
