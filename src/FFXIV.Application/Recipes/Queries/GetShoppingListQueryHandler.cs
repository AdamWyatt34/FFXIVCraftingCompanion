using FFXIV.Application.Recipes.DTOs;
using MediatR;

namespace FFXIV.Application.Recipes.Queries;

public sealed class GetShoppingListQueryHandler
    : IRequestHandler<GetShoppingListQuery, ShoppingListResultDto?>
{
    private readonly IMediator _mediator;

    public GetShoppingListQueryHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task<ShoppingListResultDto?> Handle(
        GetShoppingListQuery request, CancellationToken cancellationToken)
    {
        var fullResult = await _mediator.Send(
            new GetMaterialTreeQuery(request.RecipeId, request.Quantity),
            cancellationToken);

        if (fullResult is null)
            return null;

        return new ShoppingListResultDto(
            ShoppingList: fullResult.ShoppingList,
            CrystalRequirements: fullResult.CrystalRequirements,
            IntermediateCrafts: fullResult.IntermediateCrafts,
            Summary: fullResult.Summary);
    }
}
