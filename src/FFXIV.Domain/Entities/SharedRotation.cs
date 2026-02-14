using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Entities;

public class SharedRotation
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int RecipeId { get; set; }
    public string? Name { get; set; }
    public string? Notes { get; set; }
    public List<int> ActionIds { get; set; } = [];

    // Stats snapshot (what the rotation was created/tested with)
    public int Craftsmanship { get; set; }
    public int Control { get; set; }
    public int CP { get; set; }
    public int CrafterLevel { get; set; }

    // Auto-computed metrics from simulation
    public decimal QualityPercent { get; set; }
    public int HQPercent { get; set; }
    public int TotalSteps { get; set; }
    public bool CraftCompleted { get; set; }

    public RotationSource Source { get; set; }
    public string? Algorithm { get; set; }
    public int Upvotes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Recipe Recipe { get; set; } = null!;
}
