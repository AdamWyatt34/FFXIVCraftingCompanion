using FFXIV.Domain.CraftingEngine.Solver;
using FFXIV.Domain.CraftingEngine.Solver.Models;

namespace FFXIV.Application.CraftingEngine.Solver;

public interface ISolverFactory
{
    IRotationSolver Create(SolverAlgorithm algorithm);
    SolverAlgorithm ResolveAlgorithm(SolverAlgorithm requested, SolverConstraints constraints);
}
