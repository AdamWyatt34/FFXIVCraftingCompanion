using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine;

public class CrafterStatsTests
{
    [Fact]
    public void GetEffectiveControl_ZeroStacks_ReturnsBaseControl()
    {
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 3600, CP = 620 };
        stats.GetEffectiveControl(0).Should().Be(3600);
    }

    [Fact]
    public void GetEffectiveControl_OneStack_Adds10Percent()
    {
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 1000, CP = 620 };
        // 1 stack = (int)(1000 * 1.1) = 1100
        stats.GetEffectiveControl(1).Should().Be(1100);
    }

    [Fact]
    public void GetEffectiveControl_FiveStacks_Adds50Percent()
    {
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 1000, CP = 620 };
        // 5 stacks = (int)(1000 * 1.5) = 1500
        stats.GetEffectiveControl(5).Should().Be(1500);
    }

    [Fact]
    public void GetEffectiveControl_TenStacks_Adds100Percent()
    {
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 1000, CP = 620 };
        // 10 stacks = (int)(1000 * 2.0) = 2000
        stats.GetEffectiveControl(10).Should().Be(2000);
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
