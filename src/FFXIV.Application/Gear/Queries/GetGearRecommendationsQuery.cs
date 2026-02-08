using FFXIV.Application.Gear.DTOs;
using MediatR;

namespace FFXIV.Application.Gear.Queries;

public sealed record GetGearRecommendationsQuery(
    string Job,
    int Level,
    string Profile = "balanced") : IRequest<GearSetDto>;
