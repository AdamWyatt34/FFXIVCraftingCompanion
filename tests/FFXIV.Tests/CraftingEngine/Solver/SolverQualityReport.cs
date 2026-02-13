using System.Text;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Infrastructure.Solver;
using FFXIV.Infrastructure.Solver.MCTS;
using Xunit.Abstractions;

namespace FFXIV.Tests.CraftingEngine.Solver;

[Trait("Category", "Report")]
public class SolverQualityReport
{
    private readonly CraftingSimulator _simulator = new();
    private readonly ITestOutputHelper _output;

    public SolverQualityReport(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task GenerateFullComparisonReport()
    {
        var dataset = BenchmarkLoader.Load();
        var sb = new StringBuilder();

        sb.AppendLine("=== Solver Quality Benchmark Report ===");
        sb.AppendLine($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();

        sb.AppendLine(
            "| Benchmark                   | Community Q% | Greedy Q% | Greedy ms | MCTS Q%  | MCTS ms  | MCTS Iter  | MCTS/Community |");
        sb.AppendLine(
            "|-----------------------------|-------------|-----------|-----------|----------|----------|------------|----------------|");

        foreach (var entry in dataset.Benchmarks)
        {
            var recipe = BenchmarkLoader.ToRecipeParameters(entry.Recipe);
            var stats = BenchmarkLoader.ToCrafterStats(entry.Stats);

            // Community
            var communityIds = BenchmarkLoader.ResolveActionNames(entry.CommunityRotation.Actions);
            var communityResult = _simulator.Simulate(recipe, stats, communityIds);

            // Greedy
            var greedySolver = new GreedySolver(_simulator);
            var greedyResult = await greedySolver.SolveAsync(
                recipe, stats, new SolverConstraints(), new SolverSettings());

            // MCTS
            var mctsSolver = new MCTSSolver(_simulator, greedySolver);
            var mctsResult = await mctsSolver.SolveAsync(
                recipe, stats, new SolverConstraints(),
                new SolverSettings
                {
                    TimeBudgetMs = entry.SolverTargets.MctsTimeBudgetMs,
                    RandomSeed = 42,
                });

            var mctsVsCommunity = communityResult.FinalQualityPercent > 0
                ? mctsResult.Metrics.FinalQualityPercent / communityResult.FinalQualityPercent * 100
                : 0;

            sb.AppendLine(
                $"| {entry.Id,-27} " +
                $"| {communityResult.FinalQualityPercent,10:F1}% " +
                $"| {greedyResult.Metrics.FinalQualityPercent,8:F1}% " +
                $"| {greedyResult.Metrics.SolveTimeMs,9} " +
                $"| {mctsResult.Metrics.FinalQualityPercent,7:F1}% " +
                $"| {mctsResult.Metrics.SolveTimeMs,8} " +
                $"| {mctsResult.Metrics.IterationsCompleted,10} " +
                $"| {mctsVsCommunity,13:F1}% |");
        }

        sb.AppendLine();
        sb.AppendLine("=== End Report ===");

        _output.WriteLine(sb.ToString());
    }
}
