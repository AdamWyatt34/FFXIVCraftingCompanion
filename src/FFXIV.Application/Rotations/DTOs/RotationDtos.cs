using System.ComponentModel.DataAnnotations;

namespace FFXIV.Application.Rotations.DTOs;

// ── Request DTOs ──

public sealed class SubmitRotationRequest
{
    public const int MaxActions = 50;

    [Range(1, int.MaxValue, ErrorMessage = "RecipeId must be a positive integer.")]
    public int RecipeId { get; set; }

    [MinLength(1, ErrorMessage = "ActionIds must not be empty.")]
    [MaxLength(MaxActions, ErrorMessage = "ActionIds must have at most 50 actions.")]
    public List<int> ActionIds { get; set; } = [];

    [Required(ErrorMessage = "Stats are required.")]
    public CrafterStatsInput Stats { get; set; } = null!;

    [MaxLength(32, ErrorMessage = "Source must be 32 characters or fewer.")]
    public string Source { get; set; } = "User";

    [MaxLength(256, ErrorMessage = "Name must be 256 characters or fewer.")]
    public string? Name { get; set; }

    [MaxLength(2048, ErrorMessage = "Notes must be 2048 characters or fewer.")]
    public string? Notes { get; set; }

    /// <summary>Mirrors the 64-character SharedRotations.Algorithm column.</summary>
    [MaxLength(64, ErrorMessage = "Algorithm must be 64 characters or fewer.")]
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
    [Range(1, 100, ErrorMessage = "Level must be between 1 and 100.")]
    public int Level { get; init; }

    [Range(0, 99_999, ErrorMessage = "Craftsmanship must be between 0 and 99999.")]
    public int Craftsmanship { get; init; }

    [Range(0, 99_999, ErrorMessage = "Control must be between 0 and 99999.")]
    public int Control { get; init; }

    [Range(0, 99_999, ErrorMessage = "CP must be between 0 and 99999.")]
    public int CP { get; init; }

    public bool IsSpecialist { get; init; } = false;
}
