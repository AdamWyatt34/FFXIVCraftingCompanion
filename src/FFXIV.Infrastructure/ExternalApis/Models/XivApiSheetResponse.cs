using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Response from GET /api/sheet/{Sheet}?limit=N&amp;after=N&amp;fields=...
/// No "next" cursor — pagination uses the last row_id as the "after" param.
/// </summary>
public sealed class XivApiSheetResponse<TFields>
{
    [JsonPropertyName("schema")]
    public string Schema { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("rows")]
    public List<XivApiSheetRow<TFields>> Rows { get; init; } = [];
}

public sealed class XivApiSheetRow<TFields>
{
    [JsonPropertyName("row_id")]
    public int RowId { get; init; }

    [JsonPropertyName("fields")]
    public TFields Fields { get; init; } = default!;
}
