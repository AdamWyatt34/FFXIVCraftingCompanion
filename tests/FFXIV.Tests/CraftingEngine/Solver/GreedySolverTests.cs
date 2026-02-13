using System.Diagnostics;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Infrastructure.Solver;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine.Solver;

public class GreedySolverTests
{
    private readonly CraftingSimulator _simulator = new();
    private readonly GreedySolver _solver;
    private readonly SolverConstraints _defaultConstraints = new();
    private readonly SolverSettings _defaultSettings = new();

    public GreedySolverTests()
    {
        _solver = new GreedySolver(_simulator);
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

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings);

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

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings);

        result.Status.Should().Be(SolverStatus.Completed);
        result.Simulation.Should().NotBeNull();
        result.Simulation!.Success.Should().BeTrue();
        result.Metrics.FinalQualityPercent.Should().BeGreaterThanOrEqualTo(10);
        result.ActionIds.Count.Should().BeLessThanOrEqualTo(50);
    }

    [Fact]
    public async Task SolveAsync_HighEndRecipe_CompletesSuccessfully()
    {
        var stats = new CrafterStats { Level = 100, Craftsmanship = 4500, Control = 4400, CP = 700 };
        var recipe = new RecipeParameters
        {
            RecipeId = 200,
            RecipeLevel = 690,
            ClassJobLevel = 100,
            Difficulty = 6600,
            Quality = 12000,
            Durability = 70,
            Stars = 4,
            ProgressDivider = 150,
            ProgressModifier = 80,
            QualityDivider = 130,
            QualityModifier = 70,
        };

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings);

        result.Status.Should().Be(SolverStatus.Completed);
        result.Simulation.Should().NotBeNull();
        result.Simulation!.Success.Should().BeTrue();
        result.Metrics.CraftCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task SolveAsync_TrainedEyeEligible_UsesTrainedEyeAndMaxesQuality()
    {
        var stats = new CrafterStats { Level = 100, Craftsmanship = 4500, Control = 4400, CP = 700 };
        var recipe = new RecipeParameters
        {
            RecipeId = 300,
            RecipeLevel = 300,
            ClassJobLevel = 80, // Level 100 crafter >= 80 + 10 = eligible
            Difficulty = 2000,
            Quality = 5000,
            Durability = 70,
            ProgressDivider = 110,
            ProgressModifier = 100,
            QualityDivider = 100,
            QualityModifier = 100,
        };

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings);

        result.Status.Should().Be(SolverStatus.Completed);
        result.Simulation.Should().NotBeNull();
        result.Simulation!.Success.Should().BeTrue();

        // Trained Eye should be the first action (ID 100283)
        result.ActionIds.Should().NotBeEmpty();
        result.ActionIds[0].Should().Be(100283);

        // Quality should be maxed
        result.Simulation.FinalState.Quality.Should().Be(recipe.Quality);
    }

    [Fact]
    public async Task SolveAsync_VeryLowCP_DoesNotCrashAndProducesValidResult()
    {
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 3600, CP = 100 };
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings);

        result.Status.Should().Be(SolverStatus.Completed);
        result.Simulation.Should().NotBeNull();
        result.ActionIds.Should().NotBeEmpty();
        // With very low CP, the craft may not complete or have low quality, but shouldn't crash
    }

    [Fact]
    public async Task SolveAsync_CompletesWithinTimeBudget()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();

        var stopwatch = Stopwatch.StartNew();
        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings);
        stopwatch.Stop();

        result.Status.Should().Be(SolverStatus.Completed);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(100);
    }

    [Fact]
    public async Task SolveAsync_SimulationConsistency_ResultMatchesReplay()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings);

        result.Simulation.Should().NotBeNull();

        // Re-simulate the action sequence independently
        var replay = _simulator.Simulate(recipe, stats, result.ActionIds);

        replay.Success.Should().Be(result.Simulation!.Success);
        replay.FinalState.Progress.Should().Be(result.Simulation.FinalState.Progress);
        replay.FinalState.Quality.Should().Be(result.Simulation.FinalState.Quality);
        replay.TotalSteps.Should().Be(result.Simulation.TotalSteps);
    }

    [Fact]
    public async Task SolveAsync_ExpertRecipe_ReturnsFailedWithMessage()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = new RecipeParameters
        {
            RecipeId = 400,
            RecipeLevel = 580,
            ClassJobLevel = 90,
            Difficulty = 5000,
            Quality = 10000,
            Durability = 70,
            IsExpert = true,
            ConditionsFlag = 0x3F,
            ProgressDivider = 130,
            ProgressModifier = 80,
            QualityDivider = 115,
            QualityModifier = 70,
        };

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings);

        result.Status.Should().Be(SolverStatus.Failed);
        result.ErrorMessage.Should().Contain("Expert recipes");
        result.ActionIds.Should().BeEmpty();
    }

    [Fact]
    public async Task SolveAsync_GeneratesMacros()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings);

        result.Status.Should().Be(SolverStatus.Completed);
        result.Macros.Should().NotBeEmpty();
        result.Macros[0].Should().Contain("/ac");
    }

    [Fact]
    public async Task SolveAsync_CancellationToken_ThrowsWhenCancelled()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _solver.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task SolveAsync_MetricsArePopulated()
    {
        var stats = CraftingTestHelpers.CreateDefaultStats();
        var recipe = CraftingTestHelpers.CreateDefaultRecipe();

        var result = await _solver.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings);

        result.Metrics.AlgorithmUsed.Should().Be(SolverAlgorithm.Greedy);
        result.Metrics.TotalSteps.Should().BeGreaterThan(0);
        result.Metrics.SolveTimeMs.Should().BeGreaterThanOrEqualTo(0);
        result.Metrics.IterationsCompleted.Should().Be(1);
        result.Metrics.NodesExplored.Should().Be(result.ActionIds.Count);
    }
}
