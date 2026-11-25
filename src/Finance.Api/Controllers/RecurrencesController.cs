using Finance.Application.Recurrences;
using Finance.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/v1/recurrences")]
[Authorize]
[EnableRateLimiting("api-authenticated")]
public sealed class RecurrencesController(IRecurrenceService recurrenceService) : ControllerBase
{
    public sealed record CreateRecurrenceRequest(
        RecurrenceType Type,
        RecurrenceFrequency Frequency,
        string Description,
        decimal Amount,
        Guid CategoryId,
        DateOnly StartDate,
        Guid? AccountId = null,
        Guid? CreditCardId = null,
        int? TotalInstallments = null);

    public sealed record MaterializeRecurrencesRequest(int Year, int Month);

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RecurrenceRuleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await recurrenceService.GetAllAsync(includeInactive, ct));

    [HttpPost]
    [ProducesResponseType(typeof(RecurrenceRuleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateRecurrenceRequest request,
        CancellationToken ct = default)
    {
        RecurrenceRuleDto created = await recurrenceService.CreateAsync(
            new CreateRecurrenceCommand(
                request.Type,
                request.Frequency,
                request.TotalInstallments,
                request.Description,
                request.Amount,
                request.CategoryId,
                request.AccountId,
                request.CreditCardId,
                request.StartDate),
            ct);

        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost("materialize")]
    [ProducesResponseType(typeof(MaterializeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Materialize(
        [FromBody] MaterializeRecurrencesRequest request,
        CancellationToken ct = default)
        => Ok(await recurrenceService.MaterializeAsync(
            new MaterializeRecurrencesCommand(request.Year, request.Month),
            ct));

    [HttpPatch("{id:guid}/deactivate")]
    [ProducesResponseType(typeof(RecurrenceRuleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct = default)
        => Ok(await recurrenceService.DeactivateAsync(id, ct));
}
