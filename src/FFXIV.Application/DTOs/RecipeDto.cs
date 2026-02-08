using FFXIV.Application.Recipes.DTOs;

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
    int Yield,
    List<RecipeIngredientDto> Ingredients,
    MaterialSummaryDto? MaterialSummary = null,
    RecipeEquipmentInfoDto? EquipmentInfo = null);

public sealed record RecipeEquipmentInfoDto(
    int EquipmentId,
    string EquipSlot,
    string ClassJobCategory,
    int Craftsmanship,
    int Control,
    int CP,
    int Gathering,
    int Perception,
    int GP);
