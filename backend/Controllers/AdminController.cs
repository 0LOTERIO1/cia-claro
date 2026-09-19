using Cia.Api.DTOs;
using Cia.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cia.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly IRegionalOutageService _outages;

    public AdminController(IDashboardService dashboard, IRegionalOutageService outages)
    {
        _dashboard = dashboard;
        _outages = outages;
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

    [HttpGet("outages")]
    [ProducesResponseType(typeof(IReadOnlyList<RegionalOutageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOutages(
        [FromQuery] bool includeResolved = true,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _outages.ListAsync(includeResolved, cancellationToken));
    }

    [HttpPost("outages")]
    [ProducesResponseType(typeof(RegionalOutageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateOutage(
        [FromBody] CreateRegionalOutageRequest request,
        CancellationToken cancellationToken)
    {
        var outage = await _outages.CreateAsync(User.GetUserId(), request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, outage);
    }

    [HttpPost("outages/{id:guid}/resolve")]
    [ProducesResponseType(typeof(RegionalOutageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ResolveOutage(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await _outages.ResolveAsync(id, cancellationToken));
    }
}
