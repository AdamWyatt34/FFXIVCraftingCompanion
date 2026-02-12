using FFXIV.Application.Crafting.DTOs;
using FFXIV.Application.CraftingEngine;
using FFXIV.Domain.CraftingEngine;
using MediatR;

namespace FFXIV.Application.Crafting.Commands;

public sealed record ExecuteSingleActionCommand(ExecuteActionRequest Request)
    : IRequest<(StepResultDto? Result, string? Error)>;

public sealed class ExecuteSingleActionCommandHandler
    : IRequestHandler<ExecuteSingleActionCommand, (StepResultDto? Result, string? Error)>
{
    private readonly ICraftingSimulator _simulator;
    private readonly IRecipeParameterFactory _recipeParamFactory;

    public ExecuteSingleActionCommandHandler(
        ICraftingSimulator simulator,
        IRecipeParameterFactory recipeParamFactory)
    {
        _simulator = simulator;
        _recipeParamFactory = recipeParamFactory;
    }

    public async Task<(StepResultDto? Result, string? Error)> Handle(
        ExecuteSingleActionCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        if (req.Stats.Level is < 1 or > 100)
            return (null, "Level must be between 1 and 100.");

        if (CraftingActionRegistry.GetById(req.ActionId) is null)
            return (null, $"Action ID {req.ActionId} not found.");

        var recipeParams = await _recipeParamFactory.CreateAsync(req.RecipeId, cancellationToken);
        if (recipeParams is null)
            return (null, $"Recipe {req.RecipeId} not found.");

        var state = CraftingMapper.ToDomain(req.State);
        var stats = CraftingMapper.ToDomain(req.Stats);

        var result = _simulator.ExecuteAction(state, req.ActionId, stats, recipeParams);
        var dto = CraftingMapper.ToStepDto(result);

        return (dto, null);
    }
}
