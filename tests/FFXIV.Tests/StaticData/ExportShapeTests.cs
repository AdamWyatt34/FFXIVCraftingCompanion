using FFXIV.Application.Recipes.DTOs;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Export;

namespace FFXIV.Tests.StaticData;

public class ExportShapeTests
{
    [Theory]
    [InlineData(35649, "https://v2.xivapi.com/api/asset?path=ui%2Ficon%2F035000%2F035649.tex&format=png")]
    [InlineData(20015, "https://v2.xivapi.com/api/asset?path=ui%2Ficon%2F020000%2F020015.tex&format=png")]
    [InlineData(999, "https://v2.xivapi.com/api/asset?path=ui%2Ficon%2F000000%2F000999.tex&format=png")]
    public void Icon_ids_round_trip_through_the_xivapi_template(int icon, string url)
    {
        IconUrl.FromId(icon).Should().Be(url);
        IconUrl.ToId(url).Should().Be(icon);
    }

    [Fact]
    public void Icon_url_outside_the_template_is_rejected()
    {
        var act = () => IconUrl.ToId("https://v2.xivapi.com/api/asset?path=ui%2Ficon%2F035000%2F036649.tex&format=png");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Gear_frontier_keeps_exactly_the_pieces_a_level_query_can_return()
    {
        // Three pieces per equip level, item level rising with equip level; limit 2 per slot.
        var equipment = new List<Equipment>();
        var id = 1;
        foreach (var level in new[] { 10, 20, 30 })
            for (var k = 0; k < 3; k++)
                equipment.Add(new Equipment { EquipmentId = id++, EquipSlot = EquipSlot.Head, EquipLevel = level, ItemLevel = level * 10 + k });

        var frontier = GearFrontier.Build(equipment, limit: 2)["Head"];

        // At level 10 the top two are ids 3 and 2; at 20 they are 6 and 5; at 30, 9 and 8.
        // Every other piece is beaten by two higher pieces already equippable at its own level.
        frontier.Select(e => e.EquipmentId).Should().Equal(9, 8, 6, 5, 3, 2);
    }

    [Fact]
    public void Gear_frontier_breaks_item_level_ties_by_id()
    {
        var equipment = Enumerable.Range(1, 4)
            .Select(i => new Equipment { EquipmentId = 5 - i, EquipSlot = EquipSlot.Ring, EquipLevel = 1, ItemLevel = 100 })
            .ToList();

        GearFrontier.Build(equipment, limit: 3)["Ring"].Select(e => e.EquipmentId).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void Gear_frontier_keeps_the_best_crafted_pieces_for_the_crafted_only_pool()
    {
        // Five pieces at one level, item level descending with id; the fourth is crafted, so it
        // leads the crafted-only pool although it is outside the overall top two.
        var equipment = Enumerable.Range(1, 5)
            .Select(i => new Equipment
            {
                EquipmentId = i, EquipSlot = EquipSlot.Body, EquipLevel = 50, ItemLevel = 100 - i,
                SourceType = i == 4 ? ItemSourceType.Crafted : ItemSourceType.Vendor,
            })
            .ToList();

        GearFrontier.Build(equipment, limit: 2)["Body"].Select(e => e.EquipmentId).Should().Equal(1, 2, 4);
    }

    [Fact]
    public void Material_graph_factors_items_out_of_the_tree_and_flags_truncated_occurrences()
    {
        var sources = new List<ItemSourceDto> { new("Gathered", "Mined somewhere", "Zone", null, null, null, 10, "MIN") };
        // Ingot yields 3 per craft; a quantity of 1 is one craft, so child quantities are per craft.
        var ore = new MaterialTreeNodeDto(100, "Ore", "icon", 4, true, "Gathered", sources, null, [], 1);
        var crystal = new MaterialTreeNodeDto(2, "Fire Shard", "icon", 2, true, "Gathered", [], null, [], 1);
        var ingotRecipe = new RecipeSummaryDto(7, "BSM", "Blacksmith", 10, 0, 0, 3);
        // The same ingot appears again deeper as a cycle-cut raw leaf.
        var ingotCut = new MaterialTreeNodeDto(200, "Ingot", "icon", 1, true, "Gathered", [], null, [], 2);
        var ingot = new MaterialTreeNodeDto(200, "Ingot", "icon", 1, false, "Crafted", [], ingotRecipe, [ore, crystal, ingotCut], 0);
        var tree = new MaterialTreeResultDto(
            ingot,
            [new ShoppingListItemDto(100, "Ore", "icon", 4, "Gathered", sources, "Gathered", false)],
            [],
            [new ShoppingListItemDto(2, "Fire Shard", "icon", 2, "Gathered", [], "Crystal", false)],
            new TreeSummaryDto(1, 1, 0, 2, 1));

        var crystals = new Dictionary<int, MaterialItemExport>();
        var graph = MaterialGraph.Build(tree, crystals);

        graph.Items.Keys.Should().BeEquivalentTo([100, 200]);
        graph.Items[200].Recipe.Should().Be(ingotRecipe);
        graph.Items[200].Category.Should().Be("Crafted");
        graph.Items[100].Category.Should().Be("Gathered");
        graph.Tree.Children.Select(c => c.ItemId).Should().Equal(100, 2, 200);
        graph.Tree.Children.Select(c => c.PerCraft).Should().Equal(4, 2, 1);
        graph.Tree.PerCraft.Should().Be(1);
        graph.Tree.Children[2].Truncated.Should().BeTrue();
        graph.Tree.Truncated.Should().BeFalse();
        crystals.Should().ContainKey(2);
        crystals[2].Category.Should().Be("Crystal");
    }

    [Fact]
    public void Material_graph_divides_the_parent_crafts_out_of_child_quantities()
    {
        // Root needs 5 planks; planks yield 2, so 3 crafts; each craft needs 4 logs: 12 logs.
        var log = new MaterialTreeNodeDto(10, "Log", "icon", 12, true, "Gathered", [], null, [], 2);
        var plank = new MaterialTreeNodeDto(20, "Plank", "icon", 5, false, "Crafted", [], new RecipeSummaryDto(2, "CRP", "Carpenter", 5, 0, 0, 2), [log], 1);
        var root = new MaterialTreeNodeDto(30, "Chair", "icon", 1, false, "Crafted", [], new RecipeSummaryDto(3, "CRP", "Carpenter", 10, 0, 0, 1), [plank], 0);
        var tree = new MaterialTreeResultDto(root, [], [], [], new TreeSummaryDto(1, 0, 1, 2, 1));

        var graph = MaterialGraph.Build(tree);

        graph.Tree.Children[0].PerCraft.Should().Be(5);
        graph.Tree.Children[0].Children[0].PerCraft.Should().Be(4);
        MaterialGraph.CraftsNeeded(5, 2).Should().Be(3);
        MaterialGraph.CraftsNeeded(1, 0).Should().Be(1);
    }

    [Fact]
    public void Material_graph_prefers_the_crafted_occurrence_whichever_comes_first()
    {
        var recipe = new RecipeSummaryDto(7, "BSM", "Blacksmith", 10, 0, 0, 1);
        var cutFirst = new MaterialTreeNodeDto(200, "Ingot", "icon", 1, true, "Gathered", [], null, [], 1);
        var crafted = new MaterialTreeNodeDto(200, "Ingot", "icon", 1, false, "Crafted", [], recipe, [], 1);
        var root = new MaterialTreeNodeDto(1, "Root", "icon", 1, false, "Crafted", [], new RecipeSummaryDto(1, "CRP", "Carpenter", 1, 0, 0, 1), [cutFirst, crafted], 0);
        var tree = new MaterialTreeResultDto(root, [], [], [], new TreeSummaryDto(0, 0, 1, 1, 1));

        var graph = MaterialGraph.Build(tree);

        graph.Items[200].Recipe.Should().Be(recipe);
        graph.Tree.Children[0].Truncated.Should().BeTrue();
        graph.Tree.Children[1].Truncated.Should().BeFalse();
    }
}
