using System.Text;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Infrastructure.Solver;
using FFXIV.Infrastructure.Solver.MCTS;
using Xunit.Abstractions;

namespace FFXIV.Tests.CraftingEngine.Solver;

[Trait("Category", "Tuning")]
public class MCTSParameterTuningTests
{
    private readonly CraftingSimulator _simulator = new();
    private readonly ITestOutputHelper _output;

    public MCTSParameterTuningTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task TuneExplorationConstant()
    {
        var values = new[] { 0.5, 1.0, 1.41, 2.0 };
        await RunParameterSweep("ExplorationConstant", values,
            v => new MCTSConfig { ExplorationConstant = v });
    }

    [Fact]
    public async Task TuneRolloutTemperature()
    {
        var values = new[] { 0.25, 0.5, 1.0, 2.0 };
        await RunParameterSweep("RolloutTemperature", values,
            v => new MCTSConfig { RolloutTemperature = v });
    }

    [Fact]
    public async Task TuneHeuristicExpansionProbability()
    {
        var values = new[] { 0.5, 0.7, 0.9 };
        await RunParameterSweep("HeuristicExpansionProb", values,
            v => new MCTSConfig { HeuristicExpansionProbability = v });
    }

    [Fact]
    public async Task TuneTimeBudget()
    {
        var timeBudgets = new[] { 5000, 10000, 20000, 30000 };
        var dataset = BenchmarkLoader.Load();
        var benchmarks = dataset.Benchmarks
            .Where(b => b.Id is "lv100_4star_endgame" or "lv90_35dur_food" or "lv100_undergeared")
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("=== Time Budget Tuning ===");
        sb.AppendLine("| Benchmark                   | Budget(ms) | Quality% | Iterations | Completed |");
        sb.AppendLine("|-----------------------------|------------|----------|------------|-----------|");

        foreach (var entry in benchmarks)
        {
            var recipe = BenchmarkLoader.ToRecipeParameters(entry.Recipe);
            var stats = BenchmarkLoader.ToCrafterStats(entry.Stats);

            foreach (var budget in timeBudgets)
            {
                var greedy = new GreedySolver(_simulator);
                var mcts = new MCTSSolver(_simulator, greedy);
                var settings = new SolverSettings
                {
                    TimeBudgetMs = budget,
                    RandomSeed = 42,
                };

                var result = await mcts.SolveAsync(
                    recipe, stats, new SolverConstraints(), settings);

                sb.AppendLine(
                    $"| {entry.Id,-27} | {budget,10} " +
                    $"| {result.Metrics.FinalQualityPercent,7:F1}% " +
                    $"| {result.Metrics.IterationsCompleted,10} " +
                    $"| {result.Simulation?.Success,9} |");
            }
        }

        _output.WriteLine(sb.ToString());
    }

    private async Task RunParameterSweep(
        string paramName, double[] values,
        Func<double, MCTSConfig> configFactory)
    {
        var dataset = BenchmarkLoader.Load();
        var benchmarks = dataset.Benchmarks
            .Where(b => b.Id is "lv100_4star_endgame" or "lv90_3star_standard" or "lv90_35dur_food")
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"=== {paramName} Tuning ===");
        sb.AppendLine($"| Benchmark                   | {paramName,-22} | Quality% | Iterations | Time(ms) |");
        sb.AppendLine($"|-----------------------------|-{new string('-', 22)}|----------|------------|----------|");

        foreach (var entry in benchmarks)
        {
            var recipe = BenchmarkLoader.ToRecipeParameters(entry.Recipe);
            var stats = BenchmarkLoader.ToCrafterStats(entry.Stats);

            foreach (var value in values)
            {
                var config = configFactory(value);
                var greedy = new GreedySolver(_simulator);
                var mcts = new MCTSSolver(_simulator, greedy, config);
                var settings = new SolverSettings
                {
                    TimeBudgetMs = 10000,
                    RandomSeed = 42,
                };

                var result = await mcts.SolveAsync(
                    recipe, stats, new SolverConstraints(), settings);

                sb.AppendLine(
                    $"| {entry.Id,-27} | {value,-22} " +
                    $"| {result.Metrics.FinalQualityPercent,7:F1}% " +
                    $"| {result.Metrics.IterationsCompleted,10} " +
                    $"| {result.Metrics.SolveTimeMs,8} |");
            }
        }

        _output.WriteLine(sb.ToString());
    }
}
