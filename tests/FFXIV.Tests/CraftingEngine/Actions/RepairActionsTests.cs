using FFXIV.Domain.CraftingEngine.Actions.Repair;
using FFXIV.Domain.CraftingEngine.Models;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine.Actions;

public class RepairActionsTests
{
    private readonly CrafterStats _stats = CraftingTestHelpers.CreateDefaultStats();
    private readonly RecipeParameters _recipe = CraftingTestHelpers.CreateDefaultRecipe();

    [Fact]
    public void MastersMend_Restores30Durability()
    {
        var action = new MastersMend();
        var state = CraftingTestHelpers.CreateDefaultState() with { Durability = 20 };

        var result = action.Execute(state, _stats, _recipe);

        result.Durability.Should().Be(50); // 20 + 30
    }

    [Fact]
    public void MastersMend_CapsAtMaxDurability()
    {
        var action = new MastersMend();
        var state = CraftingTestHelpers.CreateDefaultState() with { Durability = 60 };

        var result = action.Execute(state, _stats, _recipe);

        result.Durability.Should().Be(70); // capped at MaxDurability
    }

    [Fact]
    public void MastersMend_Costs88CP()
    {
        var action = new MastersMend();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetCPCost(state).Should().Be(88);
    }

    [Fact]
    public void MastersMend_Uses0Durability()
    {
        var action = new MastersMend();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetDurabilityCost(state).Should().Be(0);
    }

    [Fact]
    public void ImmaculateMend_RestoresFullDurability()
    {
        var action = new ImmaculateMend();
        var state = CraftingTestHelpers.CreateDefaultState() with { Durability = 10 };

        var result = action.Execute(state, _stats, _recipe);

        result.Durability.Should().Be(70); // MaxDurability
    }

    [Fact]
    public void ImmaculateMend_Costs112CP()
    {
        var action = new ImmaculateMend();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetCPCost(state).Should().Be(112);
    }
}
