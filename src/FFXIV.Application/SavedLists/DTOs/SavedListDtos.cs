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

// Request DTOs

public sealed class SaveShoppingListRequest
{
    public string Name { get; set; } = string.Empty;
    public int RecipeId { get; set; }
    public int Quantity { get; set; } = 1;
    public string? Notes { get; set; }
}

public sealed class SaveGearSetRequest
{
    public string Name { get; set; } = string.Empty;
    public string Job { get; set; } = string.Empty;
    public Dictionary<string, int> Slots { get; set; } = new();
}

public sealed class SaveCraftingPlanRequest
{
    public string Name { get; set; } = string.Empty;
    public List<CraftingPlanItemRequest> Items { get; set; } = [];
}

public sealed class CraftingPlanItemRequest
{
    public int RecipeId { get; set; }
    public int Quantity { get; set; } = 1;
}

public sealed class UpdateSavedListRequest
{
    public string? Name { get; set; }
    public bool? IsPinned { get; set; }
}
