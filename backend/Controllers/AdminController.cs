using Cia.Api.DTOs;
using Cia.Api.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Cia.Api.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IDashboardService _dashboard;

    public AdminController(IDashboardService dashboard)
    {
        _dashboard = dashboard;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(DashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard(CancellationToken cancellationToken)
    {
        return Ok(await _dashboard.GetDashboardAsync(cancellationToken));
    }

    [HttpGet("sessions")]
    [ProducesResponseType(typeof(IReadOnlyList<SessionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSessions(CancellationToken cancellationToken)
    {
        return Ok(await _dashboard.GetSessionsAsync(cancellationToken));
    }

    [HttpGet("sessions/{id:guid}")]
    [ProducesResponseType(typeof(AdminSessionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetSession(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _dashboard.GetSessionDetailAsync(id, cancellationToken));
    }
}
