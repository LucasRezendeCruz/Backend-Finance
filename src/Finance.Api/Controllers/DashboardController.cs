using Finance.Application.Common.Interfaces;
using Finance.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
[Authorize]
[EnableRateLimiting("api-authenticated")]
public sealed class DashboardController(
    IDashboardService dashboardService,
    IDateTimeProvider dateTimeProvider) : ControllerBase
{
    [HttpGet("monthly-summary")]
    [ProducesResponseType(typeof(MonthlyDashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetMonthlySummary(
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        CancellationToken ct = default)
    {
        DateTime utcNow = dateTimeProvider.UtcNow.UtcDateTime;
        int targetYear = year ?? utcNow.Year;
        int targetMonth = month ?? utcNow.Month;

        return Ok(await dashboardService.GetMonthlySummaryAsync(targetYear, targetMonth, ct));
    }

    [HttpGet("projection")]
    [ProducesResponseType(typeof(CashflowProjectionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetProjection(
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] int months = 6,
        CancellationToken ct = default)
    {
        DateTime utcNow = dateTimeProvider.UtcNow.UtcDateTime;
        int startYear = year ?? utcNow.Year;
        int startMonth = month ?? utcNow.Month;

        return Ok(await dashboardService.GetProjectionAsync(startYear, startMonth, months, ct));
    }
}
