using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Response from GET /api/version.
/// Contains an array of version entries; the latest has "latest" in its names.
/// </summary>
public sealed class XivApiVersionResponse
{
    [JsonPropertyName("versions")]
    public List<XivApiVersionEntry> Versions { get; init; } = [];
}

public sealed class XivApiVersionEntry
{
    [JsonPropertyName("key")]
    public string Key { get; init; } = string.Empty;

    [JsonPropertyName("names")]
    public List<string> Names { get; init; } = [];
}
