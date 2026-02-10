namespace FFXIV.Application.Favorites.DTOs;

public sealed record FavoriteRecipeDto(
    int RecipeId,
    string ItemName,
    string IconUrl,
    string CraftType,
    int RecipeLevel,
    string? Note,
    DateTimeOffset CreatedAt);

public sealed record FavoriteStatusDto(
    bool IsFavorited);

public sealed class ToggleFavoriteRequest
{
    public string? Note { get; set; }
}
