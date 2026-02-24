using FFXIV.Application.Gathering.DTOs;
using FFXIV.Domain.Models;
using FFXIV.Domain.Utilities;

namespace FFXIV.Application.Gathering;

public static class GatheringGuideMapper
{
    public static GatheringGuideDto ToDto(GatheringGuide guide)
    {
        return new GatheringGuideDto(
            ZoneGroups: guide.ZoneGroups.Select(MapZoneGroup).ToList(),
            TimedNodes: guide.TimedNodes.Select(MapTimedNode).ToList(),
            CrystalSummary: guide.CrystalSummary.Select(MapCrystal).ToList(),
            Summary: new GatheringGuideSummaryDto(
                guide.Summary.TotalZones,
                guide.Summary.TotalItems,
                guide.Summary.TimedNodeCount,
                guide.Summary.TotalCrystalTypes));
    }

    private static GatheringZoneGroupDto MapZoneGroup(GatheringZoneGroup group)
    {
        return new GatheringZoneGroupDto(
            ZoneName: group.ZoneName,
            Items: group.Items.Select(MapGuideItem).ToList());
    }

    private static GatheringGuideItemDto MapGuideItem(GatheringGuideItem item)
    {
        return new GatheringGuideItemDto(
            ItemId: item.ItemId,
            ItemName: item.ItemName,
            IconUrl: item.IconUrl,
            Quantity: item.Quantity,
            GatheringClass: item.GatheringClass,
            GatheringLevel: item.GatheringLevel,
            NodeType: item.NodeType.ToString(),
            CoordinateX: item.CoordinateX,
            CoordinateY: item.CoordinateY,
            TimeWindow: item.TimeWindow is not null ? MapTimeWindow(item.TimeWindow) : null,
            IsHidden: item.IsHidden);
    }

    private static EorzeanTimeWindowDto MapTimeWindow(EorzeanTimeWindow window)
    {
        var isActive = EorzeanTime.IsWindowActive(window.StartHour, window.EndHour);
        var timeUntil = EorzeanTime.TimeUntilWindow(window.StartHour, window.EndHour);

        return new EorzeanTimeWindowDto(
            StartHour: window.StartHour,
            EndHour: window.EndHour,
            IsCurrentlyActive: isActive,
            TimeUntil: isActive ? null : FormatTimeUntil(timeUntil));
    }

    private static TimedNodeEntryDto MapTimedNode(TimedNodeEntry entry)
    {
        var isActive = EorzeanTime.IsWindowActive(entry.StartHour, entry.EndHour);
        var timeUntil = EorzeanTime.TimeUntilWindow(entry.StartHour, entry.EndHour);

        return new TimedNodeEntryDto(
            ItemId: entry.ItemId,
            ItemName: entry.ItemName,
            IconUrl: entry.IconUrl,
            Quantity: entry.Quantity,
            ZoneName: entry.ZoneName,
            GatheringClass: entry.GatheringClass,
            GatheringLevel: entry.GatheringLevel,
            NodeType: entry.NodeType.ToString(),
            StartHour: entry.StartHour,
            EndHour: entry.EndHour,
            IsCurrentlyActive: isActive,
            TimeUntil: isActive ? null : FormatTimeUntil(timeUntil),
            CoordinateX: entry.CoordinateX,
            CoordinateY: entry.CoordinateY);
    }

    private static CrystalRequirementDto MapCrystal(CrystalRequirement crystal)
    {
        return new CrystalRequirementDto(
            ItemId: crystal.ItemId,
            ItemName: crystal.ItemName,
            IconUrl: crystal.IconUrl,
            Quantity: crystal.Quantity,
            Element: crystal.Element,
            Tier: crystal.Tier);
    }

    private static string FormatTimeUntil(TimeSpan timeUntil)
    {
        if (timeUntil.TotalMinutes < 1)
            return "< 1 min";

        return timeUntil.TotalHours >= 1
            ? $"{(int)timeUntil.TotalHours}h {timeUntil.Minutes}m"
            : $"{timeUntil.Minutes}m {timeUntil.Seconds}s";
    }
}
