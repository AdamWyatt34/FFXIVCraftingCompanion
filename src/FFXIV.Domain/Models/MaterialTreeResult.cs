namespace FFXIV.Domain.Models;

public class MaterialTreeResult
{
    public required MaterialTreeNode RootItem { get; set; }
    public List<ShoppingListItem> ShoppingList { get; set; } = [];
    public int TotalUniqueItems { get; set; }
    public int TreeDepth { get; set; }
    public List<ShoppingListItem> CrystalRequirements { get; set; } = [];
    public List<ShoppingListItem> IntermediateCrafts { get; set; } = [];
}
