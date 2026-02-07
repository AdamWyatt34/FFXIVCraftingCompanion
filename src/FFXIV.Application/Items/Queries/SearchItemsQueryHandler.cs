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
    private readonly ICacheService _cache;

    public SearchItemsQueryHandler(IItemRepository itemRepository, ICacheService cache)
    {
        _itemRepository = itemRepository;
        _cache = cache;
    }

    public async Task<ItemSearchResultDto> Handle(SearchItemsQuery request, CancellationToken cancellationToken)
    {
        var queryHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(request.Query.ToLowerInvariant())));
        var cacheKey = $"search:{queryHash}:p{request.Page}:s{request.PageSize}";

        var cached = await _cache.GetAsync<ItemSearchResultDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var offset = (request.Page - 1) * request.PageSize;
        var (items, totalCount) = await _itemRepository.SearchByNameAsync(
            request.Query,
            request.PageSize,
            offset,
            cancellationToken);

        var itemDtos = items.Select(i => Mapper.ToDto(i)).ToList();

        var result = new ItemSearchResultDto(
            Items: itemDtos,
            TotalCount: totalCount,
            Page: request.Page,
            PageSize: request.PageSize);

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(15), cancellationToken);
        return result;
    }
}
