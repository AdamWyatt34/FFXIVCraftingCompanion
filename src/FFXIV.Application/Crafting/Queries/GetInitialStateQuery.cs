using FFXIV.Application.Crafting.DTOs;
using FFXIV.Application.CraftingEngine;
using FFXIV.Domain.CraftingEngine;
using MediatR;

namespace FFXIV.Application.Crafting.Queries;

public sealed record GetInitialStateQuery(int RecipeId, CrafterStatsInput Stats)
    : IRequest<(CraftingStateDto? Result, string? Error)>;

public sealed class GetInitialStateQueryHandler
    : IRequestHandler<GetInitialStateQuery, (CraftingStateDto? Result, string? Error)>
{
    private readonly ICraftingSimulator _simulator;
    private readonly IRecipeParameterFactory _recipeParamFactory;

    public GetInitialStateQueryHandler(
        ICraftingSimulator simulator,
        IRecipeParameterFactory recipeParamFactory)
    {
        _simulator = simulator;
        _recipeParamFactory = recipeParamFactory;
    }

    public async Task<(CraftingStateDto? Result, string? Error)> Handle(
        GetInitialStateQuery request, CancellationToken cancellationToken)
    {
        if (request.Stats.Level is < 1 or > 100)
            return (null, "Level must be between 1 and 100.");

        var recipeParams = await _recipeParamFactory.CreateAsync(request.RecipeId, cancellationToken);
        if (recipeParams is null)
            return (null, $"Recipe {request.RecipeId} not found.");

        var stats = CraftingMapper.ToDomain(request.Stats);
        var state = _simulator.CreateInitialState(recipeParams, stats);
        var dto = CraftingMapper.ToDto(state);

        return (dto, null);
    }
}
