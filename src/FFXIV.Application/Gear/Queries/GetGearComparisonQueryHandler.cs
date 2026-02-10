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

        // Load user profile
        var userProfile = await _profileRepo.GetByUserIdAsync(request.UserId, cancellationToken);
        var currentGear = userProfile?.CurrentGear;

        if (currentGear is null || currentGear.Count == 0)
        {
            return BuildEmptyComparison(gearSet);
        }

        // Batch load all current equipment
        var currentEquipmentIds = currentGear.Values.Distinct().ToList();
        var equipmentLookup = new Dictionary<int, Domain.Entities.Equipment>();
        foreach (var eqId in currentEquipmentIds)
        {
            var eq = await _equipmentRepo.GetByIdAsync(eqId, cancellationToken);
            if (eq is not null)
                equipmentLookup[eqId] = eq;
        }

        var slotComparisons = new List<SlotComparisonDto>();
        int upgradeCount = 0, equippedCount = 0, alternativeCount = 0, emptySlotCount = 0;
        int totalCraftDiff = 0, totalCtrlDiff = 0, totalCpDiff = 0;
        int totalGathDiff = 0, totalPercDiff = 0, totalGpDiff = 0;

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
                    Stats = new GearStatsDto(
                        currentEquipment.Craftsmanship,
                        currentEquipment.Control,
                        currentEquipment.CP,
                        currentEquipment.Gathering,
                        currentEquipment.Perception,
                        currentEquipment.GP,
                        currentScore)
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
                totalGathDiff, totalPercDiff, totalGpDiff));

        return new GearComparisonDto(gearSet, slotComparisons, summary);
    }

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

        var totalCraft = gearSet.TotalStats.Craftsmanship;
        var totalCtrl = gearSet.TotalStats.Control;
        var totalCp = gearSet.TotalStats.CP;
        var totalGath = gearSet.TotalStats.Gathering;
        var totalPerc = gearSet.TotalStats.Perception;
        var totalGp = gearSet.TotalStats.GP;

        var summary = new UpgradeSummaryDto(
            upgradeCount, 0, 0, emptyCount,
            new StatDifferenceTotalDto(totalCraft, totalCtrl, totalCp, totalGath, totalPerc, totalGp));

        return new GearComparisonDto(gearSet, slotComparisons, summary);
    }

    private static List<StatDifferenceDto> BuildStatDifferences(
        Domain.Entities.Equipment current, EquipmentDto recommended)
    {
        return
        [
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
        ];
    }

    private static List<StatDifferenceDto> BuildStatDifferencesFromNothing(EquipmentDto recommended)
    {
        return
        [
            new("Craftsmanship", 0, recommended.Stats.Craftsmanship, recommended.Stats.Craftsmanship),
            new("Control", 0, recommended.Stats.Control, recommended.Stats.Control),
            new("CP", 0, recommended.Stats.CP, recommended.Stats.CP),
            new("Gathering", 0, recommended.Stats.Gathering, recommended.Stats.Gathering),
            new("Perception", 0, recommended.Stats.Perception, recommended.Stats.Perception),
            new("GP", 0, recommended.Stats.GP, recommended.Stats.GP),
        ];
    }

    private static double CalculateScore(Domain.Entities.Equipment equipment, Domain.Models.StatWeightProfile profile)
    {
        return (equipment.Craftsmanship * profile.CraftsmanshipWeight)
             + (equipment.Control * profile.ControlWeight)
             + (equipment.CP * profile.CpWeight)
             + (equipment.Gathering * profile.GatheringWeight)
             + (equipment.Perception * profile.PerceptionWeight)
             + (equipment.GP * profile.GpWeight);
    }
}
