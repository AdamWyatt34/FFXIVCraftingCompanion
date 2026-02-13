using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;

namespace FFXIV.Tests.CraftingEngine.Solver;

public class SolverModelsTests
{
    [Fact]
    public void SolverConstraints_HasCorrectDefaults()
    {
        var constraints = new SolverConstraints();

        constraints.MustComplete.Should().BeTrue();
        constraints.MaximizeQuality.Should().BeTrue();
        constraints.MinimizeSteps.Should().BeFalse();
        constraints.TargetQualityPercent.Should().BeNull();
        constraints.AllowUnsafeActions.Should().BeFalse();
        constraints.MaxSteps.Should().Be(50);
    }

    [Fact]
    public void SolverSettings_HasCorrectDefaults()
    {
        var settings = new SolverSettings();

        settings.Algorithm.Should().Be(SolverAlgorithm.Auto);
        settings.TimeBudgetMs.Should().Be(10000);
        settings.RandomSeed.Should().BeNull();
    }

    [Fact]
    public void SolveRequest_CanBeConstructed()
    {
        var stats = new CrafterStats
        {
            Level = 90,
            Craftsmanship = 3800,
            Control = 3600,
            CP = 600,
        };

        var request = new SolveRequest
        {
            RecipeId = 100,
            Stats = stats,
        };

        request.RecipeId.Should().Be(100);
        request.Stats.Should().Be(stats);
        request.Constraints.Should().NotBeNull();
        request.Settings.Should().NotBeNull();
    }

    [Fact]
    public void SolverResult_HasEmptyDefaults()
    {
        var result = new SolverResult();

        result.JobId.Should().BeEmpty();
        result.ActionIds.Should().BeEmpty();
        result.Macros.Should().BeEmpty();
        result.Simulation.Should().BeNull();
        result.ErrorMessage.Should().BeNull();
        result.Metrics.Should().NotBeNull();
    }

    [Fact]
    public void SolverMetrics_HasZeroDefaults()
    {
        var metrics = new SolverMetrics();

        metrics.AlgorithmUsed.Should().Be(SolverAlgorithm.Auto);
        metrics.SolveTimeMs.Should().Be(0);
        metrics.TotalSteps.Should().Be(0);
        metrics.FinalQualityPercent.Should().Be(0);
        metrics.FinalHQPercent.Should().Be(0);
        metrics.CraftCompleted.Should().BeFalse();
        metrics.NodesExplored.Should().Be(0);
        metrics.IterationsCompleted.Should().Be(0);
    }
}
