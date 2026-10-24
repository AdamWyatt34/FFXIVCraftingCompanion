using FFXIV.Application.CraftPlans;
using FFXIV.Application.Recipes;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.CraftPlans;

public class CraftPlanResolverTests
{
    private readonly IRecipeRepository _recipeRepo = Substitute.For<IRecipeRepository>();
    private readonly IRecipeTreeResolver _treeResolver = Substitute.For<IRecipeTreeResolver>();
    private readonly CraftPlanResolver _resolver;

    public CraftPlanResolverTests()
    {
        _resolver = new CraftPlanResolver(_recipeRepo, _treeResolver);
    }

    [Fact]
    public async Task SingleItem_ResolvesCorrectly()
    {
        // Arrange: Potion = 3x Herb + 2x Water
        var recipe = CreateRecipe(10, 200, "Potion", CraftType.ALC);
        SetupRecipe(recipe);

        var tree = CreateSimpleTree(200, "Potion",
            raw: [(201, "Herb", 3, "Gathering"), (202, "Water", 2, "Gathering")],
            crystals: [(2, "Fire Crystal", 5)]);

        _treeResolver.ResolveTreeAsync(200, 1, Arg.Any<CancellationToken>()).Returns(tree);

        // Act
        var result = await _resolver.ResolveAsync([new CraftPlanInput(10, 1)]);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(1);
        result.Items[0].RecipeId.Should().Be(10);
        result.Items[0].ItemName.Should().Be("Potion");
        result.RawMaterials.Should().HaveCount(2);
        result.Crystals.Should().HaveCount(1);
        result.IntermediateCrafts.Should().BeEmpty();
        result.Summary.TotalPlanItems.Should().Be(1);
    }

    [Fact]
    public async Task MultipleItems_SharedRawMaterial_QuantitiesAggregated()
    {
        // Arrange: Potion A = 3x Herb, Potion B = 5x Herb
        var recipeA = CreateRecipe(10, 200, "Potion A", CraftType.ALC);
        var recipeB = CreateRecipe(20, 300, "Potion B", CraftType.ALC);
        SetupRecipe(recipeA);
        SetupRecipe(recipeB);

        var treeA = CreateSimpleTree(200, "Potion A",
            raw: [(201, "Herb", 3, "Gathering")],
            crystals: [(2, "Fire Crystal", 2)]);
        var treeB = CreateSimpleTree(300, "Potion B",
            raw: [(201, "Herb", 5, "Gathering")],
            crystals: [(2, "Fire Crystal", 3)]);

        _treeResolver.ResolveTreeAsync(200, 1, Arg.Any<CancellationToken>()).Returns(treeA);
        _treeResolver.ResolveTreeAsync(300, 2, Arg.Any<CancellationToken>()).Returns(treeB);

        // Act
        var result = await _resolver.ResolveAsync([
            new CraftPlanInput(10, 1),
            new CraftPlanInput(20, 2)
        ]);

        // Assert
        result.Should().NotBeNull();
        result!.Items.Should().HaveCount(2);
        result.RawMaterials.Should().HaveCount(1);
        result.RawMaterials[0].ItemName.Should().Be("Herb");
        result.RawMaterials[0].TotalQuantity.Should().Be(8); // 3 + 5
        result.RawMaterials[0].Contributions.Should().HaveCount(2);
        result.Crystals.Should().HaveCount(1);
        result.Crystals[0].TotalQuantity.Should().Be(5); // 2 + 3
    }

    [Fact]
    public async Task MultipleItems_SharedIntermediateCraft_QuantitiesSummed()
    {
        // Arrange: Sword and Shield both need Ingot
        var swordRecipe = CreateRecipe(10, 200, "Sword", CraftType.BSM);
        var shieldRecipe = CreateRecipe(20, 300, "Shield", CraftType.ARM);
        SetupRecipe(swordRecipe);
        SetupRecipe(shieldRecipe);

        var swordTree = CreateTreeWithIntermediate(200, "Sword",
            intermediateId: 400, intermediateName: "Ingot", intermediateQty: 3,
            rawId: 500, rawName: "Ore", rawQty: 15);
        var shieldTree = CreateTreeWithIntermediate(300, "Shield",
            intermediateId: 400, intermediateName: "Ingot", intermediateQty: 2,
            rawId: 500, rawName: "Ore", rawQty: 10);

        _treeResolver.ResolveTreeAsync(200, 1, Arg.Any<CancellationToken>()).Returns(swordTree);
        _treeResolver.ResolveTreeAsync(300, 1, Arg.Any<CancellationToken>()).Returns(shieldTree);

        // Act
        var result = await _resolver.ResolveAsync([
            new CraftPlanInput(10, 1),
            new CraftPlanInput(20, 1)
        ]);

        // Assert
        result.Should().NotBeNull();
        result!.IntermediateCrafts.Should().HaveCount(1);
        result.IntermediateCrafts[0].ItemName.Should().Be("Ingot");
        result.IntermediateCrafts[0].TotalQuantity.Should().Be(5); // 3 + 2
        result.RawMaterials.Should().HaveCount(1);
        result.RawMaterials[0].TotalQuantity.Should().Be(25); // 15 + 10
    }

