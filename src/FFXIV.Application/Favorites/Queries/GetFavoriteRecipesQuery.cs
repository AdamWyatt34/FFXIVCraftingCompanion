using FFXIV.Application.Favorites.DTOs;
using MediatR;

namespace FFXIV.Application.Favorites.Queries;

public sealed record GetFavoriteRecipesQuery(string UserId) : IRequest<IReadOnlyList<FavoriteRecipeDto>>;
