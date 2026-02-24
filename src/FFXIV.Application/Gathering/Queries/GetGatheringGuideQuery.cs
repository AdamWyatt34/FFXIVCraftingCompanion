using FFXIV.Application.Gathering.DTOs;
using MediatR;

namespace FFXIV.Application.Gathering.Queries;

public sealed record GetGatheringGuideQuery(
    List<GatheredMaterialInput> Materials) : IRequest<GatheringGuideDto>;