    [Fact]
    public async Task MultipleItems_SharedMultiYieldIntermediate_CraftedOnceForBoth()
    {
        // Sword and Shield each need 1 Rivet (yield 3); a Rivet needs 1 Ore. Two Rivets in all:
        // one craft, so 1 Ore, not one per plan item.
        SetupRecipe(CreateRecipe(10, 200, "Sword", CraftType.BSM));
        SetupRecipe(CreateRecipe(20, 300, "Shield", CraftType.ARM));
        _treeResolver.ResolveTreeAsync(200, 1, Arg.Any<CancellationToken>()).Returns(
            CreateTreeWithIntermediate(200, "Sword", 400, "Rivet", 1, 500, "Ore", 1, intermediateYield: 3));
        _treeResolver.ResolveTreeAsync(300, 1, Arg.Any<CancellationToken>()).Returns(
            CreateTreeWithIntermediate(300, "Shield", 400, "Rivet", 1, 500, "Ore", 1, intermediateYield: 3));

        var result = await _resolver.ResolveAsync([new CraftPlanInput(10, 1), new CraftPlanInput(20, 1)]);

        result.Should().NotBeNull();
        result!.IntermediateCrafts.Single().TotalQuantity.Should().Be(2);
        var ore = result.RawMaterials.Single();
        ore.TotalQuantity.Should().Be(1);
        // The Shield's Rivet comes out of the Sword's craft, so it adds no Ore.
        ore.Contributions.Select(c => (c.RecipeId, c.Quantity)).Should().Equal((10, 1), (20, 0));
    }

    [Fact]
    public async Task MultipleItems_SharedMultiYieldIntermediate_QuantitiesApplyBeforeRounding()
    {
        // Sword x4 needs 4 Rivets (alone: 2 crafts, 2 Ore); Shield x1 needs 1 (alone: 1 craft, 1 Ore).
        // Together 5 Rivets: ceil(5/3) = 2 crafts, so 2 Ore, not 3.
        SetupRecipe(CreateRecipe(10, 200, "Sword", CraftType.BSM));
        SetupRecipe(CreateRecipe(20, 300, "Shield", CraftType.ARM));
        _treeResolver.ResolveTreeAsync(200, 4, Arg.Any<CancellationToken>()).Returns(
            CreateTreeWithIntermediate(200, "Sword", 400, "Rivet", 4, 500, "Ore", 2, intermediateYield: 3, rootQty: 4));
        _treeResolver.ResolveTreeAsync(300, 1, Arg.Any<CancellationToken>()).Returns(
            CreateTreeWithIntermediate(300, "Shield", 400, "Rivet", 1, 500, "Ore", 1, intermediateYield: 3));

        var result = await _resolver.ResolveAsync([new CraftPlanInput(10, 4), new CraftPlanInput(20, 1)]);

        result.Should().NotBeNull();
        result!.IntermediateCrafts.Single().TotalQuantity.Should().Be(5);
        var ore = result.RawMaterials.Single();
        ore.TotalQuantity.Should().Be(2);
        ore.Contributions.Select(c => (c.RecipeId, c.Quantity)).Should().Equal((10, 2), (20, 0));
    }

    [Fact]
    public async Task IntermediateCrafts_SortedByDepth_DeepestFirst()
    {
        // Arrange: Sword → Ingot (depth 1) → Ore Cluster (depth 2)
        var recipe = CreateRecipe(10, 200, "Sword", CraftType.BSM);
        SetupRecipe(recipe);

        var tree = CreateDeepTree();
        _treeResolver.ResolveTreeAsync(200, 1, Arg.Any<CancellationToken>()).Returns(tree);

        // Act
        var result = await _resolver.ResolveAsync([new CraftPlanInput(10, 1)]);

        // Assert
        result.Should().NotBeNull();
        result!.IntermediateCrafts.Should().HaveCount(2);
        // Deeper item should come first (higher depth = craft first)
        result.IntermediateCrafts[0].ItemName.Should().Be("Ore Cluster");
        result.IntermediateCrafts[0].CraftOrder.Should().Be(1);
        result.IntermediateCrafts[1].ItemName.Should().Be("Ingot");
        result.IntermediateCrafts[1].CraftOrder.Should().Be(2);
    }

