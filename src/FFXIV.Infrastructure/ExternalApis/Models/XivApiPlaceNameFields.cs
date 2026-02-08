using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "PlaceName" sheet.
/// Request with: fields=Name
/// </summary>
public sealed class XivApiPlaceNameFields
{
    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;
}
