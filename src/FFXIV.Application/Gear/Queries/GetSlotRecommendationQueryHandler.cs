using FFXIV.Application.Gear.DTOs;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using MediatR;

namespace FFXIV.Application.Gear.Queries;

public sealed class GetSlotRecommendationQueryHandler
    : IRequestHandler<GetSlotRecommendationQuery, GearSlotDto?>
{
    private readonly IGearRecommendationService _gearService;
    private readonly ICacheService _cache;

    public GetSlotRecommendationQueryHandler(
        IGearRecommendationService gearService,
        ICacheService cache)
    {
        _gearService = gearService;
        _cache = cache;
    }

    public async Task<GearSlotDto?> Handle(
        GetSlotRecommendationQuery request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<EquipSlot>(request.Slot, ignoreCase: true, out var slot))
            return null;

        var job = request.Job.ToUpperInvariant();
        var profile = request.Profile.ToLowerInvariant();
        var cacheKey = $"gearslot:{slot}:{job}:{request.Level}:{profile}";

        var cached = await _cache.GetAsync<GearSlotDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var recommendation = await _gearService.GetSlotRecommendationAsync(
            slot, job, request.Level, profile, cancellationToken);

        var dto = GearMapper.ToDto(recommendation);

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromHours(24), cancellationToken);
        return dto;
    }
}
