using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "ENpcResident" sheet.
/// Request with: fields=Singular
/// </summary>
public sealed class XivApiENpcResidentFields
{
    [JsonPropertyName("Singular")]
    public string Singular { get; init; } = string.Empty;
}
