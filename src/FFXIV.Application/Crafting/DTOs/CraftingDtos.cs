namespace FFXIV.Application.Crafting.DTOs;

// ── Request DTOs ──

public sealed record SimulateRequest
{
    public int RecipeId { get; init; }
    public CrafterStatsInput? Stats { get; init; }
    public bool UseProfileStats { get; init; } = false;
    public List<int> ActionIds { get; init; } = new();
    public List<string>? ActionNames { get; init; }
    public SimulationOptionsInput? Options { get; init; }
}

public sealed record CrafterStatsInput
{
    public int Level { get; init; }
    public int Craftsmanship { get; init; }
    public int Control { get; init; }
    public int CP { get; init; }
    public bool IsSpecialist { get; init; } = false;
}

public sealed record SimulationOptionsInput
{
    public bool AssumeAllSuccess { get; init; } = true;
    public bool AssumeNormalCondition { get; init; } = true;
    public int? RandomSeed { get; init; }
    public string? ForceCondition { get; init; }
}

public sealed record ExecuteActionRequest
{
    public CraftingStateDto State { get; init; } = null!;
    public int ActionId { get; init; }
    public CrafterStatsInput Stats { get; init; } = null!;
    public int RecipeId { get; init; }
}

public sealed record AvailableActionsRequest
{
    public CraftingStateDto State { get; init; } = null!;
    public CrafterStatsInput Stats { get; init; } = null!;
    public int RecipeId { get; init; }
}

public sealed record ExportMacroRequest
{
    public List<int> ActionIds { get; init; } = new();
    public MacroOptionsInput? Options { get; init; }
}

public sealed record MacroOptionsInput
{
    public int LinesPerMacro { get; init; } = 14;
    public int SoundEffect { get; init; } = 1;
    public int CompleteSoundEffect { get; init; } = 2;
    public bool IncludeEcho { get; init; } = true;
}

// ── Response DTOs ──

public sealed record SimulationResultDto
{
    public bool Success { get; init; }
    public int TotalSteps { get; init; }
    public double FinalQualityPercent { get; init; }
    public int FinalHQPercent { get; init; }
    public CraftingStateDto FinalState { get; init; } = null!;
    public List<StepResultDto> Steps { get; init; } = new();
    public List<string> Macros { get; init; } = new();
    public Recipes.DTOs.RecipeSummaryDto Recipe { get; init; } = null!;
}

public sealed record CraftingStateDto
{
    public int Progress { get; init; }
    public int MaxProgress { get; init; }
    public double ProgressPercent { get; init; }
    public int Quality { get; init; }
    public int MaxQuality { get; init; }
    public double QualityPercent { get; init; }
    public int Durability { get; init; }
    public int MaxDurability { get; init; }
    public int CP { get; init; }
    public int MaxCP { get; init; }
    public int Step { get; init; }
    public string Condition { get; init; } = "Normal";
    public string Status { get; init; } = "InProgress";
    public int HQPercent { get; init; }
    public List<ActiveBuffDto> ActiveBuffs { get; init; } = new();
    public int InnerQuietStacks { get; init; }

    // Hidden state for DTO→Domain roundtrip (step-by-step mode)
    public int ComboAction { get; init; }
    public bool ObserveActive { get; init; }
    public bool IsFirstStep { get; init; }
    public bool QuickInnovationUsed { get; init; }
    public bool HeartAndSoulUsed { get; init; }
    public bool TrainedPerfectionUsed { get; init; }
    public int CarefulObservationCount { get; init; }
}

public sealed record ActiveBuffDto
{
    public string Name { get; init; } = string.Empty;
    public int RemainingTurns { get; init; }
    public int? Stacks { get; init; }
}

public sealed record StepResultDto
{
    public int StepNumber { get; init; }
    public string ActionName { get; init; } = string.Empty;
    public int ActionId { get; init; }
    public string ActionCategory { get; init; } = string.Empty;
    public bool Succeeded { get; init; }
    public string? FailReason { get; init; }
    public StepDeltaDto Deltas { get; init; } = null!;
    public CraftingStateDto StateAfter { get; init; } = null!;
}

public sealed record StepDeltaDto
{
    public int Progress { get; init; }
    public int Quality { get; init; }
    public int Durability { get; init; }
    public int CP { get; init; }
}

public sealed record ActionInfoDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string InGameName { get; init; } = string.Empty;
    public int LevelRequirement { get; init; }
    public string Category { get; init; } = string.Empty;
    public int BaseCPCost { get; init; }
    public int BaseDurabilityCost { get; init; }
    public int BaseSuccessRate { get; init; }
}

public sealed record ActionAvailabilityDto
{
    public int ActionId { get; init; }
    public string ActionName { get; init; } = string.Empty;
    public bool IsAvailable { get; init; }
    public string? UnavailableReason { get; init; }
    public int CPCost { get; init; }
    public int DurabilityCost { get; init; }
    public int SuccessRate { get; init; }
    public string Category { get; init; } = string.Empty;
}

public sealed record ExportMacroResultDto
{
    public List<string> Macros { get; init; } = new();
    public int TotalActions { get; init; }
    public int MacroCount { get; init; }
}
