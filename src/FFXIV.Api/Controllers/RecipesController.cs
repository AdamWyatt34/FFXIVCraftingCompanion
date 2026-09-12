using FFXIV.Application.DTOs;
using FFXIV.Application.Gathering;
using FFXIV.Application.Gathering.DTOs;
using FFXIV.Application.Journey.DTOs;
using FFXIV.Application.Journey.Queries;
using FFXIV.Application.Market.DTOs;
using FFXIV.Application.Market.Queries;
using FFXIV.Application.Recipes.DTOs;
using FFXIV.Application.Recipes.Queries;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Provides endpoints for retrieving FFXIV crafting recipes.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class RecipesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IGatheringGuideService _gatheringGuide;

    public RecipesController(IMediator mediator, ICurrentUserService currentUser, IGatheringGuideService gatheringGuide)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _gatheringGuide = gatheringGuide;
    }

    /// <summary>
    /// Get a recipe by its ID, including ingredients.
    /// </summary>
    /// <param name="id">The recipe ID.</param>
    /// <param name="includeMaterials">When true, includes a lightweight material summary (counts only).</param>
    /// <response code="200">Returns the recipe with ingredients.</response>
    /// <response code="404">Recipe not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RecipeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, [FromQuery] bool includeMaterials = false)
    {
        var result = await _mediator.Send(new GetRecipeByIdQuery(id, includeMaterials));
        if (result is null)
            return NotFound(new { error = $"Recipe {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Get the crafting engine parameters for a recipe: the recipe row plus its RecipeLevelTable entry.
    /// A client-side engine runs the simulator and solver from these.
    /// </summary>
    /// <param name="id">The recipe ID.</param>
    /// <response code="200">Returns the crafting parameters.</response>
    /// <response code="404">Recipe or its recipe level not found.</response>
    [HttpGet("{id:int}/crafting-parameters")]
    [ProducesResponseType(typeof(RecipeCraftingParametersDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCraftingParameters(int id)
    {
        var result = await _mediator.Send(new GetRecipeCraftingParametersQuery(id));
        if (result is null)
            return NotFound(new { error = $"Recipe {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Get the full material tree for a recipe, including shopping list and crafting order.
    /// </summary>
    /// <param name="id">The recipe ID.</param>
    /// <param name="quantity">How many of the item to craft (1-999, default: 1).</param>
    /// <param name="includeGathering">When true, includes a gathering guide with zone grouping and timed nodes.</param>
    /// <response code="200">Returns the full material tree and shopping list.</response>
    /// <response code="400">Invalid quantity.</response>
    /// <response code="404">Recipe not found.</response>
    [HttpGet("{id:int}/materials")]
    [ProducesResponseType(typeof(MaterialTreeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMaterialTree(int id, [FromQuery] int quantity = 1, [FromQuery] bool includeGathering = false)
    {
        if (quantity is < 1 or > 999)
            return BadRequest(new { error = "Quantity must be between 1 and 999." });

        var result = await _mediator.Send(new GetMaterialTreeQuery(id, quantity));
        if (result is null)
            return NotFound(new { error = $"Recipe {id} not found." });

        if (!includeGathering)
            return Ok(result);

        var materials = ExtractGatheredMaterials(result.ShoppingList, result.CrystalRequirements);
        var guide = await _gatheringGuide.GenerateGuideAsync(materials);
        return Ok(new MaterialTreeWithGatheringDto(result, GatheringGuideMapper.ToDto(guide)));
    }

    private static List<GatheredMaterialInput> ExtractGatheredMaterials(
        List<ShoppingListItemDto> shoppingList,
        List<ShoppingListItemDto> crystals)
    {
        var materials = new List<GatheredMaterialInput>();

        foreach (var item in shoppingList)
        {
            if (item.PrimarySource == nameof(ItemSourceType.Gathered))
                materials.Add(new GatheredMaterialInput(item.ItemId, item.TotalQuantity));
        }

        foreach (var crystal in crystals)
            materials.Add(new GatheredMaterialInput(crystal.ItemId, crystal.TotalQuantity));

        return materials;
    }

    /// <summary>
    /// Get just the shopping list for a recipe (raw materials, crystals, and intermediate crafts).
    /// </summary>
    /// <param name="id">The recipe ID.</param>
    /// <param name="quantity">How many of the item to craft (1-999, default: 1).</param>
    /// <response code="200">Returns the shopping list.</response>
    /// <response code="400">Invalid quantity.</response>
    /// <response code="404">Recipe not found.</response>
    [HttpGet("{id:int}/shoppinglist")]
    [ProducesResponseType(typeof(ShoppingListResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetShoppingList(int id, [FromQuery] int quantity = 1)
    {
        if (quantity is < 1 or > 999)
            return BadRequest(new { error = "Quantity must be between 1 and 999." });

        var result = await _mediator.Send(new GetShoppingListQuery(id, quantity));
        if (result is null)
            return NotFound(new { error = $"Recipe {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Resolve the full material tree for an item, including shopping list.
    /// </summary>
    /// <param name="itemId">The result item ID to resolve the tree for.</param>
    /// <param name="quantity">How many of the item to craft (default: 1).</param>
    /// <response code="200">Returns the material tree and shopping list.</response>
    /// <response code="400">Invalid quantity.</response>
    [HttpGet("tree/{itemId:int}")]
    [ProducesResponseType(typeof(MaterialTreeResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetRecipeTree(int itemId, [FromQuery] int quantity = 1)
    {
        if (quantity is < 1 or > 999)
            return BadRequest(new { error = "Quantity must be between 1 and 999." });

        var result = await _mediator.Send(new GetRecipeTreeQuery(itemId, quantity));
        return Ok(result);
    }

    /// <summary>
    /// Get a cost estimate for crafting a recipe, with market board and vendor prices.
    /// </summary>
    /// <param name="id">The recipe ID.</param>
    /// <param name="world">The world or data center name (e.g., "Adamantoise" or "Aether").</param>
    /// <param name="quantity">How many of the item to craft (1-999, default: 1).</param>
    /// <param name="useMinPrice">Use minimum listing price instead of average (default: false).</param>
    /// <param name="includeCraftVsBuy">Include craft vs. buy analysis (default: true).</param>
    /// <param name="includeGatheredPrices">Include MB prices for gathered items (default: false).</param>
    /// <response code="200">Returns the cost estimate.</response>
    /// <response code="400">Invalid parameters.</response>
    /// <response code="404">Recipe not found.</response>
    [HttpGet("{id:int}/cost")]
    [ProducesResponseType(typeof(CostEstimateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCostEstimate(
        int id,
        [FromQuery] string world,
        [FromQuery] int quantity = 1,
        [FromQuery] bool useMinPrice = false,
        [FromQuery] bool includeCraftVsBuy = true,
        [FromQuery] bool includeGatheredPrices = true)
    {
        if (string.IsNullOrWhiteSpace(world) || !FfxivServers.IsValidWorldOrDc(world))
            return BadRequest(new { error = "Invalid or missing world/data center name." });

        if (quantity is < 1 or > 999)
            return BadRequest(new { error = "Quantity must be between 1 and 999." });

        var options = new CostEstimationOptions
        {
            UseMinPrice = useMinPrice,
            IncludeCraftVsBuyAnalysis = includeCraftVsBuy,
            IncludeGatheredItemPrices = includeGatheredPrices,
        };

        var result = await _mediator.Send(new GetRecipeCostQuery(id, quantity, world, options));
        if (result is null)
            return NotFound(new { error = $"Recipe {id} not found." });

        return Ok(result);
    }

    /// <summary>
    /// Analyze stat sufficiency for a recipe given crafter stats or profile.
    /// </summary>
    /// <param name="id">The recipe ID.</param>
    /// <param name="request">Stats to analyze, or set useProfileStats to true.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Returns sufficiency analysis.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="401">Authentication required for profile stats.</response>
    /// <response code="404">Recipe or profile not found.</response>
    [HttpPost("{id:int}/sufficiency")]
    [ProducesResponseType(typeof(StatSufficiencyResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AnalyzeSufficiency(
        int id, [FromBody] SufficiencyRequestDto request, CancellationToken ct = default)
    {
        var userId = _currentUser.IsAuthenticated ? _currentUser.UserId : null;
        var (result, error, statusCode) = await _mediator.Send(
            new AnalyzeSufficiencyQuery(id, request, userId), ct);

        if (result is not null)
            return Ok(result);

        return statusCode switch
        {
            401 => Unauthorized(new { error }),
            404 => NotFound(new { error }),
            _ => BadRequest(new { error }),
        };
    }
}
