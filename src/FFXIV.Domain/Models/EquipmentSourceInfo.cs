using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Models;

public class EquipmentSourceInfo
{
    public ItemSourceType SourceType { get; set; }
    public int? RecipeId { get; set; }
    public string? VendorName { get; set; }
    public int? VendorCost { get; set; }
    public string? CurrencyType { get; set; }
    public int? ScripCost { get; set; }
    public string Description { get; set; } = string.Empty;
}
