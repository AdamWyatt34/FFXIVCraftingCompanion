using System.Text.Json;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Tests.CraftingEngine.Solver;

internal static class BenchmarkLoader
{
    private static BenchmarkDataset? _cached;

    public static BenchmarkDataset Load()
    {
        if (_cached != null) return _cached;

        var path = Path.Combine(AppContext.BaseDirectory,
            "CraftingEngine", "TestData", "solver-benchmarks.json");
        var json = File.ReadAllText(path);
        _cached = JsonSerializer.Deserialize<BenchmarkDataset>(json)
            ?? throw new InvalidOperationException("Failed to deserialize benchmarks");
        return _cached;
    }

    public static RecipeParameters ToRecipeParameters(BenchmarkRecipe r) => new()
    {
        RecipeId = r.RecipeId,
        RecipeLevel = r.RecipeLevel,
        ClassJobLevel = r.ClassJobLevel,
        Difficulty = r.Difficulty,
        Quality = r.Quality,
        Durability = r.Durability,
        Stars = r.Stars,
        ProgressDivider = r.ProgressDivider,
        ProgressModifier = r.ProgressModifier,
        QualityDivider = r.QualityDivider,
        QualityModifier = r.QualityModifier,
        SuggestedCraftsmanship = r.SuggestedCraftsmanship,
        SuggestedControl = r.SuggestedControl,
    };

    public static CrafterStats ToCrafterStats(BenchmarkStats s) => new()
    {
        Level = s.Level,
        Craftsmanship = s.Craftsmanship,
        Control = s.Control,
        CP = s.CP,
        IsSpecialist = s.IsSpecialist,
    };

    public static List<int> ResolveActionNames(IReadOnlyList<string> actionNames)
    {
        var ids = new List<int>(actionNames.Count);
        foreach (var name in actionNames)
        {
            var action = CraftingActionRegistry.GetByName(name)
                ?? throw new InvalidOperationException(
                    $"Unknown action name in benchmark: '{name}'");
            ids.Add(action.Id);
        }
        return ids;
    }

    public static IEnumerable<object[]> GetBenchmarkMemberData()
    {
        var dataset = Load();
        return dataset.Benchmarks.Select(b => new object[] { b });
    }
}
