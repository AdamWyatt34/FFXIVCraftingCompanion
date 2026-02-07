using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Response from GET /api/sheet/{Sheet}/{RowId}?fields=...
/// Single row response — NOT wrapped in a rows array.
/// </summary>
public sealed class XivApiSingleRowResponse<TFields>
{
    [JsonPropertyName("schema")]
    public string Schema { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("row_id")]
    public int RowId { get; init; }

    [JsonPropertyName("fields")]
    public TFields Fields { get; init; } = default!;
}
