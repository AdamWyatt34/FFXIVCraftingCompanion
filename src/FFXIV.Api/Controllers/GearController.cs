using FFXIV.Application.Gear.DTOs;
using FFXIV.Application.Gear.Queries;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Gear recommendations compared against the signed-in user's profile. Anonymous gear
/// recommendations, profiles and equipment search are served from the static game-data export.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GearController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public GearController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get gear recommendations with comparison against the current user's profile gear.
    /// Requires authentication. Returns standard recommendations with per-slot comparison data.
    /// </summary>
    /// <param name="job">Job abbreviation (e.g. BSM, CRP, MIN, PLD, WHM).</param>
    /// <param name="level">Character level (1-100).</param>
    /// <param name="profile">Stat weight profile name (default: "balanced").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns gear recommendations with comparison data.</response>
    /// <response code="400">Invalid job or level.</response>
    /// <response code="401">Not authenticated.</response>
    [Authorize]
    [HttpGet("recommendations/compare")]
    [ProducesResponseType(typeof(GearComparisonDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetRecommendationsWithComparison(
        [FromQuery] string job,
        [FromQuery] int level = 90,
        [FromQuery] string profile = "balanced",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(job))
            return BadRequest(new { error = "Job is required." });

        var normalizedJob = job.ToUpperInvariant();
        if (!JobClassification.IsKnownJob(normalizedJob))
            return BadRequest(new { error = $"'{job}' is not a valid job. Valid jobs: {string.Join(", ", JobClassification.AllJobs)}." });

        if (level is < 1 or > 100)
            return BadRequest(new { error = "Level must be between 1 and 100." });

        var result = await _mediator.Send(
            new GetGearComparisonQuery(_currentUser.UserId!, normalizedJob, level, profile), ct);

        return Ok(result);
    }
}
