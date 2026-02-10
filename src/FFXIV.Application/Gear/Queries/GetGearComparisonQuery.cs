using FFXIV.Application.Gear.DTOs;
using MediatR;

namespace FFXIV.Application.Gear.Queries;

public sealed record GetGearComparisonQuery(
    string UserId,
    string Job,
    int Level,
    string Profile = "balanced") : IRequest<GearComparisonDto>;
