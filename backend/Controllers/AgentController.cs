using Cia.Api.DTOs;
using Cia.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cia.Api.Controllers;

[ApiController]
[Route("api/agent")]
[Authorize(Roles = "Agent,Admin")]
public class AgentController : ControllerBase
{
    private readonly IHumanAgentService _humanAgents;

    public AgentController(IHumanAgentService humanAgents)
    {
        _humanAgents = humanAgents;
    }

    [HttpGet("queue")]
    [ProducesResponseType(typeof(IReadOnlyList<AgentQueueItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Queue(CancellationToken cancellationToken)
    {
        return Ok(await _humanAgents.GetQueueAsync(cancellationToken));
    }

    [HttpGet("mine")]
    [ProducesResponseType(typeof(IReadOnlyList<AgentQueueItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Mine(CancellationToken cancellationToken)
    {
        return Ok(await _humanAgents.GetAssignedAsync(User.GetUserId(), cancellationToken));
    }

    [HttpGet("requests/{id:guid}")]
    [ProducesResponseType(typeof(AgentSessionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetRequest(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _humanAgents.GetDetailAsync(id, cancellationToken));
    }

    [HttpPost("requests/{id:guid}/assume")]
    [ProducesResponseType(typeof(AgentSessionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Assume(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _humanAgents.AssumeAsync(id, User.GetUserId(), cancellationToken));
    }

    [HttpPost("requests/{id:guid}/finish")]
    [ProducesResponseType(typeof(AgentSessionDetailDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Finish(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _humanAgents.FinishAsync(id, User.GetUserId(), cancellationToken));
    }

    [HttpPost("sessions/{sessionId:guid}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<MessageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SendMessage(
        Guid sessionId,
        [FromBody] AgentMessageRequest request,
        CancellationToken cancellationToken)
    {
        var messages = await _humanAgents.SendMessageAsync(
            sessionId,
            User.GetUserId(),
            request.Content,
            cancellationToken);
        return Ok(messages);
    }
}
