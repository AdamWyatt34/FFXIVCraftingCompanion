namespace FFXIV.Application.DTOs;

public sealed record RecipeIngredientDto(
    int ItemId,
    string ItemName,
    string IconUrl,
    int Quantity,
    bool CanBeHQ);
