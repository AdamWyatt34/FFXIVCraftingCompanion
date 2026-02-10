using FFXIV.Application.SavedLists.Commands;
using FFXIV.Application.SavedLists.DTOs;
using FFXIV.Application.SavedLists.Queries;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Saved lists management (shopping lists, gear sets, crafting plans). All endpoints require authentication.
/// </summary>
[Authorize]
[ApiController]
[Route("api/saved-lists")]
[Produces("application/json")]
public class SavedListsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public SavedListsController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Get all saved lists for the current user, optionally filtered by type.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<SavedListSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? type, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetSavedListsQuery(_currentUser.UserId!, type), ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a saved list by ID with full data.
    /// </summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(SavedListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetSavedListByIdQuery(_currentUser.UserId!, id), ct);

        return result is not null
            ? Ok(result)
            : NotFound(new { error = "Saved list not found." });
    }

    /// <summary>
    /// Save a shopping list by resolving a recipe's material tree.
    /// </summary>
    [HttpPost("shopping-list")]
    [ProducesResponseType(typeof(SavedListDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveShoppingList(
        [FromBody] SaveShoppingListRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });

        if (request.Name.Length > 256)
            return BadRequest(new { error = "Name must be 256 characters or fewer." });

        if (request.RecipeId <= 0)
            return BadRequest(new { error = "RecipeId must be a positive integer." });

        if (request.Quantity < 1)
            return BadRequest(new { error = "Quantity must be at least 1." });

        var result = await _mediator.Send(
            new CreateShoppingListCommand(
                _currentUser.UserId!,
                request.Name.Trim(),
                request.RecipeId,
                request.Quantity,
                request.Notes?.Trim()), ct);

        if (result is null)
            return Conflict(new { error = "Saved list quota exceeded (max 50) or recipe not found." });

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Save a gear set with slot-to-equipment mapping and calculated stats.
    /// </summary>
    [HttpPost("gear-set")]
    [ProducesResponseType(typeof(SavedListDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveGearSet(
        [FromBody] SaveGearSetRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });

        if (request.Name.Length > 256)
            return BadRequest(new { error = "Name must be 256 characters or fewer." });

        if (string.IsNullOrWhiteSpace(request.Job))
            return BadRequest(new { error = "Job is required." });

        var normalizedJob = request.Job.ToUpperInvariant();
        if (!JobClassification.IsDoH(normalizedJob) && !JobClassification.IsDoL(normalizedJob))
            return BadRequest(new { error = $"'{request.Job}' is not a valid DoH/DoL job." });

        if (request.Slots.Count == 0)
            return BadRequest(new { error = "At least one equipment slot is required." });

        var result = await _mediator.Send(
            new CreateGearSetCommand(
                _currentUser.UserId!,
                request.Name.Trim(),
                normalizedJob,
                request.Slots), ct);

        if (result is null)
            return Conflict(new { error = "Saved list quota exceeded (max 50)." });

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Save a crafting plan with multiple recipes and combined materials.
    /// </summary>
    [HttpPost("crafting-plan")]
    [ProducesResponseType(typeof(SavedListDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveCraftingPlan(
        [FromBody] SaveCraftingPlanRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { error = "Name is required." });

        if (request.Name.Length > 256)
            return BadRequest(new { error = "Name must be 256 characters or fewer." });

        if (request.Items.Count == 0)
            return BadRequest(new { error = "At least one item is required." });

        if (request.Items.Count > 20)
            return BadRequest(new { error = "Crafting plan can have at most 20 items." });

        foreach (var item in request.Items)
        {
            if (item.RecipeId <= 0)
                return BadRequest(new { error = $"Invalid RecipeId: {item.RecipeId}" });

            if (item.Quantity < 1)
                return BadRequest(new { error = $"Quantity must be at least 1 for recipe {item.RecipeId}." });
        }

        var planItems = request.Items
            .Select(i => new CraftingPlanItem(i.RecipeId, i.Quantity))
            .ToList();

        var result = await _mediator.Send(
            new CreateCraftingPlanCommand(
                _currentUser.UserId!,
                request.Name.Trim(),
                planItems), ct);

        if (result is null)
            return Conflict(new { error = "Saved list quota exceeded (max 50)." });

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>
    /// Update a saved list's name or pinned status.
    /// </summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(SavedListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        int id, [FromBody] UpdateSavedListRequest request, CancellationToken ct)
    {
        if (request.Name is not null && request.Name.Length > 256)
            return BadRequest(new { error = "Name must be 256 characters or fewer." });

        var result = await _mediator.Send(
            new UpdateSavedListCommand(
                _currentUser.UserId!,
                id,
                request.Name?.Trim(),
                request.IsPinned), ct);

        return result is not null
            ? Ok(result)
            : NotFound(new { error = "Saved list not found." });
    }

    /// <summary>
    /// Delete a saved list.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var deleted = await _mediator.Send(
            new DeleteSavedListCommand(_currentUser.UserId!, id), ct);

        return deleted
            ? NoContent()
            : NotFound(new { error = "Saved list not found." });
    }
}
