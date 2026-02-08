using FFXIV.Application.Gear.DTOs;
using MediatR;

namespace FFXIV.Application.Gear.Queries;

public sealed record CalculateGearStatsQuery(
    Dictionary<string, int> Slots) : IRequest<GearStatsDto>;
