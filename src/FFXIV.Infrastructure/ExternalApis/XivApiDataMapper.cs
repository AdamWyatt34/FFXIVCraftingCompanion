using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Infrastructure.ExternalApis.Models;

namespace FFXIV.Infrastructure.ExternalApis;

/// <summary>
/// Maps XIVAPI v2 response models to domain entities.
/// </summary>
public static class XivApiDataMapper
{
    private const string AssetBaseUrl = "https://v2.xivapi.com/api/asset";

    public static Item ToItem(int rowId, XivApiItemFields fields)
    {
        return new Item
        {
            ItemId = rowId,
            Name = fields.Name,
            IconPath = BuildIconUrl(fields.Icon),
            ItemLevel = fields.LevelItem?.RowId ?? 0,
            Category = MapItemCategory(fields.ItemSearchCategory?.RowId, fields.ItemUICategory?.RowId),
            CanBeHQ = fields.CanBeHq
        };
    }

    public static Recipe ToRecipe(int rowId, XivApiRecipeFields fields)
    {
        var rlt = fields.RecipeLevelTable?.Fields;

        return new Recipe
        {
            RecipeId = rowId,
            ResultItemId = fields.ItemResult?.RowId ?? 0,
            CraftType = MapCraftType(fields.CraftType?.RowId ?? 0),
            RecipeLevel = rlt?.ClassJobLevel ?? 0,
            Difficulty = CalculateFactoredValue(rlt?.Difficulty ?? 0, fields.DifficultyFactor),
            Quality = CalculateFactoredValue(rlt?.Quality ?? 0, fields.QualityFactor),
            Durability = CalculateFactoredValue(rlt?.Durability ?? 0, fields.DurabilityFactor),
            RequiredCraftsmanship = rlt?.SuggestedCraftsmanship ?? 0,
            RequiredControl = 0,
            Yield = fields.AmountResult > 0 ? fields.AmountResult : 1
        };
    }

    public static List<RecipeIngredient> ToRecipeIngredients(int recipeId, XivApiRecipeFields fields)
    {
        var ingredients = new List<RecipeIngredient>();

        if (fields.Ingredient is null || fields.AmountIngredient is null)
            return ingredients;

        var count = Math.Min(fields.Ingredient.Count, fields.AmountIngredient.Count);

        for (var i = 0; i < count; i++)
        {
            var ingredientRef = fields.Ingredient[i];
            var amount = fields.AmountIngredient[i];

            if (ingredientRef.RowId == 0 || amount <= 0)
                continue;

            ingredients.Add(new RecipeIngredient
            {
                RecipeId = recipeId,
                ItemId = ingredientRef.RowId,
                Quantity = amount,
                CanBeHQ = false // Cross-referenced from Item table by sync service
            });
        }

        return ingredients;
    }

    /// <summary>
    /// CraftType row_id maps directly to our enum by index.
    /// 0=CRP, 1=BSM, 2=ARM, 3=GSM, 4=LTW, 5=WVR, 6=ALC, 7=CUL
    /// </summary>
    public static CraftType MapCraftType(int rowId)
    {
        return Enum.IsDefined(typeof(CraftType), rowId)
            ? (CraftType)rowId
            : CraftType.CRP;
    }

    /// <summary>
    /// Heuristic mapping from XIVAPI ItemSearchCategory/ItemUICategory to our ItemCategory enum.
    /// </summary>
    public static ItemCategory MapItemCategory(int? searchCategoryId, int? uiCategoryId)
    {
        if (searchCategoryId is null or 0)
            return ItemCategory.Other;

        return uiCategoryId switch
        {
            59 => ItemCategory.Material, // Crystals
            >= 47 and <= 58 => ItemCategory.Material, // Metal, Stone, Bone, Cloth, Reagent, etc.
            >= 44 and <= 46 => ItemCategory.Gatherable, // Lumber, Cloth, Leather raw
            _ => ItemCategory.Other
        };
    }

    public static string BuildIconUrl(XivApiIcon? icon)
    {
        if (icon is null || string.IsNullOrEmpty(icon.Path))
            return string.Empty;

        return $"{AssetBaseUrl}?path={Uri.EscapeDataString(icon.Path)}&format=png";
    }

    /// <summary>
    /// Recipe values = baseValue * factor / 100.
    /// </summary>
    private static int CalculateFactoredValue(int baseValue, int factor)
    {
        return baseValue * factor / 100;
    }

