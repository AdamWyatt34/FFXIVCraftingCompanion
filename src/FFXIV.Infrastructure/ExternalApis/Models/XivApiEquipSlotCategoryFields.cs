using System.Text.Json.Serialization;

namespace FFXIV.Infrastructure.ExternalApis.Models;

/// <summary>
/// Fields from the XIVAPI "EquipSlotCategory" sheet.
/// Integer slot fields (0/1) indicating which slots an item can be equipped in.
/// XIVAPI v2 returns these as integers, unlike ClassJobCategory which uses booleans.
/// Request with: fields=MainHand,OffHand,Head,Body,Gloves,Legs,Feet,Ears,Neck,Wrists,FingerL,FingerR
/// </summary>
public sealed class XivApiEquipSlotCategoryFields
{
    [JsonPropertyName("MainHand")]
    public int MainHand { get; init; }

    [JsonPropertyName("OffHand")]
    public int OffHand { get; init; }

    [JsonPropertyName("Head")]
    public int Head { get; init; }

    [JsonPropertyName("Body")]
    public int Body { get; init; }

    [JsonPropertyName("Gloves")]
    public int Gloves { get; init; }

    [JsonPropertyName("Legs")]
    public int Legs { get; init; }

    [JsonPropertyName("Feet")]
    public int Feet { get; init; }

    [JsonPropertyName("Ears")]
    public int Ears { get; init; }

    [JsonPropertyName("Neck")]
    public int Neck { get; init; }

    [JsonPropertyName("Wrists")]
    public int Wrists { get; init; }

    [JsonPropertyName("FingerL")]
    public int FingerL { get; init; }

    [JsonPropertyName("FingerR")]
    public int FingerR { get; init; }
}
