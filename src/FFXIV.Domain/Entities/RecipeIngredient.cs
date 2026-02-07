namespace FFXIV.Domain.Entities;

public class RecipeIngredient
{
    public int RecipeId { get; set; }
    public int ItemId { get; set; }
    public int Quantity { get; set; }
    public bool CanBeHQ { get; set; }

    public Recipe Recipe { get; set; } = null!;
    public Item Item { get; set; } = null!;
}
