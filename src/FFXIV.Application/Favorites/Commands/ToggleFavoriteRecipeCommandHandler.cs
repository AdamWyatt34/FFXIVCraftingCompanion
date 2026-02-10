using FFXIV.Application.Favorites.DTOs;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Favorites.Commands;

public sealed class ToggleFavoriteRecipeCommandHandler
    : IRequestHandler<ToggleFavoriteRecipeCommand, FavoriteStatusDto?>
{
    private readonly IFavoriteRecipeRepository _favoriteRepo;

    public ToggleFavoriteRecipeCommandHandler(IFavoriteRecipeRepository favoriteRepo)
    {
        _favoriteRepo = favoriteRepo;
    }

    public async Task<FavoriteStatusDto?> Handle(
        ToggleFavoriteRecipeCommand request, CancellationToken cancellationToken)
    {
        var isFavorited = await _favoriteRepo.IsFavoritedAsync(
            request.UserId, request.RecipeId, cancellationToken);

        if (isFavorited)
        {
            await _favoriteRepo.RemoveAsync(request.UserId, request.RecipeId, cancellationToken);
            return new FavoriteStatusDto(IsFavorited: false);
        }

        var count = await _favoriteRepo.GetCountByUserAsync(request.UserId, cancellationToken);
        if (count >= 100)
            return null; // quota exceeded

        var favorite = new FavoriteRecipe
        {
            UserId = request.UserId,
            RecipeId = request.RecipeId,
            Note = request.Note,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _favoriteRepo.AddAsync(favorite, cancellationToken);
        return new FavoriteStatusDto(IsFavorited: true);
    }
}
