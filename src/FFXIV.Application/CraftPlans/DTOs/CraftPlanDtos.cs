using System.ComponentModel.DataAnnotations;
using FFXIV.Application.Recipes.DTOs;

namespace FFXIV.Application.CraftPlans.DTOs;

// --- Request DTOs ---

public sealed class ResolveCraftPlanRequest
{
    public List<CraftPlanItemInput> Items { get; set; } = [];
}

public sealed class CreateCraftPlanRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Name is required and must be 256 characters or fewer.")]
    [MaxLength(CraftPlanLimits.MaxNameLength, ErrorMessage = "Name is required and must be 256 characters or fewer.")]
    public string Name { get; set; } = string.Empty;

    [MinLength(1, ErrorMessage = "At least one item is required.")]
    [MaxLength(CraftPlanLimits.MaxItems, ErrorMessage = "Maximum of 20 items per plan.")]
    public List<CraftPlanItemInput> Items { get; set; } = [];
}

public sealed class UpdateCraftPlanRequest
{
    [MaxLength(CraftPlanLimits.MaxNameLength, ErrorMessage = "Name must be 256 characters or fewer.")]
    public string? Name { get; set; }

    [MinLength(1, ErrorMessage = "At least one item is required.")]
    [MaxLength(CraftPlanLimits.MaxItems, ErrorMessage = "Maximum of 20 items per plan.")]
    public List<CraftPlanItemInput>? Items { get; set; }
}

/// <summary>Bounds shared by the craft-plan request DTOs.</summary>
public static class CraftPlanLimits
{
    public const int MaxNameLength = 256;
    public const int MaxItems = 20;
    public const int MaxQuantity = 999;
}

public sealed class ExportCraftPlanRequest
{
    public List<CraftPlanItemInput> Items { get; set; } = [];
    public string Format { get; set; } = "text";
}

public sealed class CraftPlanItemInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Recipe ids must be positive integers.")]
    public int RecipeId { get; set; }

    [Range(1, CraftPlanLimits.MaxQuantity, ErrorMessage = "Quantity must be between 1 and 999.")]
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
