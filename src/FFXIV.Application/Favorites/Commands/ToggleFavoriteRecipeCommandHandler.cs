using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Favorites.Commands;

public sealed class ToggleFavoriteRecipeCommandHandler
    : IRequestHandler<ToggleFavoriteRecipeCommand, ToggleFavoriteResult>
{
    public const int MaxFavorites = 100;

    private readonly IFavoriteRecipeRepository _favoriteRepo;
    private readonly IRecipeRepository _recipeRepo;

    public ToggleFavoriteRecipeCommandHandler(
        IFavoriteRecipeRepository favoriteRepo,
        IRecipeRepository recipeRepo)
    {
        _favoriteRepo = favoriteRepo;
        _recipeRepo = recipeRepo;
    }

    public async Task<ToggleFavoriteResult> Handle(
        ToggleFavoriteRecipeCommand request, CancellationToken cancellationToken)
    {
        var isFavorited = await _favoriteRepo.IsFavoritedAsync(
            request.UserId, request.RecipeId, cancellationToken);

        if (isFavorited)
        {
            await _favoriteRepo.RemoveAsync(request.UserId, request.RecipeId, cancellationToken);
            return ToggleFavoriteResult.Toggled(isFavorited: false);
        }

        // The foreign key to Recipes is Restrict, so an unknown id used to surface as a 500.
        if (!await _recipeRepo.ExistsAsync(request.RecipeId, cancellationToken))
            return ToggleFavoriteResult.RecipeNotFound;

        var count = await _favoriteRepo.GetCountByUserAsync(request.UserId, cancellationToken);
        if (count >= MaxFavorites)
            return ToggleFavoriteResult.QuotaExceeded;

        // A concurrent duplicate makes AddAsync return false; the favourite is on record either way.
        await _favoriteRepo.AddAsync(new FavoriteRecipe
        {
            UserId = request.UserId,
            RecipeId = request.RecipeId,
            Note = request.Note,
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        return ToggleFavoriteResult.Toggled(isFavorited: true);
    }
}