    /// <summary>
    /// Maps XIVAPI GatheringType row_id to our GatheringType enum.
    /// 0=Mining, 1=Quarrying, 2=Logging, 3=Harvesting.
    /// </summary>
    public static GatheringType MapGatheringType(int rowId)
    {
        return rowId switch
        {
            0 => GatheringType.Mining,
            1 => GatheringType.Quarrying,
            2 => GatheringType.Logging,
            3 => GatheringType.Harvesting,
            _ => GatheringType.Mining
        };
    }

    /// <summary>
    /// Maps a GatheringType to its corresponding class/job abbreviation.
    /// </summary>
    public static string GatheringTypeToClassJob(GatheringType gatheringType)
    {
        return gatheringType switch
        {
            GatheringType.Mining or GatheringType.Quarrying => "MIN",
            GatheringType.Logging or GatheringType.Harvesting => "BTN",
            GatheringType.Fishing => "FSH",
            _ => "MIN"
        };
    }

    /// <summary>
    /// Maps XIVAPI EquipSlotCategory boolean fields to our EquipSlot enum.
    /// Returns null if no recognized slot is set.
    /// </summary>
    public static EquipSlot? ResolveEquipSlot(XivApiEquipSlotCategoryFields fields)
    {
        if (fields.MainHand != 0) return EquipSlot.MainHand;
        if (fields.OffHand != 0) return EquipSlot.OffHand;
        if (fields.Head != 0) return EquipSlot.Head;
        if (fields.Body != 0) return EquipSlot.Body;
        if (fields.Gloves != 0) return EquipSlot.Hands;
        if (fields.Legs != 0) return EquipSlot.Legs;
        if (fields.Feet != 0) return EquipSlot.Feet;
        if (fields.Ears != 0) return EquipSlot.Earring;
        if (fields.Neck != 0) return EquipSlot.Necklace;
        if (fields.Wrists != 0) return EquipSlot.Bracelet;
        if (fields.FingerL != 0 || fields.FingerR != 0) return EquipSlot.Ring;
        return null;
    }

    /// <summary>
    /// Builds a comma-separated string of DoH/DoL job abbreviations from ClassJobCategory fields.
    /// Returns empty string if no DoH/DoL jobs are set.
    /// </summary>
    public static string BuildClassJobString(XivApiClassJobCategoryFields fields)
    {
        var jobs = new List<string>(11);
        if (fields.CRP) jobs.Add("CRP");
        if (fields.BSM) jobs.Add("BSM");
        if (fields.ARM) jobs.Add("ARM");
        if (fields.GSM) jobs.Add("GSM");
        if (fields.LTW) jobs.Add("LTW");
        if (fields.WVR) jobs.Add("WVR");
        if (fields.ALC) jobs.Add("ALC");
        if (fields.CUL) jobs.Add("CUL");
        if (fields.MIN) jobs.Add("MIN");
        if (fields.BTN) jobs.Add("BTN");
        if (fields.FSH) jobs.Add("FSH");
        return string.Join(",", jobs);
    }

    /// <summary>
    /// Extracts crafting/gathering stats from parallel BaseParam/BaseParamValue arrays.
    /// BaseParam row_ids: 70=Craftsmanship, 71=Control, 11=CP, 72=Gathering, 73=Perception, 10=GP.
    /// </summary>
    public static (int Craftsmanship, int Control, int CP, int Gathering, int Perception, int GP)
        ExtractEquipmentStats(List<int>? baseParams, List<int>? baseParamValues)
    {
        int craftsmanship = 0, control = 0, cp = 0, gathering = 0, perception = 0, gp = 0;

        if (baseParams is null || baseParamValues is null)
            return (craftsmanship, control, cp, gathering, perception, gp);

        var count = Math.Min(baseParams.Count, baseParamValues.Count);
        for (var i = 0; i < count; i++)
        {
            switch (baseParams[i])
            {
                case 70: craftsmanship = baseParamValues[i]; break;
                case 71: control = baseParamValues[i]; break;
                case 11: cp = baseParamValues[i]; break;
                case 72: gathering = baseParamValues[i]; break;
                case 73: perception = baseParamValues[i]; break;
                case 10: gp = baseParamValues[i]; break;
            }
        }

        return (craftsmanship, control, cp, gathering, perception, gp);
    }
}
