namespace FFXIV.Domain.Entities;

public class FavoriteRecipe
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int RecipeId { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Recipe Recipe { get; set; } = null!;
}
