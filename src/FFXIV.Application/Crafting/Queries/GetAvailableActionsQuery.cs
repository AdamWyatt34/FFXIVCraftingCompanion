using FFXIV.Application.Crafting.DTOs;
using FFXIV.Application.CraftingEngine;
using FFXIV.Domain.CraftingEngine;
using MediatR;

namespace FFXIV.Application.Crafting.Queries;

public sealed record GetAvailableActionsQuery(AvailableActionsRequest Request)
    : IRequest<(List<ActionAvailabilityDto>? Result, string? Error)>;

public sealed class GetAvailableActionsQueryHandler
    : IRequestHandler<GetAvailableActionsQuery, (List<ActionAvailabilityDto>? Result, string? Error)>
{
    private readonly ICraftingSimulator _simulator;
    private readonly IRecipeParameterFactory _recipeParamFactory;

    public GetAvailableActionsQueryHandler(
        ICraftingSimulator simulator,
        IRecipeParameterFactory recipeParamFactory)
    {
        _simulator = simulator;
        _recipeParamFactory = recipeParamFactory;
    }

    public async Task<(List<ActionAvailabilityDto>? Result, string? Error)> Handle(
        GetAvailableActionsQuery query, CancellationToken cancellationToken)
    {
        var req = query.Request;

        if (req.Stats.Level is < 1 or > 100)
            return (null, "Level must be between 1 and 100.");

        var recipeParams = await _recipeParamFactory.CreateAsync(req.RecipeId, cancellationToken);
        if (recipeParams is null)
            return (null, $"Recipe {req.RecipeId} not found.");

        var state = CraftingMapper.ToDomain(req.State);
        var stats = CraftingMapper.ToDomain(req.Stats);

        var availability = _simulator.GetAvailableActions(state, stats, recipeParams);
        var dtos = availability.Select(CraftingMapper.ToDto).ToList();

        return (dtos, null);
    }
}
