using Cia.Api.DTOs;
using Cia.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cia.Api.Controllers;

[ApiController]
[Route("api/chat")]
[Authorize(Roles = "Customer")]
public class ChatController : ControllerBase
{
    private readonly IConversationService _conversations;

    public ChatController(IConversationService conversations)
    {
        _conversations = conversations;
    }

    [HttpPost("message")]
    [EnableRateLimiting("chat")]
    [RequestSizeLimit(8_192)]
    [ProducesResponseType(typeof(SendMessageResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Send([FromBody] SendMessageRequest request, CancellationToken cancellationToken)
    {
        request.CustomerId = User.GetCustomerId();
        var response = await _conversations.SendMessageAsync(request, cancellationToken);
        return Ok(response);
    }
}
