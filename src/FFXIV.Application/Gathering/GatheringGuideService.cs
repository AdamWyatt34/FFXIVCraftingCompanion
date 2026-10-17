using System.Text.Json;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;

namespace FFXIV.Application.Gathering;

public class GatheringGuideService : IGatheringGuideService
{
    private readonly IGatheringInfoRepository _gatheringRepo;
    private readonly IItemRepository _itemRepo;

    private static readonly Dictionary<int, (string Element, string Tier)> CrystalMap = new()
    {
        [2] = ("Fire", "Shard"), [3] = ("Ice", "Shard"), [4] = ("Wind", "Shard"),
        [5] = ("Earth", "Shard"), [6] = ("Lightning", "Shard"), [7] = ("Water", "Shard"),
        [8] = ("Fire", "Crystal"), [9] = ("Ice", "Crystal"), [10] = ("Wind", "Crystal"),
        [11] = ("Earth", "Crystal"), [12] = ("Lightning", "Crystal"), [13] = ("Water", "Crystal"),
        [14] = ("Fire", "Cluster"), [15] = ("Ice", "Cluster"), [16] = ("Wind", "Cluster"),
        [17] = ("Earth", "Cluster"), [18] = ("Lightning", "Cluster"), [19] = ("Water", "Cluster"),
    };

    public GatheringGuideService(IGatheringInfoRepository gatheringRepo, IItemRepository itemRepo)
    {
        _gatheringRepo = gatheringRepo;
        _itemRepo = itemRepo;
    }

    public async Task<GatheringGuide> GenerateGuideAsync(List<GatheredMaterialInput> materials, CancellationToken ct = default)
    {
        if (materials.Count == 0)
            return new GatheringGuide();

        // Separate crystals from non-crystal items
        var crystalInputs = materials.Where(m => m.ItemId is >= 2 and <= 19).ToList();
        var gatherableInputs = materials.Where(m => m.ItemId is < 2 or > 19).ToList();

        // Load item names
        var allItemIds = materials.Select(m => m.ItemId).Distinct().ToList();
        var items = await _itemRepo.GetByIdsAsync(allItemIds, ct);
        var itemLookup = items.ToDictionary(i => i.ItemId);

        // Build crystal summary
        var crystalSummary = BuildCrystalSummary(crystalInputs, itemLookup);

        // Load gathering data for non-crystal items
        var gatherableItemIds = gatherableInputs.Select(m => m.ItemId).Distinct().ToList();
        var gatheringData = gatherableItemIds.Count > 0
            ? await _gatheringRepo.GetByItemIdsAsync(gatherableItemIds, ct)
            : new Dictionary<int, List<GatheringInfo>>();

        // Build guide items with preferred node selection
        var quantityLookup = materials.ToDictionary(m => m.ItemId, m => m.Quantity);
        var guideItems = new List<(GatheringGuideItem Item, string ZoneName)>();
        var timedNodes = new List<TimedNodeEntry>();

        foreach (var input in gatherableInputs)
        {
            if (!gatheringData.TryGetValue(input.ItemId, out var nodes) || nodes.Count == 0)
                continue;

            var preferred = PickPreferredNode(nodes);
            var itemName = itemLookup.TryGetValue(input.ItemId, out var item) ? item.Name : $"Item {input.ItemId}";
            var iconUrl = itemLookup.TryGetValue(input.ItemId, out var iconItem) ? iconItem.IconPath : string.Empty;
            var timeWindow = ParseTimeWindow(preferred.SpawnTimes);

            var guideItem = new GatheringGuideItem
            {
                ItemId = input.ItemId,
                ItemName = itemName,
                IconUrl = iconUrl,
                Quantity = input.Quantity,
                GatheringClass = preferred.ClassJob,
                GatheringLevel = preferred.Level,
                NodeType = preferred.NodeType,
                CoordinateX = preferred.CoordinateX,
                CoordinateY = preferred.CoordinateY,
                TimeWindow = timeWindow,
                IsHidden = preferred.IsHidden,
            };

            guideItems.Add((guideItem, preferred.ZoneName));

            if (preferred.IsTimed && timeWindow is not null)
            {
                timedNodes.Add(new TimedNodeEntry
                {
                    ItemId = input.ItemId,
                    ItemName = itemName,
                    IconUrl = iconUrl,
                    Quantity = input.Quantity,
                    ZoneName = preferred.ZoneName,
                    GatheringClass = preferred.ClassJob,
                    GatheringLevel = preferred.Level,
                    NodeType = preferred.NodeType,
                    StartHour = timeWindow.StartHour,
                    EndHour = timeWindow.EndHour,
                    CoordinateX = preferred.CoordinateX,
                    CoordinateY = preferred.CoordinateY,
                });
            }
        }

        // Group by zone, sort within zone by class then level
        // Ordinal and stable, so the order does not depend on the server's culture (the frontend
        // port reproduces it).
        var zoneGroups = guideItems
            .GroupBy(g => g.ZoneName)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => new GatheringZoneGroup
            {
                ZoneName = g.Key,
                Items = g.Select(x => x.Item)
                    .OrderBy(i => i.GatheringClass, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(i => i.GatheringLevel)
                    .ToList(),
            })
            .ToList();

        timedNodes = timedNodes.OrderBy(t => t.StartHour).ToList();

        return new GatheringGuide
        {
            ZoneGroups = zoneGroups,
            TimedNodes = timedNodes,
            CrystalSummary = crystalSummary,
            Summary = new GatheringGuideSummary
            {
                TotalZones = zoneGroups.Count,
                TotalItems = guideItems.Count,
                TimedNodeCount = timedNodes.Count,
                TotalCrystalTypes = crystalSummary.Count,
            },
        };
    }

