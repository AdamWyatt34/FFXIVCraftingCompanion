using System.Diagnostics;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver;
using FFXIV.Domain.CraftingEngine.Solver.Models;

namespace FFXIV.Infrastructure.Solver;

public sealed class GreedySolver : IRotationSolver
{
    private readonly ICraftingSimulator _simulator;
    private readonly GreedySolverConfig _config;

    public GreedySolver(ICraftingSimulator simulator, GreedySolverConfig? config = null)
    {
        _simulator = simulator;
        _config = config ?? GreedySolverConfig.Default;
    }

    public SolverAlgorithm Algorithm => SolverAlgorithm.Greedy;

    public Task<SolverResult> SolveAsync(
        RecipeParameters recipe,
        CrafterStats stats,
        SolverConstraints constraints,
        SolverSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (recipe.IsExpert)
        {
            return Task.FromResult(new SolverResult
            {
                Status = SolverStatus.Failed,
                ErrorMessage = "Expert recipes require the MCTS solver.",
                Metrics = new SolverMetrics { AlgorithmUsed = Algorithm },
            });
        }

        var stopwatch = Stopwatch.StartNew();
        var state = _simulator.CreateInitialState(recipe, stats);
        var actionIds = new List<int>();

        while (state.Status == CraftStatus.InProgress
               && actionIds.Count < constraints.MaxSteps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var bestAction = SelectBestAction(state, stats, recipe, constraints);
            if (bestAction == null) break;

            var result = _simulator.ExecuteAction(state, bestAction.Id, stats, recipe);
            state = result.StateAfter;
            actionIds.Add(bestAction.Id);
        }

        stopwatch.Stop();

        var simulation = _simulator.Simulate(recipe, stats, actionIds);
        var macros = MacroExporter.ExportToMacro(actionIds);

        return Task.FromResult(new SolverResult
        {
            Status = SolverStatus.Completed,
            ActionIds = actionIds,
            Simulation = simulation,
            Macros = macros,
            Metrics = new SolverMetrics
            {
                AlgorithmUsed = SolverAlgorithm.Greedy,
                SolveTimeMs = stopwatch.ElapsedMilliseconds,
                TotalSteps = actionIds.Count,
                FinalQualityPercent = simulation.FinalQualityPercent,
                FinalHQPercent = simulation.FinalHQPercent,
                CraftCompleted = simulation.Success,
                NodesExplored = actionIds.Count,
                IterationsCompleted = 1,
            },
        });
    }

    private ICraftingAction? SelectBestAction(
        CraftingState state, CrafterStats stats,
        RecipeParameters recipe, SolverConstraints constraints)
    {
        var available = _simulator.GetAvailableActions(state, stats, recipe);
        var phase = DeterminePhase(state, stats, recipe);

        ICraftingAction? best = null;
        var bestScore = double.MinValue;

        foreach (var a in available)
        {
            if (!a.IsAvailable) continue;

            var action = CraftingActionRegistry.GetById(a.ActionId);
            if (action == null) continue;

            var score = ScoreAction(a, action, state, stats, recipe, phase, constraints);
            if (score > bestScore)
            {
                bestScore = score;
                best = action;
            }
        }

        return bestScore > double.MinValue ? best : null;
    }

    #region Phase Detection

    private enum CraftPhase { Opening, Quality, Finishing }

    private CraftPhase DeterminePhase(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        if (state.IsFirstStep)
            return CraftPhase.Opening;

        if (state.Quality >= state.MaxQuality)
            return CraftPhase.Finishing;

        var finishing = EstimateFinishingResources(state, stats, recipe);

        bool cpTight = state.CP <= finishing.CPNeeded + _config.FinishingCPBuffer;
        int effectiveDurBuffer = _config.GetEffectiveDurabilityBuffer(recipe.Durability);
        bool durabilityTight = state.Durability <= finishing.DurabilityNeeded + effectiveDurBuffer;

        if (cpTight || durabilityTight)
            return CraftPhase.Finishing;

        return CraftPhase.Quality;
    }

    private record struct FinishingEstimate(int ActionsNeeded, int CPNeeded, int DurabilityNeeded);

    private static FinishingEstimate EstimateFinishingResources(
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        int remainingProgress = state.MaxProgress - state.Progress;
        int progressPerAction = EstimateProgressPerAction(stats, recipe);
        int actionsToFinish = progressPerAction > 0
            ? (int)Math.Ceiling((double)remainingProgress / progressPerAction)
            : 3;

        // Groundwork costs 20 dur each; account for Manipulation healing if active
        int rawDurability = actionsToFinish * 20;
        bool manipActive = state.ManipulationTurns > 0;
        int manipHealing = manipActive
            ? Math.Min(state.ManipulationTurns, actionsToFinish + 1) * 5  // +1 for Veneration turn
            : 0;
        int netDurability = Math.Max(rawDurability - manipHealing, actionsToFinish * 5);

        // CP: Veneration + Groundwork actions. If durability is short, add Manipulation cost.
        int baseCPNeeded = 18 + (actionsToFinish * 18);
        bool needsManipulation = !manipActive && state.Durability < rawDurability;
        if (needsManipulation)
            baseCPNeeded += 96; // Manipulation CP cost

        return new FinishingEstimate(actionsToFinish, baseCPNeeded, netDurability);
    }

