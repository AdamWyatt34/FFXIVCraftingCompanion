using FFXIV.Application.Crafting.Commands;
using FFXIV.Application.Crafting.DTOs;
using FFXIV.Application.Crafting.Queries;
using FFXIV.Domain.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Provides endpoints for crafting simulation, action management, and macro export.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CraftingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public CraftingController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Simulate a full crafting rotation and return step-by-step results with macros.
    /// </summary>
    /// <param name="request">Simulation request with recipe, actions, stats, and options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the simulation result.</response>
    /// <response code="400">Invalid input (recipe not found, invalid stats, unknown action).</response>
    [HttpPost("simulate")]
    [ProducesResponseType(typeof(SimulationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Simulate(
        [FromBody] SimulateRequest request, CancellationToken ct = default)
    {
        var userId = _currentUser.IsAuthenticated ? _currentUser.UserId : null;
        var (result, error) = await _mediator.Send(new SimulateCraftCommand(request, userId), ct);

        return result is not null ? Ok(result) : BadRequest(new { error });
    }

    /// <summary>
    /// Execute a single crafting action on a given state (step-by-step mode).
    /// </summary>
    /// <param name="request">Current state, action ID, stats, and recipe ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the step result with updated state.</response>
    /// <response code="400">Invalid input.</response>
    [HttpPost("execute-action")]
    [ProducesResponseType(typeof(StepResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExecuteAction(
        [FromBody] ExecuteActionRequest request, CancellationToken ct = default)
    {
        var (result, error) = await _mediator.Send(new ExecuteSingleActionCommand(request), ct);

        return result is not null ? Ok(result) : BadRequest(new { error });
    }

    /// <summary>
    /// Get all available crafting actions, optionally filtered by crafter level.
    /// </summary>
    /// <param name="level">Optional level filter (1-100). Returns actions available at this level.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the list of actions.</response>
    [HttpGet("actions")]
    [ProducesResponseType(typeof(List<ActionInfoDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActions(
        [FromQuery] int? level, CancellationToken ct = default)
    {
        var result = await _mediator.Send(new GetCraftingActionsQuery(level), ct);
        return Ok(result);
    }

    /// <summary>
    /// Get which actions are available for a given crafting state and why unavailable ones cannot be used.
    /// </summary>
    /// <param name="request">Current state, crafter stats, and recipe ID.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns action availability list.</response>
    /// <response code="400">Invalid input.</response>
    [HttpPost("available-actions")]
    [ProducesResponseType(typeof(List<ActionAvailabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAvailableActions(
        [FromBody] AvailableActionsRequest request, CancellationToken ct = default)
    {
        var (result, error) = await _mediator.Send(new GetAvailableActionsQuery(request), ct);

        return result is not null ? Ok(result) : BadRequest(new { error });
    }

    /// <summary>
    /// Export a sequence of action IDs to FFXIV in-game macro format.
    /// </summary>
    /// <param name="request">Action IDs and optional macro formatting options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the generated macros.</response>
    /// <response code="400">Invalid input.</response>
    [HttpPost("export-macro")]
    [ProducesResponseType(typeof(ExportMacroResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportMacro(
        [FromBody] ExportMacroRequest request, CancellationToken ct = default)
    {
        var (result, error) = await _mediator.Send(new ExportMacroCommand(request), ct);

        return result is not null ? Ok(result) : BadRequest(new { error });
    }

    /// <summary>
    /// Get the initial crafting state for a recipe with the given crafter stats.
    /// </summary>
    /// <param name="recipeId">The recipe ID.</param>
    /// <param name="level">Crafter level (1-100).</param>
    /// <param name="craftsmanship">Craftsmanship stat.</param>
    /// <param name="control">Control stat.</param>
    /// <param name="cp">CP stat.</param>
    /// <param name="isSpecialist">Whether the crafter is a specialist.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns the initial crafting state.</response>
    /// <response code="400">Invalid input.</response>
    [HttpGet("initial-state")]
    [ProducesResponseType(typeof(CraftingStateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetInitialState(
        [FromQuery] int recipeId,
        [FromQuery] int level,
        [FromQuery] int craftsmanship,
        [FromQuery] int control,
        [FromQuery] int cp,
        [FromQuery] bool isSpecialist = false,
        CancellationToken ct = default)
    {
        if (level is < 1 or > 100)
            return BadRequest(new { error = "Level must be between 1 and 100." });

        if (craftsmanship < 0 || control < 0 || cp < 0)
            return BadRequest(new { error = "Stats must be non-negative." });

        var stats = new CrafterStatsInput
        {
            Level = level,
            Craftsmanship = craftsmanship,
            Control = control,
            CP = cp,
            IsSpecialist = isSpecialist,
        };

        var (result, error) = await _mediator.Send(new GetInitialStateQuery(recipeId, stats), ct);

        return result is not null ? Ok(result) : BadRequest(new { error });
    }
}
