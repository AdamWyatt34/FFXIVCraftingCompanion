using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Entities;

public class Recipe
{
    public int RecipeId { get; set; }
    public int ResultItemId { get; set; }
    public CraftType CraftType { get; set; }
    public int RecipeLevel { get; set; }
    public int Difficulty { get; set; }
    public int Quality { get; set; }
    public int Durability { get; set; }
    public int RequiredCraftsmanship { get; set; }
    public int RequiredControl { get; set; }
    public int Yield { get; set; } = 1;
    public int MaterialQualityFactor { get; set; }

    public Item ResultItem { get; set; } = null!;
    public ICollection<RecipeIngredient> Ingredients { get; set; } = new List<RecipeIngredient>();
}
