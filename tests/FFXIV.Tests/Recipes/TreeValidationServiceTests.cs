using FFXIV.Application.Recipes;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FFXIV.Tests.Recipes;

public class TreeValidationServiceTests
{
    private readonly IRecipeTreeResolver _treeResolver = Substitute.For<IRecipeTreeResolver>();
    private readonly IGarlandToolsClient _garlandClient = Substitute.For<IGarlandToolsClient>();
    private readonly IRecipeRepository _recipeRepo = Substitute.For<IRecipeRepository>();
    private readonly TreeValidationService _service;

    public TreeValidationServiceTests()
    {
        _service = new TreeValidationService(
            _treeResolver,
            _garlandClient,
            _recipeRepo,
            NullLogger<TreeValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateTree_MatchingTree_ReturnsValid()
    {
        // Arrange: Recipe 1 produces item 100 from 3x item 201 + 2x item 202
        var recipe = TestDataBuilder.CreateRecipe(1, 100, "Test Item", 1,
            (201, "Ingredient A", 3), (202, "Ingredient B", 2));
        _recipeRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(recipe);

        _treeResolver.ResolveTreeAsync(100, 1, Arg.Any<CancellationToken>())
            .Returns(new MaterialTreeResult
            {
                RootItem = new MaterialTreeNode { ItemId = 100, ItemName = "Test Item" },
                ShoppingList =
                [
                    new ShoppingListItem { ItemId = 201, ItemName = "Ingredient A", TotalQuantity = 3 },
                    new ShoppingListItem { ItemId = 202, ItemName = "Ingredient B", TotalQuantity = 2 }
                ],
                CrystalRequirements = []
            });

        SetupGarlandItem(100, "Test Item",
            new GarlandCraftInfo
            {
                Id = 1, Job = 1, Rlvl = 50, Yield = 1,
                Ingredients = [new() { Id = 201, Amount = 3 }, new() { Id = 202, Amount = 2 }]
            });
        SetupGarlandRawItem(201, "Ingredient A");
        SetupGarlandRawItem(202, "Ingredient B");

        // Act
        var result = await _service.ValidateTreeAsync(1);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Discrepancies.Should().BeEmpty();
        result.RecipeId.Should().Be(1);
        result.ItemName.Should().Be("Test Item");
        result.OurTotalRawMaterials.Should().Be(5);
        result.GarlandTotalRawMaterials.Should().Be(5);
    }

    [Fact]
    public async Task ValidateTree_QuantityMismatch_DetectsDiscrepancy()
    {
        var recipe = TestDataBuilder.CreateRecipe(1, 100, "Test Item", 1,
            (201, "Ore", 5));
        _recipeRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(recipe);

        _treeResolver.ResolveTreeAsync(100, 1, Arg.Any<CancellationToken>())
            .Returns(new MaterialTreeResult
            {
                RootItem = new MaterialTreeNode { ItemId = 100 },
                ShoppingList =
                [
                    new ShoppingListItem { ItemId = 201, ItemName = "Ore", TotalQuantity = 5 }
                ],
                CrystalRequirements = []
            });

        // Garland says 3 ore, we say 5
        SetupGarlandItem(100, "Test Item",
            new GarlandCraftInfo
            {
                Id = 1, Job = 1, Rlvl = 50, Yield = 1,
                Ingredients = [new() { Id = 201, Amount = 3 }]
            });
        SetupGarlandRawItem(201, "Ore");

        var result = await _service.ValidateTreeAsync(1);

        result.IsValid.Should().BeFalse();
        result.Discrepancies.Should().ContainSingle()
            .Which.Should().Match<TreeDiscrepancy>(d =>
                d.ItemId == 201 &&
                d.OurQuantity == 5 &&
                d.GarlandQuantity == 3 &&
                d.DiscrepancyType == "QuantityMismatch");
    }

    [Fact]
    public async Task ValidateTree_ExtraItemInOurs_DetectsDiscrepancy()
    {
        var recipe = TestDataBuilder.CreateRecipe(1, 100, "Test Item", 1,
            (201, "Ore", 3), (202, "Extra", 1));
        _recipeRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(recipe);

        _treeResolver.ResolveTreeAsync(100, 1, Arg.Any<CancellationToken>())
            .Returns(new MaterialTreeResult
            {
                RootItem = new MaterialTreeNode { ItemId = 100 },
                ShoppingList =
                [
                    new ShoppingListItem { ItemId = 201, ItemName = "Ore", TotalQuantity = 3 },
                    new ShoppingListItem { ItemId = 202, ItemName = "Extra", TotalQuantity = 1 }
                ],
                CrystalRequirements = []
            });

        // Garland only has Ore
        SetupGarlandItem(100, "Test Item",
            new GarlandCraftInfo
            {
                Id = 1, Job = 1, Rlvl = 50, Yield = 1,
                Ingredients = [new() { Id = 201, Amount = 3 }]
            });
        SetupGarlandRawItem(201, "Ore");

        var result = await _service.ValidateTreeAsync(1);

        result.IsValid.Should().BeFalse();
        result.Discrepancies.Should().Contain(d =>
            d.ItemId == 202 && d.DiscrepancyType == "Extra");
    }

    [Fact]
    public async Task ValidateTree_MissingItemInOurs_DetectsDiscrepancy()
    {
        var recipe = TestDataBuilder.CreateRecipe(1, 100, "Test Item", 1,
            (201, "Ore", 3));
        _recipeRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(recipe);

        _treeResolver.ResolveTreeAsync(100, 1, Arg.Any<CancellationToken>())
            .Returns(new MaterialTreeResult
            {
                RootItem = new MaterialTreeNode { ItemId = 100 },
                ShoppingList =
                [
                    new ShoppingListItem { ItemId = 201, ItemName = "Ore", TotalQuantity = 3 }
                ],
                CrystalRequirements = []
            });

        // Garland has an extra ingredient
        SetupGarlandItem(100, "Test Item",
            new GarlandCraftInfo
            {
                Id = 1, Job = 1, Rlvl = 50, Yield = 1,
                Ingredients = [new() { Id = 201, Amount = 3 }, new() { Id = 202, Amount = 1 }]
            },
            [new GarlandIngredient { Id = 201, Name = "Ore" }, new GarlandIngredient { Id = 202, Name = "Missing Item" }]);
        SetupGarlandRawItem(201, "Ore");
        SetupGarlandRawItem(202, "Missing Item");

        var result = await _service.ValidateTreeAsync(1);

        result.IsValid.Should().BeFalse();
        result.Discrepancies.Should().Contain(d =>
            d.ItemId == 202 && d.DiscrepancyType == "Missing");
    }

    [Fact]
    public async Task ValidateTree_RecipeNotFound_ReturnsError()
    {
        _recipeRepo.GetByIdAsync(999, Arg.Any<CancellationToken>()).Returns((Recipe?)null);

        var result = await _service.ValidateTreeAsync(999);

        result.ErrorMessage.Should().Contain("not found");
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateTree_GarlandApiFailure_ReturnsError()
    {
        var recipe = TestDataBuilder.CreateRecipe(1, 100, "Test Item", 1,
            (201, "Ore", 3));
        _recipeRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(recipe);

        _treeResolver.ResolveTreeAsync(100, 1, Arg.Any<CancellationToken>())
            .Returns(new MaterialTreeResult
            {
                RootItem = new MaterialTreeNode { ItemId = 100 },
                ShoppingList = [new ShoppingListItem { ItemId = 201, ItemName = "Ore", TotalQuantity = 3 }],
                CrystalRequirements = []
            });

        _garlandClient.GetItemAsync(100, Arg.Any<CancellationToken>())
            .Returns<GarlandItemData?>(_ => throw new HttpRequestException("Connection refused"));

        var result = await _service.ValidateTreeAsync(1);

        result.ErrorMessage.Should().Contain("Connection refused");
    }

    [Fact]
    public async Task ValidateTree_MultiYieldRecipe_CalculatesCorrectly()
    {
        // Recipe yields 3, need 5 → 2 crafts → 2 * ingredientAmount
        var recipe = TestDataBuilder.CreateRecipe(1, 100, "Test Item", 3,
            (201, "Ore", 4));
        _recipeRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(recipe);

        // Our resolver for quantity=1 should compute ceil(1/3) = 1 craft → 4 ore
        _treeResolver.ResolveTreeAsync(100, 1, Arg.Any<CancellationToken>())
            .Returns(new MaterialTreeResult
            {
                RootItem = new MaterialTreeNode { ItemId = 100 },
                ShoppingList = [new ShoppingListItem { ItemId = 201, ItemName = "Ore", TotalQuantity = 4 }],
                CrystalRequirements = []
            });

        SetupGarlandItem(100, "Test Item",
            new GarlandCraftInfo
            {
                Id = 1, Job = 1, Rlvl = 50, Yield = 3,
                Ingredients = [new() { Id = 201, Amount = 4 }]
            });
        SetupGarlandRawItem(201, "Ore");

        var result = await _service.ValidateTreeAsync(1);

        // Both should compute ceil(1/3)=1 craft → 4 ore
        result.IsValid.Should().BeTrue();
        result.OurTotalRawMaterials.Should().Be(4);
        result.GarlandTotalRawMaterials.Should().Be(4);
    }

    [Fact]
    public async Task ValidateBatch_AggregatesCorrectly()
    {
        // Recipe 1: valid
        var recipe1 = TestDataBuilder.CreateRecipe(1, 100, "Item A", 1, (201, "Ore", 3));
        _recipeRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(recipe1);
        _treeResolver.ResolveTreeAsync(100, 1, Arg.Any<CancellationToken>())
            .Returns(new MaterialTreeResult
            {
                RootItem = new MaterialTreeNode { ItemId = 100 },
                ShoppingList = [new ShoppingListItem { ItemId = 201, ItemName = "Ore", TotalQuantity = 3 }],
                CrystalRequirements = []
            });
        SetupGarlandItem(100, "Item A",
            new GarlandCraftInfo { Id = 1, Job = 1, Rlvl = 50, Yield = 1, Ingredients = [new() { Id = 201, Amount = 3 }] });
        SetupGarlandRawItem(201, "Ore");

        // Recipe 2: mismatch
        var recipe2 = TestDataBuilder.CreateRecipe(2, 110, "Item B", 1, (202, "Log", 5));
        _recipeRepo.GetByIdAsync(2, Arg.Any<CancellationToken>()).Returns(recipe2);
        _treeResolver.ResolveTreeAsync(110, 1, Arg.Any<CancellationToken>())
            .Returns(new MaterialTreeResult
            {
                RootItem = new MaterialTreeNode { ItemId = 110 },
                ShoppingList = [new ShoppingListItem { ItemId = 202, ItemName = "Log", TotalQuantity = 5 }],
                CrystalRequirements = []
            });
        SetupGarlandItem(110, "Item B",
            new GarlandCraftInfo { Id = 2, Job = 2, Rlvl = 50, Yield = 1, Ingredients = [new() { Id = 202, Amount = 3 }] });
        SetupGarlandRawItem(202, "Log");

        // Recipe 3: error
        _recipeRepo.GetByIdAsync(3, Arg.Any<CancellationToken>()).Returns((Recipe?)null);

        var result = await _service.ValidateBatchAsync([1, 2, 3]);

        result.TotalValidated.Should().Be(3);
        result.PassedCount.Should().Be(1);
        result.FailedCount.Should().Be(1);
        result.ErrorCount.Should().Be(1);
        result.Results.Should().HaveCount(3);
    }

    [Fact]
    public async Task ValidateTree_CycleInGarland_DoesNotHang()
    {
        var recipe = TestDataBuilder.CreateRecipe(1, 100, "Cycle Item", 1, (201, "Sub Item", 2));
        _recipeRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(recipe);

        _treeResolver.ResolveTreeAsync(100, 1, Arg.Any<CancellationToken>())
            .Returns(new MaterialTreeResult
            {
                RootItem = new MaterialTreeNode { ItemId = 100 },
                ShoppingList = [new ShoppingListItem { ItemId = 201, ItemName = "Sub Item", TotalQuantity = 2 }],
                CrystalRequirements = []
            });

        // Garland: Item 100 crafts from 201, but 201 crafts from 100 (cycle)
        SetupGarlandItem(100, "Cycle Item",
            new GarlandCraftInfo { Id = 1, Job = 1, Rlvl = 50, Yield = 1, Ingredients = [new() { Id = 201, Amount = 2 }] });
        SetupGarlandItem(201, "Sub Item",
            new GarlandCraftInfo { Id = 2, Job = 1, Rlvl = 50, Yield = 1, Ingredients = [new() { Id = 100, Amount = 1 }] });

        // Should not throw or hang — cycle is detected
        var result = await _service.ValidateTreeAsync(1);

        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ValidateTree_CrystalsIncluded_ComparesCorrectly()
    {
        var recipe = TestDataBuilder.CreateRecipe(1, 100, "Test Item", 1,
            (201, "Ore", 3), (2, "Fire Shard", 1));
        _recipeRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(recipe);

        _treeResolver.ResolveTreeAsync(100, 1, Arg.Any<CancellationToken>())
            .Returns(new MaterialTreeResult
            {
                RootItem = new MaterialTreeNode { ItemId = 100 },
                ShoppingList = [new ShoppingListItem { ItemId = 201, ItemName = "Ore", TotalQuantity = 3 }],
                CrystalRequirements = [new ShoppingListItem { ItemId = 2, ItemName = "Fire Shard", TotalQuantity = 1 }]
            });

        SetupGarlandItem(100, "Test Item",
            new GarlandCraftInfo
            {
                Id = 1, Job = 1, Rlvl = 50, Yield = 1,
                Ingredients = [new() { Id = 201, Amount = 3 }, new() { Id = 2, Amount = 1 }]
            });
        SetupGarlandRawItem(201, "Ore");
        SetupGarlandRawItem(2, "Fire Shard");

        var result = await _service.ValidateTreeAsync(1);

        result.IsValid.Should().BeTrue();
        result.OurTotalRawMaterials.Should().Be(4);
    }

    // --- Helpers ---

    private void SetupGarlandItem(int itemId, string name, GarlandCraftInfo craft,
        List<GarlandIngredient>? ingredients = null)
    {
        var craftIngredientIds = craft.Ingredients.Select(i => i.Id).ToList();
        var ingredientList = ingredients ?? craftIngredientIds
            .Select(id => new GarlandIngredient { Id = id, Name = $"Item {id}" })
            .ToList();

        _garlandClient.GetItemAsync(itemId, Arg.Any<CancellationToken>())
            .Returns(new GarlandItemData
            {
                Item = new GarlandItem
                {
                    Id = itemId,
                    Name = name,
                    Craft = [craft]
                },
                Ingredients = ingredientList
            });
    }

    private void SetupGarlandRawItem(int itemId, string name)
    {
        _garlandClient.GetItemAsync(itemId, Arg.Any<CancellationToken>())
            .Returns(new GarlandItemData
            {
                Item = new GarlandItem
                {
                    Id = itemId,
                    Name = name,
                    Craft = null
                }
            });
    }
}
