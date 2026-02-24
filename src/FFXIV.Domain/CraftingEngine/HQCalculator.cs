using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine;

public static class HQCalculator
{
    public static int CalculateStartingQuality(
        int maxQuality,
        int materialQualityFactor,
        List<RecipeIngredientInfo> ingredients,
        List<HQIngredientInput> hqIngredients)
    {
        if (materialQualityFactor <= 0 || maxQuality <= 0)
            return 0;

        if (hqIngredients is null || hqIngredients.Count == 0)
            return 0;

        int totalHQableItems = 0;
        int totalHQItems = 0;

        foreach (var ingredient in ingredients)
        {
            if (!ingredient.CanBeHQ)
                continue;

            totalHQableItems += ingredient.Quantity;

            var hqInput = hqIngredients.FirstOrDefault(h => h.ItemId == ingredient.ItemId);
            if (hqInput is not null)
            {
                totalHQItems += Math.Min(hqInput.HQQuantity, ingredient.Quantity);
            }
        }

        if (totalHQableItems == 0 || totalHQItems == 0)
            return 0;

        int maxStartingQuality = (int)Math.Floor((double)maxQuality * materialQualityFactor / 100);
        return (int)Math.Floor((double)maxStartingQuality * totalHQItems / totalHQableItems);
    }
}

public sealed record HQIngredientInput(int ItemId, int HQQuantity);