    [Fact]
    public async Task ContributionsTrack_PerPlanItemBreakdown()
    {
        // Arrange: two items both using same raw material
        var recipeA = CreateRecipe(10, 200, "Item A", CraftType.CRP);
        var recipeB = CreateRecipe(20, 300, "Item B", CraftType.CRP);
        SetupRecipe(recipeA);
        SetupRecipe(recipeB);

        var treeA = CreateSimpleTree(200, "Item A",
            raw: [(500, "Wood", 4, "Gathering")], crystals: []);
        var treeB = CreateSimpleTree(300, "Item B",
            raw: [(500, "Wood", 6, "Gathering")], crystals: []);

        _treeResolver.ResolveTreeAsync(200, 1, Arg.Any<CancellationToken>()).Returns(treeA);
        _treeResolver.ResolveTreeAsync(300, 1, Arg.Any<CancellationToken>()).Returns(treeB);

        // Act
        var result = await _resolver.ResolveAsync([
            new CraftPlanInput(10, 1),
            new CraftPlanInput(20, 1)
        ]);

        // Assert
        result.Should().NotBeNull();
        var wood = result!.RawMaterials.Single(m => m.ItemName == "Wood");
        wood.TotalQuantity.Should().Be(10);
        wood.Contributions.Should().HaveCount(2);
        wood.Contributions[0].RecipeId.Should().Be(10);
        wood.Contributions[0].Quantity.Should().Be(4);
        wood.Contributions[1].RecipeId.Should().Be(20);
        wood.Contributions[1].Quantity.Should().Be(6);
    }

    [Fact]
    public async Task InvalidRecipe_ReturnsNull()
    {
        _recipeRepo.GetByIdAsync(999, Arg.Any<CancellationToken>())
            .Returns((Recipe?)null);

        var result = await _resolver.ResolveAsync([new CraftPlanInput(999, 1)]);

        result.Should().BeNull();
    }

    [Fact]
    public async Task MultiYieldRecipe_CalculatesCorrectly()
    {
        // Arrange: recipe yields 3 per craft
        var recipe = CreateRecipe(10, 200, "Arrows", CraftType.CRP, yield: 3);
        SetupRecipe(recipe);

        var tree = CreateSimpleTree(200, "Arrows",
            raw: [(201, "Shaft", 6, "Gathering")], crystals: []);

        _treeResolver.ResolveTreeAsync(200, 9, Arg.Any<CancellationToken>()).Returns(tree);

        // Act
        var result = await _resolver.ResolveAsync([new CraftPlanInput(10, 9)]);

        // Assert
        result.Should().NotBeNull();
        result!.Items[0].Quantity.Should().Be(9);
        result.RawMaterials.Should().HaveCount(1);
    }

    // --- Helper methods ---

    private static Recipe CreateRecipe(int recipeId, int resultItemId, string name, CraftType craftType, int yield = 1)
    {
        return new Recipe
        {
            RecipeId = recipeId,
            ResultItemId = resultItemId,
            CraftType = craftType,
            RecipeLevel = 50,
            Difficulty = 1000,
            Quality = 2000,
            Durability = 80,
            RequiredCraftsmanship = 400,
            RequiredControl = 350,
            Yield = yield,
            ResultItem = new Item
            {
                ItemId = resultItemId,
                Name = name,
                IconPath = $"icon/{resultItemId}.png",
                ItemLevel = 50,
                Category = ItemCategory.Material,
                CanBeHQ = false
            }
        };
    }

    private void SetupRecipe(Recipe recipe)
    {
        _recipeRepo.GetByIdAsync(recipe.RecipeId, Arg.Any<CancellationToken>())
            .Returns(recipe);
    }

    private static MaterialTreeResult CreateSimpleTree(
        int rootItemId, string rootName,
        List<(int Id, string Name, int Qty, string Source)> raw,
        List<(int Id, string Name, int Qty)> crystals)
    {
        var rootNode = new MaterialTreeNode
        {
            ItemId = rootItemId,
            ItemName = rootName,
            IconUrl = $"icon/{rootItemId}.png",
            Quantity = 1,
            IsRawMaterial = false,
            Depth = 0,
            Children = raw.Select(r => (r.Id, r.Name, r.Qty)).Concat(crystals).Select(r => new MaterialTreeNode
            {
                ItemId = r.Id,
                ItemName = r.Name,
                Quantity = r.Qty,
                IsRawMaterial = true,
                Depth = 1,
                SourceType = ItemSourceType.Gathered
            }).ToList()
        };

        return new MaterialTreeResult
        {
            RootItem = rootNode,
            ShoppingList = raw.Select(r => new ShoppingListItem
            {
                ItemId = r.Id,
                ItemName = r.Name,
                TotalQuantity = r.Qty,
                SourceType = ItemSourceType.Gathered,
                SortCategory = "Material"
            }).ToList(),
            CrystalRequirements = crystals.Select(c => new ShoppingListItem
            {
                ItemId = c.Id,
                ItemName = c.Name,
                TotalQuantity = c.Qty,
                SourceType = ItemSourceType.Gathered,
                SortCategory = "Crystal"
            }).ToList(),
            IntermediateCrafts = [],
            TreeDepth = 1,
            TotalUniqueItems = raw.Count + crystals.Count
        };
    }

