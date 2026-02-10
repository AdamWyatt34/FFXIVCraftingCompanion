using FFXIV.Application.Favorites.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Favorites.Queries;

public sealed class GetFavoriteRecipesQueryHandler
    : IRequestHandler<GetFavoriteRecipesQuery, IReadOnlyList<FavoriteRecipeDto>>
{
    private readonly IFavoriteRecipeRepository _favoriteRepo;

    public GetFavoriteRecipesQueryHandler(IFavoriteRecipeRepository favoriteRepo)
    {
        _favoriteRepo = favoriteRepo;
    }

    public async Task<IReadOnlyList<FavoriteRecipeDto>> Handle(
        GetFavoriteRecipesQuery request, CancellationToken cancellationToken)
    {
        var favorites = await _favoriteRepo.GetByUserAsync(request.UserId, cancellationToken);
        return favorites.Select(FavoriteMapper.ToDto).ToList();
    }
}
