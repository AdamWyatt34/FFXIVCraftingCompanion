using System.Text.Json;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;

namespace FFXIV.Application.Gear;

public sealed class GearRecommendationService : IGearRecommendationService
{
    private readonly IEquipmentRepository _equipmentRepo;
    private readonly IItemSourceRepository _itemSourceRepo;

    public GearRecommendationService(
        IEquipmentRepository equipmentRepo,
        IItemSourceRepository itemSourceRepo)
    {
        _equipmentRepo = equipmentRepo;
        _itemSourceRepo = itemSourceRepo;
    }

    public async Task<GearSet> GetRecommendationsAsync(
        string job, int level, string profile = "balanced",
        bool craftedOnly = false, CancellationToken ct = default)
    {
        var weightProfile = ResolveProfile(job, profile);
        var slots = GetSlotsForJob(job);

        // Batch-load candidates for all slots in a single query
        var distinctSlots = slots.Select(s => s.Slot).Distinct();
        var allCandidates = await _equipmentRepo.GetBestForSlotsAsync(
            distinctSlots, job, level, craftedOnly ? 50 : 10, ct);

        if (craftedOnly)
            allCandidates = allCandidates
                .Where(e => e.SourceType == ItemSourceType.Crafted)
                .ToList();

        var candidatesBySlot = allCandidates
            .GroupBy(e => e.EquipSlot)
            .ToDictionary(g => g.Key, g => g.ToList());

        var recommendations = new List<GearSlotRecommendation>();
        int? firstRingEquipmentId = null;

        foreach (var (slot, label) in slots)
        {
            var candidates = candidatesBySlot.TryGetValue(slot, out var slotCandidates)
                ? (IReadOnlyList<Equipment>)slotCandidates
                : [];

            var scored = candidates
                .Select(e => (Equipment: e, Score: CalculateScore(e, weightProfile)))
                .OrderByDescending(x => x.Score)
                .ToList();

            // Ring dedup: second ring slot filters out the first ring's item
            if (slot == EquipSlot.Ring && firstRingEquipmentId.HasValue)
            {
                scored = scored
                    .Where(x => x.Equipment.EquipmentId != firstRingEquipmentId.Value)
                    .ToList();
            }

            var best = scored.FirstOrDefault();
            var alternatives = scored.Skip(1).Take(3).Select(x => x.Equipment).ToList();

            var recommendation = new GearSlotRecommendation
            {
                Slot = slot,
                SlotLabel = label,
                RecommendedItem = best.Equipment,
                Alternatives = alternatives,
                WeightedScore = best.Score,
                Source = best.Equipment is not null
                    ? BuildSourceInfo(best.Equipment)
                    : new EquipmentSourceInfo()
            };

            recommendations.Add(recommendation);

            if (slot == EquipSlot.Ring && !firstRingEquipmentId.HasValue && best.Equipment is not null)
            {
                firstRingEquipmentId = best.Equipment.EquipmentId;
            }
        }

        await EnrichSourceInfoAsync(recommendations, ct);

        var totalStats = AggregateStats(recommendations, weightProfile);

        var filledSlots = recommendations.Count(r => r.RecommendedItem is not null);
        var completionPct = slots.Count > 0
            ? (double)filledSlots / slots.Count * 100.0
            : 0;

        return new GearSet
        {
            Job = job,
            Level = level,
            Profile = weightProfile,
            Slots = recommendations,
            TotalStats = totalStats,
            CompletionPercentage = completionPct
        };
    }

    public async Task<GearSlotRecommendation> GetSlotRecommendationAsync(
        EquipSlot slot, string job, int level, string profile = "balanced",
        CancellationToken ct = default)
    {
        var weightProfile = ResolveProfile(job, profile);
        var candidates = await _equipmentRepo.GetBestBySlotAsync(slot, job, level, 10, ct);

        var scored = candidates
            .Select(e => (Equipment: e, Score: CalculateScore(e, weightProfile)))
            .OrderByDescending(x => x.Score)
            .ToList();

        var best = scored.FirstOrDefault();
        var alternatives = scored.Skip(1).Take(3).Select(x => x.Equipment).ToList();

        return new GearSlotRecommendation
        {
            Slot = slot,
            SlotLabel = slot.ToString(),
            RecommendedItem = best.Equipment,
            Alternatives = alternatives,
            WeightedScore = best.Score,
            Source = best.Equipment is not null
                ? BuildSourceInfo(best.Equipment)
                : new EquipmentSourceInfo()
        };
    }

