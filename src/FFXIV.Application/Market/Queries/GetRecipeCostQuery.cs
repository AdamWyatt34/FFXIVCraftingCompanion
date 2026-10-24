using FFXIV.Application.Market.DTOs;
using FFXIV.Application.Recipes;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Market.Queries;

public sealed record GetRecipeCostQuery(
    int RecipeId,
    int Quantity,
    string WorldOrDc,
    CostEstimationOptions Options) : IRequest<CostEstimateDto?>;

public sealed class GetRecipeCostQueryHandler : IRequestHandler<GetRecipeCostQuery, CostEstimateDto?>
{
    private readonly IRecipeRepository _recipeRepo;
    private readonly IRecipeTreeResolver _resolver;
    private readonly ICostEstimationService _costService;

    public GetRecipeCostQueryHandler(
        IRecipeRepository recipeRepo,
        IRecipeTreeResolver resolver,
        ICostEstimationService costService)
    {
        _recipeRepo = recipeRepo;
        _resolver = resolver;
        _costService = costService;
    }

    public async Task<CostEstimateDto?> Handle(
        GetRecipeCostQuery request, CancellationToken cancellationToken)
    {
        var recipe = await _recipeRepo.GetByIdAsync(request.RecipeId, cancellationToken);
        if (recipe is null)
            return null;

        var tree = await _resolver.ResolveTreeAsync(
            recipe.ResultItemId, request.Quantity, cancellationToken);

        // Convert shopping list items to AggregatedMaterial for cost estimation
        var materials = tree.ShoppingList.Concat(tree.CrystalRequirements)
            .Select(s => new AggregatedMaterial
            {
                ItemId = s.ItemId,
                ItemName = s.ItemName,
                IconUrl = s.IconUrl,
                TotalQuantity = s.TotalQuantity,
                PrimarySourceType = s.SourceType,
                Sources = s.Sources,
            }).ToList();

        var finalItemId = request.Options.IncludeCraftVsBuyAnalysis
            ? recipe.ResultItemId
            : (int?)null;

        var estimate = await _costService.EstimateCostAsync(
            materials, finalItemId, request.WorldOrDc, request.Options, request.Quantity, cancellationToken);

        return MarketMapper.ToDto(estimate);
    }
}
