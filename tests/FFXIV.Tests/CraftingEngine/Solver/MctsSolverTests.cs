using System.Diagnostics;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Infrastructure.Solver;
using FFXIV.Infrastructure.Solver.MCTS;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine.Solver;

public class MCTSSolverTests
{
    private readonly CraftingSimulator _simulator = new();
    private readonly MCTSSolver _solver;
    private readonly SolverConstraints _defaultConstraints = new();

    public MCTSSolverTests()
    {
        var greedySolver = new GreedySolver(_simulator);
        _solver = new MCTSSolver(_simulator, greedySolver);
    }

    [Fact]
    public async Task SolveAsync_SimpleRecipe_CompletesSuccessfully()
    {
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 3600, CP = 600 };
        var recipe = new RecipeParameters
        {
            RecipeId = 1,
            RecipeLevel = 150,
            ClassJobLevel = 50,
            Difficulty = 1000,
            Quality = 2000,
            Durability = 60,
            ProgressDivider = 100,
            ProgressModifier = 100,
            QualityDivider = 100,
            QualityModifier = 100,
        };
        var settings = new SolverSettings { TimeBudgetMs = 1000, RandomSeed = 42 };

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, settings);

        result.Status.Should().Be(SolverStatus.Completed);
        result.Simulation.Should().NotBeNull();
        result.Simulation!.Success.Should().BeTrue();
        result.ActionIds.Should().NotBeEmpty();
        result.Metrics.CraftCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task SolveAsync_Level90Recipe_CompletesWithReasonableQuality()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();
        var settings = new SolverSettings { TimeBudgetMs = 2000, RandomSeed = 42 };

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, settings);

        result.Status.Should().Be(SolverStatus.Completed);
        result.Simulation.Should().NotBeNull();
        result.Simulation!.Success.Should().BeTrue();
        result.Metrics.FinalQualityPercent.Should().BeGreaterThanOrEqualTo(10);
        result.ActionIds.Count.Should().BeLessThanOrEqualTo(50);
    }

    [Fact]
    public async Task SolveAsync_RespectsTimeBudget()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();
        var settings = new SolverSettings { TimeBudgetMs = 1000, RandomSeed = 42 };

        var stopwatch = Stopwatch.StartNew();
        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, settings);
        stopwatch.Stop();

        result.Status.Should().Be(SolverStatus.Completed);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(3000);
        result.Metrics.IterationsCompleted.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SolveAsync_CancellationToken_ThrowsWhenCancelled()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();
        var settings = new SolverSettings { TimeBudgetMs = 10000 };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _solver.SolveAsync(recipe, stats, _defaultConstraints, settings, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SolveAsync_MaxStepsRespected()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();
        var constraints = new SolverConstraints { MaxSteps = 15 };
        var settings = new SolverSettings { TimeBudgetMs = 1000, RandomSeed = 42 };

        var result = await _solver.SolveAsync(recipe, stats, constraints, settings);

        result.Status.Should().Be(SolverStatus.Completed);
        result.ActionIds.Count.Should().BeLessThanOrEqualTo(15);
    }

    [Fact]
    public async Task SolveAsync_GeneratesMacros()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();
        var settings = new SolverSettings { TimeBudgetMs = 1000, RandomSeed = 42 };

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, settings);

        result.Status.Should().Be(SolverStatus.Completed);
        result.Macros.Should().NotBeEmpty();
        result.Macros[0].Should().Contain("/ac");
    }

    [Fact]
    public async Task SolveAsync_SimulationConsistency_ResultMatchesReplay()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();
        var settings = new SolverSettings { TimeBudgetMs = 1000, RandomSeed = 42 };

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, settings);

        result.Simulation.Should().NotBeNull();

        // Re-simulate the action sequence independently
        var replay = _simulator.Simulate(recipe, stats, result.ActionIds);

        replay.Success.Should().Be(result.Simulation!.Success);
        replay.FinalState.Progress.Should().Be(result.Simulation.FinalState.Progress);
        replay.FinalState.Quality.Should().Be(result.Simulation.FinalState.Quality);
        replay.TotalSteps.Should().Be(result.Simulation.TotalSteps);
    }

    [Fact]
    public async Task SolveAsync_MetricsArePopulated()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();
        var settings = new SolverSettings { TimeBudgetMs = 1000, RandomSeed = 42 };

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, settings);

        result.Metrics.AlgorithmUsed.Should().Be(SolverAlgorithm.MCTS);
        result.Metrics.TotalSteps.Should().BeGreaterThan(0);
        result.Metrics.SolveTimeMs.Should().BeGreaterThanOrEqualTo(0);
        result.Metrics.IterationsCompleted.Should().BeGreaterThan(0);
        result.Metrics.NodesExplored.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SolveAsync_ProducesAtLeastGreedyQuality()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();
        var settings = new SolverSettings { TimeBudgetMs = 2000, RandomSeed = 42 };

        // Run greedy
        var greedySolver = new GreedySolver(_simulator);
        var greedyResult = await greedySolver.SolveAsync(
            recipe, stats, _defaultConstraints, new SolverSettings());

        // Run MCTS
        var mctsResult = await _solver.SolveAsync(recipe, stats, _defaultConstraints, settings);

        mctsResult.Status.Should().Be(SolverStatus.Completed);
        greedyResult.Status.Should().Be(SolverStatus.Completed);

        // MCTS should be at least as good as greedy (it seeds with greedy)
        mctsResult.Metrics.FinalQualityPercent.Should()
            .BeGreaterThanOrEqualTo(greedyResult.Metrics.FinalQualityPercent);
    }
}

