using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Models;

public class CraftPlanResult
{
    public List<CraftPlanItemResult> Items { get; set; } = [];
    public List<AggregatedMaterial> RawMaterials { get; set; } = [];
    public List<AggregatedMaterial> Crystals { get; set; } = [];
    public List<AggregatedIntermediateCraft> IntermediateCrafts { get; set; } = [];
    public CraftPlanSummary Summary { get; set; } = new();
}

public class CraftPlanItemResult
{
    public int RecipeId { get; set; }
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public string CraftType { get; set; } = string.Empty;
}

public class AggregatedMaterial
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public ItemSourceType PrimarySourceType { get; set; }
    public List<ItemSourceSummary> Sources { get; set; } = [];
    public string SortCategory { get; set; } = string.Empty;
    public List<MaterialContribution> Contributions { get; set; } = [];
}

public class AggregatedIntermediateCraft
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int RecipeId { get; set; }
    public CraftType CraftType { get; set; }
    public int RecipeLevel { get; set; }
    public int YieldPerCraft { get; set; }
    public int MaxDepth { get; set; }
    public int CraftOrder { get; set; }
}

public class MaterialContribution
{
    public int RecipeId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public int Quantity { get; set; }
}

public class CraftPlanSummary
{
    public int TotalPlanItems { get; set; }
    public int TotalUniqueRawMaterials { get; set; }
    public int TotalUniqueCrystals { get; set; }
    public int TotalIntermediateCrafts { get; set; }
    public int MaxTreeDepth { get; set; }
}
