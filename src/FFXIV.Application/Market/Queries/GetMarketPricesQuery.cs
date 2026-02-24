using FFXIV.Application.Market.DTOs;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFXIV.Application.Market.Queries;

public sealed record GetMarketPricesQuery(string WorldOrDc, List<int> ItemIds)
    : IRequest<Dictionary<int, MarketBoardDataDto>>;

public sealed class GetMarketPricesQueryHandler
    : IRequestHandler<GetMarketPricesQuery, Dictionary<int, MarketBoardDataDto>>
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(15);

    private readonly IUniversalisClient _universalis;
    private readonly ICacheService _cache;
    private readonly ILogger<GetMarketPricesQueryHandler> _logger;

    public GetMarketPricesQueryHandler(
        IUniversalisClient universalis,
        ICacheService cache,
        ILogger<GetMarketPricesQueryHandler> logger)
    {
        _universalis = universalis;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Dictionary<int, MarketBoardDataDto>> Handle(
        GetMarketPricesQuery request, CancellationToken cancellationToken)
    {
        var result = new Dictionary<int, MarketBoardDataDto>();
        var uncachedIds = new List<int>();

        foreach (var id in request.ItemIds.Distinct())
        {
            var cacheKey = $"universalis:{request.WorldOrDc}:{id}";
            var cached = await _cache.GetAsync<MarketBoardData>(cacheKey, cancellationToken);
            if (cached is not null)
            {
                result[id] = MarketMapper.ToDto(cached);
            }
            else
            {
                uncachedIds.Add(id);
            }
        }

        if (uncachedIds.Count == 0)
            return result;

        _logger.LogDebug("Fetching {Count} uncached market prices for {World}",
            uncachedIds.Count, request.WorldOrDc);

        var fetched = await _universalis.GetItemPricesBatchAsync(
            request.WorldOrDc, uncachedIds, cancellationToken);

        foreach (var (id, data) in fetched)
        {
            result[id] = MarketMapper.ToDto(data);
            var cacheKey = $"universalis:{request.WorldOrDc}:{id}";
            await _cache.SetAsync(cacheKey, data, CacheTtl, cancellationToken);
        }

        return result;
    }
}
