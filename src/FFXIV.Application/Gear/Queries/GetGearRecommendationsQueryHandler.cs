using FFXIV.Application.Gear.DTOs;
using FFXIV.Domain.Interfaces;
using MediatR;

namespace FFXIV.Application.Gear.Queries;

public sealed class GetGearRecommendationsQueryHandler
    : IRequestHandler<GetGearRecommendationsQuery, GearSetDto>
{
    private readonly IGearRecommendationService _gearService;
    private readonly ICacheService _cache;

    public GetGearRecommendationsQueryHandler(
        IGearRecommendationService gearService,
        ICacheService cache)
    {
        _gearService = gearService;
        _cache = cache;
    }

    public async Task<GearSetDto> Handle(
        GetGearRecommendationsQuery request, CancellationToken cancellationToken)
    {
        var job = request.Job.ToUpperInvariant();
        var profile = request.Profile.ToLowerInvariant();
        var craftedSuffix = request.CraftedOnly ? ":crafted" : "";
        var cacheKey = $"gearset:{job}:{request.Level}:{profile}{craftedSuffix}";

        var cached = await _cache.GetAsync<GearSetDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var gearSet = await _gearService.GetRecommendationsAsync(
            job, request.Level, profile, request.CraftedOnly, cancellationToken);

        var dto = GearMapper.ToDto(gearSet);

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromHours(24), cancellationToken);
        return dto;
    }
}
