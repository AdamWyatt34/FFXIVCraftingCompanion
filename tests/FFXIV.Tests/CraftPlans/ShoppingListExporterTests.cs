using System.Text.Json;
using FFXIV.Application.CraftPlans;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;

namespace FFXIV.Tests.CraftPlans;

public class ShoppingListExporterTests
{
    private static CraftPlanResult CreateTestPlan()
    {
        return new CraftPlanResult
        {
            Items =
            [
                new CraftPlanItemResult
                {
                    RecipeId = 10, ItemId = 200, ItemName = "Iron Sword",
                    IconUrl = "icon/200.png", Quantity = 1, CraftType = "BSM"
                }
            ],
            RawMaterials =
            [
                new AggregatedMaterial
                {
                    ItemId = 500, ItemName = "Iron Ore", TotalQuantity = 15,
                    PrimarySourceType = ItemSourceType.Gathered,
                    Sources =
                    [
                        new ItemSourceSummary
                        {
                            SourceType = ItemSourceType.Gathered,
                            Description = "Mining in Western Thanalan (Lv. 25)"
                        }
                    ],
                    SortCategory = "Material",
                    Contributions =
                    [
                        new MaterialContribution { RecipeId = 10, ItemName = "Iron Sword", Quantity = 15 }
                    ]
                }
            ],
            Crystals =
            [
                new AggregatedMaterial
                {
                    ItemId = 2, ItemName = "Fire Crystal", TotalQuantity = 5,
                    PrimarySourceType = ItemSourceType.Gathered,
                    SortCategory = "Crystal",
                    Contributions =
                    [
                        new MaterialContribution { RecipeId = 10, ItemName = "Iron Sword", Quantity = 5 }
                    ]
                }
            ],
            IntermediateCrafts =
            [
                new AggregatedIntermediateCraft
                {
                    ItemId = 400, ItemName = "Iron Ingot", TotalQuantity = 3,
                    RecipeId = 1400, CraftType = CraftType.BSM, RecipeLevel = 50,
                    YieldPerCraft = 1, MaxDepth = 1, CraftOrder = 1
                }
            ],
            Summary = new CraftPlanSummary
            {
                TotalPlanItems = 1,
                TotalUniqueRawMaterials = 1,
                TotalUniqueCrystals = 1,
                TotalIntermediateCrafts = 1,
                MaxTreeDepth = 2
            }
        };
    }

    [Fact]
    public void TextExport_ContainsCorrectSections()
    {
        var plan = CreateTestPlan();

        var text = ShoppingListExporter.ExportAsText(plan);

        text.Should().Contain("=== Items to Craft ===");
        text.Should().Contain("Iron Sword x1 (BSM)");
        text.Should().Contain("=== Intermediate Crafts ===");
        text.Should().Contain("1. Iron Ingot x3 (BSM, Lv.50)");
        text.Should().Contain("=== Raw Materials ===");
        text.Should().Contain("Iron Ore x15");
        text.Should().Contain("Mining in Western Thanalan (Lv. 25)");
        text.Should().Contain("=== Crystals ===");
        text.Should().Contain("Fire Crystal x5");
        text.Should().Contain("=== Summary ===");
    }

    [Fact]
    public void JsonExport_ProducesValidJson()
    {
        var plan = CreateTestPlan();

        var json = ShoppingListExporter.ExportAsJson(plan);

        var act = () => JsonDocument.Parse(json);
        act.Should().NotThrow();

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("rawMaterials").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("crystals").GetArrayLength().Should().Be(1);
        doc.RootElement.GetProperty("intermediateCrafts").GetArrayLength().Should().Be(1);
    }

    [Fact]
    public void TeamcraftExport_ProducesItemIdQuantityPerLine()
    {
        var plan = CreateTestPlan();

        var teamcraft = ShoppingListExporter.ExportAsTeamcraft(plan);

        var lines = teamcraft.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // 1 final item + 1 intermediate + 1 raw + 1 crystal = 4
        lines.Should().HaveCount(4);
    }

    [Fact]
    public void TeamcraftExport_CorrectLineCount()
    {
        var plan = CreateTestPlan();

        var teamcraft = ShoppingListExporter.ExportAsTeamcraft(plan);

        var lines = teamcraft.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        // 1 final item + 1 intermediate craft + 1 raw material + 1 crystal = 4
        lines.Should().HaveCount(4);
        lines.Should().Contain("200,1");   // Final: Iron Sword
        lines.Should().Contain("400,3");   // Intermediate: Iron Ingot
        lines.Should().Contain("500,15");  // Raw: Iron Ore
        lines.Should().Contain("2,5");     // Crystal: Fire Crystal
    }

    [Fact]
    public void EmptyPlan_HandlesGracefully()
    {
        var plan = new CraftPlanResult
        {
            Summary = new CraftPlanSummary()
        };

        var text = ShoppingListExporter.ExportAsText(plan);
        text.Should().Contain("=== Items to Craft ===");
        text.Should().Contain("Plan Items: 0");

        var json = ShoppingListExporter.ExportAsJson(plan);
        var act = () => JsonDocument.Parse(json);
        act.Should().NotThrow();

        var teamcraft = ShoppingListExporter.ExportAsTeamcraft(plan);
        teamcraft.Trim().Should().BeEmpty();
    }
}
