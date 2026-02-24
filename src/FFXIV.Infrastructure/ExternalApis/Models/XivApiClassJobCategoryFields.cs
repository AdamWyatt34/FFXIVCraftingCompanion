using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "ClassJobCategory" sheet.
/// Job booleans indicating which jobs can use items in this category.
/// XIVAPI v2 returns these as true/false booleans.
/// </summary>
public sealed class XivApiClassJobCategoryFields
{
    [JsonPropertyName("Name")]
    public string Name { get; init; } = string.Empty;

    // DoH
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

    // DoL
    [JsonPropertyName("MIN")]
    public bool MIN { get; init; }

    [JsonPropertyName("BTN")]
    public bool BTN { get; init; }

    [JsonPropertyName("FSH")]
    public bool FSH { get; init; }

    // Tank
    [JsonPropertyName("PLD")]
    public bool PLD { get; init; }

    [JsonPropertyName("WAR")]
    public bool WAR { get; init; }

    [JsonPropertyName("DRK")]
    public bool DRK { get; init; }

    [JsonPropertyName("GNB")]
    public bool GNB { get; init; }

    // Healer
    [JsonPropertyName("WHM")]
    public bool WHM { get; init; }

    [JsonPropertyName("SCH")]
    public bool SCH { get; init; }

    [JsonPropertyName("AST")]
    public bool AST { get; init; }

    [JsonPropertyName("SGE")]
    public bool SGE { get; init; }

    // Melee DPS
    [JsonPropertyName("MNK")]
    public bool MNK { get; init; }

    [JsonPropertyName("DRG")]
    public bool DRG { get; init; }

    [JsonPropertyName("NIN")]
    public bool NIN { get; init; }

    [JsonPropertyName("SAM")]
    public bool SAM { get; init; }

    [JsonPropertyName("RPR")]
    public bool RPR { get; init; }

    [JsonPropertyName("VPR")]
    public bool VPR { get; init; }

    // Ranged DPS
    [JsonPropertyName("BRD")]
    public bool BRD { get; init; }

    [JsonPropertyName("MCH")]
    public bool MCH { get; init; }

    [JsonPropertyName("DNC")]
    public bool DNC { get; init; }

    // Magic DPS
    [JsonPropertyName("BLM")]
    public bool BLM { get; init; }

    [JsonPropertyName("SMN")]
    public bool SMN { get; init; }

    [JsonPropertyName("RDM")]
    public bool RDM { get; init; }

    [JsonPropertyName("PCT")]
    public bool PCT { get; init; }
}
