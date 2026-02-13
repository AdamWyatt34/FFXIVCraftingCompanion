using FFXIV.Application.CraftingEngine.Solver;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Solver;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Infrastructure.Solver.MCTS;

namespace FFXIV.Infrastructure.Solver;

public sealed class SolverFactory : ISolverFactory
{
    private readonly ICraftingSimulator _simulator;
    private readonly GreedySolverConfig _greedyConfig;
    private readonly MCTSConfig _mctsConfig;

    public SolverFactory(
        ICraftingSimulator simulator,
        GreedySolverConfig? greedyConfig = null,
        MCTSConfig? mctsConfig = null)
    {
        _simulator = simulator;
        _greedyConfig = greedyConfig ?? GreedySolverConfig.Default;
        _mctsConfig = mctsConfig ?? MCTSConfig.Default;
    }

    public SolverAlgorithm ResolveAlgorithm(
        SolverAlgorithm requested, SolverConstraints constraints)
    {
        if (requested != SolverAlgorithm.Auto)
            return requested;

        return constraints.MaximizeQuality || constraints.TargetQualityPercent.HasValue
            ? SolverAlgorithm.MCTS
            : SolverAlgorithm.Greedy;
    }

    public IRotationSolver Create(SolverAlgorithm algorithm) => algorithm switch
    {
        SolverAlgorithm.Greedy => new GreedySolver(_simulator, _greedyConfig),
        SolverAlgorithm.MCTS => new MCTSSolver(
            _simulator, new GreedySolver(_simulator, _greedyConfig), _mctsConfig),
        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unknown solver algorithm"),
    };
}
