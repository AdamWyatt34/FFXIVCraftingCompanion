using FFXIV.Application.CraftingEngine;
using FFXIV.Application.CraftingEngine.Solver;
using FFXIV.Application.Gear;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;

namespace FFXIV.Application.Journey;

public sealed class StatSufficiencyService : IStatSufficiencyService
{
    private readonly IRecipeRepository _recipeRepo;
    private readonly IRecipeParameterFactory _recipeParamFactory;
    private readonly ISolverFactory _solverFactory;
    private readonly ICraftingSimulator _simulator;
    private readonly IGearRecommendationService _gearService;
    private readonly IEquipmentRepository _equipmentRepo;
    private readonly ICacheService _cache;

    public StatSufficiencyService(
        IRecipeRepository recipeRepo,
        IRecipeParameterFactory recipeParamFactory,
        ISolverFactory solverFactory,
        ICraftingSimulator simulator,
        IGearRecommendationService gearService,
        IEquipmentRepository equipmentRepo,
        ICacheService cache)
    {
        _recipeRepo = recipeRepo;
        _recipeParamFactory = recipeParamFactory;
        _solverFactory = solverFactory;
        _simulator = simulator;
        _gearService = gearService;
        _equipmentRepo = equipmentRepo;
        _cache = cache;
    }

    public async Task<StatSufficiencyResult> AnalyzeAsync(
        int recipeId,
        CrafterStats stats,
        CharacterProfile? profile = null,
        CancellationToken ct = default)
    {
        // The cached part depends on the stats only; the gear suggestions depend on the profile
        // and are computed per call (a signed-out result must not hide a signed-in user's upgrades).
        var cacheKey = $"sufficiency:{recipeId}:{stats.Craftsmanship}:{stats.Control}:{stats.CP}:{stats.Level}:{stats.IsSpecialist}";
        var result = await _cache.GetAsync<StatSufficiencyResult>(cacheKey, ct)
            ?? await AnalyzeStatsAsync(recipeId, stats, cacheKey, ct);

        if (profile?.CurrentGear != null && profile.ActiveJob is not null)
            return result with { SuggestedGearUpgrades = await GetGearUpgradeSuggestionsAsync(profile, stats, ct) };

        return result;
    }

