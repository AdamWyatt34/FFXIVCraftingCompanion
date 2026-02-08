using System.Text.Json.Serialization;

namespace FFXIV.Domain.Interfaces;

public sealed class GarlandItemData
{
    [JsonPropertyName("item")]
    public GarlandItem Item { get; init; } = null!;

    [JsonPropertyName("ingredients")]
    public List<GarlandIngredient>? Ingredients { get; init; }
}

public sealed class GarlandItem
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("icon")]
    public int Icon { get; init; }

    [JsonPropertyName("category")]
    public int Category { get; init; }

    [JsonPropertyName("craft")]
    public List<GarlandCraftInfo>? Craft { get; init; }

    [JsonPropertyName("vendors")]
    public List<int>? Vendors { get; init; }

    [JsonPropertyName("nodes")]
    public List<int>? Nodes { get; init; }
}

public sealed class GarlandCraftInfo
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("job")]
    public int Job { get; init; }

    [JsonPropertyName("rlvl")]
    public int Rlvl { get; init; }

    [JsonPropertyName("ingredients")]
    public List<GarlandIngredientRef> Ingredients { get; init; } = [];

    [JsonPropertyName("yield")]
    public int Yield { get; init; }
}

public sealed class GarlandIngredientRef
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("amount")]
    public int Amount { get; init; }
}

public sealed class GarlandIngredient
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("icon")]
    public int Icon { get; init; }
}
