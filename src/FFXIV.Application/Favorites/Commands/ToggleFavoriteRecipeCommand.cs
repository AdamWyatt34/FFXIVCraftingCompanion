using FFXIV.Application.Favorites.DTOs;
using MediatR;

namespace FFXIV.Application.Favorites.Commands;

public sealed record ToggleFavoriteRecipeCommand(
    string UserId,
    int RecipeId,
    string? Note) : IRequest<ToggleFavoriteResult>;

public enum ToggleFavoriteOutcome
{
    Toggled,
    RecipeNotFound,
    QuotaExceeded,
}

public sealed record ToggleFavoriteResult(ToggleFavoriteOutcome Outcome, FavoriteStatusDto? Status)
{
    public static ToggleFavoriteResult Toggled(bool isFavorited) =>
        new(ToggleFavoriteOutcome.Toggled, new FavoriteStatusDto(isFavorited));

    public static readonly ToggleFavoriteResult RecipeNotFound = new(ToggleFavoriteOutcome.RecipeNotFound, null);
    public static readonly ToggleFavoriteResult QuotaExceeded = new(ToggleFavoriteOutcome.QuotaExceeded, null);
}
