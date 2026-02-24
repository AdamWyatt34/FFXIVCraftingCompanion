using FFXIV.Application.Journey.DTOs;
using FFXIV.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFXIV.Application.Journey.Queries;

public sealed record GetItemContextQuery(int ItemId) : IRequest<ItemContextDto?>;

public sealed class GetItemContextQueryHandler
    : IRequestHandler<GetItemContextQuery, ItemContextDto?>
{
    private readonly IContextualLinkService _linkService;
    private readonly ICacheService _cache;
    private readonly ILogger<GetItemContextQueryHandler> _logger;

    public GetItemContextQueryHandler(
        IContextualLinkService linkService,
        ICacheService cache,
        ILogger<GetItemContextQueryHandler> logger)
    {
        _linkService = linkService;
        _cache = cache;
        _logger = logger;
    }

    public async Task<ItemContextDto?> Handle(
        GetItemContextQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"itemcontext:{request.ItemId}";
        var cached = await _cache.GetAsync<ItemContextDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        try
        {
            var links = await _linkService.GetLinksForItemAsync(request.ItemId, cancellationToken);
            var dto = JourneyMapper.ToDto(links);

            await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(30), cancellationToken);
            return dto;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Item {ItemId} not found for context lookup", request.ItemId);
            return null;
        }
    }
}
