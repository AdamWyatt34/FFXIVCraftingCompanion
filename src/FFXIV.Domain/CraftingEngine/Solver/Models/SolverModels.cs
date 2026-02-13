using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine.Solver.Models;

public enum SolverAlgorithm
{
    Auto,
    Greedy,
    MCTS
}

public enum SolverStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    TimedOut
}

public sealed record SolveRequest
{
    public int RecipeId { get; init; }
    public CrafterStats Stats { get; init; } = null!;
    public SolverConstraints Constraints { get; init; } = new();
    public SolverSettings Settings { get; init; } = new();
}

public sealed record SolverConstraints
{
    public bool MustComplete { get; init; } = true;
    public bool MaximizeQuality { get; init; } = true;
    public bool MinimizeSteps { get; init; }
    public int? TargetQualityPercent { get; init; }
    public bool AllowUnsafeActions { get; init; }
    public int MaxSteps { get; init; } = 50;
}

public sealed record SolverSettings
{
    public SolverAlgorithm Algorithm { get; init; } = SolverAlgorithm.Auto;
    public int TimeBudgetMs { get; init; } = 10000;
    public int? RandomSeed { get; init; }
}

public sealed record SolverResult
{
    public string JobId { get; init; } = string.Empty;
    public SolverStatus Status { get; init; }
    public List<int> ActionIds { get; init; } = [];
    public SimulationResult? Simulation { get; init; }
    public List<string> Macros { get; init; } = [];
    public SolverMetrics Metrics { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

public sealed record SolverMetrics
{
    public SolverAlgorithm AlgorithmUsed { get; init; }
    public long SolveTimeMs { get; init; }
    public int TotalSteps { get; init; }
    public double FinalQualityPercent { get; init; }
    public int FinalHQPercent { get; init; }
    public bool CraftCompleted { get; init; }
    public long NodesExplored { get; init; }
    public long IterationsCompleted { get; init; }
}
