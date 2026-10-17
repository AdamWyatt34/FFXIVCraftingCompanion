using FFXIV.Application.Gear;
using FFXIV.Application.Gear.DTOs;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;

namespace FFXIV.Export;

public static class GearExport
{
    public static async Task<GearJobExport> BuildJobAsync(
        string job,
        IEquipmentRepository equipmentRepo,
        IItemSourceRepository itemSourceRepo,
        IGearRecommendationService gearService,
        CancellationToken ct)
    {
        var equipment = await equipmentRepo.GetByJobAndLevelAsync(job, int.MaxValue, ct);
        var frontier = GearFrontier.Build(equipment);
        var ids = frontier.Values.SelectMany(v => v).Select(e => e.EquipmentId).Distinct().ToList();
        var sourcesByItem = await itemSourceRepo.GetByItemIdsAsync(ids, ct);

        var profiles = gearService.GetAvailableProfiles(job).Select(ToExport).ToList();
        var candidates = frontier.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<EquipmentDto>)kv.Value.Select(e =>
            {
                // What the recommendation would carry: BuildSourceInfo plus the item-source
                // fallback the service applies to the piece it picks.
                var source = GearMapper.BuildSourceInfo(e);
                GearMapper.EnrichFromItemSources(source, e, sourcesByItem.GetValueOrDefault(e.EquipmentId));
                return GearMapper.ToDto(e, source, 0);
            }).ToList());

        return new GearJobExport(job, JobClassification.GetJobDisplayName(job), profiles, candidates);
    }

    public static IReadOnlyList<StatWeightProfileExport> AllProfiles() =>
        StatWeightProfiles.All.Select(ToExport).ToList();

    public static StatWeightProfileExport ToExport(StatWeightProfile p) =>
        new(p.Name, GearMapper.ToDto(p).DisplayName, p.Description, StatWeights(p));

    private static IReadOnlyDictionary<string, double> StatWeights(StatWeightProfile p) =>
        new Dictionary<string, double>
        {
            ["craftsmanship"] = p.CraftsmanshipWeight,
            ["control"] = p.ControlWeight,
            ["cp"] = p.CpWeight,
            ["gathering"] = p.GatheringWeight,
            ["perception"] = p.PerceptionWeight,
            ["gp"] = p.GpWeight,
            ["vitality"] = p.VitalityWeight,
            ["strength"] = p.StrengthWeight,
            ["dexterity"] = p.DexterityWeight,
            ["intelligence"] = p.IntelligenceWeight,
            ["mind"] = p.MindWeight,
            ["piety"] = p.PietyWeight,
            ["tenacity"] = p.TenacityWeight,
            ["directHitRate"] = p.DirectHitWeight,
            ["criticalHit"] = p.CriticalHitWeight,
            ["determination"] = p.DeterminationWeight,
            ["skillSpeed"] = p.SkillSpeedWeight,
            ["spellSpeed"] = p.SpellSpeedWeight,
            ["physicalDamage"] = p.PhysicalDamageWeight,
            ["defense"] = p.DefenseWeight,
            ["magicDefense"] = p.MagicDefenseWeight,
        };
}
