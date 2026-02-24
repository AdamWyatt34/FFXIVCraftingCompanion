using FFXIV.Application.Crafting.DTOs;
using FFXIV.Application.CraftingEngine;
using FFXIV.Application.Recipes.DTOs;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.Repositories;
using MediatR;

// HQIngredientInput used for starting quality calculation

namespace FFXIV.Application.Crafting.Commands;

public sealed record SimulateCraftCommand(SimulateRequest Request, string? UserId)
    : IRequest<(SimulationResultDto? Result, string? Error)>;

public sealed class SimulateCraftCommandHandler
    : IRequestHandler<SimulateCraftCommand, (SimulationResultDto? Result, string? Error)>
{
    private readonly ICraftingSimulator _simulator;
    private readonly IRecipeParameterFactory _recipeParamFactory;
    private readonly IRecipeRepository _recipeRepository;
    private readonly ICharacterProfileRepository _profileRepository;

    public SimulateCraftCommandHandler(
        ICraftingSimulator simulator,
        IRecipeParameterFactory recipeParamFactory,
        IRecipeRepository recipeRepository,
        ICharacterProfileRepository profileRepository)
    {
        _simulator = simulator;
        _recipeParamFactory = recipeParamFactory;
        _recipeRepository = recipeRepository;
        _profileRepository = profileRepository;
    }

    public async Task<(SimulationResultDto? Result, string? Error)> Handle(
        SimulateCraftCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        // Resolve actions (prefer ActionIds, fall back to ActionNames)
        var (actionIds, actionError) = CraftingMapper.ResolveActionIds(req.ActionIds, req.ActionNames);
        if (actionError is not null)
            return (null, actionError);

        if (actionIds.Count == 0)
            return (null, "At least one action is required (provide ActionIds or ActionNames).");

        if (actionIds.Count > 100)
            return (null, "Action sequence exceeds maximum of 100 actions.");

        // Resolve recipe parameters
        var recipeParams = await _recipeParamFactory.CreateAsync(req.RecipeId, cancellationToken);
        if (recipeParams is null)
            return (null, $"Recipe {req.RecipeId} not found.");

        // Apply HQ ingredient starting quality
        if (req.HQIngredients is { Count: > 0 })
        {
            var hqInputs = req.HQIngredients
                .Select(h => new HQIngredientInput(h.ItemId, h.HQQuantity))
                .ToList();

            var startingQuality = HQCalculator.CalculateStartingQuality(
                recipeParams.Quality, recipeParams.MaterialQualityFactor,
                recipeParams.Ingredients, hqInputs);

            if (startingQuality > 0)
                recipeParams = recipeParams with { StartingQuality = startingQuality };
        }

        // Resolve crafter stats
        CrafterStats stats;
        if (req.UseProfileStats)
        {
            if (string.IsNullOrEmpty(command.UserId))
                return (null, "UseProfileStats requires authentication.");

            var profile = await _profileRepository.GetByUserIdAsync(command.UserId, cancellationToken);
            if (profile is null)
                return (null, "No character profile found. Create a profile first.");

            stats = new CrafterStats
            {
                Level = profile.ActiveJobLevel,
                Craftsmanship = profile.Craftsmanship,
                Control = profile.Control,
                CP = profile.CP,
                IsSpecialist = false,
            };
        }
        else
        {
            if (req.Stats is null)
                return (null, "Either provide Stats or set UseProfileStats to true.");

            if (req.Stats.Level is < 1 or > 100)
                return (null, "Level must be between 1 and 100.");
            if (req.Stats.Craftsmanship < 0 || req.Stats.Control < 0 || req.Stats.CP < 0)
                return (null, "Stats must be non-negative.");

            stats = CraftingMapper.ToDomain(req.Stats);
        }

        // Run simulation
        var options = CraftingMapper.ToDomain(req.Options);
        var result = _simulator.Simulate(recipeParams, stats, actionIds, options);

        // Generate macros
        var macros = MacroExporter.ExportToMacro(actionIds);

        // Build recipe summary
        var recipe = await _recipeRepository.GetByIdAsync(req.RecipeId, cancellationToken);
        var recipeSummary = new RecipeSummaryDto(
            recipe!.RecipeId,
            recipe.CraftType.ToString(),
            recipe.CraftType.ToString(),
            recipe.RecipeLevel,
            recipe.RequiredCraftsmanship,
            recipe.RequiredControl,
            recipe.Yield);

        var dto = CraftingMapper.ToDto(result, recipeSummary, macros);
        return (dto, null);
    }
}
