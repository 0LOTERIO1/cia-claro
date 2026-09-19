using Cia.Api.DTOs;
using Cia.Api.Enums;
using Cia.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cia.Api.Controllers;

[ApiController]
[Route("api/customer")]
[Authorize(Roles = "Customer")]
public class CustomerController : ControllerBase
{
    private readonly IChannelIdentityService _identities;
    private readonly IConversationService _conversations;
    private readonly IServiceRatingService _ratings;
    private readonly ISessionLifecycleService _lifecycle;
    private readonly IRegionalOutageService _outages;
    private readonly IHandoffService _handoffs;

    public CustomerController(
        IChannelIdentityService identities,
        IConversationService conversations,
        IServiceRatingService ratings,
        ISessionLifecycleService lifecycle,
        IRegionalOutageService outages,
        IHandoffService handoffs)
    {
        _identities = identities;
        _conversations = conversations;
        _ratings = ratings;
        _lifecycle = lifecycle;
        _outages = outages;
        _handoffs = handoffs;
    }

    [HttpGet("channels")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerChannelDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChannels(CancellationToken cancellationToken)
    {
        return Ok(await _identities.ListChannelsAsync(User.GetCustomerId(), cancellationToken));
    }

    [HttpPost("channels/telegram/link-code")]
    [ProducesResponseType(typeof(TelegramLinkCodeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateTelegramLinkCode(CancellationToken cancellationToken)
    {
        return Ok(await _identities.GenerateTelegramLinkCodeAsync(User.GetCustomerId(), cancellationToken));
    }

    [HttpDelete("channels/telegram")]
    [ProducesResponseType(typeof(ChannelUnlinkDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UnlinkTelegram(CancellationToken cancellationToken)
    {
        return Ok(await _identities.UnlinkTelegramAsync(User.GetCustomerId(), cancellationToken));
    }

    [HttpGet("active-session")]
    [ProducesResponseType(typeof(ActiveSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveSession(CancellationToken cancellationToken)
    {
        return Ok(await _identities.GetActiveSessionAsync(User.GetCustomerId(), cancellationToken));
    }

    [HttpGet("regional-outage")]
    [ProducesResponseType(typeof(RegionalOutageCheckResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CheckRegionalOutage(
        [FromQuery] string postalCode,
        CancellationToken cancellationToken)
    {
        return Ok(await _outages.CheckAsync(postalCode, cancellationToken));
    }

    [HttpPost("active-session/resume")]
    [ProducesResponseType(typeof(ActiveSessionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> ResumeActiveSession(CancellationToken cancellationToken)
    {
        return Ok(await _identities.ResumeActiveSessionAsync(User.GetCustomerId(), cancellationToken));
    }

    [HttpPost("messages")]
    [EnableRateLimiting("chat")]
    [RequestSizeLimit(8_192)]
    [ProducesResponseType(typeof(SendMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendMessage(
        [FromBody] CustomerMessageRequest request,
        CancellationToken cancellationToken)
    {
        var response = await _conversations.SendMessageAsync(
            new SendMessageRequest
            {
                CustomerId = User.GetCustomerId(),
                Channel = ChannelType.WebPortal,
                Content = request.Content
            },
            cancellationToken);
        return Ok(response);
    }

    [HttpPost("sessions/{sessionId:guid}/rating")]
    [ProducesResponseType(typeof(SubmitServiceRatingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitRating(
        Guid sessionId,
        [FromBody] SubmitServiceRatingRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _ratings.SubmitAsync(User.GetCustomerId(), sessionId, request, cancellationToken));
    }

    [HttpPost("sessions/{sessionId:guid}/handoff")]
    [EnableRateLimiting("handoff")]
    [ProducesResponseType(typeof(HandoffDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateHandoff(Guid sessionId, CancellationToken cancellationToken)
    {
        var session = await _conversations.GetSessionAsync(sessionId, cancellationToken);
        if (!string.Equals(session.CustomerId, User.GetCustomerId(), StringComparison.Ordinal))
        {
            return Forbid();
        }

        var handoff = await _handoffs.CreateHandoffAsync(sessionId, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, handoff);
    }

    [HttpPost("sessions/restart")]
    [ProducesResponseType(typeof(SessionLifecycleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> RestartSession(CancellationToken cancellationToken)
    {
        return Ok(await ExecuteLifecycleAsync(
            customerId => _lifecycle.RestartAsync(customerId, ChannelType.WebPortal, cancellationToken: cancellationToken),
            cancellationToken));
    }

    [HttpPost("sessions/end")]
    [ProducesResponseType(typeof(SessionLifecycleResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> EndSession(CancellationToken cancellationToken)
    {
        return Ok(await ExecuteLifecycleAsync(
            customerId => _lifecycle.EndAsync(customerId, cancellationToken),
            cancellationToken));
    }

    private async Task<SessionLifecycleResponse> ExecuteLifecycleAsync(
        Func<string, Task<SessionLifecycleResult>> action,
        CancellationToken cancellationToken)
    {
        var customerId = User.GetCustomerId();
        var result = await action(customerId);
        return new SessionLifecycleResponse
        {
            Action = result.Action,
            Message = result.Message,
            ClosedSessionId = result.ClosedSessionId,
            NewSessionId = result.NewSessionId,
            Snapshot = await _identities.GetActiveSessionAsync(customerId, cancellationToken)
        };
    }
}
