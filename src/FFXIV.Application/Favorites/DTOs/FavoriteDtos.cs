using System.ComponentModel.DataAnnotations;

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
    /// <summary>Mirrors the 1024-character FavoriteRecipes.Note column.</summary>
    [MaxLength(1024, ErrorMessage = "Note must be 1024 characters or fewer.")]
    public string? Note { get; set; }
}
