using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Icon struct returned by XIVAPI v2.
/// Example: { "id": 20801, "path": "ui/icon/020000/020801.tex", "path_hr1": "ui/icon/020000/020801_hr1.tex" }
/// </summary>
public sealed class XivApiIcon
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("path")]
    public string Path { get; init; } = string.Empty;

    [JsonPropertyName("path_hr1")]
    public string PathHr1 { get; init; } = string.Empty;
}
