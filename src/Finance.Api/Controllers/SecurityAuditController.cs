using Finance.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/v1/security")]
[Authorize]
[EnableRateLimiting("api-authenticated")]
public sealed class SecurityAuditController(ISecurityAuditService securityAuditService) : ControllerBase
{
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(PagedAuditLogsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetAuditLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool alertsOnly = false,
        CancellationToken ct = default)
        => Ok(await securityAuditService.GetAuditLogsAsync(page, pageSize, alertsOnly, ct));
}
