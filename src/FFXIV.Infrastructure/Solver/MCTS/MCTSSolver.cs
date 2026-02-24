using System.Diagnostics;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver;
using FFXIV.Domain.CraftingEngine.Solver.Models;

namespace FFXIV.Infrastructure.Solver.MCTS;

public sealed class MCTSSolver : IRotationSolver
{
    private readonly ICraftingSimulator _simulator;
    private readonly GreedySolver _greedySolver;
    private readonly MCTSConfig _config;

    public MCTSSolver(ICraftingSimulator simulator, GreedySolver greedySolver, MCTSConfig? config = null)
    {
        _simulator = simulator;
        _greedySolver = greedySolver;
        _config = config ?? MCTSConfig.Default;
    }

    public SolverAlgorithm Algorithm => SolverAlgorithm.MCTS;

    public async Task<SolverResult> SolveAsync(
        RecipeParameters recipe,
        CrafterStats stats,
        SolverConstraints constraints,
        SolverSettings settings,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var initialState = _simulator.CreateInitialState(recipe, stats);
        var random = settings.RandomSeed.HasValue ? new Random(settings.RandomSeed.Value) : Random.Shared;
        long iterationsCompleted = 0;

        // === Phase 1: Seed with greedy solution ===
        var bestActionSequence = new List<int>();
        double bestReward = -1;

        var greedyResult = await _greedySolver.SolveAsync(
            recipe, stats, constraints, settings, cancellationToken);

        if (greedyResult.Status == SolverStatus.Completed && greedyResult.Simulation is { Success: true })
        {
            bestReward = CalculateReward(greedyResult.Simulation.FinalState, constraints, _config);
            bestActionSequence = new List<int>(greedyResult.ActionIds);
        }

        // === Phase 2: MCTS iterations ===
        var root = new MCTSNode
        {
            State = initialState,
            UntriedActions = GetLegalActions(initialState, stats, recipe, constraints),
        };

        int timeBudgetMs = settings.TimeBudgetMs;
        long iterationsWithoutImprovement = 0;

        while (stopwatch.ElapsedMilliseconds < timeBudgetMs)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 1. SELECTION: Walk tree choosing best UCB1 child
            var selectedNode = Select(root);

            // 2. EXPANSION: Add a new child with an untried action
            var expandedNode = Expand(selectedNode, stats, recipe, constraints, random);

            // 3. SIMULATION (ROLLOUT): Play out from expanded node
            var (rolloutReward, rolloutActions) = Rollout(
                expandedNode, stats, recipe, constraints, random);

            // 4. BACKPROPAGATION: Update statistics up the tree
            Backpropagate(expandedNode, rolloutReward);

            // Track best overall solution
            if (rolloutReward > bestReward)
            {
                var candidateActions = ExtractActionSequence(expandedNode);
                candidateActions.AddRange(rolloutActions);

                if (candidateActions.Count <= constraints.MaxSteps)
                {
                    bestReward = rolloutReward;
                    bestActionSequence = candidateActions;
                    iterationsWithoutImprovement = 0;
                }
                else
                {
                    iterationsWithoutImprovement++;
                }
            }
            else
            {
                iterationsWithoutImprovement++;
            }

            iterationsCompleted++;

            // Early termination: perfect quality achieved
            if (bestReward >= _config.PerfectRewardThreshold)
                break;

            // Early termination: convergence (no improvement for threshold iterations)
            if (iterationsWithoutImprovement >= _config.EarlyTerminationThreshold)
                break;
        }

        stopwatch.Stop();

        // Run the best action sequence through the simulator for full results
        if (bestActionSequence.Count == 0)
        {
            return new SolverResult
            {
                Status = SolverStatus.Failed,
                ErrorMessage = "MCTS solver could not find a valid rotation.",
                Metrics = new SolverMetrics
                {
                    AlgorithmUsed = SolverAlgorithm.MCTS,
                    SolveTimeMs = stopwatch.ElapsedMilliseconds,
                    IterationsCompleted = iterationsCompleted,
                    NodesExplored = iterationsCompleted,
                },
            };
        }

        var simulation = _simulator.Simulate(recipe, stats, bestActionSequence);
        var macros = MacroExporter.ExportToMacro(bestActionSequence);

