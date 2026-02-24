using FFXIV.Application.Gathering;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.Gathering;

public class GatheringGuideServiceTests
{
    private readonly IGatheringInfoRepository _gatheringRepo = Substitute.For<IGatheringInfoRepository>();
    private readonly IItemRepository _itemRepo = Substitute.For<IItemRepository>();
    private readonly GatheringGuideService _sut;

    public GatheringGuideServiceTests()
    {
        _sut = new GatheringGuideService(_gatheringRepo, _itemRepo);
    }

    [Fact]
    public async Task EmptyInput_ReturnsEmptyGuide()
    {
        var result = await _sut.GenerateGuideAsync([]);

        result.ZoneGroups.Should().BeEmpty();
        result.TimedNodes.Should().BeEmpty();
        result.CrystalSummary.Should().BeEmpty();
        result.Summary.TotalZones.Should().Be(0);
    }

    [Fact]
    public async Task CrystalsOnly_ReturnsCrystalSummaryOnly()
    {
        var materials = new List<GatheredMaterialInput>
        {
            new(2, 10), // Fire Shard
            new(8, 5),  // Fire Crystal
            new(14, 3), // Fire Cluster
        };

        SetupItems(
            (2, "Fire Shard", ""),
            (8, "Fire Crystal", ""),
            (14, "Fire Cluster", ""));

        var result = await _sut.GenerateGuideAsync(materials);

        result.ZoneGroups.Should().BeEmpty();
        result.TimedNodes.Should().BeEmpty();
        result.CrystalSummary.Should().HaveCount(3);
        result.CrystalSummary[0].Element.Should().Be("Fire");
        result.CrystalSummary[0].Tier.Should().Be("Cluster");
        result.CrystalSummary[1].Tier.Should().Be("Crystal");
        result.CrystalSummary[2].Tier.Should().Be("Shard");
    }

    [Fact]
    public async Task CrystalElements_MappedCorrectly()
    {
        var materials = new List<GatheredMaterialInput>
        {
            new(2, 1), new(3, 1), new(4, 1), new(5, 1), new(6, 1), new(7, 1),
        };

        SetupItems(
            (2, "Fire Shard", ""), (3, "Ice Shard", ""), (4, "Wind Shard", ""),
            (5, "Earth Shard", ""), (6, "Lightning Shard", ""), (7, "Water Shard", ""));

        var result = await _sut.GenerateGuideAsync(materials);

        var elements = result.CrystalSummary.Select(c => c.Element).ToList();
        elements.Should().Contain("Fire");
        elements.Should().Contain("Ice");
        elements.Should().Contain("Wind");
        elements.Should().Contain("Earth");
        elements.Should().Contain("Lightning");
        elements.Should().Contain("Water");
        result.CrystalSummary.Should().OnlyContain(c => c.Tier == "Shard");
    }

    [Fact]
    public async Task SingleZone_GroupsCorrectly()
    {
        var materials = new List<GatheredMaterialInput>
        {
            new(100, 5),
            new(101, 10),
        };

        SetupItems((100, "Iron Ore", "icon1"), (101, "Copper Ore", "icon2"));
        SetupGathering(new Dictionary<int, List<GatheringInfo>>
        {
            [100] = [MakeGathering(100, "Central Thanalan", "MIN", 10)],
            [101] = [MakeGathering(101, "Central Thanalan", "MIN", 15)],
        });

        var result = await _sut.GenerateGuideAsync(materials);

        result.ZoneGroups.Should().HaveCount(1);
        result.ZoneGroups[0].ZoneName.Should().Be("Central Thanalan");
        result.ZoneGroups[0].Items.Should().HaveCount(2);
        result.ZoneGroups[0].Items[0].GatheringLevel.Should().Be(10); // Sorted by level
        result.ZoneGroups[0].Items[1].GatheringLevel.Should().Be(15);
    }

    [Fact]
    public async Task MultipleZones_GroupsSeparately()
    {
        var materials = new List<GatheredMaterialInput>
        {
            new(100, 5),
            new(101, 10),
        };

        SetupItems((100, "Iron Ore", ""), (101, "Elm Log", ""));
        SetupGathering(new Dictionary<int, List<GatheringInfo>>
        {
            [100] = [MakeGathering(100, "Central Thanalan", "MIN", 10)],
            [101] = [MakeGathering(101, "Central Shroud", "BTN", 15)],
        });

        var result = await _sut.GenerateGuideAsync(materials);

        result.ZoneGroups.Should().HaveCount(2);
        result.Summary.TotalZones.Should().Be(2);
        result.Summary.TotalItems.Should().Be(2);
    }

