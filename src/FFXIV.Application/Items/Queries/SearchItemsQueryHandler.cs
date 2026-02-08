using System.Security.Cryptography;
using System.Text;
using FFXIV.Application.DTOs;
using FFXIV.Application.Mapping;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Items.Queries;

public sealed class SearchItemsQueryHandler : IRequestHandler<SearchItemsQuery, ItemSearchResultDto>
{
    private readonly IItemRepository _itemRepository;
    private readonly IRecipeRepository _recipeRepository;
    private readonly ICacheService _cache;

    public SearchItemsQueryHandler(IItemRepository itemRepository, IRecipeRepository recipeRepository, ICacheService cache)
    {
        _itemRepository = itemRepository;
        _recipeRepository = recipeRepository;
        _cache = cache;
    }

    public async Task<ItemSearchResultDto> Handle(SearchItemsQuery request, CancellationToken cancellationToken)
    {
        var queryPart = request.Query?.ToLowerInvariant() ?? "_all";
        var queryHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(queryPart)));
        var craftTypePart = request.CraftType?.ToString() ?? "any";
        var cacheKey = $"search:{queryHash}:ct{craftTypePart}:p{request.Page}:s{request.PageSize}";

        var cached = await _cache.GetAsync<ItemSearchResultDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var offset = (request.Page - 1) * request.PageSize;
        var (items, totalCount) = await _itemRepository.SearchByNameAsync(
            request.Query,
            request.PageSize,
            offset,
            request.CraftType,
            cancellationToken);

        // Batch lookup recipe IDs for all returned items
        // When filtering by craft type, prefer the recipe matching that type
        var itemIds = items.Select(i => i.ItemId);
        var recipes = await _recipeRepository.GetByResultItemIdsAsync(itemIds, cancellationToken);
        var recipeByItemId = recipes
            .Where(r => r.RecipeId > 0)
            .GroupBy(r => r.ResultItemId)
            .ToDictionary(
                g => g.Key,
                g => request.CraftType.HasValue
                    ? (g.FirstOrDefault(r => r.CraftType == request.CraftType.Value) ?? g.First()).RecipeId
                    : g.First().RecipeId);

        var itemDtos = items.Select(i =>
            Mapper.ToDto(i, recipeByItemId.TryGetValue(i.ItemId, out var rid) ? rid : null)
        ).ToList();

        var result = new ItemSearchResultDto(
            Items: itemDtos,
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize);

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15), cancellationToken);
        return result;
    }
}
