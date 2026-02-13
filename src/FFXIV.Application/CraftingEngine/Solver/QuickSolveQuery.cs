using FFXIV.Application.Crafting;
using FFXIV.Application.Crafting.DTOs;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using MediatR;

namespace FFXIV.Application.CraftingEngine.Solver;

public sealed record QuickSolveQuery(
    int RecipeId, int Craftsmanship, int Control, int CP, int Level)
    : IRequest<(SolverResultDto? Result, string? Error)>;

public sealed class QuickSolveQueryHandler
    : IRequestHandler<QuickSolveQuery, (SolverResultDto? Result, string? Error)>
{
    private readonly ISolverFactory _solverFactory;
    private readonly IRecipeParameterFactory _recipeParamFactory;
    private readonly ICraftingSimulator _simulator;

    public QuickSolveQueryHandler(
        ISolverFactory solverFactory,
        IRecipeParameterFactory recipeParamFactory,
        ICraftingSimulator simulator)
    {
        _solverFactory = solverFactory;
        _recipeParamFactory = recipeParamFactory;
        _simulator = simulator;
    }

    public async Task<(SolverResultDto? Result, string? Error)> Handle(
        QuickSolveQuery request, CancellationToken cancellationToken)
    {
        if (request.Level is < 1 or > 100)
            return (null, "Level must be between 1 and 100.");

        if (request.Craftsmanship <= 0 || request.CP <= 0)
            return (null, "Craftsmanship and CP must be positive.");

        if (request.Control < 0)
            return (null, "Control must be non-negative.");

        var recipe = await _recipeParamFactory.CreateAsync(request.RecipeId, cancellationToken);
        if (recipe is null)
            return (null, $"Recipe {request.RecipeId} not found.");

        var stats = new CrafterStats
        {
            Level = request.Level,
            Craftsmanship = request.Craftsmanship,
            Control = request.Control,
            CP = request.CP,
        };

        var solver = _solverFactory.Create(SolverAlgorithm.Greedy);
        var constraints = new SolverConstraints();
        var settings = new SolverSettings
        {
            Algorithm = SolverAlgorithm.Greedy,
            TimeBudgetMs = 5000,
        };

        var result = await solver.SolveAsync(recipe, stats, constraints, settings, cancellationToken);

        // Simulate and generate macros for complete results
        if (result.Status == SolverStatus.Completed && result.ActionIds.Count > 0)
        {
            var simulation = _simulator.Simulate(recipe, stats, result.ActionIds);
            var macros = MacroExporter.ExportToMacro(result.ActionIds);

            result = result with
            {
                Simulation = simulation,
                Macros = macros,
                Metrics = result.Metrics with
                {
                    TotalSteps = result.ActionIds.Count,
                    FinalQualityPercent = simulation.FinalQualityPercent,
                    FinalHQPercent = simulation.FinalHQPercent,
                    CraftCompleted = simulation.Success,
                },
            };
        }

        var dto = SolverMapper.ToDto(result);
        return (dto, null);
    }
}
