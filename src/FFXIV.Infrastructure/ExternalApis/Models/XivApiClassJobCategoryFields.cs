using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "ClassJobCategory" sheet.
/// DoH/DoL job booleans indicating which jobs can use items in this category.
/// XIVAPI v2 returns these as true/false booleans.
/// Request with: fields=Name,CRP,BSM,ARM,GSM,LTW,WVR,ALC,CUL,MIN,BTN,FSH
/// </summary>
public sealed class XivApiClassJobCategoryFields
{
    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("CRP")]
    public bool CRP { get; init; }

    [JsonPropertyName("BSM")]
    public bool BSM { get; init; }

    [JsonPropertyName("ARM")]
    public bool ARM { get; init; }

    [JsonPropertyName("GSM")]
    public bool GSM { get; init; }

    [JsonPropertyName("LTW")]
    public bool LTW { get; init; }

    [JsonPropertyName("WVR")]
    public bool WVR { get; init; }

    [JsonPropertyName("ALC")]
    public bool ALC { get; init; }

    [JsonPropertyName("CUL")]
    public bool CUL { get; init; }

    [JsonPropertyName("MIN")]
    public bool MIN { get; init; }

    [JsonPropertyName("BTN")]
    public bool BTN { get; init; }

    [JsonPropertyName("FSH")]
    public bool FSH { get; init; }
}
