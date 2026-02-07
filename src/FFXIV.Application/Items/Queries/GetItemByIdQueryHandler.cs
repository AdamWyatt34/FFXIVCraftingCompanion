using FFXIV.Application.DTOs;
using FFXIV.Application.Mapping;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Items.Queries;

public sealed class GetItemByIdQueryHandler : IRequestHandler<GetItemByIdQuery, ItemDto?>
{
    private readonly IItemRepository _itemRepository;
    private readonly IRecipeRepository _recipeRepository;
    private readonly ICacheService _cache;

    public GetItemByIdQueryHandler(
        IItemRepository itemRepository,
        IRecipeRepository recipeRepository,
        ICacheService cache)
    {
        _itemRepository = itemRepository;
        _recipeRepository = recipeRepository;
        _cache = cache;
    }

    public async Task<ItemDto?> Handle(GetItemByIdQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"item:{request.ItemId}";
        var cached = await _cache.GetAsync<ItemDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var item = await _itemRepository.GetByIdAsync(request.ItemId, cancellationToken);
        if (item is null)
            return null;

        var recipe = await _recipeRepository.GetByResultItemIdAsync(item.ItemId, cancellationToken);
        var dto = Mapper.ToDto(item, recipe?.RecipeId);

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromHours(1), cancellationToken);
        return dto;
    }
}