    public async Task<GearStats> CalculateSetStatsAsync(
        Dictionary<EquipSlot, int> equipmentIds,
        CancellationToken ct = default)
    {
        var stats = new GearStats();

        var ids = equipmentIds.Values.Distinct().ToList();
        if (ids.Count == 0) return stats;

        var equipmentList = await _equipmentRepo.GetByIdsAsync(ids, ct);
        var lookup = equipmentList.ToDictionary(e => e.EquipmentId);

        foreach (var (_, equipmentId) in equipmentIds)
        {
            if (lookup.TryGetValue(equipmentId, out var equipment))
                AccumulateStats(stats, equipment);
        }

        return stats;
    }

    public List<StatWeightProfile> GetAvailableProfiles(string job)
    {
        if (JobClassification.IsDoL(job))
            return [.. StatWeightProfiles.DoLProfiles];

        if (JobClassification.IsCombat(job))
        {
            var role = JobClassification.GetCombatRole(job);
            return role.HasValue
                ? [.. StatWeightProfiles.GetProfilesForRole(role.Value)]
                : [StatWeightProfiles.TankBalanced];
        }

        return [.. StatWeightProfiles.DoHProfiles];
    }

    private static StatWeightProfile ResolveProfile(string job, string profileName)
    {
        var profile = StatWeightProfiles.GetProfile(profileName);

        if (JobClassification.IsDoL(job))
        {
            var isGatheringProfile = profile.GatheringWeight > 0
                                     || profile.PerceptionWeight > 0
                                     || profile.GpWeight > 0;
            if (!isGatheringProfile)
                return StatWeightProfiles.GatheringBalanced;
        }
        else if (JobClassification.IsDoH(job))
        {
            var isCraftingProfile = profile.CraftsmanshipWeight > 0
                                    || profile.ControlWeight > 0
                                    || profile.CpWeight > 0;
            if (!isCraftingProfile)
                return StatWeightProfiles.Balanced;
        }
        else if (JobClassification.IsCombat(job))
        {
            var isCombatProfile = profile.VitalityWeight > 0
                                  || profile.StrengthWeight > 0
                                  || profile.DexterityWeight > 0
                                  || profile.IntelligenceWeight > 0
                                  || profile.MindWeight > 0
                                  || profile.PhysicalDamageWeight > 0;
            if (!isCombatProfile)
            {
                var role = JobClassification.GetCombatRole(job);
                return role switch
                {
                    CombatRole.Tank => StatWeightProfiles.TankBalanced,
                    CombatRole.Healer => StatWeightProfiles.HealerBalanced,
                    CombatRole.MeleeDPS => StatWeightProfiles.MeleeDPSBalanced,
                    CombatRole.RangedDPS => StatWeightProfiles.RangedDPSBalanced,
                    CombatRole.MagicDPS => StatWeightProfiles.MagicDPSBalanced,
                    _ => StatWeightProfiles.TankBalanced
                };
            }
        }

        return profile;
    }

    private static List<(EquipSlot Slot, string Label)> GetSlotsForJob(string job)
    {
        var slots = new List<(EquipSlot, string)>
        {
            (EquipSlot.MainHand, "Main Hand")
        };

        if (JobClassification.HasOffhand(job))
            slots.Add((EquipSlot.OffHand, "Off Hand"));

        slots.Add((EquipSlot.Head, "Head"));
        slots.Add((EquipSlot.Body, "Body"));
        slots.Add((EquipSlot.Hands, "Hands"));
        slots.Add((EquipSlot.Legs, "Legs"));
        slots.Add((EquipSlot.Feet, "Feet"));
        slots.Add((EquipSlot.Earring, "Earring"));
        slots.Add((EquipSlot.Necklace, "Necklace"));
        slots.Add((EquipSlot.Bracelet, "Bracelet"));
        slots.Add((EquipSlot.Ring, "Ring 1"));
        slots.Add((EquipSlot.Ring, "Ring 2"));

        return slots;
    }

    private static double CalculateScore(Equipment equipment, StatWeightProfile profile)
    {
        return (equipment.Craftsmanship * profile.CraftsmanshipWeight)
             + (equipment.Control * profile.ControlWeight)
             + (equipment.CP * profile.CpWeight)
             + (equipment.Gathering * profile.GatheringWeight)
             + (equipment.Perception * profile.PerceptionWeight)
             + (equipment.GP * profile.GpWeight)
             + (equipment.Vitality * profile.VitalityWeight)
             + (equipment.Strength * profile.StrengthWeight)
             + (equipment.Dexterity * profile.DexterityWeight)
             + (equipment.Intelligence * profile.IntelligenceWeight)
             + (equipment.Mind * profile.MindWeight)
             + (equipment.Piety * profile.PietyWeight)
             + (equipment.Tenacity * profile.TenacityWeight)
             + (equipment.DirectHitRate * profile.DirectHitWeight)
             + (equipment.CriticalHit * profile.CriticalHitWeight)
             + (equipment.Determination * profile.DeterminationWeight)
             + (equipment.SkillSpeed * profile.SkillSpeedWeight)
             + (equipment.SpellSpeed * profile.SpellSpeedWeight)
             + (equipment.PhysicalDamage * profile.PhysicalDamageWeight)
             + (equipment.Defense * profile.DefenseWeight)
             + (equipment.MagicDefense * profile.MagicDefenseWeight);
    }

