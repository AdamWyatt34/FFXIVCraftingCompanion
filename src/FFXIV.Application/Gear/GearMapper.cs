using System.Globalization;
using FFXIV.Application.Gear.DTOs;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Models;

namespace FFXIV.Application.Gear;

public static class GearMapper
{
    public static GearSetDto ToDto(GearSet gearSet) =>
        new(
            Job: gearSet.Job,
            JobName: JobClassification.GetJobDisplayName(gearSet.Job),
            Level: gearSet.Level,
            Profile: ToDto(gearSet.Profile),
            Slots: gearSet.Slots.Select(ToDto).ToList(),
            TotalStats: ToDto(gearSet.TotalStats));

    public static GearSlotDto ToDto(GearSlotRecommendation rec) =>
        new(
            SlotName: rec.SlotLabel,
            SlotKey: rec.Slot.ToString(),
            Recommendation: rec.RecommendedItem is not null
                ? ToDto(rec.RecommendedItem, rec.Source, rec.WeightedScore)
                : null,
            Alternatives: rec.Alternatives.Select(e => ToDto(e)).ToList(),
            IsEmpty: rec.RecommendedItem is null);

    public static EquipmentDto ToDto(Equipment equipment, EquipmentSourceInfo? source, double weightedScore) =>
        new(
            EquipmentId: equipment.EquipmentId,
            Name: equipment.Name,
            IconUrl: equipment.IconPath,
            ItemLevel: equipment.ItemLevel,
            EquipLevel: equipment.EquipLevel,
            Stats: new GearStatsDto(
                equipment.Craftsmanship,
                equipment.Control,
                equipment.CP,
                equipment.Gathering,
                equipment.Perception,
                equipment.GP,
                weightedScore),
            MateriaSlots: equipment.MateriaSlots,
            CanBeHQ: equipment.CanBeHQ,
            HQNote: equipment.CanBeHQ ? "HQ version available with ~15% higher stats" : null,
            Source: source is not null ? ToDto(source) : null,
            WeightedScore: weightedScore,
            Rarity: equipment.Rarity,
            RarityName: GetRarityName(equipment.Rarity),
            ClassJobCategory: equipment.ClassJobCategory);

    public static EquipmentDto ToDto(Equipment equipment) =>
        ToDto(equipment, null, 0);

    public static EquipmentSourceDto ToDto(EquipmentSourceInfo source) =>
        new(
            Type: source.SourceType.ToString(),
            Description: source.Description,
            RecipeId: source.RecipeId,
            HasRecipe: source.RecipeId.HasValue,
            VendorName: source.VendorName,
            Cost: source.VendorCost ?? source.ScripCost,
            Currency: source.CurrencyType);

    public static GearStatsDto ToDto(GearStats stats) =>
        new(
            Craftsmanship: stats.Craftsmanship,
            Control: stats.Control,
            CP: stats.CP,
            Gathering: stats.Gathering,
            Perception: stats.Perception,
            GP: stats.GP,
            TotalWeightedScore: stats.TotalWeightedScore);

    public static StatWeightProfileDto ToDto(StatWeightProfile profile) =>
        new(
            Name: profile.Name,
            DisplayName: CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
                profile.Name.Replace("_", " ").Replace("-", " ").ToLowerInvariant()),
            Description: profile.Description);

    public static string MapSlotToProfileKey(string slotKey, string slotName) =>
        slotKey != "Ring" ? slotKey : slotName == "Ring 1" ? "Ring1" : "Ring2";

    private static string GetRarityName(int rarity) => rarity switch
    {
        0 => "Common",
        1 => "Uncommon",
        2 => "Rare",
        3 => "Relic",
        _ => "Unknown"
    };
}
