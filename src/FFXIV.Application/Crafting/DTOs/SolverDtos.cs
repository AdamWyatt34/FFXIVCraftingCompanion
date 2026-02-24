namespace FFXIV.Application.Crafting.DTOs;

// ── Request DTOs ──

public sealed record SolveRequestDto
{
    public int RecipeId { get; init; }
    public CrafterStatsInput? Stats { get; init; }
    public bool UseProfileStats { get; init; }
    public SolverConstraintsDto? Constraints { get; init; }
    public SolverSettingsDto? Settings { get; init; }
    public List<HQIngredientInputDto>? HQIngredients { get; init; }
}

public sealed record SolverConstraintsDto
{
    public bool MustComplete { get; init; } = true;
    public bool MaximizeQuality { get; init; } = true;
    public bool MinimizeSteps { get; init; }
    public int? TargetQualityPercent { get; init; }
    public bool AllowUnsafeActions { get; init; }
    public int MaxSteps { get; init; } = 50;
}

public sealed record SolverSettingsDto
{
    public string Algorithm { get; init; } = "Auto";
    public int TimeBudgetMs { get; init; } = 10000;
    public int? RandomSeed { get; init; }
}

// ── Response DTOs ──

public sealed record SolveJobResponseDto
{
    public string JobId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
}

public sealed record SolverJobDto
{
    public string JobId { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public SolverResultDto? Result { get; init; }
    public int ElapsedMs { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record SolverResultDto
{
    public List<int> ActionIds { get; init; } = [];
    public List<string> ActionNames { get; init; } = [];
    public SimulationResultDto? Simulation { get; init; }
    public List<string> Macros { get; init; } = [];
    public SolverMetricsDto Metrics { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

public sealed record SolverMetricsDto
{
    public string AlgorithmUsed { get; init; } = string.Empty;
    public int SolveTimeMs { get; init; }
    public int TotalSteps { get; init; }
    public double FinalQualityPercent { get; init; }
    public int FinalHQPercent { get; init; }
    public bool CraftCompleted { get; init; }
    public int NodesExplored { get; init; }
    public int IterationsCompleted { get; init; }
}
