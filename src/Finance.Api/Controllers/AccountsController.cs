using Finance.Application.Accounts;
using Finance.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/v1/accounts")]
[Authorize]
[EnableRateLimiting("api-authenticated")]
public sealed class AccountsController(IAccountService accountService) : ControllerBase
{
    public sealed record CreateAccountRequest(
        string Name,
        AccountType Type,
        decimal InitialBalance,
        string? Color = null);

    public sealed record UpdateAccountRequest(
        string Name,
        AccountType Type,
        string? Color = null);

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] bool includeArchived = false, CancellationToken ct = default)
        => Ok(await accountService.GetAllAsync(includeArchived, ct));

    [HttpPost]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateAccountRequest request, CancellationToken ct = default)
    {
        AccountDto created = await accountService.CreateAsync(
            new CreateAccountCommand(request.Name, request.Type, request.InitialBalance, request.Color),
            ct);

        return StatusCode(StatusCodes.Status201Created, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAccountRequest request,
        CancellationToken ct = default)
        => Ok(await accountService.UpdateAsync(
            id,
            new UpdateAccountCommand(request.Name, request.Type, request.Color),
            ct));

    [HttpPatch("{id:guid}/archive")]
    [ProducesResponseType(typeof(AccountDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(
        Guid id,
        [FromBody] ArchiveRequest? request = null,
        CancellationToken ct = default)
        => Ok(await accountService.SetArchivedAsync(id, request?.IsArchived ?? true, ct));
}
