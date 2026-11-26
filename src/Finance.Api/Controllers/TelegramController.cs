using System.Security.Cryptography;
using System.Text;
using Finance.Api.Services.Telegram;
using Finance.Application.Common.Interfaces;
using Finance.Application.Telegram;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Finance.Api.Controllers;

[ApiController]
[Route("api/v1/telegram")]
public sealed class TelegramController(
    ITelegramBotService telegramBotService,
    ITelegramBotClient botClient) : ControllerBase
{
    public sealed record UpdatePreferencesRequest(
        Guid? DefaultAccountId,
        Guid? DefaultCreditCardId);

    [HttpGet("status")]
    [Authorize]
    [EnableRateLimiting("api-authenticated")]
    [ProducesResponseType(typeof(TelegramStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(CancellationToken ct = default)
        => Ok(await telegramBotService.GetStatusAsync(ct));

    [HttpPost("link-token")]
    [Authorize]
    [EnableRateLimiting("api-authenticated")]
    [ProducesResponseType(typeof(TelegramLinkTokenResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GenerateLinkToken(CancellationToken ct = default)
        => Ok(await telegramBotService.GenerateLinkTokenAsync(ct));

    [HttpPut("preferences")]
    [Authorize]
    [EnableRateLimiting("api-authenticated")]
    [ProducesResponseType(typeof(TelegramStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] UpdatePreferencesRequest request,
        CancellationToken ct = default)
        => Ok(await telegramBotService.UpdatePreferencesAsync(
            new UpdateTelegramPreferencesCommand(request.DefaultAccountId, request.DefaultCreditCardId),
            ct));

    [HttpDelete("link")]
    [Authorize]
    [EnableRateLimiting("api-authenticated")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unlink(CancellationToken ct = default)
    {
        await telegramBotService.UnlinkAsync(ct);
        return NoContent();
    }

    [HttpPost("webhook")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Webhook(
        [FromBody] TelegramRawUpdate rawUpdate,
        CancellationToken ct = default)
    {
        string? expectedSecret = botClient.WebhookSecret;
        if (string.IsNullOrWhiteSpace(expectedSecret))
            return Unauthorized();

        string providedSecret = Request.Headers["X-Telegram-Bot-Api-Secret-Token"].ToString();
        if (!FixedTimeEquals(providedSecret, expectedSecret))
            return Unauthorized();

        TelegramBotUpdate update = HttpTelegramBotClient.MapUpdate(rawUpdate);
        await telegramBotService.HandleUpdateAsync(update, ct);
        return Ok();
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] a = Encoding.UTF8.GetBytes(left);
        byte[] b = Encoding.UTF8.GetBytes(right);
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
