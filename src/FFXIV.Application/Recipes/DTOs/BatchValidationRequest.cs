namespace FFXIV.Application.Recipes.DTOs;

public sealed class BatchValidationRequest
{
    public List<int>? RecipeIds { get; set; }
    public string? RandomSpec { get; set; }
}
