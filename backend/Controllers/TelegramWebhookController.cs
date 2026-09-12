using System.Security.Cryptography;
using System.Text;
using Cia.Api.Configuration;
using Cia.Api.DTOs;
using Cia.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Cia.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/telegram")]
public class TelegramWebhookController : ControllerBase
{
    public const string SecretHeaderName = "X-Telegram-Bot-Api-Secret-Token";

    private readonly TelegramOptions _options;
    private readonly ITelegramInboundService _inbound;
    private readonly ILogger<TelegramWebhookController> _logger;

    public TelegramWebhookController(
        IOptions<TelegramOptions> options,
        ITelegramInboundService inbound,
        ILogger<TelegramWebhookController> logger)
    {
        _options = options.Value;
        _inbound = inbound;
        _logger = logger;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(TelegramStatusDto), StatusCodes.Status200OK)]
    public IActionResult Status()
    {
        return Ok(new TelegramStatusDto { Configured = _options.IsConfigured });
    }

    [HttpPost("webhook")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Webhook(
        [FromBody] TelegramUpdateDto? update,
        CancellationToken cancellationToken)
    {
        if (!IsValidSecret(Request.Headers[SecretHeaderName].ToString()))
        {
            return Unauthorized();
        }

        if (update is not null)
        {
            try
            {
                await _inbound.HandleUpdateAsync(update, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Telegram message processing failed. UpdateId={UpdateId}", update.UpdateId);
            }
        }

        return Ok();
    }

    private bool IsValidSecret(string? provided)
    {
        if (string.IsNullOrEmpty(provided) || string.IsNullOrEmpty(_options.WebhookSecret))
        {
            return false;
        }

        var actual = Encoding.UTF8.GetBytes(provided);
        var expected = Encoding.UTF8.GetBytes(_options.WebhookSecret);
        return actual.Length == expected.Length && CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
