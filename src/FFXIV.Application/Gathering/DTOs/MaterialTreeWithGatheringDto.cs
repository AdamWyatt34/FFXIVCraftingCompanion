using FFXIV.Application.Recipes.DTOs;

namespace FFXIV.Application.Gathering.DTOs;

public sealed record MaterialTreeWithGatheringDto(
    MaterialTreeResultDto MaterialTree,
    GatheringGuideDto GatheringGuide);
