using FFXIV.Domain.Enums;
using FFXIV.Domain.Utilities;
using FFXIV.Infrastructure.DataSync;
using FFXIV.Infrastructure.ExternalApis.Models;

namespace FFXIV.Tests.DataSync;

public class GatheringNodeTimingTests
{
    private const int None = SpawnWindows.NoTime;

    [Fact]
    public void A_row_with_no_data_is_a_standard_untimed_node()
    {
        GatheringNodeTiming.Resolve(None, None, [None, None, None], [0, 0, 0])
            .Should().Be(GatheringNodeTiming.Result.Untimed);
        GatheringNodeTiming.Resolve(0, 0, [], [])
            .Should().Be(GatheringNodeTiming.Result.Untimed);
    }

    [Theory]
    [InlineData(2000, 0, "[\"20:00\",\"0:00\"]")]
    [InlineData(0, 400, "[\"0:00\",\"4:00\"]")]
    [InlineData(400, 800, "[\"4:00\",\"8:00\"]")]
    [InlineData(1600, 2000, "[\"16:00\",\"20:00\"]")]
    public void Ephemeral_windows_are_stored_as_hours(int start, int end, string expected)
    {
        var result = GatheringNodeTiming.Resolve(start, end, [None, None, None], [0, 0, 0]);

        result.IsTimed.Should().BeTrue();
        result.SpawnTimes.Should().Be(expected);
        result.NodeType.Should().Be(NodeType.Ephemeral);
        SpawnWindows.Parse(result.SpawnTimes).Should().HaveCount(1);
    }

    [Fact]
    public void An_a_realm_reborn_time_table_gives_a_three_hour_window()
    {
        var result = GatheringNodeTiming.Resolve(None, None, [900, None, None], [300, 0, 0]);

        result.IsTimed.Should().BeTrue();
        result.NodeType.Should().Be(NodeType.Unspoiled);
        SpawnWindows.Parse(result.SpawnTimes).Should().Equal(new SpawnWindow(9, 12));
    }

    [Fact]
    public void A_later_time_table_gives_two_hour_windows_that_wrap_midnight()
    {
        var result = GatheringNodeTiming.Resolve(None, None, [1000, 2200, None], [160, 160, 0]);

        result.NodeType.Should().Be(NodeType.Unspoiled);
        SpawnWindows.Parse(result.SpawnTimes).Should().Equal(new SpawnWindow(10, 12), new SpawnWindow(22, 0));
    }

    [Fact]
    public void Unused_and_broken_table_slots_are_skipped()
    {
        var result = GatheringNodeTiming.Resolve(None, None, [None, 1400, 2500], [160, 0, 160]);

        result.Should().Be(GatheringNodeTiming.Result.Untimed);
    }

    [Fact]
    public void An_ephemeral_window_wins_over_a_time_table()
    {
        var result = GatheringNodeTiming.Resolve(400, 800, [900, None, None], [300, 0, 0]);

        result.NodeType.Should().Be(NodeType.Ephemeral);
        SpawnWindows.Parse(result.SpawnTimes).Should().Equal(new SpawnWindow(4, 8));
    }

    [Fact]
    public void Resolves_the_sheet_row_shape_xivapi_returns()
    {
        var row = new XivApiGatheringPointTransientFields
        {
            EphemeralStartTime = None,
            EphemeralEndTime = None,
            GatheringRarePopTimeTable = new XivApiRowRef<XivApiGatheringRarePopTimeTableFields>
            {
                Value = 44,
                RowId = 44,
                Sheet = "GatheringRarePopTimeTable",
                Fields = new XivApiGatheringRarePopTimeTableFields { StartTime = [0, 1200, None], Duration = [160, 160, 0] },
            },
        };

        var result = GatheringNodeTiming.Resolve(row);

        result.SpawnTimes.Should().Be("[\"0:00\",\"2:00\",\"12:00\",\"14:00\"]");
        GatheringNodeTiming.Resolve(new XivApiGatheringPointTransientFields { EphemeralStartTime = None, EphemeralEndTime = None })
            .Should().Be(GatheringNodeTiming.Result.Untimed);
    }

    [Theory]
    [InlineData(300, 3)]
    [InlineData(160, 2)]
    [InlineData(200, 2)]
    [InlineData(100, 1)]
    [InlineData(130, 2)]
    public void Durations_decode_to_whole_hours(int encoded, int hours)
    {
        GatheringNodeTiming.RarePopDurationHours(encoded).Should().Be(hours);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(20)]
    [InlineData(65535)]
    [InlineData(2400)]
    public void Durations_without_a_usable_hour_count_are_dropped(int encoded)
    {
        GatheringNodeTiming.RarePopDurationHours(encoded).Should().BeNull();
    }
}
