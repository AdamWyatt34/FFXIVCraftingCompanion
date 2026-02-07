using FFXIV.Application.DTOs;
using FFXIV.Application.Recipes.Queries;
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
    /// <response code="200">Returns the recipe with ingredients.</response>
    /// <response code="404">Recipe not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(RecipeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetRecipeByIdQuery(id));
        if (result is null)
            return NotFound(new { error = $"Recipe {id} not found." });

        return Ok(result);
    }
}
