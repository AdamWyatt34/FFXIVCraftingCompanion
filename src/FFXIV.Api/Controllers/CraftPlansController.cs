using FFXIV.Application.CraftPlans.Commands;
using FFXIV.Application.CraftPlans.DTOs;
using FFXIV.Application.CraftPlans.Queries;
using FFXIV.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Saved craft plans for signed-in users, plus the shareable read of a plan's items. Resolving a
/// plan into materials, exporting it and estimating its cost happen in the browser from the
/// static game-data export.
/// </summary>
[ApiController]
[Route("api/craft-plans")]
[Produces("application/json")]
public class CraftPlansController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public CraftPlansController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Create a saved craft plan.
    /// </summary>
    /// <param name="request">The plan name and items.</param>
    /// <response code="201">Returns the created plan.</response>
    /// <response code="400">Invalid request or plan limit reached.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(CraftPlanDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Create([FromBody] CreateCraftPlanRequest request)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 256)
            return BadRequest(new { error = "Name is required and must be 256 characters or fewer." });

        var error = ValidateItems(request.Items);
        if (error is not null)
            return BadRequest(new { error });

        var result = await _mediator.Send(new CreateCraftPlanCommand(userId, request.Name, request.Items));
        if (result is null)
            return BadRequest(new { error = "Maximum of 20 craft plans reached." });

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Get all craft plans for the current user.
    /// </summary>
    /// <response code="200">Returns the list of craft plans.</response>
    /// <response code="401">Not authenticated.</response>
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(List<CraftPlanDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll()
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _mediator.Send(new GetCraftPlansQuery(userId));
        return Ok(result);
    }

    /// <summary>
    /// Get a specific craft plan by ID (owner only).
    /// </summary>
    /// <param name="id">The craft plan ID.</param>
    /// <response code="200">Returns the craft plan.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Plan not found or not owned by user.</response>
    [HttpGet("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(CraftPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(string id)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var result = await _mediator.Send(new GetCraftPlanQuery(userId, id));
        if (result is null)
            return NotFound(new { error = "Craft plan not found." });

        return Ok(result);
    }

    /// <summary>
    /// Get a craft plan's name and items by ID (public, for shareable links; the ID is the
    /// 128-bit share capability).
    /// </summary>
    /// <param name="id">The craft plan ID.</param>
    /// <response code="200">Returns the plan's name and items.</response>
    /// <response code="404">Plan not found.</response>
    [HttpGet("{id}/items")]
    [AllowAnonymous]
    [EnableRateLimiting("public-read")]
    [ProducesResponseType(typeof(CraftPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetItems(string id)
    {
        var result = await _mediator.Send(new GetSharedCraftPlanQuery(id));
        if (result is null)
            return NotFound(new { error = "Craft plan not found." });

        return Ok(result);
    }

    /// <summary>
    /// Update a craft plan.
    /// </summary>
    /// <param name="id">The craft plan ID.</param>
    /// <param name="request">The fields to update.</param>
    /// <response code="200">Returns the updated plan.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Plan not found or not owned by user.</response>
    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(CraftPlanDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateCraftPlanRequest request)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (request.Name is not null && (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 256))
            return BadRequest(new { error = "Name must be 256 characters or fewer." });

        if (request.Items is not null)
        {
            var error = ValidateItems(request.Items);
            if (error is not null)
                return BadRequest(new { error });
        }

        var result = await _mediator.Send(new UpdateCraftPlanCommand(userId, id, request.Name, request.Items));
        if (result is null)
            return NotFound(new { error = "Craft plan not found." });

        return Ok(result);
    }

    /// <summary>
    /// Delete a craft plan.
    /// </summary>
    /// <param name="id">The craft plan ID.</param>
    /// <response code="204">Plan deleted successfully.</response>
    /// <response code="401">Not authenticated.</response>
    /// <response code="404">Plan not found or not owned by user.</response>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var deleted = await _mediator.Send(new DeleteCraftPlanCommand(userId, id));
        if (!deleted)
            return NotFound(new { error = "Craft plan not found." });

        return NoContent();
    }

    private static string? ValidateItems(List<CraftPlanItemInput>? items)
    {
        if (items is null || items.Count == 0)
            return "At least one item is required.";

        if (items.Count > 20)
            return "Maximum of 20 items per plan.";

        foreach (var item in items)
        {
            if (item.RecipeId <= 0)
                return $"Invalid recipe ID: {item.RecipeId}.";

            if (item.Quantity is < 1 or > 999)
                return $"Quantity must be between 1 and 999 for recipe {item.RecipeId}.";
        }

        return null;
    }
}
