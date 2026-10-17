using System.Text.Json;
using FFXIV.Domain.Enums;
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
                equipment.Vitality,
                equipment.Strength,
                equipment.Dexterity,
                equipment.Intelligence,
                equipment.Mind,
                equipment.Piety,
                equipment.Tenacity,
                equipment.DirectHitRate,
                equipment.CriticalHit,
                equipment.Determination,
                equipment.SkillSpeed,
                equipment.SpellSpeed,
                equipment.PhysicalDamage,
                equipment.Defense,
                equipment.MagicDefense,
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
            Vitality: stats.Vitality,
            Strength: stats.Strength,
            Dexterity: stats.Dexterity,
            Intelligence: stats.Intelligence,
            Mind: stats.Mind,
            Piety: stats.Piety,
            Tenacity: stats.Tenacity,
            DirectHitRate: stats.DirectHitRate,
            CriticalHit: stats.CriticalHit,
            Determination: stats.Determination,
            SkillSpeed: stats.SkillSpeed,
            SpellSpeed: stats.SpellSpeed,
            PhysicalDamage: stats.PhysicalDamage,
            Defense: stats.Defense,
            MagicDefense: stats.MagicDefense,
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

    // A piece the sync could not classify takes its first recorded item source.
    public static void EnrichFromItemSources(EquipmentSourceInfo info, Equipment equipment, List<ItemSource>? sources)
    {
        if (equipment.SourceType is not null || sources is null || sources.Count == 0)
            return;
        info.SourceType = sources[0].SourceType;
        info.Description = sources[0].SourceType.ToString();
    }

    public static EquipmentSourceInfo BuildSourceInfo(Equipment equipment)
    {
        var info = new EquipmentSourceInfo
        {
            SourceType = equipment.SourceType ?? ItemSourceType.Other,
            RecipeId = equipment.RecipeId
        };

        if (!string.IsNullOrEmpty(equipment.SourceDetails))
        {
            try
            {
                using var doc = JsonDocument.Parse(equipment.SourceDetails);
                var root = doc.RootElement;

                if (root.TryGetProperty("vendorName", out var vendorName))
                    info.VendorName = vendorName.GetString();
                if (root.TryGetProperty("cost", out var cost))
                    info.VendorCost = cost.GetInt32();
                if (root.TryGetProperty("currencyType", out var currency))
                    info.CurrencyType = currency.GetString();
                if (root.TryGetProperty("scripCost", out var scripCost))
                    info.ScripCost = scripCost.GetInt32();
            }
            catch (JsonException)
            {
                // Malformed JSON — leave fields null
            }
        }

        info.Description = equipment.SourceType switch
        {
            ItemSourceType.Crafted => equipment.RecipeId.HasValue
                ? $"Crafted (Recipe #{equipment.RecipeId})"
                : "Crafted",
            ItemSourceType.Vendor => info.VendorName is not null
                ? $"Sold by {info.VendorName} for {info.VendorCost} {info.CurrencyType ?? "Gil"}"
                : "Purchased from vendor",
            ItemSourceType.Scrip => info.ScripCost.HasValue
                ? $"Scrip exchange ({info.ScripCost} {info.CurrencyType ?? "scrips"})"
                : "Scrip exchange",
            ItemSourceType.Quest => "Quest reward",
            ItemSourceType.Dungeon => "Dungeon drop",
            _ => equipment.SourceType?.ToString() ?? "Unknown"
        };

        return info;
    }
}
