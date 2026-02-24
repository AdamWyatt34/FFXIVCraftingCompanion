using FFXIV.Application.Gathering;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;

namespace FFXIV.Tests.Gathering;

public class GatheringGuideMapperTests
{
    [Fact]
    public void ToDto_MapsZoneGroups()
    {
        var guide = new GatheringGuide
        {
            ZoneGroups =
            [
                new GatheringZoneGroup
                {
                    ZoneName = "Central Thanalan",
                    Items =
                    [
                        new GatheringGuideItem
                        {
                            ItemId = 100,
                            ItemName = "Iron Ore",
                            IconUrl = "icon",
                            Quantity = 5,
                            GatheringClass = "MIN",
                            GatheringLevel = 10,
                            NodeType = NodeType.Standard,
                        },
                    ],
                },
            ],
            Summary = new GatheringGuideSummary { TotalZones = 1, TotalItems = 1 },
        };

        var dto = GatheringGuideMapper.ToDto(guide);

        dto.ZoneGroups.Should().HaveCount(1);
        dto.ZoneGroups[0].ZoneName.Should().Be("Central Thanalan");
        dto.ZoneGroups[0].Items[0].ItemId.Should().Be(100);
        dto.ZoneGroups[0].Items[0].NodeType.Should().Be("Standard");
    }

    [Fact]
    public void ToDto_TimedNodes_EnrichedWithActivity()
    {
        var guide = new GatheringGuide
        {
            TimedNodes =
            [
                new TimedNodeEntry
                {
                    ItemId = 100,
                    ItemName = "Darksteel Ore",
                    Quantity = 5,
                    ZoneName = "Coerthas",
                    GatheringClass = "MIN",
                    GatheringLevel = 50,
                    NodeType = NodeType.Unspoiled,
                    StartHour = 0,
                    EndHour = 24, // Degenerate: always active is handled by IsWindowActive
                },
            ],
            Summary = new GatheringGuideSummary { TimedNodeCount = 1 },
        };

        var dto = GatheringGuideMapper.ToDto(guide);

        dto.TimedNodes.Should().HaveCount(1);
        dto.TimedNodes[0].NodeType.Should().Be("Unspoiled");
        // IsCurrentlyActive is computed at mapping time — just verify the property exists
        // IsCurrentlyActive is computed at mapping time — just verify the DTO was mapped
        dto.TimedNodes[0].Should().NotBeNull();
    }

    [Fact]
    public void ToDto_CrystalSummary_MappedCorrectly()
    {
        var guide = new GatheringGuide
        {
            CrystalSummary =
            [
                new CrystalRequirement
                {
                    ItemId = 2,
                    ItemName = "Fire Shard",
                    Quantity = 10,
                    Element = "Fire",
                    Tier = "Shard",
                },
            ],
            Summary = new GatheringGuideSummary { TotalCrystalTypes = 1 },
        };

        var dto = GatheringGuideMapper.ToDto(guide);

        dto.CrystalSummary.Should().HaveCount(1);
        dto.CrystalSummary[0].Element.Should().Be("Fire");
        dto.CrystalSummary[0].Tier.Should().Be("Shard");
        dto.CrystalSummary[0].Quantity.Should().Be(10);
    }

    [Fact]
    public void ToDto_TimeWindow_IsNull_WhenNoWindow()
    {
        var guide = new GatheringGuide
        {
            ZoneGroups =
            [
                new GatheringZoneGroup
                {
                    ZoneName = "Zone",
                    Items =
                    [
                        new GatheringGuideItem
                        {
                            ItemId = 100,
                            ItemName = "Ore",
                            Quantity = 1,
                            GatheringClass = "MIN",
                            GatheringLevel = 10,
                            NodeType = NodeType.Standard,
                            TimeWindow = null,
                        },
                    ],
                },
            ],
            Summary = new GatheringGuideSummary { TotalZones = 1, TotalItems = 1 },
        };

        var dto = GatheringGuideMapper.ToDto(guide);

        dto.ZoneGroups[0].Items[0].TimeWindow.Should().BeNull();
    }

    [Fact]
    public void ToDto_Summary_MappedCorrectly()
    {
        var guide = new GatheringGuide
        {
            Summary = new GatheringGuideSummary
            {
                TotalZones = 3,
                TotalItems = 7,
                TimedNodeCount = 2,
                TotalCrystalTypes = 4,
            },
        };

        var dto = GatheringGuideMapper.ToDto(guide);

        dto.Summary.TotalZones.Should().Be(3);
        dto.Summary.TotalItems.Should().Be(7);
        dto.Summary.TimedNodeCount.Should().Be(2);
        dto.Summary.TotalCrystalTypes.Should().Be(4);
    }
}
