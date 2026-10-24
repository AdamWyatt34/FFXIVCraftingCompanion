using System.Text.Json;
using FFXIV.Infrastructure.ExternalApis;
using FFXIV.Infrastructure.ExternalApis.Models;

namespace FFXIV.Tests.DataSync;

public class XivApiDataMapperTests
{
    // Recipe-sheet rows in XIVAPI v2's shape; the required values are the live sheet's
    // (Recipe/35423 = 3700/3280, Recipe/35400 = 0/0), the RecipeLevelTable values differ on purpose.
    private const string Recipe35423 = """
        {
          "ItemResult": { "value": 38896, "sheet": "Item", "row_id": 38896 },
          "CraftType": { "value": 2, "sheet": "CraftType", "row_id": 2 },
          "RecipeLevelTable": {
            "value": 620, "sheet": "RecipeLevelTable", "row_id": 620,
            "fields": { "ClassJobLevel": 90, "Stars": 3, "Difficulty": 5720, "Quality": 12900, "Durability": 70,
                        "SuggestedCraftsmanship": 4045, "SuggestedControl": 3792 }
          },
          "DifficultyFactor": 100, "QualityFactor": 100, "DurabilityFactor": 100,
          "AmountResult": 1, "CanHq": true, "MaterialQualityFactor": 50,
          "RequiredCraftsmanship": 3700, "RequiredControl": 3280
        }
        """;

    private const string Recipe35400 = """
        {
          "ItemResult": { "value": 38800, "sheet": "Item", "row_id": 38800 },
          "RecipeLevelTable": {
            "value": 535, "sheet": "RecipeLevelTable", "row_id": 535,
            "fields": { "ClassJobLevel": 80, "SuggestedCraftsmanship": 2549, "SuggestedControl": 0 }
          },
          "RequiredCraftsmanship": 0, "RequiredControl": 0
        }
        """;

    private const string RecipeWithoutRequiredFields = """
        {
          "ItemResult": { "value": 5000, "sheet": "Item", "row_id": 5000 },
          "RecipeLevelTable": {
            "value": 50, "sheet": "RecipeLevelTable", "row_id": 50,
            "fields": { "ClassJobLevel": 50, "SuggestedCraftsmanship": 136, "SuggestedControl": 0 }
          }
        }
        """;

    private static XivApiRecipeFields Parse(string json) =>
        JsonSerializer.Deserialize<XivApiRecipeFields>(json, JsonSerializerOptions.Web)!;

    [Fact]
    public void Required_stats_come_from_the_recipe_sheet()
    {
        var recipe = XivApiDataMapper.ToRecipe(35423, Parse(Recipe35423));

        recipe.RequiredCraftsmanship.Should().Be(3700);
        recipe.RequiredControl.Should().Be(3280);
    }

    [Fact]
    public void A_recommended_level_table_value_is_never_a_requirement()
    {
        var recipe = XivApiDataMapper.ToRecipe(35400, Parse(Recipe35400));

        recipe.RequiredCraftsmanship.Should().Be(0);
        recipe.RequiredControl.Should().Be(0);
    }

    [Fact]
    public void Missing_required_fields_mean_no_requirement()
    {
        var recipe = XivApiDataMapper.ToRecipe(5000, Parse(RecipeWithoutRequiredFields));

        recipe.RequiredCraftsmanship.Should().Be(0);
        recipe.RequiredControl.Should().Be(0);
    }
}
