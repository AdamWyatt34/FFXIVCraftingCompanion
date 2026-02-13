using System.Text.Json.Serialization;

namespace FFXIV.Tests.CraftingEngine.Solver;

public sealed class BenchmarkDataset
{
    [JsonPropertyName("benchmarks")]
    public List<BenchmarkEntry> Benchmarks { get; set; } = [];
}

public sealed class BenchmarkEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("recipe")]
    public BenchmarkRecipe Recipe { get; set; } = new();

    [JsonPropertyName("stats")]
    public BenchmarkStats Stats { get; set; } = new();

    [JsonPropertyName("communityRotation")]
    public CommunityRotation CommunityRotation { get; set; } = new();

    [JsonPropertyName("solverTargets")]
    public SolverTargets SolverTargets { get; set; } = new();

    public override string ToString() => Id;
}

public sealed class BenchmarkRecipe
{
    [JsonPropertyName("recipeId")] public int RecipeId { get; set; }
    [JsonPropertyName("recipeLevel")] public int RecipeLevel { get; set; }
    [JsonPropertyName("classJobLevel")] public int ClassJobLevel { get; set; }
    [JsonPropertyName("difficulty")] public int Difficulty { get; set; }
    [JsonPropertyName("quality")] public int Quality { get; set; }
    [JsonPropertyName("durability")] public int Durability { get; set; }
    [JsonPropertyName("stars")] public int Stars { get; set; }
    [JsonPropertyName("progressDivider")] public int ProgressDivider { get; set; }
    [JsonPropertyName("progressModifier")] public int ProgressModifier { get; set; }
    [JsonPropertyName("qualityDivider")] public int QualityDivider { get; set; }
    [JsonPropertyName("qualityModifier")] public int QualityModifier { get; set; }
    [JsonPropertyName("suggestedCraftsmanship")] public int SuggestedCraftsmanship { get; set; }
    [JsonPropertyName("suggestedControl")] public int SuggestedControl { get; set; }
}

public sealed class BenchmarkStats
{
    [JsonPropertyName("level")] public int Level { get; set; }
    [JsonPropertyName("craftsmanship")] public int Craftsmanship { get; set; }
    [JsonPropertyName("control")] public int Control { get; set; }
    [JsonPropertyName("cp")] public int CP { get; set; }
    [JsonPropertyName("isSpecialist")] public bool IsSpecialist { get; set; }
}

public sealed class CommunityRotation
{
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    [JsonPropertyName("actions")] public List<string> Actions { get; set; } = [];
    [JsonPropertyName("expectedCompletion")] public bool ExpectedCompletion { get; set; }
    [JsonPropertyName("expectedMinQualityPercent")] public int ExpectedMinQualityPercent { get; set; }
}

public sealed class SolverTargets
{
    [JsonPropertyName("greedyMustComplete")] public bool GreedyMustComplete { get; set; } = true;
    [JsonPropertyName("mctsMinQualityPercentOfCommunity")] public int MctsMinQualityPercentOfCommunity { get; set; } = 90;
    [JsonPropertyName("mctsTimeBudgetMs")] public int MctsTimeBudgetMs { get; set; } = 10000;
}