    private static GatheringInfo PickPreferredNode(List<GatheringInfo> nodes)
    {
        // Prefer: Standard non-hidden > Standard hidden > timed, then lowest level
        return nodes
            .OrderBy(n => n.NodeType != NodeType.Standard ? 1 : 0)
            .ThenBy(n => n.IsHidden ? 1 : 0)
            .ThenBy(n => n.Level)
            .First();
    }

    private static List<CrystalRequirement> BuildCrystalSummary(
        List<GatheredMaterialInput> crystalInputs,
        Dictionary<int, Item> itemLookup)
    {
        return crystalInputs
            .Where(c => CrystalMap.ContainsKey(c.ItemId))
            .Select(c =>
            {
                var (element, tier) = CrystalMap[c.ItemId];
                var itemName = itemLookup.TryGetValue(c.ItemId, out var item) ? item.Name : $"Item {c.ItemId}";
                var iconUrl = itemLookup.TryGetValue(c.ItemId, out var iconItem) ? iconItem.IconPath : string.Empty;
                return new CrystalRequirement
                {
                    ItemId = c.ItemId,
                    ItemName = itemName,
                    IconUrl = iconUrl,
                    Quantity = c.Quantity,
                    Element = element,
                    Tier = tier,
                };
            })
            .OrderBy(c => c.Tier, StringComparer.OrdinalIgnoreCase)
            .ThenBy(c => c.Element, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static EorzeanTimeWindow? ParseTimeWindow(string? spawnTimes)
    {
        if (string.IsNullOrEmpty(spawnTimes))
            return null;

        try
        {
            var times = JsonSerializer.Deserialize<string[]>(spawnTimes);
            if (times is null || times.Length < 2)
                return null;

            var startHour = ParseHour(times[0]);
            var endHour = ParseHour(times[1]);

            if (startHour < 0 || endHour < 0)
                return null;

            return new EorzeanTimeWindow { StartHour = startHour, EndHour = endHour };
        }
        catch
        {
            return null;
        }
    }

    private static int ParseHour(string timeString)
    {
        var parts = timeString.Split(':');
        return parts.Length > 0 && int.TryParse(parts[0], out var hour) ? hour : -1;
    }
}
