using FFXIV.Domain.Utilities;

namespace FFXIV.Tests.Gathering;

public class EorzeanTimeTests
{
    [Fact]
    public void GetCurrentEorzeanHour_ReturnsValueBetween0And23()
    {
        var hour = EorzeanTime.GetCurrentEorzeanHour();
        hour.Should().BeInRange(0, 23);
    }

    [Theory]
    [InlineData(6, 18, 10, true)]   // Within window
    [InlineData(6, 18, 6, true)]    // At start
    [InlineData(6, 18, 17, true)]   // Just before end
    [InlineData(6, 18, 18, false)]  // At end (exclusive)
    [InlineData(6, 18, 3, false)]   // Before window
    [InlineData(6, 18, 20, false)]  // After window
    public void IsWindowActive_StandardWindow_ReturnsExpected(int start, int end, int current, bool expected)
    {
        EorzeanTime.IsWindowActive(start, end, current).Should().Be(expected);
    }

    [Theory]
    [InlineData(22, 2, 23, true)]   // Within midnight-wrap window
    [InlineData(22, 2, 0, true)]    // Midnight
    [InlineData(22, 2, 1, true)]    // Just before end
    [InlineData(22, 2, 22, true)]   // At start
    [InlineData(22, 2, 2, false)]   // At end (exclusive)
    [InlineData(22, 2, 10, false)]  // Outside window
    public void IsWindowActive_MidnightWrap_ReturnsExpected(int start, int end, int current, bool expected)
    {
        EorzeanTime.IsWindowActive(start, end, current).Should().Be(expected);
    }

    [Fact]
    public void IsWindowActive_SameStartAndEnd_ReturnsFalse()
    {
        EorzeanTime.IsWindowActive(10, 10, 10).Should().BeFalse();
    }

    [Fact]
    public void TimeUntilWindow_WhenActive_ReturnsZero()
    {
        var result = EorzeanTime.TimeUntilWindow(6, 18, 10);
        result.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void TimeUntilWindow_WhenNotActive_ReturnsPositiveTimeSpan()
    {
        // Current hour is 3, window starts at 6 → 3 Eorzean hours away
        var result = EorzeanTime.TimeUntilWindow(6, 18, 3);
        result.Should().BeGreaterThan(TimeSpan.Zero);
        result.TotalSeconds.Should().BeApproximately(3 * EorzeanTime.EorzeanHourInSeconds, 0.1);
    }

    [Fact]
    public void TimeUntilWindow_WhenAfterWindow_WrapsAround()
    {
        // Current hour is 20, window starts at 6 → 10 Eorzean hours away
        var result = EorzeanTime.TimeUntilWindow(6, 18, 20);
        result.TotalSeconds.Should().BeApproximately(10 * EorzeanTime.EorzeanHourInSeconds, 0.1);
    }

    [Fact]
    public void GetEorzeanHour_KnownEpoch_ReturnsExpected()
    {
        // Use a known UTC time and verify it produces a valid hour
        var time = DateTimeOffset.UnixEpoch;
        var hour = EorzeanTime.GetEorzeanHour(time);
        hour.Should().BeInRange(0, 23);
    }
}
