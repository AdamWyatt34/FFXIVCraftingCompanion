using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine;

public class CrafterStatsTests
{
    // Inner Quiet multiplies the quality gain instead (Raphael raphael-sim/src/effects.rs:76-80).
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void GetEffectiveControl_IgnoresInnerQuiet(int stacks)
    {
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 1000, CP = 620 };
        stats.GetEffectiveControl(stacks).Should().Be(1000);
    }

    [Fact]
    public void IsSpecialist_DefaultsFalse()
    {
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 3600, CP = 620 };
        stats.IsSpecialist.Should().BeFalse();
    }

    [Fact]
    public void IsSpecialist_CanBeSetTrue()
    {
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 3600, CP = 620, IsSpecialist = true };
        stats.IsSpecialist.Should().BeTrue();
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var stats1 = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 3600, CP = 620 };
        var stats2 = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 3600, CP = 620 };
        stats1.Should().Be(stats2);
    }
}
