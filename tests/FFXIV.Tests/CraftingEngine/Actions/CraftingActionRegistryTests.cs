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
    }

    [Fact]
    public void GetAll_AllActionsHaveUniqueIds()
    {
        var actions = CraftingActionRegistry.GetAll();
        var ids = actions.Select(a => a.Id).ToList();

        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GetAll_AllActionsHaveUniqueNames()
    {
        var actions = CraftingActionRegistry.GetAll();
        var names = actions.Select(a => a.Name).ToList();

        names.Should().OnlyHaveUniqueItems();
    }
}
