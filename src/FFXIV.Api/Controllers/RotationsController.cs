using FFXIV.Application.Rotations.Commands;
using FFXIV.Application.Rotations.DTOs;
using FFXIV.Application.Rotations.Queries;
using FFXIV.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Rotation library — browse, save, and share crafting rotations.
/// </summary>
[ApiController]
[Route("api/rotations")]
[Produces("application/json")]
public class RotationsController : ControllerBase
{
    private const int MaxPage = 1000;
    private const int MaxPageSize = 50;

    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public RotationsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Save a rotation and its client-reported quality/HQ metrics to the public library.
    /// </summary>
    [Authorize]
    [HttpPost]
    [EnableRateLimiting("rotation-write")]
    [ProducesResponseType(typeof(RotationDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] SubmitRotationRequest request, CancellationToken ct)
    {
        // Action count, recipe id, stats and text lengths are DataAnnotations on the request type.
        var result = await _mediator.Send(
            new CreateRotationCommand(_currentUser.UserId!, request), ct);

        if (result is null)
            return Conflict(new { error = "Rotation quota exceeded (max 200) or invalid recipe/actions/metrics (total steps must not exceed the action count)." });

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Search and browse rotations with optional filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(RotationListResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] int? recipeId,
        [FromQuery] string? craftType,
        [FromQuery] int? minLevel,
        [FromQuery] int? maxLevel,
        [FromQuery] string? source,
        [FromQuery] string sortBy = "recent",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        // A huge page number is a huge OFFSET for the database; past the last page the result is
        // empty anyway.
        page = Math.Clamp(page, 1, MaxPage);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var result = await _mediator.Send(
            new SearchRotationsQuery(recipeId, craftType, minLevel, maxLevel,
                source, sortBy, page, pageSize), ct);

        return Ok(result);
    }

    /// <summary>
    /// Get a single rotation with full details including action names and macros.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RotationDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new GetRotationByIdQuery(id), ct);

        return result is not null
            ? Ok(result)
            : NotFound(new { error = "Rotation not found." });
    }

    /// <summary>
    /// Toggle upvote on a rotation. Requires authentication.
    /// </summary>
    [Authorize]
    [HttpPost("{id:int}/upvote")]
    [ProducesResponseType(typeof(UpvoteResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ToggleUpvote(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ToggleRotationVoteCommand(_currentUser.UserId!, id), ct);

        return result is not null
            ? Ok(result)
            : NotFound(new { error = "Rotation not found." });
    }

    /// <summary>
    /// Delete a rotation. Only the author can delete their own rotations.
    /// </summary>
    [Authorize]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await _mediator.Send(
            new DeleteRotationCommand(_currentUser.UserId!, id), ct);

        return deleted
            ? NoContent()
            : NotFound(new { error = "Rotation not found or you are not the author." });
    }
}
