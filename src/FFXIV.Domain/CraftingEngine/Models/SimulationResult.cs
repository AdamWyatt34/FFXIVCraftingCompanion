namespace FFXIV.Domain.CraftingEngine.Models;

public record SimulationResult
{
    public List<StepResult> Steps { get; init; } = new();
    public CraftingState FinalState { get; init; } = null!;
    public bool Success => FinalState.IsComplete;
    public int TotalSteps => Steps.Count;
    public double FinalQualityPercent => FinalState.QualityPercent;
    public int FinalHQPercent => FinalState.HQPercent;
}

public record StepResult
{
    public int StepNumber { get; init; }
    public string ActionName { get; init; } = string.Empty;
    public int ActionId { get; init; }
    public CraftingState StateBefore { get; init; } = null!;
    public CraftingState StateAfter { get; init; } = null!;
    public bool ActionSucceeded { get; init; } = true;
    public string? FailReason { get; init; }

    // Deltas for UI display
    public int ProgressDelta => StateAfter.Progress - StateBefore.Progress;
    public int QualityDelta => StateAfter.Quality - StateBefore.Quality;
    public int DurabilityDelta => StateAfter.Durability - StateBefore.Durability;
    public int CPDelta => StateAfter.CP - StateBefore.CP;
}
