using Cia.Api.DTOs;
using Cia.Api.Exceptions;
using Cia.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cia.Api.Controllers;

[ApiController]
[Route("api/accessibility")]
[Authorize(Roles = "Customer,Agent,Admin")]
public class AccessibilityController : ControllerBase
{
    private readonly IAccessibilityService _accessibility;

    public AccessibilityController(IAccessibilityService accessibility)
    {
        _accessibility = accessibility;
    }

    [HttpGet("preferences")]
    [ProducesResponseType(typeof(AccessibilityPreferencesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreferences(CancellationToken cancellationToken)
    {
        return Ok(await _accessibility.GetMineAsync(RequireUserId(), cancellationToken));
    }

    [HttpPut("preferences")]
    [ProducesResponseType(typeof(AccessibilityPreferencesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdatePreferences(
        [FromBody] AccessibilityPreferencesDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _accessibility.UpdateMineAsync(RequireUserId(), request, cancellationToken));
    }

    private Guid RequireUserId()
    {
        var userId = User.GetUserId();
        if (userId == Guid.Empty)
        {
            throw new UnauthorizedAppException("Usuário não autenticado.");
        }

        return userId;
    }
}
