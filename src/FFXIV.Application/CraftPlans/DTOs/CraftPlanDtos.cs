using FFXIV.Application.Recipes.DTOs;

namespace FFXIV.Application.CraftPlans.DTOs;

// --- Request DTOs ---

public sealed class ResolveCraftPlanRequest
{
    public List<CraftPlanItemInput> Items { get; set; } = [];
}

public sealed class CreateCraftPlanRequest
{
    public string Name { get; set; } = string.Empty;
    public List<CraftPlanItemInput> Items { get; set; } = [];
}

public sealed class UpdateCraftPlanRequest
{
    public string? Name { get; set; }
    public List<CraftPlanItemInput>? Items { get; set; }
}

public sealed class ExportCraftPlanRequest
{
    public List<CraftPlanItemInput> Items { get; set; } = [];
    public string Format { get; set; } = "text";
}

public sealed class CraftPlanItemInput
{
    public int RecipeId { get; set; }
    public int Quantity { get; set; } = 1;
}

// --- Response DTOs ---

public sealed record CraftPlanDto(
    string Id,
    string Name,
    List<CraftPlanItemInput> Items,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record CraftPlanResultDto(
    List<CraftPlanItemResultDto> Items,
    List<AggregatedMaterialDto> RawMaterials,
    List<AggregatedMaterialDto> Crystals,
    List<AggregatedIntermediateCraftDto> IntermediateCrafts,
    CraftPlanSummaryDto Summary);

public sealed record CraftPlanItemResultDto(
    int RecipeId,
    int ItemId,
    string ItemName,
    string IconUrl,
    int Quantity,
    string CraftType);

public sealed record AggregatedMaterialDto(
    int ItemId,
    string ItemName,
    string IconUrl,
    int TotalQuantity,
    string PrimarySource,
    List<ItemSourceDto> Sources,
    string Category,
    List<MaterialContributionDto> Contributions);

public sealed record AggregatedIntermediateCraftDto(
    int ItemId,
    string ItemName,
    string IconUrl,
    int TotalQuantity,
    int RecipeId,
    string CraftType,
    string CraftTypeName,
    int RecipeLevel,
    int YieldPerCraft,
    int CraftOrder);

public sealed record CraftPlanSummaryDto(
    int TotalPlanItems,
    int TotalUniqueRawMaterials,
    int TotalUniqueCrystals,
    int TotalIntermediateCrafts,
    int MaxTreeDepth);

public sealed record MaterialContributionDto(
    int RecipeId,
    string ItemName,
    int Quantity);

public sealed record ExportResultDto(
    string Format,
    string Content);
