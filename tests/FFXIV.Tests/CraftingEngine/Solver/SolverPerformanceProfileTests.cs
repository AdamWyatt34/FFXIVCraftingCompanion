using System.Diagnostics;
using System.Text;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Infrastructure.Solver;
using FFXIV.Infrastructure.Solver.MCTS;
using Xunit.Abstractions;

namespace FFXIV.Tests.CraftingEngine.Solver;

[Trait("Category", "Performance")]
public class SolverPerformanceProfileTests
{
    private readonly CraftingSimulator _simulator = new();
    private readonly ITestOutputHelper _output;

    public SolverPerformanceProfileTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ProfileMCTSPerformance()
    {
        var dataset = BenchmarkLoader.Load();
        var sb = new StringBuilder();
        sb.AppendLine("=== MCTS Performance Profile ===");
        sb.AppendLine("| Benchmark                   | Time(ms) | Iterations | Iter/sec | Quality% | Steps |");
        sb.AppendLine("|-----------------------------|----------|------------|----------|----------|-------|");

        foreach (var entry in dataset.Benchmarks)
        {
            var recipe = BenchmarkLoader.ToRecipeParameters(entry.Recipe);
            var stats = BenchmarkLoader.ToCrafterStats(entry.Stats);

            var greedy = new GreedySolver(_simulator);
            var mcts = new MCTSSolver(_simulator, greedy);

            var sw = Stopwatch.StartNew();
            var result = await mcts.SolveAsync(
                recipe, stats, new SolverConstraints(),
                new SolverSettings { TimeBudgetMs = 10000, RandomSeed = 42 });
            sw.Stop();

            var iterPerSec = result.Metrics.IterationsCompleted * 1000.0 / Math.Max(1, sw.ElapsedMilliseconds);

            sb.AppendLine(
                $"| {entry.Id,-27} " +
                $"| {sw.ElapsedMilliseconds,8} " +
                $"| {result.Metrics.IterationsCompleted,10} " +
                $"| {iterPerSec,7:F0} " +
                $"| {result.Metrics.FinalQualityPercent,7:F1}% " +
                $"| {result.Metrics.TotalSteps,5} |");
        }

        _output.WriteLine(sb.ToString());
    }

    [Fact]
    public async Task ProfileGreedySolverPerformance()
    {
        var dataset = BenchmarkLoader.Load();
        var sb = new StringBuilder();
        sb.AppendLine("=== Greedy Solver Performance ===");
        sb.AppendLine("| Benchmark                   | Time(ms) | Quality% | Steps |");
        sb.AppendLine("|-----------------------------|----------|----------|-------|");

        foreach (var entry in dataset.Benchmarks)
        {
            var recipe = BenchmarkLoader.ToRecipeParameters(entry.Recipe);
            var stats = BenchmarkLoader.ToCrafterStats(entry.Stats);
            var solver = new GreedySolver(_simulator);

            var sw = Stopwatch.StartNew();
            var result = await solver.SolveAsync(
                recipe, stats, new SolverConstraints(), new SolverSettings());
            sw.Stop();

            sb.AppendLine(
                $"| {entry.Id,-27} " +
                $"| {sw.ElapsedMilliseconds,8} " +
                $"| {result.Metrics.FinalQualityPercent,7:F1}% " +
                $"| {result.Metrics.TotalSteps,5} |");
        }

        _output.WriteLine(sb.ToString());
    }

    [Fact]
    public async Task MeasureMemoryUsage()
    {
        var entry = BenchmarkLoader.Load().Benchmarks
            .First(b => b.Id == "lv100_4star_endgame");
        var recipe = BenchmarkLoader.ToRecipeParameters(entry.Recipe);
        var stats = BenchmarkLoader.ToCrafterStats(entry.Stats);

        // Force GC before measurement
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var memBefore = GC.GetTotalMemory(true);

        var greedy = new GreedySolver(_simulator);
        var mcts = new MCTSSolver(_simulator, greedy);
        var result = await mcts.SolveAsync(
            recipe, stats, new SolverConstraints(),
            new SolverSettings { TimeBudgetMs = 10000, RandomSeed = 42 });

        var memAfter = GC.GetTotalMemory(false);
        var memDelta = memAfter - memBefore;

        _output.WriteLine($"MCTS Memory Profile (lv100_4star_endgame, 10s):");
        _output.WriteLine($"  Memory before: {memBefore / 1024.0:F0} KB");
        _output.WriteLine($"  Memory after:  {memAfter / 1024.0:F0} KB");
        _output.WriteLine($"  Delta:         {memDelta / 1024.0:F0} KB ({memDelta / 1024.0 / 1024.0:F2} MB)");
        _output.WriteLine($"  Iterations:    {result.Metrics.IterationsCompleted}");
        _output.WriteLine($"  Quality:       {result.Metrics.FinalQualityPercent:F1}%");

        if (result.Metrics.IterationsCompleted > 0)
        {
            var bytesPerIter = (double)memDelta / result.Metrics.IterationsCompleted;
            _output.WriteLine($"  ~Bytes/iter:   {bytesPerIter:F0}");
        }
    }
}