        return new SolverResult
        {
            Status = SolverStatus.Completed,
            ActionIds = bestActionSequence,
            Simulation = simulation,
            Macros = macros,
            Metrics = new SolverMetrics
            {
                AlgorithmUsed = SolverAlgorithm.MCTS,
                SolveTimeMs = stopwatch.ElapsedMilliseconds,
                TotalSteps = bestActionSequence.Count,
                FinalQualityPercent = simulation.FinalQualityPercent,
                FinalHQPercent = simulation.FinalHQPercent,
                CraftCompleted = simulation.Success,
                NodesExplored = iterationsCompleted,
                IterationsCompleted = iterationsCompleted,
            },
        };
    }

    #region MCTS Phases

    private MCTSNode Select(MCTSNode node)
    {
        while (!node.IsTerminal && node.IsFullyExpanded && node.Children.Count > 0)
        {
            node = node.Children
                .MaxBy(c => c.UCB1Score(_config.ExplorationConstant))!;
        }

        return node;
    }

    private MCTSNode Expand(
        MCTSNode node, CrafterStats stats,
        RecipeParameters recipe, SolverConstraints constraints, Random random)
    {
        if (node.IsTerminal || node.UntriedActions.Count == 0)
            return node;

        // Pick an untried action (prioritize promising ones)
        int actionId = PickUntriedAction(node, stats, recipe, random);
        node.UntriedActions.Remove(actionId);

        // Execute the action to get the new state
        var result = _simulator.ExecuteAction(node.State, actionId, stats, recipe);
        var childState = result.StateAfter;

        var child = new MCTSNode
        {
            Parent = node,
            ActionId = actionId,
            State = childState,
            UntriedActions = childState.Status == CraftStatus.InProgress
                ? GetLegalActions(childState, stats, recipe, constraints)
                : [],
        };

        node.Children.Add(child);
        return child;
    }

    private (double reward, List<int> actions) Rollout(
        MCTSNode node, CrafterStats stats,
        RecipeParameters recipe, SolverConstraints constraints, Random random)
    {
        var state = node.State;
        var actions = new List<int>();

        while (state.Status == CraftStatus.InProgress && actions.Count < constraints.MaxSteps)
        {
            var action = SelectRolloutAction(state, stats, recipe, constraints, random);
            if (action == null) break;

            var result = _simulator.ExecuteAction(state, action.Id, stats, recipe);
            state = result.StateAfter;
            actions.Add(action.Id);
        }

        double reward = CalculateReward(state, constraints, _config);
        return (reward, actions);
    }

    internal static void Backpropagate(MCTSNode node, double reward)
    {
        var current = node;
        while (current != null)
        {
            current.VisitCount++;
            current.TotalReward += reward;
            current.BestReward = Math.Max(current.BestReward, reward);
            current = current.Parent;
        }
    }

    #endregion

    #region Rollout Policy

    private ICraftingAction? SelectRolloutAction(
        CraftingState state, CrafterStats stats,
        RecipeParameters recipe, SolverConstraints constraints, Random random)
    {
        var available = _simulator.GetAvailableActions(state, stats, recipe)
            .Where(a => a.IsAvailable)
            .Where(a => constraints.AllowUnsafeActions || a.SuccessRate >= 100)
            .ToList();

        if (available.Count == 0) return null;

        // Score each action for rollout
        var scored = available
            .Select(a => new
            {
                Action = CraftingActionRegistry.GetById(a.ActionId),
                Score = ScoreForRollout(a, state, recipe),
            })
            .Where(x => x.Action != null && x.Score > -100)
            .ToList();

        if (scored.Count == 0) return null;

        // Softmax selection: higher scored actions more likely to be picked
        double maxScore = scored.Max(s => s.Score);
        var weights = scored
            .Select(s => Math.Exp((s.Score - maxScore) / _config.RolloutTemperature))
            .ToList();
        double totalWeight = weights.Sum();

        double roll = random.NextDouble() * totalWeight;
        double cumulative = 0;
        for (int i = 0; i < scored.Count; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
                return scored[i].Action;
        }

        return scored[^1].Action;
    }

    #endregion

    #region Scoring

    internal static double CalculateReward(CraftingState state, SolverConstraints constraints)
        => CalculateReward(state, constraints, MCTSConfig.Default);

    internal static double CalculateReward(CraftingState state, SolverConstraints constraints, MCTSConfig config)
    {
        // Failed craft: severe penalty
        if (state.Status == CraftStatus.Failed)
            return config.FailedReward;

        // In-progress (ran out of steps): partial credit for progress
        if (state.Status == CraftStatus.InProgress)
            return config.InProgressProgressWeight * state.ProgressPercent / 100.0;

        // Craft completed — score based on quality achieved
        double qualityReward = state.MaxQuality > 0
            ? state.QualityPercent / 100.0
            : 1.0;

        // Binary bonus for hitting target quality
        if (constraints.TargetQualityPercent.HasValue)
        {
            if (state.QualityPercent >= constraints.TargetQualityPercent.Value)
                qualityReward = 1.0;
        }

        // Small step efficiency bonus
        double stepPenalty = state.Step * config.StepPenaltyPerStep;

        return Math.Max(0, Math.Min(1.0,
            config.CompletedBaseReward + config.CompletedQualityWeight * qualityReward - stepPenalty));
    }

    private double ScoreForRollout(
        ActionAvailability action, CraftingState state, RecipeParameters recipe)
    {
        double score = 0;

        // Penalize resource costs
        if (action.CPCost > 0) score -= action.CPCost * _config.RolloutCPPenalty;
        if (action.DurabilityCost > 0) score -= action.DurabilityCost * _config.RolloutDurabilityPenalty;

        bool qualityCapped = state.MaxQuality > 0 && state.Quality >= state.MaxQuality;
        double cpRatio = state.MaxCP > 0 ? (double)state.CP / state.MaxCP : 0;
        int iq = state.InnerQuietStacks;

        // Emergency finishing: low CP or low durability
        if (cpRatio < _config.EmergencyCPRatio || state.Durability <= _config.EmergencyDurabilityThreshold)
        {
            score += action.Category switch
            {
                ActionCategory.Progress => 50,
                ActionCategory.Repair when state.Durability <= 10 => 40,
                _ => -20,
            };
            return score;
        }

        // Quality capped: just finish
        if (qualityCapped)
        {
            score += action.Category switch
            {
                ActionCategory.Progress => 40,
                ActionCategory.Buff => 10,
                _ => -10,
            };
            return score;
        }

        // First step bonuses
        if (state.IsFirstStep)
        {
            var actionObj = CraftingActionRegistry.GetById(action.ActionId);
            if (actionObj?.Name is "Reflect" or "MuscleMemory" or "TrainedEye")
                score += 40;
        }

        // Normal crafting: prioritize quality building
        bool innovationActive = state.InnovationTurns > 0;
        bool greatStridesActive = state.GreatStridesTurns > 0;

        var actionName = CraftingActionRegistry.GetById(action.ActionId)?.Name;
        score += actionName switch
        {
            // Quality finisher
            "ByregotsBlessing" when iq >= 8 && greatStridesActive => 80,
            "ByregotsBlessing" when iq >= 5 => 40,

            // Buff setup
            "GreatStrides" when iq >= 7 && innovationActive => 50,
            "Innovation" when !innovationActive => 45,

            // Durability management
            "Manipulation" when state.ManipulationTurns == 0 && state.Durability <= 35 => 45,
            "MastersMend" when state.Durability <= 20 => 40,

            // IQ building
            "PreparatoryTouch" when iq < 8 => 35,
            "TrainedFinesse" when iq == 10 => 35,

            // Touch combos
            "AdvancedTouch" when state.ComboAction == 100004 => 30,
            "StandardTouch" when state.ComboAction == 100002 => 30,

            // Single touches
            "PrudentTouch" => 25,
            "BasicTouch" => 20,

            // Progress actions (save for later)
            "Groundwork" when state.VenerationTurns > 0 => 30,
            "Veneration" => 15,
            "CarefulSynthesis" => 10,
            "BasicSynthesis" => 5,

            _ when action.Category == ActionCategory.Quality => 20,
            _ when action.Category == ActionCategory.Progress => 5,
            _ when action.Category == ActionCategory.Buff => 10,
            _ when action.Category == ActionCategory.Repair => state.Durability <= 30 ? 25 : 0,
            _ => 0,
        };

        return score;
    }

    private static double QuickScore(int actionId, CraftingState state, CrafterStats stats, RecipeParameters recipe)
    {
        var action = CraftingActionRegistry.GetById(actionId);
        if (action == null) return 0;

        double score = action.Category switch
        {
            ActionCategory.Quality => 20 + state.InnerQuietStacks,
            ActionCategory.Progress => 15,
            ActionCategory.Buff => 10,
            ActionCategory.Repair => state.Durability <= 25 ? 20 : 5,
            _ => 0,
        };

        score -= action.GetCPCost(state) * 0.03;

        // Opening action bonuses
        if (state.IsFirstStep && action.Name is "Reflect" or "MuscleMemory" or "TrainedEye")
            score += 30;

        return score;
    }

    #endregion

    #region Helpers

    private List<int> GetLegalActions(
        CraftingState state, CrafterStats stats,
        RecipeParameters recipe, SolverConstraints constraints)
    {
        var available = _simulator.GetAvailableActions(state, stats, recipe);

        return available
            .Where(a => a.IsAvailable)
            .Where(a => constraints.AllowUnsafeActions || a.SuccessRate >= 100)
            .Select(a => a.ActionId)
            .ToList();
    }

    private int PickUntriedAction(
        MCTSNode node, CrafterStats stats,
        RecipeParameters recipe, Random random)
    {
        if (node.UntriedActions.Count == 1)
            return node.UntriedActions[0];

        // Pick heuristic-best or random based on configured probability
        if (random.NextDouble() < _config.HeuristicExpansionProbability)
        {
            return node.UntriedActions
                .Select(id => new
                {
                    Id = id,
                    Score = QuickScore(id, node.State, stats, recipe),
                })
                .MaxBy(x => x.Score)!
                .Id;
        }

        return node.UntriedActions[random.Next(node.UntriedActions.Count)];
    }

    private static List<int> ExtractActionSequence(MCTSNode node)
    {
        var actions = new List<int>();
        var current = node;
        while (current.Parent != null)
        {
            actions.Add(current.ActionId);
            current = current.Parent;
        }

        actions.Reverse();
        return actions;
    }

    #endregion
}
