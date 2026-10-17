
namespace FFXIV.Application.Rotations.DTOs;

// ── Request DTOs ──

public sealed class SubmitRotationRequest
{
    public int RecipeId { get; set; }
    public List<int> ActionIds { get; set; } = [];
    public CrafterStatsInput Stats { get; set; } = null!;
    public string Source { get; set; } = "User";
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public string? Algorithm { get; set; }
}

// ── Response DTOs ──

public sealed record RotationListItemDto(
    int Id,
    string? Name,
    int RecipeId,
    string RecipeName,
    int RecipeLevel,
    string CraftType,
    List<int> ActionIds,
    decimal QualityPercent,
    int HQPercent,
    int TotalSteps,
    bool CraftCompleted,
    string Source,
    string? AuthorName,
    int Upvotes,
    DateTimeOffset CreatedAt,
    int Craftsmanship,
    int Control,
    int CP,
    int CrafterLevel);

public sealed record RotationDetailDto(
    int Id,
    string? Name,
    string? Notes,
    int RecipeId,
    string RecipeName,
    int RecipeLevel,
    string CraftType,
    List<int> ActionIds,
    List<string> ActionNames,
    decimal QualityPercent,
    int HQPercent,
    int TotalSteps,
    bool CraftCompleted,
    string Source,
    string? Algorithm,
    string? AuthorName,
    int Upvotes,
    DateTimeOffset CreatedAt,
    int Craftsmanship,
    int Control,
    int CP,
    int CrafterLevel,
    List<string> Macros);

public sealed record RotationListResultDto(
    List<RotationListItemDto> Rotations,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record UpvoteResultDto(
    bool Upvoted,
    int Upvotes);

/// <summary>Crafter stats attached to a shared rotation (moved here from the removed crafting API DTOs).</summary>
public sealed record CrafterStatsInput
{
    public int Level { get; init; }
    public int Craftsmanship { get; init; }
    public int Control { get; init; }
    public int CP { get; init; }
    public bool IsSpecialist { get; init; } = false;
}