    // Shaped like RecipeTreeResolver's output for that root alone: the raw quantity is the
    // ingredient amount times ceil(intermediateQty / intermediateYield).
    private static MaterialTreeResult CreateTreeWithIntermediate(
        int rootId, string rootName,
        int intermediateId, string intermediateName, int intermediateQty,
        int rawId, string rawName, int rawQty,
        int intermediateYield = 1, int rootQty = 1)
    {
        var intermediateNode = new MaterialTreeNode
        {
            ItemId = intermediateId,
            ItemName = intermediateName,
            Quantity = intermediateQty,
            IsRawMaterial = false,
            Depth = 1,
            Recipe = new RecipeSummary
            {
                RecipeId = intermediateId + 1000,
                CraftType = CraftType.BSM,
                RecipeLevel = 50,
                YieldPerCraft = intermediateYield
            },
            Children =
            [
                new MaterialTreeNode
                {
                    ItemId = rawId,
                    ItemName = rawName,
                    Quantity = rawQty,
                    IsRawMaterial = true,
                    Depth = 2,
                    SourceType = ItemSourceType.Gathered
                }
            ]
        };

        var rootNode = new MaterialTreeNode
        {
            ItemId = rootId,
            ItemName = rootName,
            Quantity = rootQty,
            IsRawMaterial = false,
            Depth = 0,
            Children = [intermediateNode]
        };

        return new MaterialTreeResult
        {
            RootItem = rootNode,
            ShoppingList =
            [
                new ShoppingListItem
                {
                    ItemId = rawId,
                    ItemName = rawName,
                    TotalQuantity = rawQty,
                    SourceType = ItemSourceType.Gathered,
                    SortCategory = "Material"
                }
            ],
            CrystalRequirements = [],
            IntermediateCrafts =
            [
                new ShoppingListItem
                {
                    ItemId = intermediateId,
                    ItemName = intermediateName,
                    TotalQuantity = intermediateQty,
                    SourceType = ItemSourceType.Crafted,
                    SortCategory = "Crafted"
                }
            ],
            TreeDepth = 2,
            TotalUniqueItems = 2
        };
    }

    private static MaterialTreeResult CreateDeepTree()
    {
        var rawOreNode = new MaterialTreeNode
        {
            ItemId = 600, ItemName = "Raw Ore", Quantity = 12,
            IsRawMaterial = true, Depth = 3, SourceType = ItemSourceType.Gathered
        };

        var oreClusterNode = new MaterialTreeNode
        {
            ItemId = 500, ItemName = "Ore Cluster", Quantity = 6,
            IsRawMaterial = false, Depth = 2,
            Recipe = new RecipeSummary
            {
                RecipeId = 1500, CraftType = CraftType.BSM, RecipeLevel = 30, YieldPerCraft = 1
            },
            Children = [rawOreNode]
        };

        var ingotNode = new MaterialTreeNode
        {
            ItemId = 400, ItemName = "Ingot", Quantity = 3,
            IsRawMaterial = false, Depth = 1,
            Recipe = new RecipeSummary
            {
                RecipeId = 1400, CraftType = CraftType.BSM, RecipeLevel = 50, YieldPerCraft = 1
            },
            Children = [oreClusterNode]
        };

        var rootNode = new MaterialTreeNode
        {
            ItemId = 200, ItemName = "Sword", Quantity = 1,
            IsRawMaterial = false, Depth = 0,
            Children = [ingotNode]
        };

        return new MaterialTreeResult
        {
            RootItem = rootNode,
            ShoppingList =
            [
                new ShoppingListItem
                {
                    ItemId = 600, ItemName = "Raw Ore", TotalQuantity = 12,
                    SourceType = ItemSourceType.Gathered, SortCategory = "Material"
                }
            ],
            CrystalRequirements = [],
            IntermediateCrafts =
            [
                new ShoppingListItem
                {
                    ItemId = 500, ItemName = "Ore Cluster", TotalQuantity = 6,
                    SourceType = ItemSourceType.Crafted, SortCategory = "Crafted"
                },
                new ShoppingListItem
                {
                    ItemId = 400, ItemName = "Ingot", TotalQuantity = 3,
                    SourceType = ItemSourceType.Crafted, SortCategory = "Crafted"
                }
            ],
            TreeDepth = 3,
            TotalUniqueItems = 3
        };
    }
}
