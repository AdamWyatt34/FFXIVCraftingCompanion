using FFXIV.Application.Gear.DTOs;
using FFXIV.Domain.Enums;
using MediatR;

namespace FFXIV.Application.Gear.Queries;

public sealed class CalculateGearStatsQueryHandler
    : IRequestHandler<CalculateGearStatsQuery, GearStatsDto>
{
    private readonly IGearRecommendationService _gearService;

    public CalculateGearStatsQueryHandler(IGearRecommendationService gearService)
    {
        _gearService = gearService;
    }

    public async Task<GearStatsDto> Handle(
        CalculateGearStatsQuery request, CancellationToken cancellationToken)
    {
        var equipmentIds = new Dictionary<EquipSlot, int>();

        foreach (var (key, equipmentId) in request.Slots)
        {
            if (Enum.TryParse<EquipSlot>(key, ignoreCase: true, out var slot))
                equipmentIds[slot] = equipmentId;
        }

        var stats = await _gearService.CalculateSetStatsAsync(equipmentIds, cancellationToken);
        return GearMapper.ToDto(stats);
    }
}
