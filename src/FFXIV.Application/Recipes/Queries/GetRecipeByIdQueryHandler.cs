using FFXIV.Application.DTOs;
using FFXIV.Application.Mapping;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Recipes.Queries;

public sealed class GetRecipeByIdQueryHandler : IRequestHandler<GetRecipeByIdQuery, RecipeDto?>
{
    private readonly IRecipeRepository _recipeRepository;
    private readonly IEquipmentRepository _equipmentRepository;
    private readonly IRecipeTreeResolver _resolver;
    private readonly ICacheService _cache;

    public GetRecipeByIdQueryHandler(
        IRecipeRepository recipeRepository,
        IEquipmentRepository equipmentRepository,
        IRecipeTreeResolver resolver,
        ICacheService cache)
    {
        _recipeRepository = recipeRepository;
        _equipmentRepository = equipmentRepository;
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

        // Check if this recipe produces equipment
        var equipment = await _equipmentRepository.GetByRecipeIdAsync(request.RecipeId, cancellationToken);
        if (equipment is not null)
        {
            dto = dto with
            {
                EquipmentInfo = new RecipeEquipmentInfoDto(
                    EquipmentId: equipment.EquipmentId,
                    EquipSlot: equipment.EquipSlot.ToString(),
                    ClassJobCategory: equipment.ClassJobCategory,
                    Craftsmanship: equipment.Craftsmanship,
                    Control: equipment.Control,
                    CP: equipment.CP,
                    Gathering: equipment.Gathering,
                    Perception: equipment.Perception,
                    GP: equipment.GP)
            };
        }

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
