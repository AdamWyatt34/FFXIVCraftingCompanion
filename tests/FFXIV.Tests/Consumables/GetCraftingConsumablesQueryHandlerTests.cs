using FFXIV.Application.Consumables.DTOs;
using FFXIV.Application.Consumables.Queries;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.Consumables;

public class GetCraftingConsumablesQueryHandlerTests
{
    private readonly IConsumableRepository _repository = Substitute.For<IConsumableRepository>();

    [Fact]
    public async Task Rows_are_grouped_per_item_with_meals_first_then_item_level_descending_then_item_id()
    {
        var steak = NewItem(44091, "Rroneek Steak", 710);
        var tea = NewItem(44092, "Rroneek Tea", 710);
        var oldMeal = NewItem(4666, "Boiled Egg", 15);
        var draught = NewItem(44170, "Craftsman's Draught", 720);
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<ConsumableBonus>
        {
            Bonus(draught, ConsumableKind.Medicine, ConsumableStat.CP, 6, 21, 8, 27),
            Bonus(steak, ConsumableKind.Meal, ConsumableStat.CP, 21, 73, 26, 92),
            Bonus(oldMeal, ConsumableKind.Meal, ConsumableStat.Control, 8, 4, 10, 5),
            Bonus(tea, ConsumableKind.Meal, ConsumableStat.Craftsmanship, 4, 80, 5, 100),
            Bonus(steak, ConsumableKind.Meal, ConsumableStat.Control, 4, 77, 5, 97),
            Bonus(tea, ConsumableKind.Meal, ConsumableStat.CP, 20, 70, 25, 88),
        });

        var result = await new GetCraftingConsumablesQueryHandler(_repository)
            .Handle(new GetCraftingConsumablesQuery(), CancellationToken.None);

        result.Select(c => c.ItemId).Should().Equal(44091, 44092, 4666, 44170);
        result[0].Should().BeEquivalentTo(new CraftingConsumableDto(
            44091, "Rroneek Steak", "https://icons/44091.png", 710, true, "Meal",
            [
                new ConsumableBonusDto("Control", true, 4, 77, 5, 97),
                new ConsumableBonusDto("CP", true, 21, 73, 26, 92),
            ]), options => options.WithStrictOrdering());
        result[1].Bonuses.Select(b => b.Stat).Should().Equal("Craftsmanship", "CP");
        result[3].Kind.Should().Be("Medicine");
    }

    [Fact]
    public async Task No_rows_yield_an_empty_list()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<ConsumableBonus>());

        var result = await new GetCraftingConsumablesQueryHandler(_repository)
            .Handle(new GetCraftingConsumablesQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    private static Item NewItem(int id, string name, int itemLevel) => new()
    {
        ItemId = id,
        Name = name,
        IconPath = $"https://icons/{id}.png",
        ItemLevel = itemLevel,
        Category = ItemCategory.Other,
        CanBeHQ = true,
    };

    private static ConsumableBonus Bonus(
        Item item, ConsumableKind kind, ConsumableStat stat, int value, int max, int valueHq, int maxHq) => new()
    {
        ItemId = item.ItemId,
        Item = item,
        Kind = kind,
        Stat = stat,
        IsRelative = true,
        Value = value,
        Max = max,
        ValueHQ = valueHq,
        MaxHQ = maxHq,
    };
}
