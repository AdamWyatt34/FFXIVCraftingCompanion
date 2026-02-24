using FFXIV.Application.CraftPlans.DTOs;

namespace FFXIV.Application.Gathering.DTOs;

public sealed record CraftPlanWithGatheringDto(
    CraftPlanResultDto CraftPlan,
    GatheringGuideDto GatheringGuide);
