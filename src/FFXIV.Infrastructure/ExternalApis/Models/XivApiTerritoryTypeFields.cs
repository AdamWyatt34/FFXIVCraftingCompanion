using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "TerritoryType" sheet.
/// Maps territory to its PlaceName for zone name display.
/// Request with: fields=PlaceName
/// </summary>
public sealed class XivApiTerritoryTypeFields
{
    [JsonPropertyName("PlaceName")]
    public XivApiRowRef<XivApiPlaceNameFields>? PlaceName { get; init; }
}
