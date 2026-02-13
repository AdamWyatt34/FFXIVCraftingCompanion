using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Infrastructure.Solver;
using FFXIV.Infrastructure.Solver.MCTS;

namespace FFXIV.Tests.CraftingEngine.Solver;

public class SolverFactoryTests
{
    private readonly ICraftingSimulator _simulator = new CraftingSimulator();
    private readonly SolverFactory _factory;

    public SolverFactoryTests()
    {
        _factory = new SolverFactory(_simulator);
    }

    [Fact]
    public void ResolveAlgorithm_Auto_WithMaximizeQuality_ReturnsMCTS()
    {
        var constraints = new SolverConstraints { MaximizeQuality = true };
        var result = _factory.ResolveAlgorithm(SolverAlgorithm.Auto, constraints);
        result.Should().Be(SolverAlgorithm.MCTS);
    }

    [Fact]
    public void ResolveAlgorithm_Auto_WithTargetQualityPercent_ReturnsMCTS()
    {
        var constraints = new SolverConstraints
        {
            MaximizeQuality = false,
            TargetQualityPercent = 100,
        };
        var result = _factory.ResolveAlgorithm(SolverAlgorithm.Auto, constraints);
        result.Should().Be(SolverAlgorithm.MCTS);
    }

    [Fact]
    public void ResolveAlgorithm_Auto_WithoutQualityGoal_ReturnsGreedy()
    {
        var constraints = new SolverConstraints
        {
            MaximizeQuality = false,
            TargetQualityPercent = null,
        };
        var result = _factory.ResolveAlgorithm(SolverAlgorithm.Auto, constraints);
        result.Should().Be(SolverAlgorithm.Greedy);
    }

    [Fact]
    public void ResolveAlgorithm_ExplicitGreedy_ReturnsGreedy()
    {
        var constraints = new SolverConstraints { MaximizeQuality = true };
        var result = _factory.ResolveAlgorithm(SolverAlgorithm.Greedy, constraints);
        result.Should().Be(SolverAlgorithm.Greedy);
    }

    [Fact]
    public void ResolveAlgorithm_ExplicitMCTS_ReturnsMCTS()
    {
        var constraints = new SolverConstraints { MaximizeQuality = false };
        var result = _factory.ResolveAlgorithm(SolverAlgorithm.MCTS, constraints);
        result.Should().Be(SolverAlgorithm.MCTS);
    }

    [Fact]
    public void Create_Greedy_ReturnsGreedySolver()
    {
        var solver = _factory.Create(SolverAlgorithm.Greedy);
        solver.Should().BeOfType<GreedySolver>();
        solver.Algorithm.Should().Be(SolverAlgorithm.Greedy);
    }

    [Fact]
    public void Create_MCTS_ReturnsMCTSSolver()
    {
        var solver = _factory.Create(SolverAlgorithm.MCTS);
        solver.Should().BeOfType<MCTSSolver>();
        solver.Algorithm.Should().Be(SolverAlgorithm.MCTS);
    }
}
