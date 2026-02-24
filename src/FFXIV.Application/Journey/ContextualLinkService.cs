using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;

namespace FFXIV.Application.Journey;

public sealed class ContextualLinkService : IContextualLinkService
{
    private readonly IItemRepository _itemRepo;
    private readonly IRecipeRepository _recipeRepo;
    private readonly IEquipmentRepository _equipmentRepo;
    private readonly IGatheringInfoRepository _gatheringRepo;
    private readonly IVendorInfoRepository _vendorRepo;
    private readonly IRecipeLevelRepository _recipeLevelRepo;
    private readonly ICacheService _cache;

    public ContextualLinkService(
        IItemRepository itemRepo,
        IRecipeRepository recipeRepo,
        IEquipmentRepository equipmentRepo,
        IGatheringInfoRepository gatheringRepo,
        IVendorInfoRepository vendorRepo,
        IRecipeLevelRepository recipeLevelRepo,
        ICacheService cache)
    {
        _itemRepo = itemRepo;
        _recipeRepo = recipeRepo;
        _equipmentRepo = equipmentRepo;
        _gatheringRepo = gatheringRepo;
        _vendorRepo = vendorRepo;
        _recipeLevelRepo = recipeLevelRepo;
        _cache = cache;
    }

    public async Task<ContextualLinks> GetLinksForItemAsync(int itemId, CancellationToken ct = default)
    {
        var cacheKey = $"contextlinks:{itemId}";
        var cached = await _cache.GetAsync<ContextualLinks>(cacheKey, ct);
        if (cached is not null)
            return cached;

        var item = await _itemRepo.GetByIdAsync(itemId, ct);
        if (item is null)
            throw new ArgumentException($"Item {itemId} not found.");

        // Run lookups sequentially to avoid DbContext threading issues
        var recipes = await _recipeRepo.GetByResultItemIdsAsync([itemId], ct);
        var usedIn = await _recipeRepo.GetRecipesUsingIngredientAsync(itemId, 20, ct);
        var equipment = await _equipmentRepo.GetByIdAsync(itemId, ct);
        var gatheringInfos = await _gatheringRepo.GetByItemIdAsync(itemId, ct);
        var vendorInfos = await _vendorRepo.GetByItemIdAsync(itemId, ct);

        // Build recipe links with stars from RecipeLevelEntry
        var recipeLinks = new List<RecipeLink>();
        foreach (var r in recipes)
        {
            var rlEntry = await _recipeLevelRepo.GetByLevelAsync(r.RecipeLevel, ct);
            recipeLinks.Add(new RecipeLink
            {
                RecipeId = r.RecipeId,
                ResultItemName = r.ResultItem?.Name ?? item.Name,
                IconUrl = r.ResultItem?.IconPath ?? item.IconPath,
                CraftType = r.CraftType.ToString(),
                Level = r.RecipeLevel,
                Stars = rlEntry?.Stars ?? 0,
            });
        }

        var usedInLinks = new List<RecipeLink>();
        foreach (var r in usedIn)
        {
            var rlEntry = await _recipeLevelRepo.GetByLevelAsync(r.RecipeLevel, ct);
            usedInLinks.Add(new RecipeLink
            {
                RecipeId = r.RecipeId,
                ResultItemName = r.ResultItem?.Name ?? string.Empty,
                IconUrl = r.ResultItem?.IconPath ?? string.Empty,
                CraftType = r.CraftType.ToString(),
                Level = r.RecipeLevel,
                Stars = rlEntry?.Stars ?? 0,
            });
        }

        var gatheringLinks = gatheringInfos.Select(g => new GatheringLink
        {
            Zone = g.ZoneName,
            Coordinates = g.CoordinateX.HasValue && g.CoordinateY.HasValue
                ? $"{g.CoordinateX:F1}, {g.CoordinateY:F1}"
                : null,
            GatheringType = g.GatheringType.ToString(),
            GatheringClass = g.ClassJob,
            GatheringLevel = g.Level,
            IsTimed = g.IsTimed,
            SpawnTimes = g.SpawnTimes,
        }).ToList();

        var vendorLinks = vendorInfos.Select(v => new VendorLink
        {
            NpcName = v.NpcName,
            Zone = v.ZoneName,
            Cost = v.Cost,
            Currency = v.CurrencyType,
        }).ToList();

        EquipmentLink? equipmentLink = null;
        if (equipment is not null)
        {
            equipmentLink = new EquipmentLink
            {
                EquipmentId = equipment.EquipmentId,
                EquipSlot = equipment.EquipSlot.ToString(),
                Craftsmanship = equipment.Craftsmanship,
                Control = equipment.Control,
                CP = equipment.CP,
                Gathering = equipment.Gathering,
                Perception = equipment.Perception,
                GP = equipment.GP,
            };
        }

        var result = new ContextualLinks
        {
            ItemId = item.ItemId,
            ItemName = item.Name,
            IconUrl = item.IconPath,
            Recipes = recipeLinks,
            CanSimulate = recipeLinks.Count > 0,
            CanSolve = recipeLinks.Count > 0,
            UsedInRecipes = usedInLinks,
            IsEquipment = equipment is not null,
            EquipmentInfo = equipmentLink,
            GatheringInfo = gatheringLinks,
            VendorSources = vendorLinks,
        };

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromHours(1), ct);
        return result;
    }
}
