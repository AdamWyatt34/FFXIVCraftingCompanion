using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// XIVAPI v2 reference to another sheet row.
/// Structure: { "value": N, "sheet": "SheetName", "row_id": N, "fields": {...} }
/// </summary>
public sealed class XivApiRowRef
{
    [JsonPropertyName("value")]
    public int Value { get; init; }

    [JsonPropertyName("sheet")]
    public string Sheet { get; init; } = string.Empty;

    [JsonPropertyName("row_id")]
    public int RowId { get; init; }
}

/// <summary>
/// Generic version that deserializes the nested fields into a typed object.
/// </summary>
public sealed class XivApiRowRef<TFields>
{
    [JsonPropertyName("value")]
    public int Value { get; init; }

    [JsonPropertyName("sheet")]
    public string Sheet { get; init; } = string.Empty;

    [JsonPropertyName("row_id")]
    public int RowId { get; init; }

    [JsonPropertyName("fields")]
    public TFields? Fields { get; init; }
}
