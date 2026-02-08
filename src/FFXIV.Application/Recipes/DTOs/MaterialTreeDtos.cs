namespace FFXIV.Application.Recipes.DTOs;

public sealed record MaterialTreeNodeDto(
    int ItemId,
    string ItemName,
    string IconUrl,
    int Quantity,
    bool IsRawMaterial,
    string PrimarySource,
    List<ItemSourceDto> Sources,
    RecipeSummaryDto? Recipe,
    List<MaterialTreeNodeDto> Children,
    int Depth);

public sealed record ItemSourceDto(
    string Type,
    string Description,
    string? Zone,
    string? Npc,
    int? Cost,
    string? Currency,
    int? GatheringLevel,
    string? GatheringClass);

public sealed record RecipeSummaryDto(
    int RecipeId,
    string CraftType,
    string CraftTypeName,
    int RecipeLevel,
    int RequiredCraftsmanship,
    int RequiredControl,
    int Yield);

public sealed record ShoppingListItemDto(
    int ItemId,
    string ItemName,
    string IconUrl,
    int TotalQuantity,
    string PrimarySource,
    List<ItemSourceDto> Sources,
    string Category,
    bool AlsoAvailableFromVendor);

public sealed record IntermediateCraftDto(
    int ItemId,
    string ItemName,
    string IconUrl,
    string CraftType,
    string CraftTypeName,
    int RecipeId,
    int Quantity,
    int RecipeLevel,
    int CraftOrder);

public sealed record TreeSummaryDto(
    int TotalUniqueRawMaterials,
    int TotalUniqueCrystals,
    int TotalIntermediateCrafts,
    int MaxTreeDepth,
    int RequestedQuantity);

public sealed record MaterialTreeResultDto(
    MaterialTreeNodeDto Tree,
    List<ShoppingListItemDto> ShoppingList,
    List<IntermediateCraftDto> IntermediateCrafts,
    List<ShoppingListItemDto> CrystalRequirements,
    TreeSummaryDto Summary);

public sealed record ShoppingListResultDto(
    List<ShoppingListItemDto> ShoppingList,
    List<ShoppingListItemDto> CrystalRequirements,
    List<IntermediateCraftDto> IntermediateCrafts,
    TreeSummaryDto Summary);

public sealed record MaterialSummaryDto(
    int RawMaterialCount,
    int IntermediateCraftCount,
    int CrystalTypeCount,
    int TreeDepth);
