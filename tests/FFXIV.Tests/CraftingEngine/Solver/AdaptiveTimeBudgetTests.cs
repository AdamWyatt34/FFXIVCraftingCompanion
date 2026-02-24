using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Infrastructure.Solver;
using FluentAssertions;

namespace FFXIV.Tests.CraftingEngine.Solver;

public class AdaptiveTimeBudgetTests
{
    private readonly SolverFactory _factory = new(new CraftingSimulator());

    [Fact]
    public void RecipeDifficulty_ExpertRecipe_ClassifiesAsExpert()
    {
        var recipe = new RecipeParameters { IsExpert = true, ClassJobLevel = 90, Stars = 4, Durability = 70 };
        var stats = new CrafterStats { Level = 90 };

        RecipeDifficultyClassifier.Classify(recipe, stats).Should().Be(RecipeDifficulty.Expert);
    }

    [Fact]
    public void RecipeDifficulty_TrivialRecipe_ClassifiesAsTrivial()
    {
        var recipe = new RecipeParameters { ClassJobLevel = 50, Stars = 0, Durability = 60 };
        var stats = new CrafterStats { Level = 90 }; // 90 >= 50 + 10

        RecipeDifficultyClassifier.Classify(recipe, stats).Should().Be(RecipeDifficulty.Trivial);
    }

    [Fact]
    public void RecipeDifficulty_EasyRecipe_ClassifiesAsEasy()
    {
        var recipe = new RecipeParameters { ClassJobLevel = 85, Stars = 0, Durability = 70 };
        var stats = new CrafterStats { Level = 90 }; // 90 >= 85 + 5 but NOT >= 85 + 10

        RecipeDifficultyClassifier.Classify(recipe, stats).Should().Be(RecipeDifficulty.Easy);
    }

    [Fact]
    public void RecipeDifficulty_HardRecipe_ClassifiesAsHard()
    {
        var recipe = new RecipeParameters { ClassJobLevel = 90, Stars = 4, Durability = 70 };
        var stats = new CrafterStats { Level = 90 }; // Same level, high stars

        RecipeDifficultyClassifier.Classify(recipe, stats).Should().Be(RecipeDifficulty.Hard);
    }

    [Fact]
    public void RecipeDifficulty_LowDurability_ClassifiesAsHard()
    {
        var recipe = new RecipeParameters { ClassJobLevel = 90, Stars = 1, Durability = 35 };
        var stats = new CrafterStats { Level = 90 };

        RecipeDifficultyClassifier.Classify(recipe, stats).Should().Be(RecipeDifficulty.Hard);
    }

    [Fact]
    public void RecipeDifficulty_StandardRecipe_ClassifiesAsStandard()
    {
        var recipe = new RecipeParameters { ClassJobLevel = 90, Stars = 2, Durability = 70 };
        var stats = new CrafterStats { Level = 90 }; // Same level, 2 stars, normal durability

        RecipeDifficultyClassifier.Classify(recipe, stats).Should().Be(RecipeDifficulty.Standard);
    }

    [Fact]
    public void AdaptiveTimeBudget_TrivialRecipe_Gets1s()
    {
        RecipeDifficultyClassifier.GetAdaptiveTimeBudgetMs(RecipeDifficulty.Trivial).Should().Be(1000);
    }

    [Fact]
    public void AdaptiveTimeBudget_EasyRecipe_Gets3s()
    {
        RecipeDifficultyClassifier.GetAdaptiveTimeBudgetMs(RecipeDifficulty.Easy).Should().Be(3000);
    }

    [Fact]
    public void AdaptiveTimeBudget_HardRecipe_Gets10s()
    {
        RecipeDifficultyClassifier.GetAdaptiveTimeBudgetMs(RecipeDifficulty.Hard).Should().Be(10000);
    }

    [Fact]
    public void AdaptiveTimeBudget_ExpertRecipe_Gets15s()
    {
        RecipeDifficultyClassifier.GetAdaptiveTimeBudgetMs(RecipeDifficulty.Expert).Should().Be(15000);
    }

    [Fact]
    public void AdaptSettings_DefaultBudget_AdaptsBasedOnDifficulty()
    {
        var easyRecipe = new RecipeParameters { ClassJobLevel = 50, Stars = 0, Durability = 60 };
        var stats = new CrafterStats { Level = 90 }; // Trivial
        var settings = new SolverSettings { TimeBudgetMs = 10000 }; // Default

        var adapted = _factory.AdaptSettings(settings, easyRecipe, stats);

        adapted.TimeBudgetMs.Should().Be(1000); // Trivial → 1s
    }

    [Fact]
    public void AdaptSettings_CustomBudget_NotOverridden()
    {
        var recipe = new RecipeParameters { ClassJobLevel = 50, Stars = 0, Durability = 60 };
        var stats = new CrafterStats { Level = 90 };
        var settings = new SolverSettings { TimeBudgetMs = 20000 }; // Custom, not default 10000

        var adapted = _factory.AdaptSettings(settings, recipe, stats);

        adapted.TimeBudgetMs.Should().Be(20000); // Not overridden
    }
}
