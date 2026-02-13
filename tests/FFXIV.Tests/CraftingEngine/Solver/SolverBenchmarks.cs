using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Infrastructure.Solver;
using FFXIV.Infrastructure.Solver.MCTS;
using Xunit.Abstractions;

namespace FFXIV.Tests.CraftingEngine.Solver;

public class SolverBenchmarks
{
    private readonly CraftingSimulator _simulator = new();
    private readonly ITestOutputHelper _output;

    public SolverBenchmarks(ITestOutputHelper output) => _output = output;

    public static IEnumerable<object[]> BenchmarkData => BenchmarkLoader.GetBenchmarkMemberData();

    [Theory]
    [MemberData(nameof(BenchmarkData))]
    public void CommunityRotation_Completes_WithExpectedQuality(BenchmarkEntry entry)
    {
        var recipe = BenchmarkLoader.ToRecipeParameters(entry.Recipe);
        var stats = BenchmarkLoader.ToCrafterStats(entry.Stats);
        var actionIds = BenchmarkLoader.ResolveActionNames(entry.CommunityRotation.Actions);

        var result = _simulator.Simulate(recipe, stats, actionIds);

        _output.WriteLine($"[{entry.Id}] Community rotation: " +
            $"Success={result.Success}, Quality={result.FinalQualityPercent:F1}%, " +
            $"HQ={result.FinalHQPercent}%, Steps={result.TotalSteps}");

        if (entry.CommunityRotation.ExpectedCompletion)
            result.Success.Should().BeTrue(
                $"community rotation for '{entry.Name}' should complete the craft");

        result.FinalQualityPercent.Should()
            .BeGreaterThanOrEqualTo(entry.CommunityRotation.ExpectedMinQualityPercent,
                $"community rotation for '{entry.Name}' should meet minimum quality target");
    }

    [Theory]
    [MemberData(nameof(BenchmarkData))]
    public async Task GreedySolver_CompletesAllBenchmarks(BenchmarkEntry entry)
    {
        var recipe = BenchmarkLoader.ToRecipeParameters(entry.Recipe);
        var stats = BenchmarkLoader.ToCrafterStats(entry.Stats);
        var solver = new GreedySolver(_simulator);
        var settings = new SolverSettings { Algorithm = SolverAlgorithm.Greedy };

        var result = await solver.SolveAsync(recipe, stats, new SolverConstraints(), settings);

        _output.WriteLine($"[{entry.Id}] Greedy: " +
            $"Success={result.Simulation?.Success}, " +
            $"Quality={result.Metrics.FinalQualityPercent:F1}%, " +
            $"Steps={result.Metrics.TotalSteps}, " +
            $"Time={result.Metrics.SolveTimeMs}ms");

        result.Status.Should().Be(SolverStatus.Completed);

        if (entry.SolverTargets.GreedyMustComplete)
            result.Simulation!.Success.Should().BeTrue(
                $"greedy solver should complete '{entry.Name}'");
    }

    [Theory]
    [MemberData(nameof(BenchmarkData))]
    public async Task MctsSolver_AchievesTargetQualityVsCommunity(BenchmarkEntry entry)
    {
        var recipe = BenchmarkLoader.ToRecipeParameters(entry.Recipe);
        var stats = BenchmarkLoader.ToCrafterStats(entry.Stats);
        var communityIds = BenchmarkLoader.ResolveActionNames(entry.CommunityRotation.Actions);
        var communityResult = _simulator.Simulate(recipe, stats, communityIds);
        var communityQuality = communityResult.FinalQualityPercent;

        var greedySolver = new GreedySolver(_simulator);
        var mctsSolver = new MCTSSolver(_simulator, greedySolver);
        var settings = new SolverSettings
        {
            Algorithm = SolverAlgorithm.MCTS,
            TimeBudgetMs = entry.SolverTargets.MctsTimeBudgetMs,
            RandomSeed = 42,
        };

        var result = await mctsSolver.SolveAsync(
            recipe, stats, new SolverConstraints(), settings);

        var mctsQuality = result.Metrics.FinalQualityPercent;
        var targetPercent = entry.SolverTargets.MctsMinQualityPercentOfCommunity;
        var targetQuality = communityQuality * targetPercent / 100.0;

        _output.WriteLine($"[{entry.Id}] MCTS vs Community: " +
            $"MCTS={mctsQuality:F1}%, Community={communityQuality:F1}%, " +
            $"Target={targetQuality:F1}% ({targetPercent}% of community), " +
            $"Iterations={result.Metrics.IterationsCompleted}, " +
            $"Time={result.Metrics.SolveTimeMs}ms");

        result.Simulation!.Success.Should().BeTrue(
            $"MCTS solver should complete '{entry.Name}'");
        mctsQuality.Should().BeGreaterThanOrEqualTo(targetQuality,
            $"MCTS should achieve >= {targetPercent}% of community quality for '{entry.Name}'");
    }

    [Theory]
    [MemberData(nameof(BenchmarkData))]
    public async Task MctsSolver_ImprovesOverGreedy(BenchmarkEntry entry)
    {
        var recipe = BenchmarkLoader.ToRecipeParameters(entry.Recipe);
        var stats = BenchmarkLoader.ToCrafterStats(entry.Stats);

        var greedySolver = new GreedySolver(_simulator);
        var greedyResult = await greedySolver.SolveAsync(
            recipe, stats, new SolverConstraints(), new SolverSettings());

        var mctsSolver = new MCTSSolver(_simulator, greedySolver);
        var mctsResult = await mctsSolver.SolveAsync(
            recipe, stats, new SolverConstraints(),
            new SolverSettings
            {
                TimeBudgetMs = entry.SolverTargets.MctsTimeBudgetMs,
                RandomSeed = 42,
            });

        _output.WriteLine($"[{entry.Id}] Greedy={greedyResult.Metrics.FinalQualityPercent:F1}% " +
            $"MCTS={mctsResult.Metrics.FinalQualityPercent:F1}%");

        // MCTS is seeded with greedy solution, so it should always be >= greedy
        mctsResult.Metrics.FinalQualityPercent.Should()
            .BeGreaterThanOrEqualTo(greedyResult.Metrics.FinalQualityPercent,
                $"MCTS should be >= Greedy for '{entry.Name}' (seeded with greedy solution)");
    }
}
