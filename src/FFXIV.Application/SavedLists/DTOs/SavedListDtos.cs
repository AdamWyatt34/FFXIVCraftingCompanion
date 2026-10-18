using System.ComponentModel.DataAnnotations;

namespace FFXIV.Application.SavedLists.DTOs;

// Response DTOs

public sealed record SavedListDto(
    int Id,
    string Name,
    string Type,
    object Data,
    bool IsPinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record SavedListSummaryDto(
    int Id,
    string Name,
    string Type,
    bool IsPinned,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Bounds shared by the saved-list request DTOs and handlers.</summary>
public static class SavedListLimits
{
    public const int MaxNameLength = 256;
    public const int MaxQuantity = 999;
    public const int MaxPlanItems = 20;

    /// <summary>Distinct materials one list may hold; the list is one JSON column.</summary>
    public const int MaxMaterials = 200;

    /// <summary>Ceiling for a material's quantity after repeated additions.</summary>
    public const int MaxMaterialQuantity = 9_999;
}

// Request DTOs

public sealed class SaveShoppingListRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Name is required.")]
    [MaxLength(SavedListLimits.MaxNameLength, ErrorMessage = "Name must be 256 characters or fewer.")]
    public string Name { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "RecipeId must be a positive integer.")]
    public int RecipeId { get; set; }

    [Range(1, SavedListLimits.MaxQuantity, ErrorMessage = "Quantity must be between 1 and 999.")]
    public int Quantity { get; set; } = 1;

    [MaxLength(2048, ErrorMessage = "Notes must be 2048 characters or fewer.")]
    public string? Notes { get; set; }
}

public sealed class SaveGearSetRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Name is required.")]
    [MaxLength(SavedListLimits.MaxNameLength, ErrorMessage = "Name must be 256 characters or fewer.")]
    public string Name { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Job is required.")]
    [MaxLength(8, ErrorMessage = "Job must be a job abbreviation.")]
    public string Job { get; set; } = string.Empty;

    public Dictionary<string, int> Slots { get; set; } = new();
}

public sealed class SaveCraftingPlanRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Name is required.")]
    [MaxLength(SavedListLimits.MaxNameLength, ErrorMessage = "Name must be 256 characters or fewer.")]
    public string Name { get; set; } = string.Empty;

    [MinLength(1, ErrorMessage = "At least one item is required.")]
    [MaxLength(SavedListLimits.MaxPlanItems, ErrorMessage = "Crafting plan can have at most 20 items.")]
    public List<CraftingPlanItemRequest> Items { get; set; } = [];
}

public sealed class CraftingPlanItemRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "RecipeId must be a positive integer.")]
    public int RecipeId { get; set; }

    [Range(1, SavedListLimits.MaxQuantity, ErrorMessage = "Quantity must be between 1 and 999.")]
    public int Quantity { get; set; } = 1;
}

public sealed class UpdateSavedListRequest
{
    [MaxLength(SavedListLimits.MaxNameLength, ErrorMessage = "Name must be 256 characters or fewer.")]
    public string? Name { get; set; }

    public bool? IsPinned { get; set; }
}
