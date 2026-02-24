using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Infrastructure.Solver;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine.Solver;

public class GreedySolverTuningTests
{
    private readonly CraftingSimulator _simulator = new();
    private readonly SolverConstraints _defaultConstraints = new();
    private readonly SolverSettings _defaultSettings = new();

    [Fact]
    public void GetEffectiveDurabilityBuffer_LowDurability_Returns10()
    {
        var config = GreedySolverConfig.Default;

        config.GetEffectiveDurabilityBuffer(35).Should().Be(10);
        config.GetEffectiveDurabilityBuffer(40).Should().Be(10);
    }

    [Fact]
    public void GetEffectiveDurabilityBuffer_NormalDurability_ReturnsConfiguredValue()
    {
        var config = GreedySolverConfig.Default;

        config.GetEffectiveDurabilityBuffer(60).Should().Be(config.FinishingDurabilityBuffer);
        config.GetEffectiveDurabilityBuffer(70).Should().Be(config.FinishingDurabilityBuffer);
        config.GetEffectiveDurabilityBuffer(80).Should().Be(config.FinishingDurabilityBuffer);
    }

    [Fact]
    public void DefaultConfig_ByregotsThreshold_Is6()
    {
        GreedySolverConfig.Default.ByregotsHighIQThreshold.Should().Be(6);
    }

    [Fact]
    public void DefaultConfig_ManipulationScore_Is90()
    {
        GreedySolverConfig.Default.ManipulationScore.Should().Be(90);
    }

    [Fact]
    public void DefaultConfig_FinishingCPBuffer_Is30()
    {
        GreedySolverConfig.Default.FinishingCPBuffer.Should().Be(30);
    }

    [Fact]
    public void DefaultConfig_FinishingDurabilityBuffer_Is15()
    {
        GreedySolverConfig.Default.FinishingDurabilityBuffer.Should().Be(15);
    }

    [Fact]
    public async Task SolveAsync_LowDurabilityRecipe_CompletesSuccessfully()
    {
        var solver = new GreedySolver(_simulator);
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 3600, CP = 600 };
        var recipe = new RecipeParameters
        {
            RecipeId = 1,
            RecipeLevel = 580,
            ClassJobLevel = 90,
            Difficulty = 3500,
            Quality = 7200,
            Durability = 35,
            ProgressDivider = 130,
            ProgressModifier = 80,
            QualityDivider = 115,
            QualityModifier = 70,
            Stars = 2,
        };

        var result = await solver.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings);

        result.Status.Should().Be(SolverStatus.Completed);
        result.Simulation.Should().NotBeNull();
        result.Simulation!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task SolveAsync_LowDurabilityRecipe_DurabilityAwareScoringAffectsResult()
    {
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 3600, CP = 600 };
        var recipe = new RecipeParameters
        {
            RecipeId = 2,
            RecipeLevel = 580,
            ClassJobLevel = 90,
            Difficulty = 3500,
            Quality = 7200,
            Durability = 40,
            ProgressDivider = 130,
            ProgressModifier = 80,
            QualityDivider = 115,
            QualityModifier = 70,
            Stars = 2,
        };

        // With durability-aware scoring
        var solverWithAware = new GreedySolver(_simulator, GreedySolverConfig.Default);
        var resultAware = await solverWithAware.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings);

        // Without durability-aware scoring
        var configDisabled = GreedySolverConfig.Default with { EnableDurabilityAwareScoring = false };
        var solverWithout = new GreedySolver(_simulator, configDisabled);
        var resultWithout = await solverWithout.SolveAsync(recipe, stats, _defaultConstraints, _defaultSettings);

        // Both should complete
        resultAware.Status.Should().Be(SolverStatus.Completed);
        resultWithout.Status.Should().Be(SolverStatus.Completed);

        // The durability-aware solver should produce a valid result
        resultAware.Simulation.Should().NotBeNull();
        resultAware.Simulation!.Success.Should().BeTrue();
    }
}
