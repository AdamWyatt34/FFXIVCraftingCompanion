using FFXIV.Application.Gear.DTOs;
using MediatR;

namespace FFXIV.Application.Gear.Queries;

public sealed record GetStatWeightProfilesQuery(
    string? Job = null) : IRequest<List<StatWeightProfileDto>>;
