using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine;

public class CraftingStateTests
{
    [Fact]
    public void IsComplete_ProgressMeetsMax_ReturnsTrue()
    {
        var state = CreateDefaultState() with { Progress = 1000, MaxProgress = 1000 };
        state.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void IsComplete_ProgressExceedsMax_ReturnsTrue()
    {
        var state = CreateDefaultState() with { Progress = 1500, MaxProgress = 1000 };
        state.IsComplete.Should().BeTrue();
    }

    [Fact]
    public void IsComplete_ProgressBelowMax_ReturnsFalse()
    {
        var state = CreateDefaultState() with { Progress = 500, MaxProgress = 1000 };
        state.IsComplete.Should().BeFalse();
    }

    [Fact]
    public void IsFailed_DurabilityZeroAndNotComplete_ReturnsTrue()
    {
        var state = CreateDefaultState() with { Durability = 0, Progress = 500, MaxProgress = 1000 };
        state.IsFailed.Should().BeTrue();
    }

    [Fact]
    public void IsFailed_DurabilityNegativeAndNotComplete_ReturnsTrue()
    {
        var state = CreateDefaultState() with { Durability = -10, Progress = 500, MaxProgress = 1000 };
        state.IsFailed.Should().BeTrue();
    }

    [Fact]
    public void IsFailed_DurabilityZeroButComplete_ReturnsFalse()
    {
        var state = CreateDefaultState() with { Durability = 0, Progress = 1000, MaxProgress = 1000 };
        state.IsFailed.Should().BeFalse();
    }

    [Fact]
    public void IsFailed_DurabilityPositive_ReturnsFalse()
    {
        var state = CreateDefaultState() with { Durability = 40, Progress = 500, MaxProgress = 1000 };
        state.IsFailed.Should().BeFalse();
    }

    [Fact]
    public void QualityPercent_AtHalfQuality_Returns50()
    {
        var state = CreateDefaultState() with { Quality = 500, MaxQuality = 1000 };
        state.QualityPercent.Should().BeApproximately(50.0, 0.01);
    }

    [Fact]
    public void QualityPercent_ExceedsMax_CapsAt100()
    {
        var state = CreateDefaultState() with { Quality = 1500, MaxQuality = 1000 };
        state.QualityPercent.Should().BeApproximately(100.0, 0.01);
    }

    [Fact]
    public void QualityPercent_MaxQualityZero_ReturnsZero()
    {
        var state = CreateDefaultState() with { MaxQuality = 0 };
        state.QualityPercent.Should().Be(0);
    }

    [Fact]
    public void ProgressPercent_AtHalfProgress_Returns50()
    {
        var state = CreateDefaultState() with { Progress = 500, MaxProgress = 1000 };
        state.ProgressPercent.Should().BeApproximately(50.0, 0.01);
    }

    [Fact]
    public void ProgressPercent_ExceedsMax_CapsAt100()
    {
        var state = CreateDefaultState() with { Progress = 2000, MaxProgress = 1000 };
        state.ProgressPercent.Should().BeApproximately(100.0, 0.01);
    }

    [Fact]
    public void HQPercent_AtFullQuality_Returns100()
    {
        var state = CreateDefaultState() with { Quality = 1000, MaxQuality = 1000 };
        state.HQPercent.Should().Be(100);
    }

    [Fact]
    public void HQPercent_AtZeroQuality_Returns1()
    {
        var state = CreateDefaultState() with { Quality = 0, MaxQuality = 1000 };
        state.HQPercent.Should().Be(1);
    }

    [Fact]
    public void HQPercent_AtMidQuality_ReturnsMidRange()
    {
        var state = CreateDefaultState() with { Quality = 500, MaxQuality = 1000 };
        // 50% quality should be in the 11-49% HQ range
        state.HQPercent.Should().BeGreaterThanOrEqualTo(11);
        state.HQPercent.Should().BeLessThanOrEqualTo(49);
    }

    [Fact]
    public void HQPercent_AtHighQuality_ReturnsHighRange()
    {
        var state = CreateDefaultState() with { Quality = 800, MaxQuality = 1000 };
        // 80% quality should be in the 50-99% HQ range
        state.HQPercent.Should().BeGreaterThanOrEqualTo(50);
        state.HQPercent.Should().BeLessThanOrEqualTo(99);
    }

    [Fact]
    public void WithBuffTick_DecrementsAllActiveBuffs()
    {
        var state = CreateDefaultState() with
        {
            WasteNotTurns = 4,
            WasteNotIITurns = 8,
            VenerationTurns = 3,
            InnovationTurns = 4,
            GreatStridesTurns = 2,
            ManipulationTurns = 8,
            FinalAppraisalTurns = 5,
            MuscleMemoryTurns = 5,
        };

        var ticked = state.WithBuffTick();

        ticked.WasteNotTurns.Should().Be(3);
        ticked.WasteNotIITurns.Should().Be(7);
        ticked.VenerationTurns.Should().Be(2);
        ticked.InnovationTurns.Should().Be(3);
        ticked.GreatStridesTurns.Should().Be(1);
        ticked.ManipulationTurns.Should().Be(7);
        ticked.FinalAppraisalTurns.Should().Be(4);
        ticked.MuscleMemoryTurns.Should().Be(4);
    }

    [Fact]
    public void WithBuffTick_DoesNotGoNegative()
    {
        var state = CreateDefaultState() with { WasteNotTurns = 0, VenerationTurns = 1 };
        var ticked = state.WithBuffTick();

        ticked.WasteNotTurns.Should().Be(0);
        ticked.VenerationTurns.Should().Be(0);
    }

    [Fact]
    public void WithBuffTick_DoesNotAffectNonBuffFields()
    {
        var state = CreateDefaultState() with
        {
            Progress = 500,
            Quality = 300,
            InnerQuietStacks = 5,
            HeartAndSoulActive = true,
            TrainedPerfectionActive = true,
            QuickInnovationUsed = true,
        };

        var ticked = state.WithBuffTick();

        ticked.Progress.Should().Be(500);
        ticked.Quality.Should().Be(300);
        ticked.InnerQuietStacks.Should().Be(5);
        ticked.HeartAndSoulActive.Should().BeTrue();
        ticked.TrainedPerfectionActive.Should().BeTrue();
        ticked.QuickInnovationUsed.Should().BeTrue();
    }

    [Fact]
    public void CreateInitial_SetsCorrectValues()
    {
        var recipe = new RecipeParameters
        {
            RecipeId = 100,
            RecipeLevel = 580,
            Difficulty = 3500,
            Quality = 7200,
            Durability = 70,
            ProgressDivider = 130,
            ProgressModifier = 80,
            QualityDivider = 115,
            QualityModifier = 70,
            Stars = 2,
            SuggestedCraftsmanship = 3700,
            SuggestedControl = 3280,
            StartingQuality = 0,
        };

        var crafter = new CrafterStats
        {
            Level = 90,
            Craftsmanship = 3800,
            Control = 3600,
            CP = 620,
        };

        var state = CraftingState.CreateInitial(recipe, crafter);

        state.Progress.Should().Be(0);
        state.Quality.Should().Be(0);
        state.MaxProgress.Should().Be(3500);
        state.MaxQuality.Should().Be(7200);
        state.Durability.Should().Be(70);
        state.MaxDurability.Should().Be(70);
        state.CP.Should().Be(620);
        state.MaxCP.Should().Be(620);
        state.Step.Should().Be(0);
        state.Condition.Should().Be(CraftCondition.Normal);
        state.Status.Should().Be(CraftStatus.InProgress);
        state.IsFirstStep.Should().BeTrue();
        state.InnerQuietStacks.Should().Be(0);
    }

    [Fact]
    public void CreateInitial_WithStartingQuality_SetsQuality()
    {
        var recipe = new RecipeParameters
        {
            RecipeId = 100,
            Difficulty = 1000,
            Quality = 5000,
            Durability = 80,
            StartingQuality = 1500,
        };
        var crafter = new CrafterStats { Level = 90, Craftsmanship = 3000, Control = 3000, CP = 600 };

        var state = CraftingState.CreateInitial(recipe, crafter);

        state.Quality.Should().Be(1500);
    }

    [Fact]
    public void Immutability_OriginalStateNotAffectedByWithExpression()
    {
        var original = CreateDefaultState();
        var modified = original with { Progress = 999, InnerQuietStacks = 5, CP = 100 };

        original.Progress.Should().Be(0);
        original.InnerQuietStacks.Should().Be(0);
        original.CP.Should().Be(600);
        modified.Progress.Should().Be(999);
        modified.InnerQuietStacks.Should().Be(5);
        modified.CP.Should().Be(100);
    }

    [Fact]
    public void Immutability_WithBuffTickDoesNotMutateOriginal()
    {
        var original = CreateDefaultState() with { VenerationTurns = 3, InnovationTurns = 4 };
        var ticked = original.WithBuffTick();

        original.VenerationTurns.Should().Be(3);
        original.InnovationTurns.Should().Be(4);
        ticked.VenerationTurns.Should().Be(2);
        ticked.InnovationTurns.Should().Be(3);
    }

    private static CraftingState CreateDefaultState() => new()
    {
        Progress = 0,
        Quality = 0,
        Durability = 70,
        CP = 600,
        MaxProgress = 1000,
        MaxQuality = 1000,
        MaxDurability = 70,
        MaxCP = 600,
        Step = 0,
        Condition = CraftCondition.Normal,
        Status = CraftStatus.InProgress,
    };
}