    private static int EstimateProgressPerAction(CrafterStats stats, RecipeParameters recipe)
    {
        int baseProgress = FormulaCalculator.CalculateBaseProgress(
            stats.Craftsmanship, recipe.ProgressDivider,
            recipe.ProgressModifier, stats.Level, recipe.RecipeLevel);

        // Groundwork (300 potency) + Veneration (+50%) = 450 effective potency
        return (int)Math.Floor(baseProgress * 450.0 / 100.0);
    }

    #endregion

    #region Scoring

    private double ScoreAction(
        ActionAvailability availability, ICraftingAction action,
        CraftingState state, CrafterStats stats,
        RecipeParameters recipe, CraftPhase phase,
        SolverConstraints constraints)
    {
        // Reject unreliable actions unless explicitly allowed
        if (availability.SuccessRate < 100 && !constraints.AllowUnsafeActions)
            return double.MinValue;

        // TricksOfTheTrade on Good/Excellent: always high priority (free 20 CP)
        if (action.Name == "TricksOfTheTrade" &&
            (state.Condition == CraftCondition.Good || state.Condition == CraftCondition.Excellent))
            return _config.TricksOfTheTradeScore;

        // Universal penalties
        double score = 0;
        if (availability.CPCost > 0)
            score -= availability.CPCost * _config.CPPenaltyWeight;
        if (availability.DurabilityCost > 0)
            score -= availability.DurabilityCost * _config.DurabilityPenaltyWeight;

        score += phase switch
        {
            CraftPhase.Opening => ScoreOpeningAction(action, stats, recipe),
            CraftPhase.Quality => ScoreQualityAction(action, availability, state, recipe),
            CraftPhase.Finishing => ScoreFinishingAction(action, availability, state, stats, recipe),
            _ => 0,
        };

        return score;
    }

    private double ScoreOpeningAction(
        ICraftingAction action, CrafterStats stats, RecipeParameters recipe)
    {
        return action.Name switch
        {
            "TrainedEye" when stats.Level >= recipe.ClassJobLevel + 10 => _config.TrainedEyeScore,
            "Reflect" => _config.ReflectScore,
            "MuscleMemory" => EstimateActionsToFinishProgress(stats, recipe) >= _config.MuscleMemoryThreshold
                ? _config.MuscleMemoryHighScore : _config.MuscleMemoryLowScore,
            _ => _config.OpeningFallbackScore,
        };
    }

    private double ScoreQualityAction(
        ICraftingAction action, ActionAvailability availability,
        CraftingState state, RecipeParameters recipe)
    {
        bool innovationActive = state.InnovationTurns > 0;
        bool greatStridesActive = state.GreatStridesTurns > 0;
        bool wasteNotActive = state.WasteNotTurns > 0 || state.WasteNotIITurns > 0;
        bool manipulationActive = state.ManipulationTurns > 0;
        int iq = state.InnerQuietStacks;

        // Durability-aware penalty: penalize touch actions when durability is critically low
        double durabilityPenalty = 0;
        if (_config.EnableDurabilityAwareScoring
            && recipe.Durability > 0
            && state.Durability < recipe.Durability * 0.3
            && availability.Category == ActionCategory.Quality)
        {
            durabilityPenalty = _config.DurabilityAwarePenalty;
        }

        return action.Name switch
        {
            // Quality finisher — maximize with Great Strides + high IQ
            "ByregotsBlessing" when iq >= _config.ByregotsHighIQThreshold && greatStridesActive
                => _config.ByregotsHighIQGreatStridesScore,
            "ByregotsBlessing" when iq >= _config.ByregotsMidIQThreshold
                => _config.ByregotsMidIQScore,

            // Buff setup for Byregot's
            "GreatStrides" when iq >= _config.GreatStridesHighIQThreshold && innovationActive
                => _config.GreatStridesHighIQInnoScore,
            "GreatStrides" when iq >= _config.GreatStridesMidIQThreshold
                => _config.GreatStridesMidIQScore,

            // Innovation: essential for quality
            "Innovation" when !innovationActive => _config.InnovationScore,

            // Resource management — use Manipulation early for durability sustain
            "ImmaculateMend" when state.Durability <= _config.ImmaculateMendDurThreshold
                && state.CP >= _config.ImmaculateMendCPThreshold => _config.ImmaculateMendScore,
            "Manipulation" when !manipulationActive
                && state.Durability <= _config.ManipulationDurThreshold => _config.ManipulationScore,
            "MastersMend" when state.Durability <= _config.MastersMendDurThreshold
                && state.CP >= _config.MastersMendCPThreshold => _config.MastersMendQualityScore,
            "WasteNotII" when !wasteNotActive => _config.WasteNotIIScore,
            "WasteNot" when !wasteNotActive => _config.WasteNotScore,

            // IQ building
            "PreparatoryTouch" when iq < _config.PreparatoryTouchIQCap => _config.PreparatoryTouchScore,
            "TrainedFinesse" when iq == 10 => _config.TrainedFinesseScore,

            // Touch combo chain (CP efficient)
            "AdvancedTouch" when state.ComboAction == 100004 => _config.ComboTouchScore,
            "StandardTouch" when state.ComboAction == 100002 => _config.ComboTouchScore,

            // Single touches
            "PrudentTouch" when !wasteNotActive => _config.PrudentTouchScore,
            "BasicTouch" => _config.BasicTouchScore,

            // Avoid doing unnecessary buffs
            "Innovation" when innovationActive => _config.InnovationActiveScore,
            "Manipulation" when manipulationActive => _config.ManipulationActiveScore,

            // Progress actions in quality phase: penalize
            _ when availability.Category == ActionCategory.Progress => _config.ProgressInQualityPhaseScore,

            // Everything else (InnerQuiet no-op, Observe, etc.)
            _ => _config.QualityFallbackScore,
        } - durabilityPenalty;
    }

