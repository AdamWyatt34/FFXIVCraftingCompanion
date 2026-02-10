using FFXIV.Application.Favorites.Commands;
using FFXIV.Application.Favorites.DTOs;
using FFXIV.Application.Favorites.Queries;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Favorite recipes management. All endpoints require authentication.
/// </summary>
[Authorize]
[ApiController]
[Route("api/favorites")]
[Produces("application/json")]
public class FavoritesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly IFavoriteRecipeRepository _favoriteRepo;

    public FavoritesController(
        IMediator mediator,
        ICurrentUserService currentUser,
        IFavoriteRecipeRepository favoriteRepo)
    {
        _mediator = mediator;
        _currentUser = currentUser;
        _favoriteRepo = favoriteRepo;
    }

    /// <summary>
    /// Get all favorited recipes for the current user.
    /// </summary>
    [HttpGet("recipes")]
    [ProducesResponseType(typeof(List<FavoriteRecipeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFavoriteRecipes(CancellationToken ct)
    {
        var result = await _mediator.Send(
            new GetFavoriteRecipesQuery(_currentUser.UserId!), ct);
        return Ok(result);
    }

    /// <summary>
    /// Toggle a recipe as favorite. If already favorited, removes it. Otherwise, adds it.
    /// </summary>
    [HttpPost("recipes/{recipeId:int}")]
    [ProducesResponseType(typeof(FavoriteStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ToggleFavorite(
        int recipeId, [FromBody] ToggleFavoriteRequest? request, CancellationToken ct)
    {
        if (recipeId <= 0)
            return BadRequest(new { error = "RecipeId must be a positive integer." });

        var result = await _mediator.Send(
            new ToggleFavoriteRecipeCommand(
                _currentUser.UserId!,
                recipeId,
                request?.Note?.Trim()), ct);

        if (result is null)
            return Conflict(new { error = "Favorites quota exceeded (max 100)." });

        return Ok(result);
    }

    /// <summary>
    /// Check if a recipe is favorited by the current user.
    /// </summary>
    [HttpGet("recipes/{recipeId:int}/status")]
    [ProducesResponseType(typeof(FavoriteStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFavoriteStatus(int recipeId, CancellationToken ct)
    {
        var isFavorited = await _favoriteRepo.IsFavoritedAsync(
            _currentUser.UserId!, recipeId, ct);
        return Ok(new FavoriteStatusDto(IsFavorited: isFavorited));
    }
}
