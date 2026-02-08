using FFXIV.Application.Gear.DTOs;
using MediatR;

namespace FFXIV.Application.Gear.Queries;

public sealed record GetSlotRecommendationQuery(
    string Slot,
    string Job,
    int Level,
    string Profile = "balanced") : IRequest<GearSlotDto?>;