public class MCTSNodeTests
{
    [Fact]
    public void UCB1Score_UnvisitedNode_ReturnsMaxValue()
    {
        var state = CraftingTestHelpers.CreateDefaultState();
        var root = new MCTSNode { State = state, VisitCount = 10 };
        var child = new MCTSNode { Parent = root, ActionId = 100001, State = state };

        child.UCB1Score(1.41).Should().Be(double.MaxValue);
    }

    [Fact]
    public void UCB1Score_VisitedNode_ReturnsFiniteValue()
    {
        var state = CraftingTestHelpers.CreateDefaultState();
        var root = new MCTSNode { State = state, VisitCount = 100 };
        var child = new MCTSNode
        {
            Parent = root,
            ActionId = 100001,
            State = state,
            VisitCount = 10,
            TotalReward = 7.0,
        };

        var score = child.UCB1Score(1.41);
        score.Should().BeGreaterThan(0);
        score.Should().BeLessThan(10);
    }

    [Fact]
    public void UCB1Score_HigherReward_HigherScore()
    {
        var state = CraftingTestHelpers.CreateDefaultState();
        var root = new MCTSNode { State = state, VisitCount = 100 };

        var goodChild = new MCTSNode
        {
            Parent = root, ActionId = 1, State = state,
            VisitCount = 10, TotalReward = 8.0,
        };
        var badChild = new MCTSNode
        {
            Parent = root, ActionId = 2, State = state,
            VisitCount = 10, TotalReward = 2.0,
        };

        goodChild.UCB1Score(1.41).Should().BeGreaterThan(badChild.UCB1Score(1.41));
    }

    [Fact]
    public void UCB1Score_LessVisited_HigherExplorationBonus()
    {
        var state = CraftingTestHelpers.CreateDefaultState();
        var root = new MCTSNode { State = state, VisitCount = 100 };

        var lessVisited = new MCTSNode
        {
            Parent = root, ActionId = 1, State = state,
            VisitCount = 2, TotalReward = 1.0, // avg = 0.5
        };
        var moreVisited = new MCTSNode
        {
            Parent = root, ActionId = 2, State = state,
            VisitCount = 50, TotalReward = 25.0, // avg = 0.5
        };

        // Same average reward, but less visited should have higher UCB1
        lessVisited.UCB1Score(1.41).Should().BeGreaterThan(moreVisited.UCB1Score(1.41));
    }

    [Fact]
    public void UCB1Score_RootNode_ReturnsAverageReward()
    {
        var state = CraftingTestHelpers.CreateDefaultState();
        var root = new MCTSNode
        {
            State = state,
            VisitCount = 10,
            TotalReward = 5.0,
        };

        root.UCB1Score(1.41).Should().Be(0.5);
    }

    [Fact]
    public void IsTerminal_InProgress_ReturnsFalse()
    {
        var state = CraftingTestHelpers.CreateDefaultState();
        var node = new MCTSNode { State = state };

        node.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void IsTerminal_Completed_ReturnsTrue()
    {
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Status = CraftStatus.Completed,
        };
        var node = new MCTSNode { State = state };

        node.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsTerminal_Failed_ReturnsTrue()
    {
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Status = CraftStatus.Failed,
        };
        var node = new MCTSNode { State = state };

        node.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void IsFullyExpanded_HasUntriedActions_ReturnsFalse()
    {
        var state = CraftingTestHelpers.CreateDefaultState();
        var node = new MCTSNode
        {
            State = state,
            UntriedActions = [1, 2, 3],
        };

        node.IsFullyExpanded.Should().BeFalse();
    }

    [Fact]
    public void IsFullyExpanded_NoUntriedActions_ReturnsTrue()
    {
        var state = CraftingTestHelpers.CreateDefaultState();
        var node = new MCTSNode { State = state };
        // UntriedActions defaults to empty list

        node.IsFullyExpanded.Should().BeTrue();
    }

    [Fact]
    public void AverageReward_CorrectCalculation()
    {
        var state = CraftingTestHelpers.CreateDefaultState();
        var node = new MCTSNode
        {
            State = state,
            VisitCount = 4,
            TotalReward = 3.0,
        };

        node.AverageReward.Should().Be(0.75);
    }

    [Fact]
    public void AverageReward_ZeroVisits_ReturnsZero()
    {
        var state = CraftingTestHelpers.CreateDefaultState();
        var node = new MCTSNode { State = state };

        node.AverageReward.Should().Be(0);
    }

    [Fact]
    public void Backpropagate_UpdatesAllAncestors()
    {
        var state = CraftingTestHelpers.CreateDefaultState();
        var root = new MCTSNode { State = state };
        var child = new MCTSNode { Parent = root, ActionId = 1, State = state };
        root.Children.Add(child);
        var grandchild = new MCTSNode { Parent = child, ActionId = 2, State = state };
        child.Children.Add(grandchild);

        MCTSSolver.Backpropagate(grandchild, 0.8);

        grandchild.VisitCount.Should().Be(1);
        grandchild.TotalReward.Should().Be(0.8);
        grandchild.BestReward.Should().Be(0.8);

        child.VisitCount.Should().Be(1);
        child.TotalReward.Should().Be(0.8);
        child.BestReward.Should().Be(0.8);

        root.VisitCount.Should().Be(1);
        root.TotalReward.Should().Be(0.8);
        root.BestReward.Should().Be(0.8);
    }

    [Fact]
    public void Backpropagate_MultipleUpdates_AccumulatesCorrectly()
    {
        var state = CraftingTestHelpers.CreateDefaultState();
        var root = new MCTSNode { State = state };
        var child = new MCTSNode { Parent = root, ActionId = 1, State = state };
        root.Children.Add(child);

        MCTSSolver.Backpropagate(child, 0.6);
        MCTSSolver.Backpropagate(child, 0.9);

        child.VisitCount.Should().Be(2);
        child.TotalReward.Should().BeApproximately(1.5, 0.001);
        child.BestReward.Should().Be(0.9);
        child.AverageReward.Should().BeApproximately(0.75, 0.001);

        root.VisitCount.Should().Be(2);
        root.TotalReward.Should().BeApproximately(1.5, 0.001);
    }
}

public class RewardFunctionTests
{
    [Fact]
    public void CalculateReward_FailedCraft_ReturnsZero()
    {
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Status = CraftStatus.Failed,
            Durability = 0,
        };
        var constraints = new SolverConstraints();

