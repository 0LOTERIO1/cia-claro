using Cia.Api.DTOs;
using Cia.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cia.Api.Controllers;

[ApiController]
[Route("api/sessions")]
public class SessionsController : ControllerBase
{
    private readonly IConversationService _conversations;
    private readonly IHandoffService _handoffs;
    private readonly IMessageRepository _messages;

    public SessionsController(
        IConversationService conversations,
        IHandoffService handoffs,
        IMessageRepository messages)
    {
        _conversations = conversations;
        _handoffs = handoffs;
        _messages = messages;
    }

    [HttpPost]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateSessionRequest request, CancellationToken cancellationToken)
    {
        var session = await _conversations.CreateSessionAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = session.Id }, session);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var session = await _conversations.GetSessionAsync(id, cancellationToken);
        return Ok(session);
    }

    [HttpGet("{id:guid}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<MessageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMessages(Guid id, CancellationToken cancellationToken)
    {
        await _conversations.GetSessionAsync(id, cancellationToken);
        var messages = await _messages.GetBySessionIdAsync(id, cancellationToken);
        return Ok(messages.Select(m => m.ToDto()));
    }

    [HttpGet("customer/{customerId}")]
    [ProducesResponseType(typeof(IReadOnlyList<SessionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByCustomer(string customerId, CancellationToken cancellationToken)
    {
        var sessions = await _conversations.GetSessionsByCustomerAsync(customerId, cancellationToken);
        return Ok(sessions);
    }

    [HttpPost("{id:guid}/channel")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeChannel(
        Guid id,
        [FromBody] ChangeChannelRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _conversations.ChangeChannelAsync(id, request.Channel, cancellationToken);
        return Ok(session);
    }

    [HttpPost("{id:guid}/department")]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeDepartment(
        Guid id,
        [FromBody] ChangeDepartmentRequest request,
        CancellationToken cancellationToken)
    {
        var session = await _conversations.ChangeDepartmentAsync(id, request.Department, request.Reason, cancellationToken);
        return Ok(session);
    }

    [HttpPost("{id:guid}/handoff")]
    [ProducesResponseType(typeof(HandoffDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Handoff(Guid id, CancellationToken cancellationToken)
    {
        var handoff = await _handoffs.CreateHandoffAsync(id, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, handoff);
    }
}
