namespace FFXIV.Domain.CraftingEngine.Models;

public record CraftingState
{
    // Progress tracking
    public int Progress { get; init; } = 0;
    public int MaxProgress { get; init; }
    public int Quality { get; init; } = 0;
    public int MaxQuality { get; init; }
    public int Durability { get; init; }
    public int MaxDurability { get; init; }
    public int CP { get; init; }
    public int MaxCP { get; init; }

    // Craft tracking
    public int Step { get; init; } = 0;
    public CraftCondition Condition { get; init; } = CraftCondition.Normal;
    public CraftStatus Status { get; init; } = CraftStatus.InProgress;

    // Buff tracking (each buff stores remaining turns, 0 = inactive)
    public int InnerQuietStacks { get; init; } = 0;
    public int WasteNotTurns { get; init; } = 0;
    public int WasteNotIITurns { get; init; } = 0;
    public int VenerationTurns { get; init; } = 0;
    public int InnovationTurns { get; init; } = 0;
    public int GreatStridesTurns { get; init; } = 0;
    public int ManipulationTurns { get; init; } = 0;
    public int FinalAppraisalTurns { get; init; } = 0;
    public int MuscleMemoryTurns { get; init; } = 0;
    public bool HeartAndSoulActive { get; init; } = false;
    public bool TrainedPerfectionActive { get; init; } = false;
    public bool QuickInnovationUsed { get; init; } = false;
    public bool HeartAndSoulUsed { get; init; } = false;
    public bool TrainedPerfectionUsed { get; init; } = false;
    public int CarefulObservationCount { get; init; } = 0;

    // Tracking
    public bool IsFirstStep { get; init; } = true;
    public int ComboAction { get; init; } = 0;
    public bool ObserveActive { get; init; } = false;

    // Computed properties
    public bool IsComplete => Progress >= MaxProgress;
    public bool IsFailed => Durability <= 0 && !IsComplete;
    public double QualityPercent => MaxQuality > 0 ? Math.Min(100.0, (double)Quality / MaxQuality * 100.0) : 0;
    public double ProgressPercent => MaxProgress > 0 ? Math.Min(100.0, (double)Progress / MaxProgress * 100.0) : 0;

    // HQ probability based on quality percentage (FFXIV's HQ curve)
    public int HQPercent => CalculateHQPercent(QualityPercent);

    public CraftingState WithBuffTick() => this with
    {
        WasteNotTurns = Math.Max(0, WasteNotTurns - 1),
        WasteNotIITurns = Math.Max(0, WasteNotIITurns - 1),
        VenerationTurns = Math.Max(0, VenerationTurns - 1),
        InnovationTurns = Math.Max(0, InnovationTurns - 1),
        GreatStridesTurns = Math.Max(0, GreatStridesTurns - 1),
        ManipulationTurns = Math.Max(0, ManipulationTurns - 1),
        FinalAppraisalTurns = Math.Max(0, FinalAppraisalTurns - 1),
        MuscleMemoryTurns = Math.Max(0, MuscleMemoryTurns - 1),
    };

    public static CraftingState CreateInitial(RecipeParameters recipe, CrafterStats crafter) => new()
    {
        Progress = 0,
        MaxProgress = recipe.Difficulty,
        Quality = recipe.StartingQuality,
        MaxQuality = recipe.Quality,
        Durability = recipe.Durability,
        MaxDurability = recipe.Durability,
        CP = crafter.CP,
        MaxCP = crafter.CP,
        Step = 0,
        Condition = CraftCondition.Normal,
        Status = CraftStatus.InProgress,
        IsFirstStep = true,
    };

    private static int CalculateHQPercent(double qualityPercent)
    {
        if (qualityPercent == 0) return 1;
        if (qualityPercent >= 100) return 100;

        // FFXIV's HQ probability curve (community-documented):
        // Approximate piecewise function:
        // 0-29%: ~1-10% HQ
        // 30-69%: ~11-49% HQ
        // 70-99%: ~50-99% HQ
        // 100%: 100% HQ
        if (qualityPercent <= 30)
            return (int)(qualityPercent * 10.0 / 30.0) + 1;
        if (qualityPercent <= 70)
            return (int)((qualityPercent - 30) * 39.0 / 40.0) + 11;
        return (int)((qualityPercent - 70) * 50.0 / 30.0) + 50;
    }
}
