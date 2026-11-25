using Finance.Application.Transactions;
using Finance.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/v1/transactions")]
[Authorize]
[EnableRateLimiting("api-authenticated")]
public sealed class TransactionsController(ITransactionService transactionService) : ControllerBase
{
    public sealed record CreateTransactionRequest(
        Guid? AccountId,
        Guid? CreditCardId,
        Guid? CategoryId,
        TransactionType Type,
        string Description,
        decimal Amount,
        DateOnly TransactionDate,
        TransactionStatus? Status = null,
        int Installments = 1,
        string? Notes = null);

    public sealed record CreateTransferRequest(
        Guid SourceAccountId,
        Guid DestinationAccountId,
        string Description,
        decimal Amount,
        DateOnly TransferDate,
        string? Notes = null);

    public sealed record UpdateTransactionRequest(
        Guid? CategoryId,
        string Description,
        decimal Amount,
        DateOnly TransactionDate,
        string? Notes = null);

    public sealed record AnticipateInstallmentsRequest(
        IReadOnlyList<Guid>? TransactionIds = null,
        IReadOnlyList<int>? InstallmentNumbers = null,
        int? Count = null,
        InstallmentSelectionOrder SelectionOrder = InstallmentSelectionOrder.FromLast,
        AnticipationMode? Mode = null,
        Guid? TargetInvoiceId = null,
        Guid? PaymentAccountId = null,
        DateOnly? PaymentDate = null,
        decimal DiscountAmount = 0m);

    [HttpGet]
    [ProducesResponseType(typeof(PagedTransactionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPaged(
        [FromQuery] int? month = null,
        [FromQuery] int? year = null,
        [FromQuery] Guid? accountId = null,
        [FromQuery] Guid? creditCardId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery] Guid? installmentGroupId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        TransactionType? parsedType = ControllerQueryHelper.ParseOptionalEnum<TransactionType>(type, nameof(type));
        TransactionStatus? parsedStatus = ControllerQueryHelper.ParseOptionalEnum<TransactionStatus>(status, nameof(status));

        var query = new TransactionFilterQuery(
            month,
            year,
            accountId,
            creditCardId,
            categoryId,
            parsedType,
            parsedStatus,
            installmentGroupId,
            page,
            pageSize);

        return Ok(await transactionService.GetPagedAsync(query, ct));
    }

    [HttpPost]
    [ProducesResponseType(typeof(IReadOnlyList<TransactionDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTransactionRequest request,
        CancellationToken ct = default)
    {
        IReadOnlyList<TransactionDto> created = await transactionService.CreateAsync(
            new CreateTransactionCommand(
                request.AccountId,
                request.CreditCardId,
                request.CategoryId,
                request.Type,
                request.Status,
                request.Description,
                request.Amount,
                request.TransactionDate,
                request.Installments,
                request.Notes),
            ct);

        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPost("transfer")]
    [ProducesResponseType(typeof(TransferResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Transfer(
        [FromBody] CreateTransferRequest request,
        CancellationToken ct = default)
    {
        TransferResultDto result = await transactionService.TransferAsync(
            new CreateTransferCommand(
                request.SourceAccountId,
                request.DestinationAccountId,
                request.Description,
                request.Amount,
                request.TransferDate,
                request.Notes),
            ct);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateTransactionRequest request,
        CancellationToken ct = default)
        => Ok(await transactionService.UpdateAsync(
            id,
            new UpdateTransactionCommand(
                request.CategoryId,
                request.Description,
                request.Amount,
                request.TransactionDate,
                request.Notes),
            ct));

    [HttpPatch("{id:guid}/confirm")]
    [ProducesResponseType(typeof(TransactionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Confirm(Guid id, CancellationToken ct = default)
        => Ok(await transactionService.ConfirmAsync(id, ct));

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct = default)
    {
        await transactionService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("installment-groups")]
    [ProducesResponseType(typeof(IReadOnlyList<InstallmentGroupSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetInstallmentGroups(
        [FromQuery] bool includeCompleted = false,
        CancellationToken ct = default)
        => Ok(await transactionService.GetInstallmentGroupsAsync(includeCompleted, ct));

    [HttpGet("installment-groups/{groupId:guid}")]
    [ProducesResponseType(typeof(InstallmentGroupDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInstallmentGroupDetail(Guid groupId, CancellationToken ct = default)
        => Ok(await transactionService.GetInstallmentGroupDetailAsync(groupId, ct));

    [HttpPost("installment-groups/{groupId:guid}/anticipate")]
    [ProducesResponseType(typeof(AnticipationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AnticipateInstallments(
        Guid groupId,
        [FromBody] AnticipateInstallmentsRequest request,
        CancellationToken ct = default)
        => Ok(await transactionService.AnticipateInstallmentsAsync(
            groupId,
            new AnticipateInstallmentsCommand(
                request.TransactionIds,
                request.InstallmentNumbers,
                request.Count,
                request.SelectionOrder,
                request.Mode,
                request.TargetInvoiceId,
                request.PaymentAccountId,
                request.PaymentDate,
                request.DiscountAmount),
            ct));
}
