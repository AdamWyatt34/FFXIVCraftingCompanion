using FFXIV.Domain.Constants;

namespace FFXIV.Tests.Gear;

public class StatWeightProfilesTests
{
    [Fact]
    public void GetProfile_ExactName_ReturnsCorrectProfile()
    {
        var profile = StatWeightProfiles.GetProfile("ProgressFocused");

        profile.Name.Should().Be("ProgressFocused");
        profile.CraftsmanshipWeight.Should().Be(1.5);
    }

    [Fact]
    public void GetProfile_CaseInsensitive_ReturnsCorrectProfile()
    {
        var profile = StatWeightProfiles.GetProfile("progressfocused");

        profile.Name.Should().Be("ProgressFocused");
    }

    [Fact]
    public void GetProfile_UnknownName_ReturnsBalanced()
    {
        var profile = StatWeightProfiles.GetProfile("nonexistent");

        profile.Name.Should().Be("Balanced");
    }

    [Fact]
    public void DoHProfiles_ContainsAllCraftingProfiles()
    {
        StatWeightProfiles.DoHProfiles.Should().HaveCount(4);
        StatWeightProfiles.DoHProfiles.Should().Contain(p => p.Name == "Balanced");
        StatWeightProfiles.DoHProfiles.Should().Contain(p => p.Name == "ProgressFocused");
        StatWeightProfiles.DoHProfiles.Should().Contain(p => p.Name == "QualityFocused");
        StatWeightProfiles.DoHProfiles.Should().Contain(p => p.Name == "CpFocused");
    }

    [Fact]
    public void DoLProfiles_ContainsAllGatheringProfiles()
    {
        StatWeightProfiles.DoLProfiles.Should().HaveCount(2);
        StatWeightProfiles.DoLProfiles.Should().Contain(p => p.Name == "GatheringBalanced");
        StatWeightProfiles.DoLProfiles.Should().Contain(p => p.Name == "PerceptionFocused");
    }

    [Fact]
    public void DoHProfiles_HaveZeroGatheringWeights()
    {
        foreach (var profile in StatWeightProfiles.DoHProfiles)
        {
            profile.GatheringWeight.Should().Be(0, $"{profile.Name} should not have Gathering weight");
            profile.PerceptionWeight.Should().Be(0, $"{profile.Name} should not have Perception weight");
            profile.GpWeight.Should().Be(0, $"{profile.Name} should not have GP weight");
        }
    }

    [Fact]
    public void DoLProfiles_HaveZeroCraftingWeights()
    {
        foreach (var profile in StatWeightProfiles.DoLProfiles)
        {
            profile.CraftsmanshipWeight.Should().Be(0, $"{profile.Name} should not have Craftsmanship weight");
            profile.ControlWeight.Should().Be(0, $"{profile.Name} should not have Control weight");
            profile.CpWeight.Should().Be(0, $"{profile.Name} should not have CP weight");
        }
    }
}
