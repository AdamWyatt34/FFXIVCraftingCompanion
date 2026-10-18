using System.Text.Json;
using FFXIV.Application.Common;
using FFXIV.Application.SavedLists.Commands;
using FFXIV.Application.SavedLists.DTOs;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.SavedLists;

public class SavedListItemManagementTests
{
    private readonly ISavedListRepository _savedListRepo = Substitute.For<ISavedListRepository>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static SavedList CreateTestList(string userId = "user-1", object? data = null)
    {
        var defaultData = new
        {
            sourceRecipeId = 100,
            sourceItemName = "Test Item",
            quantity = 1,
            materials = new[]
            {
                new { itemId = 10, itemName = "Iron Ore", quantity = 3, category = "Gathered" },
                new { itemId = 20, itemName = "Wind Crystal", quantity = 5, category = "Crystal" },
            },
            crystals = Array.Empty<object>(),
            savedAt = DateTimeOffset.UtcNow,
        };

        return new SavedList
        {
            Id = 1,
            UserId = userId,
            Name = "Test Shopping List",
            Type = SavedListType.ShoppingList,
            Data = JsonSerializer.Serialize(data ?? defaultData, JsonOptions),
            IsPinned = false,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
    }

    #region AddItemToSavedList Tests

    [Fact]
    public async Task AddItem_RepeatedAdds_AreCappedAtTheMaterialQuantityCeiling()
    {
        var list = CreateTestList(data: new
        {
            materials = new[]
            {
                new { itemId = 10, itemName = "Iron Ore", quantity = SavedListLimits.MaxMaterialQuantity - 1, category = "Gathered" },
            },
        });
        _savedListRepo.GetByIdAsync(1, "user-1", Arg.Any<CancellationToken>()).Returns(list);
        _savedListRepo.UpdateAsync(Arg.Any<SavedList>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<SavedList>(0));

        var handler = new AddItemToSavedListCommandHandler(_savedListRepo);
        var result = await handler.Handle(
            new AddItemToSavedListCommand("user-1", 1, 10, "Iron Ore", SavedListLimits.MaxQuantity, null),
            CancellationToken.None);

        var materials = JsonSerializer.Deserialize<JsonElement>(list.Data).GetProperty("materials");
        materials.GetArrayLength().Should().Be(1);
        materials[0].GetProperty("quantity").GetInt32().Should().Be(SavedListLimits.MaxMaterialQuantity);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task AddItem_FullList_RejectsANewMaterialWithAClientFacingError()
    {
        var full = Enumerable.Range(1, SavedListLimits.MaxMaterials)
            .Select(i => new { itemId = i, itemName = $"Material {i}", quantity = 1, category = "Other" })
            .ToArray();
        var list = CreateTestList(data: new { materials = full });
        _savedListRepo.GetByIdAsync(1, "user-1", Arg.Any<CancellationToken>()).Returns(list);

        var handler = new AddItemToSavedListCommandHandler(_savedListRepo);
        var act = () => handler.Handle(
            new AddItemToSavedListCommand("user-1", 1, 9999, "One Too Many", 1, null), CancellationToken.None);

        await act.Should().ThrowAsync<RequestValidationException>();
        await _savedListRepo.DidNotReceiveWithAnyArgs().UpdateAsync(default!, default);
    }

    [Fact]
    public async Task AddItem_FullList_StillAcceptsMoreOfAnExistingMaterial()
    {
        var full = Enumerable.Range(1, SavedListLimits.MaxMaterials)
            .Select(i => new { itemId = i, itemName = $"Material {i}", quantity = 1, category = "Other" })
            .ToArray();
        var list = CreateTestList(data: new { materials = full });
        _savedListRepo.GetByIdAsync(1, "user-1", Arg.Any<CancellationToken>()).Returns(list);
        _savedListRepo.UpdateAsync(Arg.Any<SavedList>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<SavedList>(0));

        var handler = new AddItemToSavedListCommandHandler(_savedListRepo);
        var result = await handler.Handle(
            new AddItemToSavedListCommand("user-1", 1, 1, "Material 1", 2, null), CancellationToken.None);

        result.Should().NotBeNull();
        var materials = JsonSerializer.Deserialize<JsonElement>(list.Data).GetProperty("materials");
        materials.GetArrayLength().Should().Be(SavedListLimits.MaxMaterials);
        materials[0].GetProperty("quantity").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task AddItem_NewItem_AppearsInMaterials()
    {
        var list = CreateTestList();
        _savedListRepo.GetByIdAsync(1, "user-1", Arg.Any<CancellationToken>()).Returns(list);
        _savedListRepo.UpdateAsync(Arg.Any<SavedList>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<SavedList>(0));

        var handler = new AddItemToSavedListCommandHandler(_savedListRepo);
        var command = new AddItemToSavedListCommand("user-1", 1, 30, "Mythril Ingot", 2, "Crafted");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();

        // Parse the updated data to verify
        var updatedData = JsonSerializer.Deserialize<JsonElement>(list.Data, JsonOptions);
        var materials = updatedData.GetProperty("materials");
        materials.GetArrayLength().Should().Be(3);

        var newItem = materials.EnumerateArray().Last();
        newItem.GetProperty("itemId").GetInt32().Should().Be(30);
        newItem.GetProperty("itemName").GetString().Should().Be("Mythril Ingot");
        newItem.GetProperty("quantity").GetInt32().Should().Be(2);
        newItem.GetProperty("category").GetString().Should().Be("Crafted");
    }

    [Fact]
    public async Task AddItem_DuplicateItem_IncrementsQuantity()
    {
        var list = CreateTestList();
        _savedListRepo.GetByIdAsync(1, "user-1", Arg.Any<CancellationToken>()).Returns(list);
        _savedListRepo.UpdateAsync(Arg.Any<SavedList>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<SavedList>(0));

        var handler = new AddItemToSavedListCommandHandler(_savedListRepo);
        var command = new AddItemToSavedListCommand("user-1", 1, 10, "Iron Ore", 5, "Gathered");

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();

        var updatedData = JsonSerializer.Deserialize<JsonElement>(list.Data, JsonOptions);
        var materials = updatedData.GetProperty("materials");
        materials.GetArrayLength().Should().Be(2); // No new entry added

        var ironOre = materials.EnumerateArray().First();
        ironOre.GetProperty("itemId").GetInt32().Should().Be(10);
        ironOre.GetProperty("quantity").GetInt32().Should().Be(8); // 3 + 5
    }

    [Fact]
    public async Task AddItem_NonExistentList_ReturnsNull()
    {
        _savedListRepo.GetByIdAsync(999, "user-1", Arg.Any<CancellationToken>())
            .Returns((SavedList?)null);

        var handler = new AddItemToSavedListCommandHandler(_savedListRepo);
        var command = new AddItemToSavedListCommand("user-1", 999, 10, "Iron Ore", 1, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddItem_OtherUserslist_ReturnsNull()
    {
        var list = CreateTestList(userId: "user-2");
        _savedListRepo.GetByIdAsync(1, "user-1", Arg.Any<CancellationToken>())
            .Returns((SavedList?)null); // Repo filters by userId

        var handler = new AddItemToSavedListCommandHandler(_savedListRepo);
        var command = new AddItemToSavedListCommand("user-1", 1, 10, "Iron Ore", 1, null);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().BeNull();
    }

    #endregion

    #region RemoveItemFromSavedList Tests

    [Fact]
    public async Task RemoveItem_ExistingItem_RemovedFromMaterials()
    {
        var list = CreateTestList();
        _savedListRepo.GetByIdAsync(1, "user-1", Arg.Any<CancellationToken>()).Returns(list);
        _savedListRepo.UpdateAsync(Arg.Any<SavedList>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.ArgAt<SavedList>(0));

        var handler = new RemoveItemFromSavedListCommandHandler(_savedListRepo);
        var command = new RemoveItemFromSavedListCommand("user-1", 1, 10);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().NotBeNull();

        var updatedData = JsonSerializer.Deserialize<JsonElement>(list.Data, JsonOptions);
        var materials = updatedData.GetProperty("materials");
        materials.GetArrayLength().Should().Be(1);

        var remaining = materials.EnumerateArray().First();
        remaining.GetProperty("itemId").GetInt32().Should().Be(20); // Wind Crystal remains
    }

    [Fact]
    public async Task RemoveItem_NonExistentList_ReturnsNull()
    {
        _savedListRepo.GetByIdAsync(999, "user-1", Arg.Any<CancellationToken>())
            .Returns((SavedList?)null);

        var handler = new RemoveItemFromSavedListCommandHandler(_savedListRepo);
        var command = new RemoveItemFromSavedListCommand("user-1", 999, 10);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Should().BeNull();
    }

    #endregion
}
