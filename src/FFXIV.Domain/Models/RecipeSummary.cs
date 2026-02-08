using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Models;

public class RecipeSummary
{
    public int RecipeId { get; set; }
    public CraftType CraftType { get; set; }
    public int RecipeLevel { get; set; }
    public int CraftsmanshipRequired { get; set; }
    public int ControlRequired { get; set; }
    public int YieldPerCraft { get; set; }
}
