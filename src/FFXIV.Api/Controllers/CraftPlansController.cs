using FFXIV.Application.CraftPlans.Commands;
using FFXIV.Application.CraftPlans.DTOs;
using FFXIV.Application.CraftPlans.Queries;
using FFXIV.Application.Gathering;
using FFXIV.Application.Gathering.DTOs;
using FFXIV.Application.Market.DTOs;
using FFXIV.Application.Market.Queries;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Provides endpoints for creating and resolving multi-item craft plans.
/// </summary>
[ApiController]
[Route("api/craft-plans")]
[Produces("application/json")]
public class CraftPlansController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IGatheringGuideService _gatheringGuide;

    public CraftPlansController(IMediator mediator, ICurrentUserService currentUser, IGatheringGuideService gatheringGuide)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _gatheringGuide = gatheringGuide;
    }

    /// <summary>
    /// Resolve a craft plan into aggregated materials without saving.
    /// </summary>
    /// <param name="request">The list of recipe IDs and quantities to resolve.</param>
    /// <param name="includeGathering">When true, includes a gathering guide with zone grouping and timed nodes.</param>
    /// <response code="200">Returns the resolved craft plan with aggregated materials.</response>
    /// <response code="400">Invalid request.</response>
    /// <response code="404">One or more recipes not found.</response>
    [HttpPost("resolve")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CraftPlanResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Resolve([FromBody] ResolveCraftPlanRequest request, [FromQuery] bool includeGathering = false)
    {
        var error = ValidateItems(request.Items);
        if (error is not null)
            return BadRequest(new { error });

        var result = await _mediator.Send(new ResolveCraftPlanQuery(request.Items));
        if (result is null)
            return NotFound(new { error = "One or more recipes not found." });

        if (!includeGathering)
            return Ok(result);

        var guide = await GenerateGatheringGuideFromPlan(result);
        return Ok(new CraftPlanWithGatheringDto(result, guide));
    }

    /// <summary>
    /// Export a craft plan as text, JSON, or Teamcraft format.
    /// </summary>
    /// <param name="request">The items to export and desired format.</param>
    /// <response code="200">Returns the exported content.</response>
    /// <response code="400">Invalid request or format.</response>
    /// <response code="404">One or more recipes not found.</response>
    [HttpPost("export")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ExportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Export([FromBody] ExportCraftPlanRequest request)
    {
        var error = ValidateItems(request.Items);
        if (error is not null)
            return BadRequest(new { error });

        var validFormats = new[] { "text", "json", "teamcraft" };
        if (!validFormats.Contains(request.Format.ToLowerInvariant()))
            return BadRequest(new { error = "Format must be one of: text, json, teamcraft." });

        var result = await _mediator.Send(new ExportCraftPlanQuery(request.Items, request.Format));
        if (result is null)
            return NotFound(new { error = "One or more recipes not found." });

        return Ok(result);
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
    /// Get a resolved craft plan by ID (public, for shareable links).
    /// </summary>
    /// <param name="id">The craft plan ID.</param>
    /// <param name="includeGathering">When true, includes a gathering guide with zone grouping and timed nodes.</param>
    /// <response code="200">Returns the resolved craft plan with aggregated materials.</response>
    /// <response code="404">Plan not found.</response>
    [HttpGet("{id}/resolved")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CraftPlanResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResolved(string id, [FromQuery] bool includeGathering = false)
    {
        var result = await _mediator.Send(new GetCraftPlanResolvedQuery(id));
        if (result is null)
            return NotFound(new { error = "Craft plan not found." });

        if (!includeGathering)
            return Ok(result);

        var guide = await GenerateGatheringGuideFromPlan(result);
        return Ok(new CraftPlanWithGatheringDto(result, guide));
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

    /// <summary>
    /// Get a cost estimate for all materials in a saved craft plan.
    /// </summary>
    /// <param name="id">The craft plan ID.</param>
    /// <param name="world">The world or data center name (e.g., "Adamantoise" or "Aether").</param>
    /// <param name="options">Cost estimation options.</param>
    /// <response code="200">Returns the cost estimate.</response>
    /// <response code="400">Invalid world/DC name.</response>
    /// <response code="404">Plan not found.</response>
    [HttpPost("{id}/cost")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CostEstimateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCostEstimate(
        string id,
        [FromQuery] string world,
        [FromBody] CostEstimationOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(world) || !FfxivServers.IsValidWorldOrDc(world))
            return BadRequest(new { error = "Invalid or missing world/data center name." });

        options ??= new CostEstimationOptions();

        var result = await _mediator.Send(new GetCraftPlanCostQuery(id, world, options));
        if (result is null)
            return NotFound(new { error = "Craft plan not found." });

        return Ok(result);
    }

    private async Task<GatheringGuideDto> GenerateGatheringGuideFromPlan(CraftPlanResultDto plan)
    {
        var materials = new List<GatheredMaterialInput>();

        foreach (var mat in plan.RawMaterials)
        {
            if (mat.PrimarySource == nameof(ItemSourceType.Gathered))
                materials.Add(new GatheredMaterialInput(mat.ItemId, mat.TotalQuantity));
        }

        foreach (var crystal in plan.Crystals)
            materials.Add(new GatheredMaterialInput(crystal.ItemId, crystal.TotalQuantity));

        var guide = await _gatheringGuide.GenerateGuideAsync(materials);
        return GatheringGuideMapper.ToDto(guide);
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
