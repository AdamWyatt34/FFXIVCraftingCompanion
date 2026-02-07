using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Response from GET /api/search?sheets=...&amp;query=...
/// Has a "next" cursor string for pagination.
/// </summary>
public sealed class XivApiSearchResponse<TFields>
{
    [JsonPropertyName("next")]
    public string? Next { get; init; }

    [JsonPropertyName("schema")]
    public string Schema { get; init; } = string.Empty;

    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;

    [JsonPropertyName("results")]
    public List<XivApiSearchResult<TFields>> Results { get; init; } = [];
}

public sealed class XivApiSearchResult<TFields>
{
    [JsonPropertyName("score")]
    public double Score { get; init; }

    [JsonPropertyName("sheet")]
    public string Sheet { get; init; } = string.Empty;

    [JsonPropertyName("row_id")]
    public int RowId { get; init; }

    [JsonPropertyName("fields")]
    public TFields Fields { get; init; } = default!;
}
