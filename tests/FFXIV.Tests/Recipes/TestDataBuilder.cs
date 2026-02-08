using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;

namespace FFXIV.Tests.Recipes;

internal static class TestDataBuilder
{
    // Item IDs
    public const int TestSwordId = 100;
    public const int TestIngotId = 101;
    public const int TestHandleId = 102;
    public const int TestOreId = 103;
    public const int TestLumberId = 104;
    public const int TestLogId = 105;
    public const int FireCrystalId = 2;
    public const int WindCrystalId = 4;

    public static Item CreateItem(int id, string name) => new()
    {
        ItemId = id,
        Name = name,
        IconPath = $"https://v2.xivapi.com/api/asset?path=icon/{id}.png&format=png",
        ItemLevel = 50,
        Category = ItemCategory.Material,
        CanBeHQ = false
    };

    public static Recipe CreateRecipe(int recipeId, int resultItemId, string resultItemName,
        int yield = 1, params (int ItemId, string Name, int Qty)[] ingredients)
    {
        var resultItem = CreateItem(resultItemId, resultItemName);
        var recipe = new Recipe
        {
            RecipeId = recipeId,
            ResultItemId = resultItemId,
            CraftType = CraftType.BSM,
            RecipeLevel = 50,
            Difficulty = 1000,
            Quality = 2000,
            Durability = 80,
            RequiredCraftsmanship = 400,
            RequiredControl = 350,
            Yield = yield,
            ResultItem = resultItem,
            Ingredients = ingredients.Select(i => new RecipeIngredient
            {
                RecipeId = recipeId,
                ItemId = i.ItemId,
                Quantity = i.Qty,
                CanBeHQ = false,
                Item = CreateItem(i.ItemId, i.Name)
            }).ToList()
        };
        return recipe;
    }

    /// <summary>
    /// Test Sword = 3x Test Ingot + 1x Test Handle
    /// </summary>
    public static Recipe CreateTestSwordRecipe() => CreateRecipe(
        1, TestSwordId, "Test Sword", 1,
        (TestIngotId, "Test Ingot", 3),
        (TestHandleId, "Test Handle", 1));

    /// <summary>
    /// Test Ingot = 5x Test Ore + 1x Fire Crystal
    /// </summary>
    public static Recipe CreateTestIngotRecipe() => CreateRecipe(
        2, TestIngotId, "Test Ingot", 1,
        (TestOreId, "Test Ore", 5),
        (FireCrystalId, "Fire Crystal", 1));

    /// <summary>
    /// Test Handle = 2x Test Lumber + 1x Wind Crystal
    /// </summary>
    public static Recipe CreateTestHandleRecipe() => CreateRecipe(
        3, TestHandleId, "Test Handle", 1,
        (TestLumberId, "Test Lumber", 2),
        (WindCrystalId, "Wind Crystal", 1));

    /// <summary>
    /// Test Lumber = 4x Test Log + 1x Wind Crystal
    /// </summary>
    public static Recipe CreateTestLumberRecipe() => CreateRecipe(
        4, TestLumberId, "Test Lumber", 1,
        (TestLogId, "Test Log", 4),
        (WindCrystalId, "Wind Crystal", 1));
}
