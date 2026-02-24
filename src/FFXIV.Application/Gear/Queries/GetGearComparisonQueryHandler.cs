using FFXIV.Application.Gear.DTOs;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Gear.Queries;

public sealed class GetGearComparisonQueryHandler
    : IRequestHandler<GetGearComparisonQuery, GearComparisonDto>
{
    private readonly IMediator _mediator;
    private readonly ICharacterProfileRepository _profileRepo;
    private readonly IEquipmentRepository _equipmentRepo;

    public GetGearComparisonQueryHandler(
        IMediator mediator,
        ICharacterProfileRepository profileRepo,
        IEquipmentRepository equipmentRepo)
    {
        _mediator = mediator;
        _profileRepo = profileRepo;
        _equipmentRepo = equipmentRepo;
    }

    public async Task<GearComparisonDto> Handle(
        GetGearComparisonQuery request, CancellationToken cancellationToken)
    {
        var job = request.Job.ToUpperInvariant();
        var profile = request.Profile.ToLowerInvariant();

        // Reuse existing cached gear recommendations
        var gearSet = await _mediator.Send(
            new GetGearRecommendationsQuery(job, request.Level, profile),
            cancellationToken);

        // Resolve the weight profile for score calculation
        var weightProfile = StatWeightProfiles.GetProfile(profile);
        if (JobClassification.IsDoL(job))
        {
            var isGatheringProfile = weightProfile.GatheringWeight > 0
                                     || weightProfile.PerceptionWeight > 0
                                     || weightProfile.GpWeight > 0;
            if (!isGatheringProfile)
                weightProfile = StatWeightProfiles.GatheringBalanced;
        }
        else if (JobClassification.IsDoH(job))
        {
            var isCraftingProfile = weightProfile.CraftsmanshipWeight > 0
                                    || weightProfile.ControlWeight > 0
                                    || weightProfile.CpWeight > 0;
            if (!isCraftingProfile)
                weightProfile = StatWeightProfiles.Balanced;
        }
        else if (JobClassification.IsCombat(job))
        {
            var isCombatProfile = weightProfile.VitalityWeight > 0
                                  || weightProfile.StrengthWeight > 0
                                  || weightProfile.DexterityWeight > 0
                                  || weightProfile.IntelligenceWeight > 0
                                  || weightProfile.MindWeight > 0
                                  || weightProfile.PhysicalDamageWeight > 0;
            if (!isCombatProfile)
            {
                var role = JobClassification.GetCombatRole(job);
                weightProfile = role switch
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

        // Load user profile
        var userProfile = await _profileRepo.GetByUserIdAsync(request.UserId, cancellationToken);
        var currentGear = userProfile?.CurrentGear;

        if (currentGear is null || currentGear.Count == 0)
        {
            return BuildEmptyComparison(gearSet);
        }

        // Batch load all current equipment in a single query
        var currentEquipmentIds = currentGear.Values.Distinct().ToList();
        var equipmentList = await _equipmentRepo.GetByIdsAsync(currentEquipmentIds, cancellationToken);
        var equipmentLookup = equipmentList.ToDictionary(e => e.EquipmentId);

        var slotComparisons = new List<SlotComparisonDto>();
        int upgradeCount = 0, equippedCount = 0, alternativeCount = 0, emptySlotCount = 0;
        int totalCraftDiff = 0, totalCtrlDiff = 0, totalCpDiff = 0;
        int totalGathDiff = 0, totalPercDiff = 0, totalGpDiff = 0;
        int totalVitDiff = 0, totalStrDiff = 0, totalDexDiff = 0;
        int totalIntDiff = 0, totalMndDiff = 0, totalPdmgDiff = 0;
        int totalCritDiff = 0, totalDetDiff = 0, totalDhDiff = 0;
        int totalDefDiff = 0, totalMdefDiff = 0;

        foreach (var slot in gearSet.Slots)
        {
            var profileSlotKey = GearMapper.MapSlotToProfileKey(slot.SlotKey, slot.SlotName);

            EquipmentDto? currentEquipmentDto = null;
            ComparisonStatusDto? comparison = null;

            if (currentGear.TryGetValue(profileSlotKey, out var currentEqId)
                && equipmentLookup.TryGetValue(currentEqId, out var currentEquipment))
            {
                var currentScore = CalculateScore(currentEquipment, weightProfile);
                currentEquipmentDto = GearMapper.ToDto(currentEquipment) with
                {
                    WeightedScore = currentScore,
                    Stats = MapEquipmentToStatsDto(currentEquipment, currentScore)
                };

                if (slot.Recommendation is not null)
                {
                    var recScore = slot.Recommendation.WeightedScore;
                    var scoreDiff = recScore - currentScore;

                    var statDiffs = BuildStatDifferences(currentEquipment, slot.Recommendation);

                    string status;
                    bool isUpgrade;
                    if (slot.Recommendation.EquipmentId == currentEqId)
                    {
                        status = "Equipped";
                        isUpgrade = false;
                        equippedCount++;
                    }
                    else if (scoreDiff > 0)
                    {
                        status = "Upgrade";
                        isUpgrade = true;
                        upgradeCount++;

                        // Accumulate stat diffs for upgrades
                        totalCraftDiff += slot.Recommendation.Stats.Craftsmanship - currentEquipment.Craftsmanship;
                        totalCtrlDiff += slot.Recommendation.Stats.Control - currentEquipment.Control;
                        totalCpDiff += slot.Recommendation.Stats.CP - currentEquipment.CP;
                        totalGathDiff += slot.Recommendation.Stats.Gathering - currentEquipment.Gathering;
                        totalPercDiff += slot.Recommendation.Stats.Perception - currentEquipment.Perception;
                        totalGpDiff += slot.Recommendation.Stats.GP - currentEquipment.GP;
                        totalVitDiff += slot.Recommendation.Stats.Vitality - currentEquipment.Vitality;
                        totalStrDiff += slot.Recommendation.Stats.Strength - currentEquipment.Strength;
                        totalDexDiff += slot.Recommendation.Stats.Dexterity - currentEquipment.Dexterity;
                        totalIntDiff += slot.Recommendation.Stats.Intelligence - currentEquipment.Intelligence;
                        totalMndDiff += slot.Recommendation.Stats.Mind - currentEquipment.Mind;
                        totalPdmgDiff += slot.Recommendation.Stats.PhysicalDamage - currentEquipment.PhysicalDamage;
                        totalCritDiff += slot.Recommendation.Stats.CriticalHit - currentEquipment.CriticalHit;
                        totalDetDiff += slot.Recommendation.Stats.Determination - currentEquipment.Determination;
                        totalDhDiff += slot.Recommendation.Stats.DirectHitRate - currentEquipment.DirectHitRate;
                        totalDefDiff += slot.Recommendation.Stats.Defense - currentEquipment.Defense;
                        totalMdefDiff += slot.Recommendation.Stats.MagicDefense - currentEquipment.MagicDefense;
                    }
                    else
                    {
                        status = "Alternative";
                        isUpgrade = false;
                        alternativeCount++;
                    }

                    comparison = new ComparisonStatusDto(status, isUpgrade, scoreDiff, statDiffs);
                }
                else
                {
                    emptySlotCount++;
                }
            }
            else if (slot.Recommendation is not null)
            {
                // User has no gear in this slot but recommendation exists
                var statDiffs = BuildStatDifferencesFromNothing(slot.Recommendation);
                comparison = new ComparisonStatusDto("Upgrade", true, slot.Recommendation.WeightedScore, statDiffs);
                upgradeCount++;

                totalCraftDiff += slot.Recommendation.Stats.Craftsmanship;
                totalCtrlDiff += slot.Recommendation.Stats.Control;
                totalCpDiff += slot.Recommendation.Stats.CP;
                totalGathDiff += slot.Recommendation.Stats.Gathering;
                totalPercDiff += slot.Recommendation.Stats.Perception;
                totalGpDiff += slot.Recommendation.Stats.GP;
                totalVitDiff += slot.Recommendation.Stats.Vitality;
                totalStrDiff += slot.Recommendation.Stats.Strength;
                totalDexDiff += slot.Recommendation.Stats.Dexterity;
                totalIntDiff += slot.Recommendation.Stats.Intelligence;
                totalMndDiff += slot.Recommendation.Stats.Mind;
                totalPdmgDiff += slot.Recommendation.Stats.PhysicalDamage;
                totalCritDiff += slot.Recommendation.Stats.CriticalHit;
                totalDetDiff += slot.Recommendation.Stats.Determination;
                totalDhDiff += slot.Recommendation.Stats.DirectHitRate;
                totalDefDiff += slot.Recommendation.Stats.Defense;
                totalMdefDiff += slot.Recommendation.Stats.MagicDefense;
            }
            else
            {
                emptySlotCount++;
            }

            slotComparisons.Add(new SlotComparisonDto(
                SlotKey: slot.SlotKey,
                SlotName: slot.SlotName,
                ProfileSlotKey: profileSlotKey,
                CurrentEquipment: currentEquipmentDto,
                RecommendedEquipment: slot.Recommendation,
                Comparison: comparison));
        }

        var summary = new UpgradeSummaryDto(
            UpgradeCount: upgradeCount,
            EquippedCount: equippedCount,
            AlternativeCount: alternativeCount,
            EmptySlotCount: emptySlotCount,
            TotalStatDifference: new StatDifferenceTotalDto(
                totalCraftDiff, totalCtrlDiff, totalCpDiff,
                totalGathDiff, totalPercDiff, totalGpDiff,
                totalVitDiff, totalStrDiff, totalDexDiff,
                totalIntDiff, totalMndDiff, totalPdmgDiff,
                totalCritDiff, totalDetDiff, totalDhDiff,
                totalDefDiff, totalMdefDiff));

        return new GearComparisonDto(gearSet, slotComparisons, summary);
    }

    private static GearStatsDto MapEquipmentToStatsDto(Domain.Entities.Equipment eq, double score) =>
        new(eq.Craftsmanship, eq.Control, eq.CP,
            eq.Gathering, eq.Perception, eq.GP,
            eq.Vitality, eq.Strength, eq.Dexterity, eq.Intelligence, eq.Mind,
            eq.Piety, eq.Tenacity, eq.DirectHitRate, eq.CriticalHit,
            eq.Determination, eq.SkillSpeed, eq.SpellSpeed,
            eq.PhysicalDamage, eq.Defense, eq.MagicDefense,
            score);

    private static GearComparisonDto BuildEmptyComparison(GearSetDto gearSet)
    {
        var slotComparisons = gearSet.Slots.Select(slot =>
            new SlotComparisonDto(
                SlotKey: slot.SlotKey,
                SlotName: slot.SlotName,
                ProfileSlotKey: GearMapper.MapSlotToProfileKey(slot.SlotKey, slot.SlotName),
                CurrentEquipment: null,
                RecommendedEquipment: slot.Recommendation,
                Comparison: slot.Recommendation is not null
                    ? new ComparisonStatusDto("Upgrade", true, slot.Recommendation.WeightedScore,
                        BuildStatDifferencesFromNothing(slot.Recommendation))
                    : null))
            .ToList();

        var upgradeCount = slotComparisons.Count(c => c.Comparison?.Status == "Upgrade");
        var emptyCount = slotComparisons.Count(c => c.Comparison is null);

        var ts = gearSet.TotalStats;
        var summary = new UpgradeSummaryDto(
            upgradeCount, 0, 0, emptyCount,
            new StatDifferenceTotalDto(
                ts.Craftsmanship, ts.Control, ts.CP,
                ts.Gathering, ts.Perception, ts.GP,
                ts.Vitality, ts.Strength, ts.Dexterity,
                ts.Intelligence, ts.Mind, ts.PhysicalDamage,
                ts.CriticalHit, ts.Determination, ts.DirectHitRate,
                ts.Defense, ts.MagicDefense));

        return new GearComparisonDto(gearSet, slotComparisons, summary);
    }

    private static List<StatDifferenceDto> BuildStatDifferences(
        Domain.Entities.Equipment current, EquipmentDto recommended)
    {
        var diffs = new List<StatDifferenceDto>
        {
            new("Craftsmanship", current.Craftsmanship, recommended.Stats.Craftsmanship,
                recommended.Stats.Craftsmanship - current.Craftsmanship),
            new("Control", current.Control, recommended.Stats.Control,
                recommended.Stats.Control - current.Control),
            new("CP", current.CP, recommended.Stats.CP,
                recommended.Stats.CP - current.CP),
            new("Gathering", current.Gathering, recommended.Stats.Gathering,
                recommended.Stats.Gathering - current.Gathering),
            new("Perception", current.Perception, recommended.Stats.Perception,
                recommended.Stats.Perception - current.Perception),
            new("GP", current.GP, recommended.Stats.GP,
                recommended.Stats.GP - current.GP),
        };

        // Include combat stats if either side has any
        if (current.Vitality > 0 || recommended.Stats.Vitality > 0)
        {
            diffs.AddRange(
            [
                new("Vitality", current.Vitality, recommended.Stats.Vitality,
                    recommended.Stats.Vitality - current.Vitality),
                new("Strength", current.Strength, recommended.Stats.Strength,
                    recommended.Stats.Strength - current.Strength),
                new("Dexterity", current.Dexterity, recommended.Stats.Dexterity,
                    recommended.Stats.Dexterity - current.Dexterity),
                new("Intelligence", current.Intelligence, recommended.Stats.Intelligence,
                    recommended.Stats.Intelligence - current.Intelligence),
                new("Mind", current.Mind, recommended.Stats.Mind,
                    recommended.Stats.Mind - current.Mind),
                new("PhysicalDamage", current.PhysicalDamage, recommended.Stats.PhysicalDamage,
                    recommended.Stats.PhysicalDamage - current.PhysicalDamage),
                new("CriticalHit", current.CriticalHit, recommended.Stats.CriticalHit,
                    recommended.Stats.CriticalHit - current.CriticalHit),
                new("Determination", current.Determination, recommended.Stats.Determination,
                    recommended.Stats.Determination - current.Determination),
                new("DirectHitRate", current.DirectHitRate, recommended.Stats.DirectHitRate,
                    recommended.Stats.DirectHitRate - current.DirectHitRate),
                new("Defense", current.Defense, recommended.Stats.Defense,
                    recommended.Stats.Defense - current.Defense),
                new("MagicDefense", current.MagicDefense, recommended.Stats.MagicDefense,
                    recommended.Stats.MagicDefense - current.MagicDefense),
            ]);
        }

        return diffs;
    }

    private static List<StatDifferenceDto> BuildStatDifferencesFromNothing(EquipmentDto recommended)
    {
        var diffs = new List<StatDifferenceDto>
        {
            new("Craftsmanship", 0, recommended.Stats.Craftsmanship, recommended.Stats.Craftsmanship),
            new("Control", 0, recommended.Stats.Control, recommended.Stats.Control),
            new("CP", 0, recommended.Stats.CP, recommended.Stats.CP),
            new("Gathering", 0, recommended.Stats.Gathering, recommended.Stats.Gathering),
            new("Perception", 0, recommended.Stats.Perception, recommended.Stats.Perception),
            new("GP", 0, recommended.Stats.GP, recommended.Stats.GP),
        };

        if (recommended.Stats.Vitality > 0)
        {
            diffs.AddRange(
            [
                new("Vitality", 0, recommended.Stats.Vitality, recommended.Stats.Vitality),
                new("Strength", 0, recommended.Stats.Strength, recommended.Stats.Strength),
                new("Dexterity", 0, recommended.Stats.Dexterity, recommended.Stats.Dexterity),
                new("Intelligence", 0, recommended.Stats.Intelligence, recommended.Stats.Intelligence),
                new("Mind", 0, recommended.Stats.Mind, recommended.Stats.Mind),
                new("PhysicalDamage", 0, recommended.Stats.PhysicalDamage, recommended.Stats.PhysicalDamage),
                new("CriticalHit", 0, recommended.Stats.CriticalHit, recommended.Stats.CriticalHit),
                new("Determination", 0, recommended.Stats.Determination, recommended.Stats.Determination),
                new("DirectHitRate", 0, recommended.Stats.DirectHitRate, recommended.Stats.DirectHitRate),
                new("Defense", 0, recommended.Stats.Defense, recommended.Stats.Defense),
                new("MagicDefense", 0, recommended.Stats.MagicDefense, recommended.Stats.MagicDefense),
            ]);
        }

        return diffs;
    }

    private static double CalculateScore(Domain.Entities.Equipment equipment, Domain.Models.StatWeightProfile profile)
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
}
