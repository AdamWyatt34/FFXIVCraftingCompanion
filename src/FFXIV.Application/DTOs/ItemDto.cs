namespace FFXIV.Application.DTOs;

public sealed record ItemDto(
    int ItemId,
    string Name,
    string IconUrl,
    int ItemLevel,
    string Category,
    bool CanBeHQ,
    int? RecipeId = null,
    bool IsEquipment = false);
