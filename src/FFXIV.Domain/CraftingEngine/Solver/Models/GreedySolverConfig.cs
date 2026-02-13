namespace FFXIV.Domain.CraftingEngine.Solver.Models;

public sealed record GreedySolverConfig
{
    // === Phase transition thresholds ===
    public int FinishingCPBuffer { get; init; } = 50;
    public int FinishingDurabilityBuffer { get; init; } = 10;

    // === Universal scoring penalties ===
    public double CPPenaltyWeight { get; init; } = 0.1;
    public double DurabilityPenaltyWeight { get; init; } = 0.05;

    // === TricksOfTheTrade ===
    public double TricksOfTheTradeScore { get; init; } = 200;

    // === Opening phase ===
    public double TrainedEyeScore { get; init; } = 150;
    public double ReflectScore { get; init; } = 100;
    public double MuscleMemoryHighScore { get; init; } = 110;
    public double MuscleMemoryLowScore { get; init; } = 90;
    public int MuscleMemoryThreshold { get; init; } = 4;
    public double OpeningFallbackScore { get; init; } = -50;

    // === Quality phase — Byregot's ===
    public double ByregotsHighIQGreatStridesScore { get; init; } = 150;
    public int ByregotsHighIQThreshold { get; init; } = 8;
    public double ByregotsMidIQScore { get; init; } = 60;
    public int ByregotsMidIQThreshold { get; init; } = 5;

    // === Quality phase — Great Strides ===
    public double GreatStridesHighIQInnoScore { get; init; } = 90;
    public int GreatStridesHighIQThreshold { get; init; } = 8;
    public double GreatStridesMidIQScore { get; init; } = 70;
    public int GreatStridesMidIQThreshold { get; init; } = 6;

    // === Quality phase — buffs & repair ===
    public double InnovationScore { get; init; } = 80;
    public double ImmaculateMendScore { get; init; } = 85;
    public int ImmaculateMendDurThreshold { get; init; } = 15;
    public int ImmaculateMendCPThreshold { get; init; } = 200;
    public double ManipulationScore { get; init; } = 78;
    public int ManipulationDurThreshold { get; init; } = 40;
    public double MastersMendQualityScore { get; init; } = 72;
    public int MastersMendDurThreshold { get; init; } = 25;
    public int MastersMendCPThreshold { get; init; } = 88;
    public double WasteNotIIScore { get; init; } = 70;
    public double WasteNotScore { get; init; } = 55;

    // === Quality phase — touch actions ===
    public double PreparatoryTouchScore { get; init; } = 65;
    public int PreparatoryTouchIQCap { get; init; } = 8;
    public double TrainedFinesseScore { get; init; } = 60;
    public double ComboTouchScore { get; init; } = 55;
    public double PrudentTouchScore { get; init; } = 45;
    public double BasicTouchScore { get; init; } = 40;
    public double InnovationActiveScore { get; init; } = -10;
    public double ManipulationActiveScore { get; init; } = -20;
    public double ProgressInQualityPhaseScore { get; init; } = -30;
    public double QualityFallbackScore { get; init; } = -20;

    // === Finishing phase — durability restore ===
    public double FinishManipulationScore { get; init; } = 120;
    public double FinishWasteNotIIScore { get; init; } = 105;
    public int FinishWasteNotIIMinActions { get; init; } = 3;
    public double FinishWasteNotScore { get; init; } = 95;
    public int FinishWasteNotMinActions { get; init; } = 2;
    public double FinishMastersMendScore { get; init; } = 85;
    public double FinishImmaculateMendScore { get; init; } = 85;

    // === Finishing phase — progress actions ===
    public double FinishGroundworkVenerationScore { get; init; } = 100;
    public double FinishVenerationScore { get; init; } = 80;
    public double FinishGroundworkScore { get; init; } = 60;
    public double FinishCarefulSynthesisScore { get; init; } = 50;
    public double FinishPrudentSynthesisScore { get; init; } = 45;
    public double FinishBasicSynthesisScore { get; init; } = 40;
    public double FinishGroundworkFallbackScore { get; init; } = 35;
    public double FinishByregotsScore { get; init; } = 90;
    public double FinishQualityPenalty { get; init; } = -50;
    public double FinishBuffPenalty { get; init; } = -30;
    public double FinishFallbackScore { get; init; } = -20;

    public static GreedySolverConfig Default => new();
}
