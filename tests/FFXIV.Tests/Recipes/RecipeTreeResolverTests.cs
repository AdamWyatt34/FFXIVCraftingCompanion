using FFXIV.Application.Recipes;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.Recipes;

public class RecipeTreeResolverTests
{
    private readonly IRecipeRepository _recipeRepo = Substitute.For<IRecipeRepository>();
    private readonly IItemRepository _itemRepo = Substitute.For<IItemRepository>();
    private readonly IItemSourceRepository _itemSourceRepo = Substitute.For<IItemSourceRepository>();
    private readonly IGatheringInfoRepository _gatheringRepo = Substitute.For<IGatheringInfoRepository>();
    private readonly IVendorInfoRepository _vendorRepo = Substitute.For<IVendorInfoRepository>();
    private readonly RecipeTreeResolver _resolver;

    public RecipeTreeResolverTests()
    {
        // Default: empty source data
        _itemSourceRepo.GetByItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, List<ItemSource>>());
        _gatheringRepo.GetByItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, List<GatheringInfo>>());
        _vendorRepo.GetByItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, List<VendorInfo>>());

        _resolver = new RecipeTreeResolver(_recipeRepo, _itemRepo, _itemSourceRepo, _gatheringRepo, _vendorRepo);
    }

    [Fact]
    public async Task SimpleRecipe_ReturnsTreeWithRawIngredients()
    {
        // Recipe: Simple Potion = 3x Herb + 2x Water
        var recipe = TestDataBuilder.CreateRecipe(10, 200, "Simple Potion", 1,
            (201, "Herb", 3),
            (202, "Water", 2));

        SetupSingleRecipe(200, recipe);
        SetupNoRecipes(201, 202);
        SetupItem(200, "Simple Potion");

        var result = await _resolver.ResolveTreeAsync(200);

        result.RootItem.ItemId.Should().Be(200);
        result.RootItem.IsRawMaterial.Should().BeFalse();
        result.RootItem.Children.Should().HaveCount(2);
        result.RootItem.Children.Should().AllSatisfy(c => c.IsRawMaterial.Should().BeTrue());
        result.ShoppingList.Should().HaveCount(2);
        result.TreeDepth.Should().Be(1);
    }

    [Fact]
    public async Task NestedRecipe_ReturnsMultiLevelTree()
    {
        // Sword → Ingot + Handle → Ore + Lumber → Log
        SetupFullTestGraph();

        var result = await _resolver.ResolveTreeAsync(TestDataBuilder.TestSwordId);

        result.RootItem.ItemId.Should().Be(TestDataBuilder.TestSwordId);
        result.RootItem.IsRawMaterial.Should().BeFalse();
        result.RootItem.Children.Should().HaveCount(2);

        // Ingot child
        var ingotNode = result.RootItem.Children.First(c => c.ItemId == TestDataBuilder.TestIngotId);
        ingotNode.IsRawMaterial.Should().BeFalse();
        ingotNode.Children.Should().HaveCount(2); // Ore + Fire Crystal

        // Handle → Lumber → Log chain
        var handleNode = result.RootItem.Children.First(c => c.ItemId == TestDataBuilder.TestHandleId);
        handleNode.IsRawMaterial.Should().BeFalse();
        var lumberNode = handleNode.Children.First(c => c.ItemId == TestDataBuilder.TestLumberId);
        lumberNode.IsRawMaterial.Should().BeFalse();

        result.TreeDepth.Should().Be(3); // Sword → Handle → Lumber → Log
    }

    [Fact]
    public async Task QuantityScaling_MultipliesCorrectly()
    {
        // Craft 5 swords. Each needs 3 ingots → 15 ingots total.
        // Each ingot needs 5 ore → 15 * 5 = 75 ore.
        SetupFullTestGraph();

        var result = await _resolver.ResolveTreeAsync(TestDataBuilder.TestSwordId, quantity: 5);

        // Root should need 5
        result.RootItem.Quantity.Should().Be(5);

        // Ingot node should be 3*5 = 15
        var ingotNode = result.RootItem.Children.First(c => c.ItemId == TestDataBuilder.TestIngotId);
        ingotNode.Quantity.Should().Be(15);

        // Ore in shopping list should be 15 * 5 = 75
        var oreItem = result.ShoppingList.FirstOrDefault(s => s.ItemId == TestDataBuilder.TestOreId);
        oreItem.Should().NotBeNull();
        oreItem!.TotalQuantity.Should().Be(75);
    }

    [Fact]
    public async Task SharedSubMaterials_AggregatesInShoppingList()
    {
        // Wind Crystal appears in both Handle recipe (1x) and Lumber recipe (1x).
        // Handle needs 1 Wind Crystal directly.
        // Handle needs 2 Lumber, each Lumber needs 1 Wind Crystal = 2 more.
        // Total Wind Crystals = 1 + 2 = 3 (for 1 sword).
        SetupFullTestGraph();

        var result = await _resolver.ResolveTreeAsync(TestDataBuilder.TestSwordId);

        var windCrystal = result.CrystalRequirements
            .FirstOrDefault(c => c.ItemId == TestDataBuilder.WindCrystalId);
        windCrystal.Should().NotBeNull();
        windCrystal!.TotalQuantity.Should().Be(3); // 1 from Handle + 2 from Lumber (2 crafts * 1 each)
    }

    [Fact]
    public async Task Crystals_AreTreatedAsTerminalNodes()
    {
        SetupFullTestGraph();

        var result = await _resolver.ResolveTreeAsync(TestDataBuilder.TestSwordId);

        // Fire Crystal in ingot recipe should be a leaf
        var ingotNode = result.RootItem.Children.First(c => c.ItemId == TestDataBuilder.TestIngotId);
        var crystalNode = ingotNode.Children.First(c => c.ItemId == TestDataBuilder.FireCrystalId);
        crystalNode.IsRawMaterial.Should().BeTrue();
        crystalNode.Children.Should().BeEmpty();

        // Crystals should be in CrystalRequirements, not ShoppingList
        result.CrystalRequirements.Should().Contain(c => c.ItemId == TestDataBuilder.FireCrystalId);
        result.ShoppingList.Should().NotContain(s => s.ItemId == TestDataBuilder.FireCrystalId);
    }

    [Fact]
    public async Task MultiYieldRecipe_CalculatesCraftCountCorrectly()
    {
        // Recipe yields 3. Need 5 → ceil(5/3) = 2 crafts → 2 * ingredient qty per craft.
        var recipe = TestDataBuilder.CreateRecipe(10, 300, "Bulk Item", yield: 3,
            (301, "Raw Material A", 2));

        SetupSingleRecipe(300, recipe);
        SetupNoRecipes(301);
        SetupItem(300, "Bulk Item");

        var result = await _resolver.ResolveTreeAsync(300, quantity: 5);

        // 2 crafts * 2 raw per craft = 4 raw material needed
        var rawNode = result.RootItem.Children.First(c => c.ItemId == 301);
        rawNode.Quantity.Should().Be(4); // ceil(5/3)=2, 2*2=4

        result.RootItem.Recipe.Should().NotBeNull();
        result.RootItem.Recipe!.YieldPerCraft.Should().Be(3);
    }

    [Fact]
    public async Task CircularReference_DoesNotInfiniteLoop()
    {
        // Recipe A → ingredient B, Recipe B → ingredient A (cycle)
        var recipeA = TestDataBuilder.CreateRecipe(10, 400, "Item A", 1,
            (401, "Item B", 1));
        var recipeB = TestDataBuilder.CreateRecipe(11, 401, "Item B", 1,
            (400, "Item A", 1));

        SetupSingleRecipe(400, recipeA);
        SetupSingleRecipe(401, recipeB);
        SetupBatchRecipes(recipeA, recipeB);
        SetupItem(400, "Item A");

        // Should not throw or infinite loop
        var result = await _resolver.ResolveTreeAsync(400);

        result.Should().NotBeNull();
        result.RootItem.ItemId.Should().Be(400);
        // One of the nodes in the cycle should be treated as a terminal
        var childB = result.RootItem.Children.First(c => c.ItemId == 401);
        // B's child A should be terminal (cycle broken)
        var childAInB = childB.Children.First(c => c.ItemId == 400);
        childAInB.IsRawMaterial.Should().BeTrue();
        childAInB.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task NonCraftableItem_ReturnsSingleLeafNode()
    {
        var item = TestDataBuilder.CreateItem(500, "Raw Ore");
        _itemRepo.GetByIdAsync(500, Arg.Any<CancellationToken>()).Returns(item);
        _recipeRepo.GetByResultItemIdAsync(500, Arg.Any<CancellationToken>()).Returns((Recipe?)null);

        var result = await _resolver.ResolveTreeAsync(500);

        result.RootItem.ItemId.Should().Be(500);
        result.RootItem.ItemName.Should().Be("Raw Ore");
        result.RootItem.IsRawMaterial.Should().BeTrue();
        result.RootItem.Children.Should().BeEmpty();
        result.ShoppingList.Should().HaveCount(1);
        result.ShoppingList[0].TotalQuantity.Should().Be(1);
        result.IntermediateCrafts.Should().BeEmpty();
        result.TreeDepth.Should().Be(0);
    }

    [Fact]
    public async Task IntermediateCrafts_InTopologicalOrder()
    {
        // Sword → Ingot + Handle → Lumber
        // Order should be: Lumber (deepest), then Ingot and Handle
        SetupFullTestGraph();

        var result = await _resolver.ResolveTreeAsync(TestDataBuilder.TestSwordId);

        result.IntermediateCrafts.Should().NotBeEmpty();

        // Lumber should come before Handle and Ingot (deeper in tree)
        var lumberIdx = result.IntermediateCrafts.FindIndex(x => x.ItemId == TestDataBuilder.TestLumberId);
        var ingotIdx = result.IntermediateCrafts.FindIndex(x => x.ItemId == TestDataBuilder.TestIngotId);
        var handleIdx = result.IntermediateCrafts.FindIndex(x => x.ItemId == TestDataBuilder.TestHandleId);

        lumberIdx.Should().BeGreaterThanOrEqualTo(0, "Lumber should be in intermediate crafts");
        ingotIdx.Should().BeGreaterThanOrEqualTo(0, "Ingot should be in intermediate crafts");
        handleIdx.Should().BeGreaterThanOrEqualTo(0, "Handle should be in intermediate crafts");

        // Lumber (depth 2) should be before Ingot/Handle (depth 1) — deepest first
        lumberIdx.Should().BeLessThan(ingotIdx);
        lumberIdx.Should().BeLessThan(handleIdx);
    }

    [Fact]
    public async Task SourceInfo_AttachedToNodes()
    {
        // Simple recipe with one gathered ingredient
        var recipe = TestDataBuilder.CreateRecipe(10, 600, "Crafted Item", 1,
            (601, "Gathered Ore", 3));

        SetupSingleRecipe(600, recipe);
        SetupNoRecipes(601);
        SetupItem(600, "Crafted Item");

        // Setup gathering info for the ore
        _gatheringRepo.GetByItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, List<GatheringInfo>>
            {
                [601] = [new GatheringInfo
                {
                    GatheringInfoId = 1,
                    ItemId = 601,
                    GatheringType = GatheringType.Mining,
                    ClassJob = "MIN",
                    Level = 50,
                    ZoneName = "Coerthas Western Highlands"
                }]
            });

        var result = await _resolver.ResolveTreeAsync(600);

        var oreNode = result.RootItem.Children.First(c => c.ItemId == 601);
        oreNode.Sources.Should().HaveCount(1);
        oreNode.Sources[0].SourceType.Should().Be(ItemSourceType.Gathered);
        oreNode.Sources[0].Description.Should().Contain("Mined");
        oreNode.Sources[0].Description.Should().Contain("Coerthas Western Highlands");
        oreNode.SourceType.Should().Be(ItemSourceType.Gathered);
    }

    // --- Helper methods ---

    private void SetupFullTestGraph()
    {
        var swordRecipe = TestDataBuilder.CreateTestSwordRecipe();
        var ingotRecipe = TestDataBuilder.CreateTestIngotRecipe();
        var handleRecipe = TestDataBuilder.CreateTestHandleRecipe();
        var lumberRecipe = TestDataBuilder.CreateTestLumberRecipe();

        SetupSingleRecipe(TestDataBuilder.TestSwordId, swordRecipe);
        SetupSingleRecipe(TestDataBuilder.TestIngotId, ingotRecipe);
        SetupSingleRecipe(TestDataBuilder.TestHandleId, handleRecipe);
        SetupSingleRecipe(TestDataBuilder.TestLumberId, lumberRecipe);

        SetupBatchRecipes(swordRecipe, ingotRecipe, handleRecipe, lumberRecipe);

        // Raw materials have no recipes
        SetupNoRecipes(TestDataBuilder.TestOreId, TestDataBuilder.TestLogId,
            TestDataBuilder.FireCrystalId, TestDataBuilder.WindCrystalId);

        SetupItem(TestDataBuilder.TestSwordId, "Test Sword");
    }

    private void SetupSingleRecipe(int itemId, Recipe recipe)
    {
        _recipeRepo.GetByResultItemIdAsync(itemId, Arg.Any<CancellationToken>()).Returns(recipe);
    }

    private void SetupBatchRecipes(params Recipe[] recipes)
    {
        _recipeRepo.GetByResultItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var requestedIds = callInfo.Arg<IEnumerable<int>>().ToHashSet();
                return recipes.Where(r => requestedIds.Contains(r.ResultItemId)).ToList().AsReadOnly();
            });
    }

    private void SetupNoRecipes(params int[] itemIds)
    {
        foreach (var id in itemIds)
        {
            _recipeRepo.GetByResultItemIdAsync(id, Arg.Any<CancellationToken>()).Returns((Recipe?)null);
        }
    }

    private void SetupItem(int itemId, string name)
    {
        _itemRepo.GetByIdAsync(itemId, Arg.Any<CancellationToken>())
            .Returns(TestDataBuilder.CreateItem(itemId, name));
    }
}