    private double ScoreFinishingAction(
        ICraftingAction action, ActionAvailability availability,
        CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        bool venerationActive = state.VenerationTurns > 0;
        bool manipulationActive = state.ManipulationTurns > 0;
        bool wasteNotActive = state.WasteNotTurns > 0 || state.WasteNotIITurns > 0;
        int iq = state.InnerQuietStacks;

        // Estimate remaining progress actions needed
        int remainingProgress = state.MaxProgress - state.Progress;
        int progressPerAction = EstimateProgressPerAction(stats, recipe);
        int actionsRemaining = progressPerAction > 0
            ? (int)Math.Ceiling((double)remainingProgress / progressPerAction)
            : 3;

        // How much durability we need for remaining Groundwork actions
        int durPerAction = wasteNotActive ? 10 : 20;
        int durNeeded = actionsRemaining * durPerAction;
        bool durabilityShort = state.Durability < durNeeded + 5;

        return action.Name switch
        {
            // Durability restoration — critical when short
            "Manipulation" when !manipulationActive && durabilityShort => _config.FinishManipulationScore,
            "WasteNotII" when !wasteNotActive && durabilityShort
                && actionsRemaining >= _config.FinishWasteNotIIMinActions => _config.FinishWasteNotIIScore,
            "WasteNot" when !wasteNotActive && durabilityShort
                && actionsRemaining >= _config.FinishWasteNotMinActions => _config.FinishWasteNotScore,
            "MastersMend" when state.Durability <= _config.MastersMendDurThreshold
                && state.CP >= _config.MastersMendCPThreshold => _config.FinishMastersMendScore,
            "ImmaculateMend" when state.Durability <= _config.ImmaculateMendDurThreshold
                && state.CP >= _config.ImmaculateMendCPThreshold => _config.FinishImmaculateMendScore,

            // Primary finishing actions
            "Groundwork" when venerationActive => _config.FinishGroundworkVenerationScore,
            "Veneration" when !venerationActive && state.Durability > durPerAction
                => _config.FinishVenerationScore,
            "Groundwork" when !durabilityShort => _config.FinishGroundworkScore,
            "CarefulSynthesis" => _config.FinishCarefulSynthesisScore,
            "PrudentSynthesis" when !wasteNotActive => _config.FinishPrudentSynthesisScore,
            "BasicSynthesis" => _config.FinishBasicSynthesisScore,

            // Fallback: Groundwork even when durability is somewhat short
            "Groundwork" => _config.FinishGroundworkFallbackScore,

            // Dump remaining quality before finishing
            "ByregotsBlessing" when iq > 0 && !durabilityShort => _config.FinishByregotsScore,

            // Quality actions other than Byregot's are wasteful
            _ when availability.Category == ActionCategory.Quality => _config.FinishQualityPenalty,

            // Buffs that don't help finishing
            "Innovation" or "GreatStrides" => _config.FinishBuffPenalty,

            _ => _config.FinishFallbackScore,
        };
    }

    #endregion

    #region Helpers

    private static int EstimateActionsToFinishProgress(CrafterStats stats, RecipeParameters recipe)
    {
        int progressPerAction = EstimateProgressPerAction(stats, recipe);
        if (progressPerAction <= 0) return 10;
        return (int)Math.Ceiling((double)recipe.Difficulty / progressPerAction);
    }

    #endregion
}