    [Fact]
    public async Task TimedNodes_AppearInTimedList()
    {
        var materials = new List<GatheredMaterialInput> { new(100, 5) };

        SetupItems((100, "Darksteel Ore", ""));
        SetupGathering(new Dictionary<int, List<GatheringInfo>>
        {
            [100] = [MakeGathering(100, "Coerthas", "MIN", 50, isTimed: true,
                spawnTimes: "[\"2:00\",\"4:00\"]", nodeType: NodeType.Unspoiled)],
        });

        var result = await _sut.GenerateGuideAsync(materials);

        result.TimedNodes.Should().HaveCount(1);
        result.TimedNodes[0].StartHour.Should().Be(2);
        result.TimedNodes[0].EndHour.Should().Be(4);
        result.TimedNodes[0].NodeType.Should().Be(NodeType.Unspoiled);
        result.Summary.TimedNodeCount.Should().Be(1);
    }

    [Fact]
    public async Task TimedNodes_SortedByStartHour()
    {
        var materials = new List<GatheredMaterialInput>
        {
            new(100, 5),
            new(101, 3),
        };

        SetupItems((100, "Ore A", ""), (101, "Ore B", ""));
        SetupGathering(new Dictionary<int, List<GatheringInfo>>
        {
            [100] = [MakeGathering(100, "Zone A", "MIN", 50, isTimed: true,
                spawnTimes: "[\"10:00\",\"12:00\"]", nodeType: NodeType.Unspoiled)],
            [101] = [MakeGathering(101, "Zone B", "MIN", 50, isTimed: true,
                spawnTimes: "[\"2:00\",\"4:00\"]", nodeType: NodeType.Unspoiled)],
        });

        var result = await _sut.GenerateGuideAsync(materials);

        result.TimedNodes.Should().HaveCount(2);
        result.TimedNodes[0].StartHour.Should().Be(2);
        result.TimedNodes[1].StartHour.Should().Be(10);
    }

    [Fact]
    public async Task MultiNodeItem_PrefersStandardOverTimed()
    {
        var materials = new List<GatheredMaterialInput> { new(100, 5) };

        SetupItems((100, "Iron Ore", ""));
        SetupGathering(new Dictionary<int, List<GatheringInfo>>
        {
            [100] =
            [
                MakeGathering(100, "Zone A", "MIN", 50, isTimed: true,
                    spawnTimes: "[\"2:00\",\"4:00\"]", nodeType: NodeType.Unspoiled),
                MakeGathering(100, "Zone B", "MIN", 10, nodeType: NodeType.Standard),
            ],
        });

        var result = await _sut.GenerateGuideAsync(materials);

        result.ZoneGroups.Should().HaveCount(1);
        result.ZoneGroups[0].ZoneName.Should().Be("Zone B");
        result.ZoneGroups[0].Items[0].NodeType.Should().Be(NodeType.Standard);
        result.TimedNodes.Should().BeEmpty();
    }

    [Fact]
    public async Task SummaryCountsAreCorrect()
    {
        var materials = new List<GatheredMaterialInput>
        {
            new(100, 5),
            new(101, 10),
            new(2, 20), // Crystal
            new(3, 15), // Crystal
        };

        SetupItems((100, "Iron Ore", ""), (101, "Elm Log", ""), (2, "Fire Shard", ""), (3, "Ice Shard", ""));
        SetupGathering(new Dictionary<int, List<GatheringInfo>>
        {
            [100] = [MakeGathering(100, "Thanalan", "MIN", 10)],
            [101] = [MakeGathering(101, "Shroud", "BTN", 15)],
        });

        var result = await _sut.GenerateGuideAsync(materials);

        result.Summary.TotalZones.Should().Be(2);
        result.Summary.TotalItems.Should().Be(2);
        result.Summary.TimedNodeCount.Should().Be(0);
        result.Summary.TotalCrystalTypes.Should().Be(2);
    }

    private void SetupItems(params (int Id, string Name, string IconPath)[] items)
    {
        var itemEntities = items.Select(i => new Item { ItemId = i.Id, Name = i.Name, IconPath = i.IconPath }).ToList();
        _itemRepo.GetByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(itemEntities);
    }

    private void SetupGathering(Dictionary<int, List<GatheringInfo>> data)
    {
        _gatheringRepo.GetByItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(data);
    }

    private static GatheringInfo MakeGathering(
        int itemId, string zone, string classJob, int level,
        bool isTimed = false, string? spawnTimes = null,
        NodeType nodeType = NodeType.Standard, bool isHidden = false)
    {
        return new GatheringInfo
        {
            GatheringInfoId = itemId * 100,
            ItemId = itemId,
            GatheringType = classJob == "MIN" ? GatheringType.Mining : GatheringType.Logging,
            ClassJob = classJob,
            Level = level,
            ZoneName = zone,
            IsTimed = isTimed,
            SpawnTimes = spawnTimes,
            IsHidden = isHidden,
            NodeType = nodeType,
        };
    }
}