    private static EquipmentSourceInfo BuildSourceInfo(Equipment equipment)
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

    private async Task EnrichSourceInfoAsync(
        List<GearSlotRecommendation> recommendations,
        CancellationToken ct)
    {
        var equipmentIds = recommendations
            .Where(r => r.RecommendedItem is not null)
            .Select(r => r.RecommendedItem!.EquipmentId)
            .Distinct()
            .ToList();

        if (equipmentIds.Count == 0) return;

        var sourcesByItem = await _itemSourceRepo.GetByItemIdsAsync(equipmentIds, ct);

        foreach (var rec in recommendations)
        {
            if (rec.RecommendedItem is null) continue;

            if (sourcesByItem.TryGetValue(rec.RecommendedItem.EquipmentId, out var sources)
                && sources.Count > 0
                && rec.RecommendedItem.SourceType is null)
            {
                var primarySource = sources[0];
                rec.Source.SourceType = primarySource.SourceType;
                rec.Source.Description = primarySource.SourceType.ToString();
            }
        }
    }

    private static void AccumulateStats(GearStats stats, Equipment equipment)
    {
        stats.Craftsmanship += equipment.Craftsmanship;
        stats.Control += equipment.Control;
        stats.CP += equipment.CP;
        stats.Gathering += equipment.Gathering;
        stats.Perception += equipment.Perception;
        stats.GP += equipment.GP;
        stats.Vitality += equipment.Vitality;
        stats.Strength += equipment.Strength;
        stats.Dexterity += equipment.Dexterity;
        stats.Intelligence += equipment.Intelligence;
        stats.Mind += equipment.Mind;
        stats.Piety += equipment.Piety;
        stats.Tenacity += equipment.Tenacity;
        stats.DirectHitRate += equipment.DirectHitRate;
        stats.CriticalHit += equipment.CriticalHit;
        stats.Determination += equipment.Determination;
        stats.SkillSpeed += equipment.SkillSpeed;
        stats.SpellSpeed += equipment.SpellSpeed;
        stats.PhysicalDamage += equipment.PhysicalDamage;
        stats.Defense += equipment.Defense;
        stats.MagicDefense += equipment.MagicDefense;
    }

    private static double CalculateTotalWeightedScore(GearStats stats, StatWeightProfile profile)
    {
        return (stats.Craftsmanship * profile.CraftsmanshipWeight)
             + (stats.Control * profile.ControlWeight)
             + (stats.CP * profile.CpWeight)
             + (stats.Gathering * profile.GatheringWeight)
             + (stats.Perception * profile.PerceptionWeight)
             + (stats.GP * profile.GpWeight)
             + (stats.Vitality * profile.VitalityWeight)
             + (stats.Strength * profile.StrengthWeight)
             + (stats.Dexterity * profile.DexterityWeight)
             + (stats.Intelligence * profile.IntelligenceWeight)
             + (stats.Mind * profile.MindWeight)
             + (stats.Piety * profile.PietyWeight)
             + (stats.Tenacity * profile.TenacityWeight)
             + (stats.DirectHitRate * profile.DirectHitWeight)
             + (stats.CriticalHit * profile.CriticalHitWeight)
             + (stats.Determination * profile.DeterminationWeight)
             + (stats.SkillSpeed * profile.SkillSpeedWeight)
             + (stats.SpellSpeed * profile.SpellSpeedWeight)
             + (stats.PhysicalDamage * profile.PhysicalDamageWeight)
             + (stats.Defense * profile.DefenseWeight)
             + (stats.MagicDefense * profile.MagicDefenseWeight);
    }

    private static GearStats AggregateStats(
        List<GearSlotRecommendation> recommendations,
        StatWeightProfile profile)
    {
        var stats = new GearStats();

        foreach (var rec in recommendations)
        {
            if (rec.RecommendedItem is null) continue;
            AccumulateStats(stats, rec.RecommendedItem);
        }

        stats.TotalWeightedScore = CalculateTotalWeightedScore(stats, profile);

        return stats;
    }
}
