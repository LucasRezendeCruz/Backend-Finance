using Finance.Application.CreditCards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/v1/credit-cards")]
[Authorize]
[EnableRateLimiting("api-authenticated")]
public sealed class CreditCardsController(ICreditCardService creditCardService) : ControllerBase
{
    public sealed record CreateCreditCardRequest(
        string Name,
        decimal CreditLimit,
        int ClosingDay,
        int DueDay,
        Guid? DefaultAccountId = null,
        string? Color = null);

    public sealed record UpdateCreditCardRequest(
        string Name,
        decimal CreditLimit,
        int ClosingDay,
        int DueDay,
        Guid? DefaultAccountId = null,
        string? Color = null);

    public sealed record PayInvoiceRequest(
        Guid? AccountId = null,
        DateOnly? PaymentDate = null);

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CreditCardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeArchived = false, CancellationToken ct = default)
        => Ok(await creditCardService.GetAllAsync(includeArchived, ct));

    [HttpPost]
    [ProducesResponseType(typeof(CreditCardDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCreditCardRequest request,
        CancellationToken ct = default)
    {
        CreditCardDto created = await creditCardService.CreateAsync(
            new CreateCreditCardCommand(
                request.Name,
                request.CreditLimit,
                request.ClosingDay,
                request.DueDay,
                request.DefaultAccountId,
                request.Color),
            ct);

        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(CreditCardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateCreditCardRequest request,
        CancellationToken ct = default)
        => Ok(await creditCardService.UpdateAsync(
            id,
            new UpdateCreditCardCommand(
                request.Name,
                request.CreditLimit,
                request.ClosingDay,
                request.DueDay,
                request.DefaultAccountId,
                request.Color),
            ct));

    [HttpGet("{id:guid}/invoices")]
    [ProducesResponseType(typeof(IReadOnlyList<CreditCardInvoiceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoices(
        Guid id,
        [FromQuery] int? year = null,
        CancellationToken ct = default)
        => Ok(await creditCardService.GetInvoicesAsync(id, year, ct));

    [HttpGet("{id:guid}/invoices/{invoiceId:guid}")]
    [ProducesResponseType(typeof(CreditCardInvoiceDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInvoiceDetail(
        Guid id,
        Guid invoiceId,
        CancellationToken ct = default)
        => Ok(await creditCardService.GetInvoiceDetailAsync(id, invoiceId, ct));

    [HttpPost("{id:guid}/invoices/{invoiceId:guid}/pay")]
    [ProducesResponseType(typeof(CreditCardInvoiceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PayInvoice(
        Guid id,
        Guid invoiceId,
        [FromBody] PayInvoiceRequest? request = null,
        CancellationToken ct = default)
        => Ok(await creditCardService.PayInvoiceAsync(
            id,
            invoiceId,
            new PayInvoiceCommand(request?.AccountId, request?.PaymentDate),
            ct));
}
