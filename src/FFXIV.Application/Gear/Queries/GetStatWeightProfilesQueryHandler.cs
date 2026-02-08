using FFXIV.Application.Gear.DTOs;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Interfaces;
using MediatR;

namespace FFXIV.Application.Gear.Queries;

public sealed class GetStatWeightProfilesQueryHandler
    : IRequestHandler<GetStatWeightProfilesQuery, List<StatWeightProfileDto>>
{
    private readonly IGearRecommendationService _gearService;
    private readonly ICacheService _cache;

    public GetStatWeightProfilesQueryHandler(
        IGearRecommendationService gearService,
        ICacheService cache)
    {
        _gearService = gearService;
        _cache = cache;
    }

    public async Task<List<StatWeightProfileDto>> Handle(
        GetStatWeightProfilesQuery request, CancellationToken cancellationToken)
    {
        var job = request.Job?.ToUpperInvariant() ?? "CRP";
        var cacheKey = $"gearprofiles:{job}";

        var cached = await _cache.GetAsync<List<StatWeightProfileDto>>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var profiles = _gearService.GetAvailableProfiles(job);
        var dtos = profiles.Select(GearMapper.ToDto).ToList();

        await _cache.SetAsync(cacheKey, dtos, TimeSpan.FromDays(7), cancellationToken);
        return dtos;
    }
}
