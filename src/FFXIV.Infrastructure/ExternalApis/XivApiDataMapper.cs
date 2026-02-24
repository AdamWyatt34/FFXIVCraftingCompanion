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
            Yield = fields.AmountResult > 0 ? fields.AmountResult : 1,
            MaterialQualityFactor = fields.MaterialQualityFactor
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

    public static RecipeLevelEntry ToRecipeLevelEntry(int rowId, XivApiRecipeLevelTableFields fields)
    {
        return new RecipeLevelEntry
        {
            RecipeLevel = rowId,
            ClassJobLevel = fields.ClassJobLevel,
            Stars = fields.Stars,
            Difficulty = fields.Difficulty,
            Quality = fields.Quality,
            Durability = fields.Durability,
            SuggestedCraftsmanship = fields.SuggestedCraftsmanship,
            SuggestedControl = fields.SuggestedControl,
            ProgressDivider = fields.ProgressDivider,
            ProgressModifier = fields.ProgressModifier,
            QualityDivider = fields.QualityDivider,
            QualityModifier = fields.QualityModifier
        };
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
    /// Builds a comma-separated string of job abbreviations from ClassJobCategory fields.
    /// Returns empty string if no jobs are set.
    /// </summary>
    public static string BuildClassJobString(XivApiClassJobCategoryFields fields)
    {
        var jobs = new List<string>(32);
        // DoH
        if (fields.CRP) jobs.Add("CRP");
        if (fields.BSM) jobs.Add("BSM");
        if (fields.ARM) jobs.Add("ARM");
        if (fields.GSM) jobs.Add("GSM");
        if (fields.LTW) jobs.Add("LTW");
        if (fields.WVR) jobs.Add("WVR");
        if (fields.ALC) jobs.Add("ALC");
        if (fields.CUL) jobs.Add("CUL");
        // DoL
        if (fields.MIN) jobs.Add("MIN");
        if (fields.BTN) jobs.Add("BTN");
        if (fields.FSH) jobs.Add("FSH");
        // Tank
        if (fields.PLD) jobs.Add("PLD");
        if (fields.WAR) jobs.Add("WAR");
        if (fields.DRK) jobs.Add("DRK");
        if (fields.GNB) jobs.Add("GNB");
        // Healer
        if (fields.WHM) jobs.Add("WHM");
        if (fields.SCH) jobs.Add("SCH");
        if (fields.AST) jobs.Add("AST");
        if (fields.SGE) jobs.Add("SGE");
        // Melee DPS
        if (fields.MNK) jobs.Add("MNK");
        if (fields.DRG) jobs.Add("DRG");
        if (fields.NIN) jobs.Add("NIN");
        if (fields.SAM) jobs.Add("SAM");
        if (fields.RPR) jobs.Add("RPR");
        if (fields.VPR) jobs.Add("VPR");
        // Ranged DPS
        if (fields.BRD) jobs.Add("BRD");
        if (fields.MCH) jobs.Add("MCH");
        if (fields.DNC) jobs.Add("DNC");
        // Magic DPS
        if (fields.BLM) jobs.Add("BLM");
        if (fields.SMN) jobs.Add("SMN");
        if (fields.RDM) jobs.Add("RDM");
        if (fields.PCT) jobs.Add("PCT");
        return string.Join(",", jobs);
    }

    /// <summary>
    /// Extracts all equipment stats from parallel BaseParam/BaseParamValue arrays.
    /// </summary>
    public static EquipmentStatBlock ExtractEquipmentStats(List<int>? baseParams, List<int>? baseParamValues)
    {
        var stats = new EquipmentStatBlock();

        if (baseParams is null || baseParamValues is null)
            return stats;

        var count = Math.Min(baseParams.Count, baseParamValues.Count);
        for (var i = 0; i < count; i++)
        {
            var value = baseParamValues[i];
            switch (baseParams[i])
            {
                // DoH/DoL
                case 70: stats.Craftsmanship = value; break;
                case 71: stats.Control = value; break;
                case 11: stats.CP = value; break;
                case 72: stats.Gathering = value; break;
                case 73: stats.Perception = value; break;
                case 10: stats.GP = value; break;
                // Combat primary
                case 1: stats.Strength = value; break;
                case 2: stats.Dexterity = value; break;
                case 3: stats.Vitality = value; break;
                case 4: stats.Intelligence = value; break;
                case 5: stats.Mind = value; break;
                // Combat secondary
                case 6: stats.Piety = value; break;
                case 19: stats.Tenacity = value; break;
                case 22: stats.DirectHitRate = value; break;
                case 27: stats.CriticalHit = value; break;
                case 44: stats.Determination = value; break;
                case 45: stats.SkillSpeed = value; break;
                case 46: stats.SpellSpeed = value; break;
                // Combat weapon/defense
                case 12: stats.PhysicalDamage = value; break;
                case 21: stats.Defense = value; break;
                case 24: stats.MagicDefense = value; break;
            }
        }

        return stats;
    }

    public sealed class EquipmentStatBlock
    {
        // DoH/DoL
        public int Craftsmanship { get; set; }
        public int Control { get; set; }
        public int CP { get; set; }
        public int Gathering { get; set; }
        public int Perception { get; set; }
        public int GP { get; set; }
        // Combat
        public int Vitality { get; set; }
        public int Strength { get; set; }
        public int Dexterity { get; set; }
        public int Intelligence { get; set; }
        public int Mind { get; set; }
        public int Piety { get; set; }
        public int Tenacity { get; set; }
        public int DirectHitRate { get; set; }
        public int CriticalHit { get; set; }
        public int Determination { get; set; }
        public int SkillSpeed { get; set; }
        public int SpellSpeed { get; set; }
        public int PhysicalDamage { get; set; }
        public int Defense { get; set; }
        public int MagicDefense { get; set; }
    }
}
