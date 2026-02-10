using FFXIV.Application.Gear.DTOs;
using FFXIV.Application.Gear.Queries;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Provides endpoints for gear recommendations and equipment search for DoH/DoL jobs.
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
    /// Get a full gear set recommendation for a job and level.
    /// </summary>
    /// <param name="job">DoH/DoL job abbreviation (e.g. BSM, CRP, MIN).</param>
    /// <param name="level">Character level (1-100).</param>
    /// <param name="profile">Stat weight profile name (default: "balanced").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the recommended gear set.</response>
    /// <response code="400">Invalid job or level.</response>
    [HttpGet("recommendations")]
    [ProducesResponseType(typeof(GearSetDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRecommendations(
        [FromQuery] string job,
        [FromQuery] int level = 90,
        [FromQuery] string profile = "balanced",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(job))
            return BadRequest(new { error = "Job is required." });

        var normalizedJob = job.ToUpperInvariant();
        if (!JobClassification.IsDoH(normalizedJob) && !JobClassification.IsDoL(normalizedJob))
            return BadRequest(new { error = $"'{job}' is not a valid DoH/DoL job. Valid jobs: CRP, BSM, ARM, GSM, LTW, WVR, ALC, CUL, MIN, BTN, FSH." });

        if (level is < 1 or > 100)
            return BadRequest(new { error = "Level must be between 1 and 100." });

        var result = await _mediator.Send(
            new GetGearRecommendationsQuery(normalizedJob, level, profile), ct);

        return Ok(result);
    }

    /// <summary>
    /// Get a recommendation for a single equipment slot.
    /// </summary>
    /// <param name="slot">Equipment slot (e.g. MainHand, Head, Body, Ring).</param>
    /// <param name="job">DoH/DoL job abbreviation.</param>
    /// <param name="level">Character level (1-100).</param>
    /// <param name="profile">Stat weight profile name (default: "balanced").</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the slot recommendation with alternatives.</response>
    /// <response code="400">Invalid slot, job, or level.</response>
    [HttpGet("recommendations/{slot}")]
    [ProducesResponseType(typeof(GearSlotDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetSlotRecommendation(
        string slot,
        [FromQuery] string job,
        [FromQuery] int level = 90,
        [FromQuery] string profile = "balanced",
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<EquipSlot>(slot, ignoreCase: true, out _))
            return BadRequest(new { error = $"'{slot}' is not a valid equipment slot. Valid slots: {string.Join(", ", Enum.GetNames<EquipSlot>())}." });

        if (string.IsNullOrWhiteSpace(job))
            return BadRequest(new { error = "Job is required." });

        var normalizedJob = job.ToUpperInvariant();
        if (!JobClassification.IsDoH(normalizedJob) && !JobClassification.IsDoL(normalizedJob))
            return BadRequest(new { error = $"'{job}' is not a valid DoH/DoL job." });

        if (level is < 1 or > 100)
            return BadRequest(new { error = "Level must be between 1 and 100." });

        var result = await _mediator.Send(
            new GetSlotRecommendationQuery(slot, normalizedJob, level, profile), ct);

        return Ok(result);
    }

    /// <summary>
    /// Get available stat weight profiles, optionally filtered by job.
    /// </summary>
    /// <param name="job">Optional DoH/DoL job abbreviation to filter profiles.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns available stat weight profiles.</response>
    [HttpGet("profiles")]
    [ProducesResponseType(typeof(List<StatWeightProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetProfiles(
        [FromQuery] string? job = null,
        CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(job))
        {
            var normalizedJob = job.ToUpperInvariant();
            if (!JobClassification.IsDoH(normalizedJob) && !JobClassification.IsDoL(normalizedJob))
                return BadRequest(new { error = $"'{job}' is not a valid DoH/DoL job." });
        }

        var result = await _mediator.Send(new GetStatWeightProfilesQuery(job), ct);
        return Ok(result);
    }

    /// <summary>
    /// Calculate aggregated stats for a custom gear set.
    /// </summary>
    /// <param name="request">A mapping of slot keys to equipment IDs.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the aggregated stats.</response>
    /// <response code="400">Empty or invalid request body.</response>
    [HttpPost("calculate-stats")]
    [ProducesResponseType(typeof(GearStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CalculateStats(
        [FromBody] CalculateGearStatsRequest request,
        CancellationToken ct = default)
    {
        if (request.Slots.Count == 0)
            return BadRequest(new { error = "At least one slot must be provided." });

        var result = await _mediator.Send(
            new CalculateGearStatsQuery(request.Slots), ct);

        return Ok(result);
    }

    /// <summary>
    /// Get gear recommendations with comparison against the current user's profile gear.
    /// Requires authentication. Returns standard recommendations with per-slot comparison data.
    /// </summary>
    /// <param name="job">DoH/DoL job abbreviation (e.g. BSM, CRP, MIN).</param>
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
        if (!JobClassification.IsDoH(normalizedJob) && !JobClassification.IsDoL(normalizedJob))
            return BadRequest(new { error = $"'{job}' is not a valid DoH/DoL job. Valid jobs: CRP, BSM, ARM, GSM, LTW, WVR, ALC, CUL, MIN, BTN, FSH." });

        if (level is < 1 or > 100)
            return BadRequest(new { error = "Level must be between 1 and 100." });

        var result = await _mediator.Send(
            new GetGearComparisonQuery(_currentUser.UserId!, normalizedJob, level, profile), ct);

        return Ok(result);
    }

    /// <summary>
    /// Search for equipment by name with optional filters.
    /// </summary>
    /// <param name="q">Search query (minimum 2 characters).</param>
    /// <param name="job">Optional job abbreviation filter.</param>
    /// <param name="slot">Optional equipment slot filter.</param>
    /// <param name="minLevel">Optional minimum equip level.</param>
    /// <param name="maxLevel">Optional maximum equip level.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns matching equipment.</response>
    /// <response code="400">Invalid search query.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(List<EquipmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SearchEquipment(
        [FromQuery] string q,
        [FromQuery] string? job = null,
        [FromQuery] string? slot = null,
        [FromQuery] int? minLevel = null,
        [FromQuery] int? maxLevel = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            return BadRequest(new { error = "Search query must be at least 2 characters." });

        if (!string.IsNullOrEmpty(slot) && !Enum.TryParse<EquipSlot>(slot, ignoreCase: true, out _))
            return BadRequest(new { error = $"'{slot}' is not a valid equipment slot." });

        var result = await _mediator.Send(
            new SearchEquipmentQuery(q, job, slot, minLevel, maxLevel), ct);

        return Ok(result);
    }
}