        var reward = MCTSSolver.CalculateReward(state, constraints);

        reward.Should().Be(0.0);
    }

    [Fact]
    public void CalculateReward_InProgress_ReturnsPartialCredit()
    {
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Progress = 1750, // 50% progress
            MaxProgress = 3500,
            Status = CraftStatus.InProgress,
        };
        var constraints = new SolverConstraints();

        var reward = MCTSSolver.CalculateReward(state, constraints);

        reward.Should().BeGreaterThan(0);
        reward.Should().BeLessThanOrEqualTo(0.1);
    }

    [Fact]
    public void CalculateReward_CompletedNoQuality_ReturnsBaseReward()
    {
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Progress = 3500,
            MaxProgress = 3500,
            Quality = 0,
            MaxQuality = 7200,
            Status = CraftStatus.Completed,
        };
        var constraints = new SolverConstraints();

        var reward = MCTSSolver.CalculateReward(state, constraints);

        // 0.2 + 0.8 * 0 = 0.2, minus small step penalty
        reward.Should().BeGreaterThanOrEqualTo(0.15);
        reward.Should().BeLessThan(0.3);
    }

    [Fact]
    public void CalculateReward_CompletedHalfQuality_ReturnsMidReward()
    {
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Progress = 3500,
            MaxProgress = 3500,
            Quality = 3600,
            MaxQuality = 7200,
            Status = CraftStatus.Completed,
        };
        var constraints = new SolverConstraints();

        var reward = MCTSSolver.CalculateReward(state, constraints);

        // 0.2 + 0.8 * 0.5 = 0.6, minus small step penalty
        reward.Should().BeGreaterThan(0.5);
        reward.Should().BeLessThan(0.7);
    }

    [Fact]
    public void CalculateReward_CompletedMaxQuality_ReturnsNearOne()
    {
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Progress = 3500,
            MaxProgress = 3500,
            Quality = 7200,
            MaxQuality = 7200,
            Status = CraftStatus.Completed,
        };
        var constraints = new SolverConstraints();

        var reward = MCTSSolver.CalculateReward(state, constraints);

        // 0.2 + 0.8 * 1.0 = 1.0, minus small step penalty
        reward.Should().BeGreaterThanOrEqualTo(0.95);
        reward.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void CalculateReward_TargetQualityMet_ReturnsFullQualityReward()
    {
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Progress = 3500,
            MaxProgress = 3500,
            Quality = 5040, // 70% quality
            MaxQuality = 7200,
            Status = CraftStatus.Completed,
        };
        var constraints = new SolverConstraints { TargetQualityPercent = 70 };

        var reward = MCTSSolver.CalculateReward(state, constraints);

        // Target met → qualityReward = 1.0 → 0.2 + 0.8 * 1.0 = 1.0
        reward.Should().BeGreaterThanOrEqualTo(0.95);
    }

    [Fact]
    public void CalculateReward_ZeroMaxQuality_ReturnsFullReward()
    {
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            Progress = 3500,
            MaxProgress = 3500,
            Quality = 0,
            MaxQuality = 0,
            Status = CraftStatus.Completed,
        };
        var constraints = new SolverConstraints();

        var reward = MCTSSolver.CalculateReward(state, constraints);

        // qualityReward = 1.0 (no quality to optimize)
        reward.Should().BeGreaterThanOrEqualTo(0.95);
    }
}
