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
    public bool ExpedienceActive { get; init; } = false;
    public bool QuickInnovationUsed { get; init; } = false;
    public bool HeartAndSoulUsed { get; init; } = false;
    public bool TrainedPerfectionUsed { get; init; } = false;
    public int CarefulObservationCount { get; init; } = 0;

    // Tracking
    public bool IsFirstStep { get; init; } = true;
    public int ComboAction { get; init; } = 0;

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

    // The in-game HQ chance per whole percent of quality (0..100), as datamined by the community
    // and used by the Teamcraft simulator; it is not a formula.
    private static readonly int[] HQChanceByQualityPercent =
    [
        1, 1, 1, 1, 1, 2, 2, 2, 2, 3, 3, 3, 3, 4, 4, 4, 4, 5, 5, 5, 5, 6, 6, 6, 6, 7, 7, 7, 7, 8, 8, 8,
        9, 9, 9, 10, 10, 10, 11, 11, 11, 12, 12, 12, 13, 13, 13, 14, 14, 14, 15, 15, 15, 16, 16, 17, 17,
        17, 18, 18, 18, 19, 19, 20, 20, 21, 22, 23, 24, 26, 28, 31, 34, 38, 42, 47, 52, 58, 64, 68, 71,
        74, 76, 78, 80, 81, 82, 83, 84, 85, 86, 87, 88, 89, 90, 91, 92, 94, 96, 98, 100,
    ];

    private static int CalculateHQPercent(double qualityPercent)
    {
        var index = Math.Clamp((int)Math.Floor(qualityPercent), 0, 100);
        return HQChanceByQualityPercent[index];
    }
}
