using Finance.Application.Common.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public sealed class HealthController(
    IDateTimeProvider dateTimeProvider,
    IHostEnvironment hostEnvironment) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "Healthy",
            timestampUtc = dateTimeProvider.UtcNow,
            environment = hostEnvironment.EnvironmentName
        });
    }
}
