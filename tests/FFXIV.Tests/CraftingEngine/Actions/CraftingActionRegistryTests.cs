using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Actions;
using FFXIV.Domain.CraftingEngine.Models;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine.Actions;

public class CraftingActionRegistryTests
{
    [Fact]
    public void GetById_ValidId_ReturnsAction()
    {
        var action = CraftingActionRegistry.GetById(100001); // BasicSynthesis

        action.Should().NotBeNull();
        action!.Name.Should().Be("BasicSynthesis");
    }

    [Fact]
    public void GetById_InvalidId_ReturnsNull()
    {
        var action = CraftingActionRegistry.GetById(999999);

        action.Should().BeNull();
    }

    [Fact]
    public void GetByName_ValidName_ReturnsAction()
    {
        var action = CraftingActionRegistry.GetByName("BasicSynthesis");

        action.Should().NotBeNull();
        action!.Id.Should().Be(100001);
    }

    [Fact]
    public void GetByName_CaseInsensitive_ReturnsAction()
    {
        var action = CraftingActionRegistry.GetByName("basicsynthesis");

        action.Should().NotBeNull();
        action!.Name.Should().Be("BasicSynthesis");
    }

    [Fact]
    public void GetByName_InvalidName_ReturnsNull()
    {
        var action = CraftingActionRegistry.GetByName("NonExistentAction");

        action.Should().BeNull();
    }

    [Fact]
    public void GetAll_Returns36Actions()
    {
        var actions = CraftingActionRegistry.GetAll();

        actions.Should().HaveCount(36);
    }

    // Focused Synthesis, Focused Touch and Inner Quiet.
    private static readonly int[] LegacyIds = [100235, 100243, 100252];

    [Fact]
    public void GetAll_ExcludesLegacyActions()
    {
        var actions = CraftingActionRegistry.GetAll();

        actions.Should().NotContain(a => a.IsLegacy);
        actions.Select(a => a.Id).Should().NotContain(LegacyIds);
    }

    [Fact]
    public void GetAll_IncludeLegacy_ReturnsTheRecognizedSet()
    {
        var recognized = CraftingActionRegistry.GetAll(includeLegacy: true);

        recognized.Should().HaveCount(39);
        recognized.Where(a => a.IsLegacy).Select(a => a.Id).Should().BeEquivalentTo(LegacyIds);
    }

    [Theory]
    [InlineData(100235, "FocusedSynthesis")]
    [InlineData(100243, "FocusedTouch")]
    [InlineData(100252, "InnerQuiet")]
    public void GetById_LegacyAction_StillResolves(int id, string name)
    {
        var action = CraftingActionRegistry.GetById(id);

        action.Should().NotBeNull();
        action!.Name.Should().Be(name);
        action.IsLegacy.Should().BeTrue();
        CraftingActionRegistry.GetByName(name).Should().BeSameAs(action);
    }

    // Our ids, never the game's 100128/100443/100451: two of those are Quick Innovation and Trained Perfection here.
    [Theory]
    [InlineData(100501, "PreciseTouch", 53)]
    [InlineData(100502, "RefinedTouch", 92)]
    [InlineData(100503, "DaringTouch", 96)]
    public void GetById_DawntrailTouches_AreOffered(int id, string name, int level)
    {
        var action = CraftingActionRegistry.GetById(id);

        action.Should().NotBeNull();
        action!.Name.Should().Be(name);
        action.LevelRequirement.Should().Be(level);
        action.Category.Should().Be(ActionCategory.Quality);
        action.MacroWaitSeconds.Should().Be(3);
        CraftingActionRegistry.GetAll().Should().Contain(action);
    }

    [Fact]
    public void GetAll_ContainsAllCategories()
    {
        var actions = CraftingActionRegistry.GetAll();

        actions.Should().Contain(a => a.Category == ActionCategory.Progress);
        actions.Should().Contain(a => a.Category == ActionCategory.Quality);
        actions.Should().Contain(a => a.Category == ActionCategory.Buff);
        actions.Should().Contain(a => a.Category == ActionCategory.Repair);
        actions.Should().Contain(a => a.Category == ActionCategory.Other);
    }

    [Fact]
    public void GetAvailable_FiltersByLevel()
    {
        var lowStats = new CrafterStats { Level = 1, Craftsmanship = 100, Control = 100, CP = 200 };
        var highStats = new CrafterStats { Level = 100, Craftsmanship = 4000, Control = 4000, CP = 700 };

        var lowActions = CraftingActionRegistry.GetAvailable(lowStats);
        var highActions = CraftingActionRegistry.GetAvailable(highStats);

        lowActions.Count.Should().BeLessThan(highActions.Count);
        lowActions.Should().OnlyContain(a => a.LevelRequirement <= 1);
        highActions.Should().HaveCount(36); // All actions available at level 100
        highActions.Should().NotContain(a => a.IsLegacy);
    }

    [Fact]
    public void GetAll_AllActionsHaveUniqueIds()
    {
        var actions = CraftingActionRegistry.GetAll(includeLegacy: true);
        var ids = actions.Select(a => a.Id).ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetAll_AllActionsHaveUniqueNames()
    {
        var actions = CraftingActionRegistry.GetAll(includeLegacy: true);
        var names = actions.Select(a => a.Name).ToList();

        names.Should().OnlyHaveUniqueItems();
    }
}
