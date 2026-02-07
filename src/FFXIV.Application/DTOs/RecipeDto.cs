namespace FFXIV.Application.DTOs;

public sealed record RecipeDto(
    int RecipeId,
    ItemDto ResultItem,
    string CraftType,
    int RecipeLevel,
    int Difficulty,
    int Quality,
    int Durability,
    int RequiredCraftsmanship,
    int RequiredControl,
    List<RecipeIngredientDto> Ingredients);
