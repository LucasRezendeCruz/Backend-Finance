namespace Finance.Application.Recurrences;

public interface IRecurrenceService
{
    Task<IReadOnlyList<RecurrenceRuleDto>> GetAllAsync(bool includeInactive, CancellationToken ct = default);
    Task<RecurrenceRuleDto> CreateAsync(CreateRecurrenceCommand command, CancellationToken ct = default);
    Task<MaterializeResultDto> MaterializeAsync(MaterializeRecurrencesCommand command, CancellationToken ct = default);
    Task<RecurrenceRuleDto> DeactivateAsync(Guid id, CancellationToken ct = default);
}
