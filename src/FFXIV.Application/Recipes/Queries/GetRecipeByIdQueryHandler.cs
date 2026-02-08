using FFXIV.Application.DTOs;
using FFXIV.Application.Mapping;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Recipes.Queries;

public sealed class GetRecipeByIdQueryHandler : IRequestHandler<GetRecipeByIdQuery, RecipeDto?>
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly IRecipeTreeResolver _resolver;
    private readonly ICacheService _cache;

    public GetRecipeByIdQueryHandler(
        IRecipeRepository recipeRepository,
        IRecipeTreeResolver resolver,
        ICacheService cache)
    {
        _recipeRepository = recipeRepository;
        _resolver = resolver;
        _cache = cache;
    }

    public async Task<RecipeDto?> Handle(GetRecipeByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = request.IncludeMaterials
            ? $"recipe:{request.RecipeId}:full"
            : $"recipe:{request.RecipeId}";

        var cached = await _cache.GetAsync<RecipeDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var recipe = await _recipeRepository.GetByIdAsync(request.RecipeId, cancellationToken);
        if (recipe is null)
            return null;

        var ingredients = recipe.Ingredients
            .Select(ri => Mapper.ToDto(ri, ri.Item))
            .ToList();

        var dto = Mapper.ToDto(recipe, recipe.ResultItem, ingredients);

        if (request.IncludeMaterials)
        {
            var treeResult = await _resolver.ResolveTreeAsync(
                recipe.ResultItemId, 1, cancellationToken);
            dto = dto with { MaterialSummary = MaterialTreeMapper.ToSummary(treeResult) };
        }

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromHours(24), cancellationToken);
        return dto;
    }
}
