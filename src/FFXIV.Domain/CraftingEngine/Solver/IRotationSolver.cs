using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;

namespace FFXIV.Domain.CraftingEngine.Solver;

/// <summary>
/// Contract for a rotation solver algorithm.
/// Implementations may run for several seconds — call from a background job.
/// </summary>
public interface IRotationSolver
{
    SolverAlgorithm Algorithm { get; }

    /// <summary>
    /// Solve for the best rotation given recipe, stats, and constraints.
    /// </summary>
    Task<SolverResult> SolveAsync(
        RecipeParameters recipe,
        CrafterStats stats,
        SolverConstraints constraints,
        SolverSettings settings,
        CancellationToken cancellationToken = default);
}
