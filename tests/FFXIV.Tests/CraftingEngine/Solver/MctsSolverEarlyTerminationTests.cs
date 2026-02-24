using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Infrastructure.Solver;
using FFXIV.Infrastructure.Solver.MCTS;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine.Solver;

public class MctsSolverEarlyTerminationTests
{
    private readonly CraftingSimulator _simulator = new();

    [Fact]
    public async Task SolveAsync_EasyRecipe_StopsEarlyWhenPerfectQuality()
    {
        // Overpowered crafter on easy recipe → should hit perfect quality quickly
        var stats = new CrafterStats { Level = 100, Craftsmanship = 4500, Control = 4400, CP = 700 };
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

        var config = MCTSConfig.Default with { PerfectRewardThreshold = 0.999 };
        var greedySolver = new GreedySolver(_simulator);
        var solver = new MCTSSolver(_simulator, greedySolver, config);

        var settings = new SolverSettings
        {
            Algorithm = SolverAlgorithm.MCTS,
            TimeBudgetMs = 30000, // Long budget — but should stop early
            RandomSeed = 42,
        };
        var constraints = new SolverConstraints { MaximizeQuality = true };

        var result = await solver.SolveAsync(recipe, stats, constraints, settings);

        result.Status.Should().Be(SolverStatus.Completed);
        result.Simulation.Should().NotBeNull();
        result.Simulation!.Success.Should().BeTrue();
        // Should finish well before the 30s budget due to early termination
        result.Metrics.SolveTimeMs.Should().BeLessThan(15000);
    }

    [Fact]
    public async Task SolveAsync_WithConvergenceThreshold_StopsAfterNoImprovement()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();

        // Very low threshold for test speed
        var config = MCTSConfig.Default with { EarlyTerminationThreshold = 100 };
        var greedySolver = new GreedySolver(_simulator);
        var solver = new MCTSSolver(_simulator, greedySolver, config);

        var settings = new SolverSettings
        {
            Algorithm = SolverAlgorithm.MCTS,
            TimeBudgetMs = 60000, // Very long budget
            RandomSeed = 42,
        };
        var constraints = new SolverConstraints { MaximizeQuality = true };

        var result = await solver.SolveAsync(recipe, stats, constraints, settings);

        result.Status.Should().Be(SolverStatus.Completed);
        // Should have stopped due to convergence, not time
        result.Metrics.SolveTimeMs.Should().BeLessThan(30000);
    }

    [Fact]
    public void MCTSConfig_DefaultEarlyTerminationThreshold_Is5000()
    {
        MCTSConfig.Default.EarlyTerminationThreshold.Should().Be(5000);
    }

    [Fact]
    public void MCTSConfig_DefaultPerfectRewardThreshold_Is0999()
    {
        MCTSConfig.Default.PerfectRewardThreshold.Should().Be(0.999);
    }
}
