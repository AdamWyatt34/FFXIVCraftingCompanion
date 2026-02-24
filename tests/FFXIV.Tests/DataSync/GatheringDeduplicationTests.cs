using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;

namespace FFXIV.Tests.DataSync;

public class GatheringDeduplicationTests
{
    /// <summary>
    /// Simulates the dedup logic used in SourceDataSyncService.SyncGatheringDataAsync.
    /// </summary>
    private static List<GatheringInfo> Deduplicate(List<GatheringInfo> infos)
    {
        return infos
            .GroupBy(g => (g.ItemId, g.GatheringType, g.ZoneName, g.IsTimed))
            .Select(g => g.First())
            .ToList();
    }

    [Fact]
    public void DuplicateEntries_AreRemoved()
    {
        var infos = new List<GatheringInfo>
        {
            new() { ItemId = 100, GatheringType = GatheringType.Mining, ZoneName = "Thanalan", IsTimed = false, Level = 10 },
            new() { ItemId = 100, GatheringType = GatheringType.Mining, ZoneName = "Thanalan", IsTimed = false, Level = 10 },
            new() { ItemId = 100, GatheringType = GatheringType.Mining, ZoneName = "Thanalan", IsTimed = false, Level = 15 },
        };

        var result = Deduplicate(infos);

        result.Should().HaveCount(1);
        result[0].ItemId.Should().Be(100);
    }

    [Fact]
    public void SameItem_DifferentZones_BothKept()
    {
        var infos = new List<GatheringInfo>
        {
            new() { ItemId = 200, GatheringType = GatheringType.Mining, ZoneName = "Thanalan", IsTimed = false },
            new() { ItemId = 200, GatheringType = GatheringType.Mining, ZoneName = "La Noscea", IsTimed = false },
        };

        var result = Deduplicate(infos);

        result.Should().HaveCount(2);
        result.Select(r => r.ZoneName).Should().BeEquivalentTo("Thanalan", "La Noscea");
    }

    [Fact]
    public void SameItem_TimedVsUntimed_BothKept()
    {
        var infos = new List<GatheringInfo>
        {
            new() { ItemId = 300, GatheringType = GatheringType.Logging, ZoneName = "Gridania", IsTimed = false },
            new() { ItemId = 300, GatheringType = GatheringType.Logging, ZoneName = "Gridania", IsTimed = true },
        };

        var result = Deduplicate(infos);

        result.Should().HaveCount(2);
        result.Select(r => r.IsTimed).Should().BeEquivalentTo(new[] { false, true });
    }

    [Fact]
    public void SameItem_DifferentGatheringTypes_BothKept()
    {
        var infos = new List<GatheringInfo>
        {
            new() { ItemId = 400, GatheringType = GatheringType.Mining, ZoneName = "Thanalan", IsTimed = false },
            new() { ItemId = 400, GatheringType = GatheringType.Quarrying, ZoneName = "Thanalan", IsTimed = false },
        };

        var result = Deduplicate(infos);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void MixedDuplicatesAndUniques_ProducesCorrectCount()
    {
        var infos = new List<GatheringInfo>
        {
            // 3 dupes for item 500 in same zone
            new() { ItemId = 500, GatheringType = GatheringType.Mining, ZoneName = "Zone A", IsTimed = false },
            new() { ItemId = 500, GatheringType = GatheringType.Mining, ZoneName = "Zone A", IsTimed = false },
            new() { ItemId = 500, GatheringType = GatheringType.Mining, ZoneName = "Zone A", IsTimed = false },
            // Unique entry for item 500 in different zone
            new() { ItemId = 500, GatheringType = GatheringType.Mining, ZoneName = "Zone B", IsTimed = false },
            // Unique entry for different item
            new() { ItemId = 600, GatheringType = GatheringType.Logging, ZoneName = "Zone A", IsTimed = true },
        };

        var result = Deduplicate(infos);

        result.Should().HaveCount(3);
    }
}
