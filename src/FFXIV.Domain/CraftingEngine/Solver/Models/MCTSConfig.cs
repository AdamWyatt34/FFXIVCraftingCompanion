namespace FFXIV.Domain.CraftingEngine.Solver.Models;

public sealed record MCTSConfig
{
    // === Tree policy ===
    public double ExplorationConstant { get; init; } = 1.41;

    // === Rollout policy ===
    public double RolloutTemperature { get; init; } = 0.5;

    // === Expansion policy ===
    public double HeuristicExpansionProbability { get; init; } = 0.7;

    // === Reward function ===
    public double FailedReward { get; init; } = 0.0;
    public double InProgressProgressWeight { get; init; } = 0.1;
    public double CompletedBaseReward { get; init; } = 0.2;
    public double CompletedQualityWeight { get; init; } = 0.8;
    public double StepPenaltyPerStep { get; init; } = 0.001;

    // === Rollout emergency thresholds ===
    public double EmergencyCPRatio { get; init; } = 0.15;
    public int EmergencyDurabilityThreshold { get; init; } = 15;

    // === Rollout scoring penalties ===
    public double RolloutCPPenalty { get; init; } = 0.05;
    public double RolloutDurabilityPenalty { get; init; } = 0.03;

    // === Early termination ===
    public int EarlyTerminationThreshold { get; init; } = 5000;
    public double PerfectRewardThreshold { get; init; } = 0.999;

    public static MCTSConfig Default => new();
}
