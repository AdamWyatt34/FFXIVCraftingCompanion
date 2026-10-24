using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Parallel arrays, one slot per stat bonus.
/// Request with: fields=BaseParam@as(raw),IsRelative,Value,Max,ValueHQ,MaxHQ
/// </summary>
public sealed class XivApiItemFoodFields
{
    [JsonPropertyName("BaseParam@as(raw)")]
    public List<int>? BaseParam { get; init; }

    [JsonPropertyName("IsRelative")]
    public List<bool>? IsRelative { get; init; }

    [JsonPropertyName("Value")]
    public List<int>? Value { get; init; }

    [JsonPropertyName("Max")]
    public List<int>? Max { get; init; }

    [JsonPropertyName("ValueHQ")]
    public List<int>? ValueHQ { get; init; }

    [JsonPropertyName("MaxHQ")]
    public List<int>? MaxHQ { get; init; }
}
