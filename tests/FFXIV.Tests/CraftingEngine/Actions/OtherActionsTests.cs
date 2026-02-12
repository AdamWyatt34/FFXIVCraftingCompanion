using FFXIV.Domain.CraftingEngine.Actions.Other;
using FFXIV.Domain.CraftingEngine.Models;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine.Actions;

public class OtherActionsTests
{
    private readonly CrafterStats _stats = CraftingTestHelpers.CreateDefaultStats();
    private readonly RecipeParameters _recipe = CraftingTestHelpers.CreateDefaultRecipe();

    // --- Tricks of the Trade ---

    [Fact]
    public void TricksOfTheTrade_RequiresGoodOrExcellentCondition()
    {
        var action = new TricksOfTheTrade();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.CanExecute(state, _stats, _recipe).Should().BeFalse();
        action.GetFailReason(state, _stats, _recipe).Should().Contain("Good or Excellent");
    }

    [Fact]
    public void TricksOfTheTrade_CanExecuteOnGood()
    {
        var action = new TricksOfTheTrade();
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Good };

        action.CanExecute(state, _stats, _recipe).Should().BeTrue();
    }

    [Fact]
    public void TricksOfTheTrade_CanExecuteOnExcellent()
    {
        var action = new TricksOfTheTrade();
        var state = CraftingTestHelpers.CreateDefaultState() with { Condition = CraftCondition.Excellent };

        action.CanExecute(state, _stats, _recipe).Should().BeTrue();
    }

    [Fact]
    public void TricksOfTheTrade_HeartAndSoul_AllowsUse()
    {
        var action = new TricksOfTheTrade();
        var state = CraftingTestHelpers.CreateDefaultState() with { HeartAndSoulActive = true };

        action.CanExecute(state, _stats, _recipe).Should().BeTrue();
    }

    [Fact]
    public void TricksOfTheTrade_Grants20CP()
    {
        var action = new TricksOfTheTrade();
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            CP = 500,
            Condition = CraftCondition.Good,
        };

        var result = action.Execute(state, _stats, _recipe);

        result.CP.Should().Be(520); // 500 + 20
    }

    [Fact]
    public void TricksOfTheTrade_CapsAtMaxCP()
    {
        var action = new TricksOfTheTrade();
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            CP = 590,
            MaxCP = 600,
            Condition = CraftCondition.Good,
        };

        var result = action.Execute(state, _stats, _recipe);

        result.CP.Should().Be(600); // capped
    }

    [Fact]
    public void TricksOfTheTrade_ConsumesHeartAndSoulWhenUsedAsSubstitute()
    {
        var action = new TricksOfTheTrade();
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            HeartAndSoulActive = true,
            // Normal condition — Heart and Soul is substituting
        };

        var result = action.Execute(state, _stats, _recipe);

        result.HeartAndSoulActive.Should().BeFalse();
    }

    [Fact]
    public void TricksOfTheTrade_DoesNotConsumeHeartAndSoulOnGoodCondition()
    {
        var action = new TricksOfTheTrade();
        var state = CraftingTestHelpers.CreateDefaultState() with
        {
            HeartAndSoulActive = true,
            Condition = CraftCondition.Good,
        };

        var result = action.Execute(state, _stats, _recipe);

        result.HeartAndSoulActive.Should().BeTrue();
    }

    // --- Careful Observation ---

    [Fact]
    public void CarefulObservation_ExpertRecipesOnly()
    {
        var action = new CarefulObservation();
        var state = CraftingTestHelpers.CreateDefaultState();
        var normalRecipe = _recipe with { IsExpert = false };

        action.CanExecute(state, _stats, normalRecipe).Should().BeFalse();
        action.GetFailReason(state, _stats, normalRecipe).Should().Contain("expert");
    }

    [Fact]
    public void CarefulObservation_CanExecuteOnExpertRecipe()
    {
        var action = new CarefulObservation();
        var state = CraftingTestHelpers.CreateDefaultState();
        var expertRecipe = _recipe with { IsExpert = true };

        action.CanExecute(state, _stats, expertRecipe).Should().BeTrue();
    }

    [Fact]
    public void CarefulObservation_MaxThreeUses()
    {
        var action = new CarefulObservation();
        var state = CraftingTestHelpers.CreateDefaultState() with { CarefulObservationCount = 3 };
        var expertRecipe = _recipe with { IsExpert = true };

        action.CanExecute(state, _stats, expertRecipe).Should().BeFalse();
        action.GetFailReason(state, _stats, expertRecipe).Should().Contain("3 times");
    }

    [Fact]
    public void CarefulObservation_IncrementsCount()
    {
        var action = new CarefulObservation();
        var state = CraftingTestHelpers.CreateDefaultState() with { CarefulObservationCount = 1 };
        var expertRecipe = _recipe with { IsExpert = true };

        var result = action.Execute(state, _stats, expertRecipe);

        result.CarefulObservationCount.Should().Be(2);
    }

    [Fact]
    public void CarefulObservation_Costs0CP()
    {
        var action = new CarefulObservation();
        var state = CraftingTestHelpers.CreateDefaultState();

        action.GetCPCost(state).Should().Be(0);
    }
}
