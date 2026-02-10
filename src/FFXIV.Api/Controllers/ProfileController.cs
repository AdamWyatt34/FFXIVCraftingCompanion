using FFXIV.Application.Profile.Commands;
using FFXIV.Application.Profile.DTOs;
using FFXIV.Application.Profile.Queries;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Character profile management. All endpoints require authentication.
/// </summary>
[Authorize]
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ProfileController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public ProfileController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get the current user's character profile.
    /// </summary>
    /// <response code="200">Returns the character profile.</response>
    /// <response code="404">No profile exists for the current user.</response>
    [HttpGet]
    [ProducesResponseType(typeof(CharacterProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProfileQuery(_currentUser.UserId!), ct);
        return result is not null
            ? Ok(result)
            : NotFound(new { error = "Profile not found." });
    }

    /// <summary>
    /// Create a character profile for the current user.
    /// </summary>
    /// <response code="201">Profile created successfully.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="409">A profile already exists for this user.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CharacterProfileDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateProfile(
        [FromBody] CreateProfileRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CharacterName))
            return BadRequest(new { error = "Character name is required." });

        if (string.IsNullOrWhiteSpace(request.ActiveJob))
            return BadRequest(new { error = "Active job is required." });

        var normalizedJob = request.ActiveJob.ToUpperInvariant();
        if (!JobClassification.IsDoH(normalizedJob) && !JobClassification.IsDoL(normalizedJob))
            return BadRequest(new { error = $"'{request.ActiveJob}' is not a valid DoH/DoL job." });

        if (request.ActiveJobLevel is < 1 or > 100)
            return BadRequest(new { error = "Active job level must be between 1 and 100." });

        if (request.Craftsmanship < 0 || request.Control < 0 || request.CP < 0 ||
            request.Gathering < 0 || request.Perception < 0 || request.GP < 0)
            return BadRequest(new { error = "Stats must be non-negative." });

        if (request.JobLevels is not null)
        {
            var jobLevelError = ValidateJobLevels(request.JobLevels);
            if (jobLevelError is not null)
                return BadRequest(new { error = jobLevelError });
        }

        var result = await _mediator.Send(new CreateProfileCommand(
            _currentUser.UserId!,
            request.CharacterName.Trim(),
            request.ServerName?.Trim(),
            request.DataCenter?.Trim(),
            request.LodestoneId?.Trim(),
            request.AvatarUrl?.Trim(),
            normalizedJob,
            request.ActiveJobLevel,
            request.JobLevels,
            request.Craftsmanship,
            request.Control,
            request.CP,
            request.Gathering,
            request.Perception,
            request.GP), ct);

        if (result is null)
            return Conflict(new { error = "A profile already exists for this user." });

        return CreatedAtAction(nameof(GetProfile), result);
    }

    /// <summary>
    /// Update the current user's character profile (partial update).
    /// </summary>
    /// <response code="200">Profile updated successfully.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="404">No profile exists for the current user.</response>
    [HttpPut]
    [ProducesResponseType(typeof(CharacterProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfile(
        [FromBody] UpdateProfileRequest request, CancellationToken ct)
    {
        if (request.ActiveJob is not null)
        {
            var normalizedJob = request.ActiveJob.ToUpperInvariant();
            if (!JobClassification.IsDoH(normalizedJob) && !JobClassification.IsDoL(normalizedJob))
                return BadRequest(new { error = $"'{request.ActiveJob}' is not a valid DoH/DoL job." });
        }

        if (request.ActiveJobLevel.HasValue && request.ActiveJobLevel is < 1 or > 100)
            return BadRequest(new { error = "Active job level must be between 1 and 100." });

        if ((request.Craftsmanship.HasValue && request.Craftsmanship < 0) ||
            (request.Control.HasValue && request.Control < 0) ||
            (request.CP.HasValue && request.CP < 0) ||
            (request.Gathering.HasValue && request.Gathering < 0) ||
            (request.Perception.HasValue && request.Perception < 0) ||
            (request.GP.HasValue && request.GP < 0))
            return BadRequest(new { error = "Stats must be non-negative." });

        var result = await _mediator.Send(new UpdateProfileCommand(
            _currentUser.UserId!,
            request.CharacterName?.Trim(),
            request.ServerName?.Trim(),
            request.DataCenter?.Trim(),
            request.LodestoneId?.Trim(),
            request.AvatarUrl?.Trim(),
            request.ActiveJob?.ToUpperInvariant(),
            request.ActiveJobLevel,
            request.Craftsmanship,
            request.Control,
            request.CP,
            request.Gathering,
            request.Perception,
            request.GP), ct);

        return result is not null
            ? Ok(result)
            : NotFound(new { error = "Profile not found." });
    }

    /// <summary>
    /// Update job levels for the current user's profile. Merges with existing levels.
    /// </summary>
    /// <response code="200">Job levels updated successfully.</response>
    /// <response code="400">Invalid job abbreviation or level.</response>
    /// <response code="404">No profile exists for the current user.</response>
    [HttpPut("jobs")]
    [ProducesResponseType(typeof(CharacterProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateJobLevels(
        [FromBody] UpdateJobLevelsRequest request, CancellationToken ct)
    {
        if (request.JobLevels.Count == 0)
            return BadRequest(new { error = "At least one job level must be provided." });

        var jobLevelError = ValidateJobLevels(request.JobLevels);
        if (jobLevelError is not null)
            return BadRequest(new { error = jobLevelError });

        var result = await _mediator.Send(new UpdateJobLevelsCommand(
            _currentUser.UserId!,
            request.JobLevels), ct);

        return result is not null
            ? Ok(result)
            : NotFound(new { error = "Profile not found." });
    }

    /// <summary>
    /// Update equipped gear and recalculate stats from equipment.
    /// </summary>
    /// <response code="200">Gear updated and stats recalculated.</response>
    /// <response code="400">Invalid slot key or no slots provided.</response>
    /// <response code="404">No profile exists for the current user.</response>
    [HttpPut("gear")]
    [ProducesResponseType(typeof(CharacterProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateCurrentGear(
        [FromBody] UpdateCurrentGearRequest request, CancellationToken ct)
    {
        if (request.Slots.Count == 0)
            return BadRequest(new { error = "At least one gear slot must be provided." });

        foreach (var slotKey in request.Slots.Keys)
        {
            if (!ValidSlotKeys.Contains(slotKey))
                return BadRequest(new { error = $"'{slotKey}' is not a valid gear slot. Valid slots: {string.Join(", ", ValidSlotKeys)}" });
        }

        var result = await _mediator.Send(new UpdateCurrentGearCommand(
            _currentUser.UserId!,
            request.Slots), ct);

        return result is not null
            ? Ok(result)
            : NotFound(new { error = "Profile not found." });
    }

    /// <summary>
    /// Get just the current user's profile stats.
    /// </summary>
    /// <response code="200">Returns the stats.</response>
    /// <response code="404">No profile exists for the current user.</response>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(ProfileStatsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var result = await _mediator.Send(new GetProfileQuery(_currentUser.UserId!), ct);
        if (result is null)
            return NotFound(new { error = "Profile not found." });

        return Ok(result.Stats);
    }

    /// <summary>
    /// Delete the current user's character profile.
    /// </summary>
    /// <response code="204">Profile deleted.</response>
    /// <response code="404">No profile exists for the current user.</response>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteProfile(CancellationToken ct)
    {
        var deleted = await _mediator.Send(new DeleteProfileCommand(_currentUser.UserId!), ct);
        return deleted
            ? NoContent()
            : NotFound(new { error = "Profile not found." });
    }

    /// <summary>
    /// Import a character from the FFXIV Lodestone by name and server.
    /// Creates or updates the user's profile with Lodestone data.
    /// </summary>
    /// <response code="200">Character imported successfully.</response>
    /// <response code="400">Invalid server name.</response>
    /// <response code="404">Character not found on Lodestone.</response>
    /// <response code="429">Rate limited — wait before importing again.</response>
    /// <response code="503">Lodestone is currently unavailable.</response>
    [HttpPost("import-lodestone")]
    [ProducesResponseType(typeof(CharacterProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> ImportLodestone(
        [FromBody] ImportLodestoneRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.CharacterName))
            return BadRequest(new { error = "Character name is required." });

        if (string.IsNullOrWhiteSpace(request.ServerName))
            return BadRequest(new { error = "Server name is required." });

        var result = await _mediator.Send(new ImportLodestoneCommand(
            _currentUser.UserId!,
            request.CharacterName.Trim(),
            request.ServerName.Trim()), ct);

        return result.Success
            ? Ok(result.Profile)
            : StatusCode(result.StatusCode, new { error = result.Error });
    }

    /// <summary>
    /// Refresh the current user's profile from the Lodestone using the stored Lodestone ID.
    /// </summary>
    /// <response code="200">Profile refreshed successfully.</response>
    /// <response code="400">No Lodestone ID linked.</response>
    /// <response code="404">No profile found.</response>
    /// <response code="429">Rate limited — wait before refreshing again.</response>
    /// <response code="503">Lodestone is currently unavailable.</response>
    [HttpPost("refresh-lodestone")]
    [ProducesResponseType(typeof(CharacterProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RefreshLodestone(CancellationToken ct)
    {
        var result = await _mediator.Send(new RefreshLodestoneCommand(_currentUser.UserId!), ct);

        return result.Success
            ? Ok(result.Profile)
            : StatusCode(result.StatusCode, new { error = result.Error });
    }

    /// <summary>
    /// Preview a character from the Lodestone without importing. No authentication required.
    /// </summary>
    /// <response code="200">Returns a preview of the character.</response>
    /// <response code="400">Missing or invalid parameters.</response>
    /// <response code="404">Character not found on Lodestone.</response>
    [AllowAnonymous]
    [HttpGet("lodestone-preview")]
    [ProducesResponseType(typeof(LodestonePreviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> LodestonePreview(
        [FromQuery] string? name, [FromQuery] string? server, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name))
            return BadRequest(new { error = "Name query parameter is required." });

        if (string.IsNullOrWhiteSpace(server))
            return BadRequest(new { error = "Server query parameter is required." });

        var result = await _mediator.Send(new PreviewLodestoneQuery(name.Trim(), server.Trim()), ct);

        return result is not null
            ? Ok(result)
            : NotFound(new { error = "Character not found on Lodestone." });
    }

    private static readonly HashSet<string> ValidSlotKeys =
    [
        "Head", "Body", "Hands", "Legs", "Feet",
        "MainHand", "OffHand",
        "Earring", "Necklace", "Bracelet",
        "Ring1", "Ring2"
    ];

    private static string? ValidateJobLevels(Dictionary<string, int> jobLevels)
    {
        foreach (var (job, level) in jobLevels)
        {
            var normalized = job.ToUpperInvariant();
            if (!JobClassification.IsDoH(normalized) && !JobClassification.IsDoL(normalized))
                return $"'{job}' is not a valid DoH/DoL job.";

            if (level is < 1 or > 100)
                return $"Level for '{job}' must be between 1 and 100.";
        }

        return null;
    }
}
