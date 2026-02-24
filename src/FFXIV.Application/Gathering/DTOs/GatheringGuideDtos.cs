namespace FFXIV.Application.Gathering.DTOs;

public sealed record GatheringGuideDto(
    List<GatheringZoneGroupDto> ZoneGroups,
    List<TimedNodeEntryDto> TimedNodes,
    List<CrystalRequirementDto> CrystalSummary,
    GatheringGuideSummaryDto Summary);

public sealed record GatheringZoneGroupDto(
    string ZoneName,
    List<GatheringGuideItemDto> Items);

public sealed record GatheringGuideItemDto(
    int ItemId,
    string ItemName,
    string IconUrl,
    int Quantity,
    string GatheringClass,
    int GatheringLevel,
    string NodeType,
    float? CoordinateX,
    float? CoordinateY,
    EorzeanTimeWindowDto? TimeWindow,
    bool IsHidden);

public sealed record EorzeanTimeWindowDto(
    int StartHour,
    int EndHour,
    bool IsCurrentlyActive,
    string? TimeUntil);

public sealed record TimedNodeEntryDto(
    int ItemId,
    string ItemName,
    string IconUrl,
    int Quantity,
    string ZoneName,
    string GatheringClass,
    int GatheringLevel,
    string NodeType,
    int StartHour,
    int EndHour,
    bool IsCurrentlyActive,
    string? TimeUntil,
    float? CoordinateX,
    float? CoordinateY);

public sealed record CrystalRequirementDto(
    int ItemId,
    string ItemName,
    string IconUrl,
    int Quantity,
    string Element,
    string Tier);

public sealed record GatheringGuideSummaryDto(
    int TotalZones,
    int TotalItems,
    int TimedNodeCount,
    int TotalCrystalTypes);

public sealed record GenerateGatheringGuideRequest(
    List<GatheringGuideItemInput> Items);

public sealed record GatheringGuideItemInput(
    int ItemId,
    int Quantity);
