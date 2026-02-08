using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Models;

public class ShoppingListItem
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public ItemSourceType SourceType { get; set; }
    public List<ItemSourceSummary> Sources { get; set; } = [];
    public string SortCategory { get; set; } = string.Empty;
}
