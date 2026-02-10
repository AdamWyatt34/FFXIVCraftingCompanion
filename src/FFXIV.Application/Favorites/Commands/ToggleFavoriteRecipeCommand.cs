using FFXIV.Application.Favorites.DTOs;
using MediatR;

namespace FFXIV.Application.Favorites.Commands;

public sealed record ToggleFavoriteRecipeCommand(
    string UserId,
    int RecipeId,
    string? Note) : IRequest<FavoriteStatusDto?>;
