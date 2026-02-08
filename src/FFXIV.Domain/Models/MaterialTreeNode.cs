using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Models;

public class MaterialTreeNode
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string IconUrl { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public bool IsRawMaterial { get; set; }
    public ItemSourceType SourceType { get; set; }
    public List<ItemSourceSummary> Sources { get; set; } = [];
    public RecipeSummary? Recipe { get; set; }
    public List<MaterialTreeNode> Children { get; set; } = [];
    public int Depth { get; set; }
}
