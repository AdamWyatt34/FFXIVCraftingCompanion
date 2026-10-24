using FFXIV.Domain.CraftingEngine.Models;

namespace FFXIV.Domain.CraftingEngine;

public static class HQCalculator
{
    // Each HQ material weighs its item level times its count (Raphael raphael-data/src/lib.rs:203-239; Teamcraft
    // recipes.extractor.ts:60-73,100-106); data without item levels weighs by count, so the total is never zero.
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

        long totalItemLevel = 0;
        long providedItemLevel = 0;
        long totalCount = 0;
        long providedCount = 0;

        foreach (var ingredient in ingredients)
        {
            if (!ingredient.CanBeHQ)
                continue;

            totalItemLevel += (long)ingredient.ItemLevel * ingredient.Quantity;
            totalCount += ingredient.Quantity;

            var hqInput = hqIngredients.FirstOrDefault(h => h.ItemId == ingredient.ItemId);
            if (hqInput is not null)
            {
                var hqQuantity = Math.Min(hqInput.HQQuantity, ingredient.Quantity);
                providedItemLevel += (long)ingredient.ItemLevel * hqQuantity;
                providedCount += hqQuantity;
            }
        }

        var (provided, total) = totalItemLevel > 0
            ? (providedItemLevel, totalItemLevel)
            : (providedCount, totalCount);

        if (total == 0 || provided <= 0)
            return 0;

        return (int)((long)maxQuality * materialQualityFactor * provided / total / 100);
    }
}

public sealed record HQIngredientInput(int ItemId, int HQQuantity);
