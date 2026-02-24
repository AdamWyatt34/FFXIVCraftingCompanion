namespace FFXIV.Domain.Models;

public class ContextualLinks
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public List<RecipeLink> Recipes { get; set; } = [];
    public bool CanSimulate { get; set; }
    public bool CanSolve { get; set; }
    public List<RecipeLink> UsedInRecipes { get; set; } = [];
    public bool IsEquipment { get; set; }
    public EquipmentLink? EquipmentInfo { get; set; }
    public List<GatheringLink> GatheringInfo { get; set; } = [];
    public List<VendorLink> VendorSources { get; set; } = [];
}

public class RecipeLink
{
    public int RecipeId { get; set; }
    public string ResultItemName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public string CraftType { get; set; } = string.Empty;
    public int Level { get; set; }
    public int Stars { get; set; }
}

public class EquipmentLink
{
    public int EquipmentId { get; set; }
    public string EquipSlot { get; set; } = string.Empty;
    public int Craftsmanship { get; set; }
    public int Control { get; set; }
    public int CP { get; set; }
    public int Gathering { get; set; }
    public int Perception { get; set; }
    public int GP { get; set; }
}

public class GatheringLink
{
    public string Zone { get; set; } = string.Empty;
    public string? Coordinates { get; set; }
    public string GatheringType { get; set; } = string.Empty;
    public string GatheringClass { get; set; } = string.Empty;
    public int GatheringLevel { get; set; }
    public bool IsTimed { get; set; }
    public string? SpawnTimes { get; set; }
}

public class VendorLink
{
    public string NpcName { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public int Cost { get; set; }
    public string Currency { get; set; } = "Gil";
}
