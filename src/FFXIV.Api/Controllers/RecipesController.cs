using FFXIV.Application.DTOs;
using FFXIV.Application.Recipes.DTOs;
using FFXIV.Application.Recipes.Queries;
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

    public RecipesController(IMediator mediator)
    {
        _mediator = mediator;
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
    /// Get the full material tree for a recipe, including shopping list and crafting order.
    /// </summary>
    /// <param name="id">The recipe ID.</param>
    /// <param name="quantity">How many of the item to craft (1-999, default: 1).</param>
    /// <response code="200">Returns the full material tree and shopping list.</response>
    /// <response code="400">Invalid quantity.</response>
    /// <response code="404">Recipe not found.</response>
    [HttpGet("{id:int}/materials")]
    [ProducesResponseType(typeof(MaterialTreeResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMaterialTree(int id, [FromQuery] int quantity = 1)
    {
        if (quantity is < 1 or > 999)
            return BadRequest(new { error = "Quantity must be between 1 and 999." });

        var result = await _mediator.Send(new GetMaterialTreeQuery(id, quantity));
        if (result is null)
            return NotFound(new { error = $"Recipe {id} not found." });

        return Ok(result);
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
}
