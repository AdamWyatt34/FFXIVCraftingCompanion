using FFXIV.Domain.Utilities;

namespace FFXIV.Tests.Gathering;

public class SpawnWindowsTests
{
    [Fact]
    public void Canonical_form_round_trips()
    {
        var windows = new List<SpawnWindow> { new(2, 4), new(10, 12) };

        var text = SpawnWindows.Serialize(windows);

        text.Should().Be("[\"2:00\",\"4:00\",\"10:00\",\"12:00\"]");
        SpawnWindows.Parse(text).Should().Equal(windows);
    }

    [Fact]
    public void No_windows_serialize_to_null()
    {
        SpawnWindows.Serialize([]).Should().BeNull();
    }

    [Theory]
    [InlineData("[\"2000:00\",\"0:00\"]", 20, 0)]
    [InlineData("[\"0:00\",\"400:00\"]", 0, 4)]
    [InlineData("[\"1600:00\",\"2000:00\"]", 16, 20)]
    [InlineData("[\"2:00\",\"4:00\"]", 2, 4)]
    [InlineData("[\"23:00\",\"1:00\"]", 23, 1)]
    [InlineData("[\"2:30\",\"4:15\"]", 2, 4)]
    [InlineData("[2, 4]", 2, 4)]
    [InlineData("[2000, 0]", 20, 0)]
    public void Legacy_and_canonical_single_windows_parse(string stored, int start, int end)
    {
        SpawnWindows.Parse(stored).Should().Equal(new SpawnWindow(start, end));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("[]")]
    [InlineData("[\"65535:00\",\"65535:00\"]")]
    [InlineData("[\"2:00\"]")]
    [InlineData("[\"25:00\",\"3:00\"]")]
    [InlineData("[\"2360:00\",\"100:00\"]")]
    [InlineData("[\"50:00\",\"100:00\"]")]
    [InlineData("[\"5:00\",\"5:00\"]")]
    [InlineData("[\"a\",\"b\"]")]
    [InlineData("[\"2:60\",\"4:00\"]")]
    [InlineData("[\"-2:00\",\"4:00\"]")]
    [InlineData("{\"start\":2}")]
    [InlineData("not json")]
    [InlineData("2-6,14-18")]
    public void Unusable_input_yields_no_window_and_never_throws(string? stored)
    {
        SpawnWindows.Parse(stored).Should().BeEmpty();
    }

    [Fact]
    public void A_bad_pair_is_dropped_without_losing_the_others()
    {
        SpawnWindows.Parse("[\"65535:00\",\"65535:00\",\"10:00\",\"12:00\",\"18:00\"]")
            .Should().Equal(new SpawnWindow(10, 12));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(23, 23)]
    [InlineData(100, 1)]
    [InlineData(2000, 20)]
    [InlineData(2359, 23)]
    public void Game_time_values_map_to_hours(int value, int hour)
    {
        SpawnWindows.HourFromGameTime(value).Should().Be(hour);
    }

    [Theory]
    [InlineData(65535)]
    [InlineData(-1)]
    [InlineData(24)]
    [InlineData(99)]
    [InlineData(2400)]
    [InlineData(1260)]
    public void Out_of_range_game_time_values_have_no_hour(int value)
    {
        SpawnWindows.HourFromGameTime(value).Should().BeNull();
    }
}