    private async Task<StatSufficiencyResult> AnalyzeStatsAsync(
        int recipeId,
        CrafterStats stats,
        string cacheKey,
        CancellationToken ct)
    {
        var recipe = await _recipeRepo.GetByIdAsync(recipeId, ct);
        if (recipe is null)
            throw new ArgumentException($"Recipe {recipeId} not found.");

        var recipeParams = await _recipeParamFactory.CreateAsync(recipeId, ct);
        if (recipeParams is null)
            throw new ArgumentException($"Recipe parameters for {recipeId} not found.");

        var craftsSufficient = stats.Craftsmanship >= recipe.RequiredCraftsmanship;
        var controlSufficient = stats.Control >= recipe.RequiredControl;
        var levelSufficient = stats.Level >= recipeParams.ClassJobLevel;
        var meetsMinimums = craftsSufficient && controlSufficient && levelSufficient;

        var craftsDeficit = craftsSufficient ? 0 : recipe.RequiredCraftsmanship - stats.Craftsmanship;
        var controlDeficit = controlSufficient ? 0 : recipe.RequiredControl - stats.Control;
        var levelDeficit = levelSufficient ? 0 : recipeParams.ClassJobLevel - stats.Level;

        int hqPercent = 0;
        double qualityPercent = 0;
        int estimatedSteps = 0;
        bool canComplete = false;

        if (meetsMinimums)
        {
            var solver = _solverFactory.Create(SolverAlgorithm.Greedy);
            var constraints = new SolverConstraints();
            var settings = new SolverSettings
            {
                Algorithm = SolverAlgorithm.Greedy,
                TimeBudgetMs = 3000,
            };

            var solverResult = await solver.SolveAsync(recipeParams, stats, constraints, settings, ct);

            if (solverResult.Status == SolverStatus.Completed && solverResult.ActionIds.Count > 0)
            {
                var simulation = _simulator.Simulate(recipeParams, stats, solverResult.ActionIds);
                hqPercent = simulation.FinalHQPercent;
                qualityPercent = simulation.FinalQualityPercent;
                estimatedSteps = simulation.TotalSteps;
                canComplete = simulation.Success;
            }
        }

        var recommendation = DetermineRecommendation(meetsMinimums, hqPercent, canComplete);

        var result = new StatSufficiencyResult
        {
            RecipeId = recipeId,
            MeetsMinimumRequirements = meetsMinimums,
            CraftsmanshipSufficient = craftsSufficient,
            ControlSufficient = controlSufficient,
            LevelSufficient = levelSufficient,
            CraftsmanshipDeficit = craftsDeficit,
            ControlDeficit = controlDeficit,
            LevelDeficit = levelDeficit,
            EstimatedHQPercent = hqPercent,
            EstimatedQualityPercent = qualityPercent,
            EstimatedSteps = estimatedSteps,
            CanComplete = canComplete,
            Recommendation = recommendation,
        };

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromHours(4), ct);
        return result;
    }

    private static StatRecommendation DetermineRecommendation(bool meetsMinimums, int hqPercent, bool canComplete)
    {
        if (!meetsMinimums || !canComplete)
            return StatRecommendation.UpgradeGear;

        return hqPercent switch
        {
            >= 80 => StatRecommendation.StatsExcellent,
            >= 60 => StatRecommendation.StatsGood,
            _ => StatRecommendation.StatsAdequate,
        };
    }

    private async Task<List<GearUpgradeSuggestion>> GetGearUpgradeSuggestionsAsync(
        CharacterProfile profile,
        CrafterStats stats,
        CancellationToken ct)
    {
        var suggestions = new List<GearUpgradeSuggestion>();

        var recommended = await _gearService.GetRecommendationsAsync(
            profile.ActiveJob, profile.ActiveJobLevel, "balanced", craftedOnly: false, ct: ct);

        // Batch-load all current equipment IDs in a single query
        // Profiles store gear under the slot keys ("MainHand", "Ring1"), not the labels.
        var currentEquipIds = recommended.Slots
            .Where(s => s.RecommendedItem is not null)
            .Select(s => profile.CurrentGear?.GetValueOrDefault(GearMapper.MapSlotToProfileKey(s.Slot.ToString(), s.SlotLabel)))
            .Where(id => id is not null and not 0)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var currentEquipList = await _equipmentRepo.GetByIdsAsync(currentEquipIds, ct);
        var equipLookup = currentEquipList.ToDictionary(e => e.EquipmentId);

        foreach (var slotRec in recommended.Slots)
        {
            if (slotRec.RecommendedItem is null)
                continue;

            var currentEquipmentId = profile.CurrentGear?.GetValueOrDefault(GearMapper.MapSlotToProfileKey(slotRec.Slot.ToString(), slotRec.SlotLabel));
            if (currentEquipmentId is null or 0)
            {
                suggestions.Add(new GearUpgradeSuggestion
                {
                    Slot = slotRec.SlotLabel,
                    CurrentItem = null,
                    SuggestedItem = slotRec.RecommendedItem.Name,
                    SuggestedEquipmentId = slotRec.RecommendedItem.EquipmentId,
                    CraftsmanshipGain = slotRec.RecommendedItem.Craftsmanship,
                    ControlGain = slotRec.RecommendedItem.Control,
                    CPGain = slotRec.RecommendedItem.CP,
                    Source = slotRec.Source.SourceType.ToString(),
                    RecipeId = slotRec.Source.RecipeId,
                });
                continue;
            }

            if (currentEquipmentId.Value == slotRec.RecommendedItem.EquipmentId)
                continue;

            if (!equipLookup.TryGetValue(currentEquipmentId.Value, out var currentEquip))
                continue;

            var craftsGain = slotRec.RecommendedItem.Craftsmanship - currentEquip.Craftsmanship;
            var controlGain = slotRec.RecommendedItem.Control - currentEquip.Control;
            var cpGain = slotRec.RecommendedItem.CP - currentEquip.CP;

            if (craftsGain <= 0 && controlGain <= 0 && cpGain <= 0)
                continue;

            suggestions.Add(new GearUpgradeSuggestion
            {
                Slot = slotRec.SlotLabel,
                CurrentItem = currentEquip.Name,
                SuggestedItem = slotRec.RecommendedItem.Name,
                SuggestedEquipmentId = slotRec.RecommendedItem.EquipmentId,
                CraftsmanshipGain = craftsGain,
                ControlGain = controlGain,
                CPGain = cpGain,
                Source = slotRec.Source.SourceType.ToString(),
                RecipeId = slotRec.Source.RecipeId,
            });
        }

        return suggestions;
    }
}
