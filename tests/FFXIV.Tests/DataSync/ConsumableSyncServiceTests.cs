using System.Text.Json;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Infrastructure.DataSync;
using FFXIV.Infrastructure.ExternalApis.Models;
using FFXIV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FFXIV.Tests.DataSync;

public sealed class ConsumableSyncServiceTests : IDisposable
{
    private const int Meals = 46;
    private const int Medicine = 44;
    private const int WellFed = 48;
    private const int Medicated = 49;
    private const int Craftsmanship = 70;
    private const int Control = 71;
    private const int Cp = 11;
    private const int Hp = 7;
    private const int Vitality = 3;

    private static readonly string RecordingDir =
        Path.Combine(AppContext.BaseDirectory, "DataSync", "TestData", "xivapi-consumables");

    // XivApiClient reads responses with GetFromJsonAsync, which uses the web defaults.
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly IXivApiClient _xivApi = Substitute.For<IXivApiClient>();
    private readonly CapturingLogger _logger = new();

    public ConsumableSyncServiceTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task Recorded_payloads_yield_the_crafting_meals_and_skip_combat_food()
    {
        var meals = RecordedSearch("item-meals.json");
        var medicine = RecordedSearch("item-medicine.json");
        var itemFood = RecordedSheet("itemfood.json");
        GivenSearch(Meals, meals.Rows, meals.Next);
        GivenSearch(Medicine, medicine.Rows, medicine.Next);
        GivenItemFood(itemFood);
        SeedItems(meals.Rows.Concat(medicine.Rows).Select(r => r.RowId).ToArray());

        var count = await SyncAsync();

        var rows = StoredRows();
        count.Should().Be(19);
        rows.Should().HaveCount(19);
        rows.Should().OnlyContain(r => r.Kind == ConsumableKind.Meal && r.IsRelative);
        rows.Should().ContainEquivalentOf(Row(4666, ConsumableKind.Meal, ConsumableStat.Control, 8, 4, 10, 5));
        rows.Should().ContainEquivalentOf(Row(4716, ConsumableKind.Meal, ConsumableStat.CP, 10, 10, 13, 12));
        rows.Should().ContainEquivalentOf(Row(4722, ConsumableKind.Meal, ConsumableStat.Craftsmanship, 12, 12, 15, 15));
        rows.Should().NotContain(r => r.ItemId == 4639, "item 4639 is combat food (BaseParams 45, 3, 22)");
        await _xivApi.Received(1).SearchAsync<XivApiConsumableItemFields>(
            Arg.Is("Item"), Arg.Is("ItemUICategory=46"), Arg.Any<string>(), Arg.Any<int>(),
            Arg.Is<string?>(meals.Next), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Meal_with_control_and_cp_slots_is_stored_and_combat_food_is_not()
    {
        // Rroneek Steak's ItemFood values, as Raphael's test_consumables.rs pins them.
        GivenSearch(Meals, [Consumable(44091, WellFed, 1), Consumable(44092, WellFed, 2)]);
        GivenSearch(Medicine, [Consumable(44200, 0, 0)]);
        GivenItemFood([
            Food(1, new Slot(Control, 4, 77, 5, 97), new Slot(Cp, 21, 73, 26, 92)),
            Food(2, new Slot(Hp, 10, 2000, 12, 2400), new Slot(Vitality, 8, 150, 10, 180)),
        ]);
        SeedItems(44091, 44092, 44200);

        await SyncAsync();

        StoredRows().Should().BeEquivalentTo(
        [
            Row(44091, ConsumableKind.Meal, ConsumableStat.Control, 4, 77, 5, 97),
            Row(44091, ConsumableKind.Meal, ConsumableStat.CP, 21, 73, 26, 92),
        ]);
    }

    [Fact]
    public async Task Medicated_status_maps_to_medicine()
    {
        GivenSearch(Meals, [Consumable(44091, WellFed, 1)]);
        GivenSearch(Medicine, [Consumable(44300, Medicated, 3)]);
        GivenItemFood([
            Food(1, new Slot(Control, 4, 77, 5, 97)),
            Food(3, new Slot(Craftsmanship, 3, 60, 4, 75)),
        ]);
        SeedItems(44091, 44300);

        await SyncAsync();

        StoredRows().Should().ContainEquivalentOf(
            Row(44300, ConsumableKind.Medicine, ConsumableStat.Craftsmanship, 3, 60, 4, 75));
    }

    [Fact]
    public async Task An_item_that_no_longer_carries_a_crafting_bonus_loses_its_rows_and_others_keep_theirs()
    {
        SeedItems(1001, 1002);
        GivenSearch(Meals, [Consumable(1001, WellFed, 1), Consumable(1002, WellFed, 2)]);
        GivenSearch(Medicine, [Consumable(2001, 0, 0)]);
        GivenItemFood([
            Food(1, new Slot(Control, 4, 77, 5, 97), new Slot(Cp, 21, 73, 26, 92)),
            Food(2, new Slot(Craftsmanship, 4, 80, 5, 100)),
        ]);
        await SyncAsync();

        GivenItemFood([
            Food(1, new Slot(Control, 4, 77, 5, 97), new Slot(Cp, 21, 73, 26, 92)),
            Food(2, new Slot(Hp, 10, 2000, 12, 2400)),
        ]);
        await SyncAsync();

        StoredRows().Select(r => (r.ItemId, r.Stat)).Should().BeEquivalentTo(
            [(1001, ConsumableStat.Control), (1001, ConsumableStat.CP)]);
    }

    [Fact]
    public async Task Changed_values_are_updated_in_place()
    {
        SeedItems(1001);
        GivenSearch(Meals, [Consumable(1001, WellFed, 1)]);
        GivenSearch(Medicine, [Consumable(2001, 0, 0)]);
        GivenItemFood([Food(1, new Slot(Control, 4, 77, 5, 97))]);
        await SyncAsync();

        GivenItemFood([Food(1, new Slot(Control, 6, 90, 8, 110))]);
        await SyncAsync();

        StoredRows().Should().BeEquivalentTo([Row(1001, ConsumableKind.Meal, ConsumableStat.Control, 6, 90, 8, 110)]);
    }

    [Fact]
    public async Task An_item_missing_from_the_search_keeps_its_rows()
    {
        SeedItems(1001, 1002);
        GivenSearch(Meals, [Consumable(1001, WellFed, 1), Consumable(1002, WellFed, 2)]);
        GivenSearch(Medicine, [Consumable(2001, 0, 0)]);
        GivenItemFood([
            Food(1, new Slot(Control, 4, 77, 5, 97)),
            Food(2, new Slot(Craftsmanship, 4, 80, 5, 100)),
        ]);
        await SyncAsync();

        GivenSearch(Meals, [Consumable(1001, WellFed, 1)]);
        await SyncAsync();

        StoredRows().Select(r => r.ItemId).Should().BeEquivalentTo([1001, 1002]);
    }

    [Fact]
    public async Task An_item_absent_from_Items_is_skipped()
    {
        SeedItems(1001);
        GivenSearch(Meals, [Consumable(1001, WellFed, 1), Consumable(1999, WellFed, 1)]);
        GivenSearch(Medicine, [Consumable(2001, 0, 0)]);
        GivenItemFood([Food(1, new Slot(Control, 4, 77, 5, 97))]);

        var count = await SyncAsync();

        count.Should().Be(1);
        StoredRows().Select(r => r.ItemId).Should().BeEquivalentTo([1001]);
        _logger.Entries.Should().Contain(e => e.Level == LogLevel.Warning && e.Message.Contains("1999"));
    }

    public enum Malformation
    {
        NullItemAction,
        NullItemActionFields,
        ShortData,
        UnknownStatus,
        MissingItemFoodRow,
        ShortItemFoodArrays,
    }

    [Theory]
    [InlineData(Malformation.NullItemAction)]
    [InlineData(Malformation.NullItemActionFields)]
    [InlineData(Malformation.ShortData)]
    [InlineData(Malformation.UnknownStatus)]
    [InlineData(Malformation.MissingItemFoodRow)]
    [InlineData(Malformation.ShortItemFoodArrays)]
    public async Task A_malformed_item_keeps_its_rows(Malformation malformation)
    {
        SeedItems(1001, 1002);
        GivenSearch(Meals, [Consumable(1001, WellFed, 1), Consumable(1002, WellFed, 2)]);
        GivenSearch(Medicine, [Consumable(2001, 0, 0)]);
        var control = Food(1, new Slot(Control, 4, 77, 5, 97));
        var craftsmanship = Food(2, new Slot(Craftsmanship, 4, 80, 5, 100));
        GivenItemFood([control, craftsmanship]);
        await SyncAsync();
        var before = StoredRows();

        var item = Consumable(1002, WellFed, 2);
        var food = new List<XivApiRow<XivApiItemFoodFields>> { control, craftsmanship };
        switch (malformation)
        {
            case Malformation.NullItemAction:
                item = new(1002, new XivApiConsumableItemFields { ItemAction = null });
                break;
            case Malformation.NullItemActionFields:
                item = new(1002, new XivApiConsumableItemFields { ItemAction = new XivApiRowRef<XivApiItemActionFields>() });
                break;
            case Malformation.ShortData:
                item = new(1002, ItemAction([WellFed], [WellFed]));
                break;
            case Malformation.UnknownStatus:
                item = Consumable(1002, 0, 2);
                break;
            case Malformation.MissingItemFoodRow:
                food = [control];
                break;
            case Malformation.ShortItemFoodArrays:
                food = [control, new(2, new XivApiItemFoodFields
                {
                    BaseParam = [Craftsmanship, 0, 0],
                    IsRelative = [true],
                    Value = [4],
                    Max = [80],
                    ValueHQ = [5],
                    MaxHQ = [100],
                })];
                break;
        }
        GivenSearch(Meals, [Consumable(1001, WellFed, 1), item]);
        GivenItemFood(food);

        await SyncAsync();

        StoredRows().Should().BeEquivalentTo(before);
    }

    public enum EmptyPass
    {
        Meals,
        Medicine,
        ItemFood,
    }

    [Theory]
    [InlineData(EmptyPass.Meals, "meals search")]
    [InlineData(EmptyPass.Medicine, "medicine search")]
    [InlineData(EmptyPass.ItemFood, "ItemFood sheet")]
    public async Task An_empty_pass_throws_and_removes_nothing(EmptyPass pass, string named)
    {
        SeedItems(1001, 1002);
        GivenSearch(Meals, [Consumable(1001, WellFed, 1)]);
        GivenSearch(Medicine, [Consumable(1002, Medicated, 2)]);
        GivenItemFood([
            Food(1, new Slot(Control, 4, 77, 5, 97)),
            Food(2, new Slot(Craftsmanship, 3, 60, 4, 75)),
        ]);
        await SyncAsync();
        var before = StoredRows();

        switch (pass)
        {
            case EmptyPass.Meals:
                GivenSearch(Meals, []);
                break;
            case EmptyPass.Medicine:
                GivenSearch(Medicine, []);
                break;
            case EmptyPass.ItemFood:
                GivenItemFood([]);
                break;
        }

        Func<Task> act = SyncAsync;

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage($"*{named} returned no rows*");
        StoredRows().Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task A_first_pass_whose_items_are_all_malformed_throws_and_stores_nothing()
    {
        SeedItems(1001, 1002);
        GivenSearch(Meals, [new(1001, new XivApiConsumableItemFields { ItemAction = null })]);
        GivenSearch(Medicine, [Consumable(1002, Medicated, 9)]);
        GivenItemFood([Food(1, new Slot(Control, 4, 77, 5, 97))]);

        Func<Task> act = SyncAsync;

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*snapshot yields no rows*");
        StoredRows().Should().BeEmpty();
    }

    [Fact]
    public async Task A_search_cursor_that_repeats_throws_and_removes_nothing()
    {
        await SyncOneControlMealAsync();
        var before = StoredRows();

        _xivApi.SearchAsync<XivApiConsumableItemFields>(
                Arg.Is("Item"), Arg.Is($"ItemUICategory={Meals}"), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(([Consumable(1001, WellFed, 1)], "page-2"));
        Func<Task> act = SyncAsync;

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*meals search returned cursor page-2 twice*");
        StoredRows().Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task An_item_food_page_that_does_not_advance_throws_and_removes_nothing()
    {
        await SyncOneControlMealAsync();
        var before = StoredRows();

        IReadOnlyList<XivApiRow<XivApiItemFoodFields>> food = [Food(1, new Slot(Control, 4, 77, 5, 97))];
        _xivApi.GetSheetAsync<XivApiItemFoodFields>(
                Arg.Is("ItemFood"), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns((food, (int?)1));
        Func<Task> act = SyncAsync;

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*ItemFood sheet did not page past row 1*");
        StoredRows().Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task A_search_that_never_stops_paging_throws_at_the_page_cap()
    {
        await SyncOneControlMealAsync();
        var before = StoredRows();

        var cursors = 0;
        _xivApi.SearchAsync<XivApiConsumableItemFields>(
                Arg.Is("Item"), Arg.Is($"ItemUICategory={Meals}"), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ => ([Consumable(1001, WellFed, 1)], $"page-{++cursors}"));
        Func<Task> act = SyncAsync;

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("*meals search ran past 1000 pages*");
        cursors.Should().Be(1000);
        StoredRows().Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task A_snapshot_below_80_percent_of_the_table_throws_and_removes_nothing()
    {
        await SyncTwentyFiveControlMealsAsync();

        GivenItemFood(ControlFood(lostBonuses: 6));
        Func<Task> act = SyncAsync;

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*19 rows against 25 stored*");
        StoredRows().Should().HaveCount(25);
    }

    [Fact]
    public async Task A_snapshot_at_80_percent_of_the_table_is_reconciled()
    {
        await SyncTwentyFiveControlMealsAsync();

        GivenItemFood(ControlFood(lostBonuses: 5));
        await SyncAsync();

        StoredRows().Should().HaveCount(20);
    }

    [Fact]
    public async Task Below_twenty_stored_rows_the_shrink_guard_does_not_apply()
    {
        var ids = Enumerable.Range(1001, 19).ToArray();
        SeedItems(ids);
        GivenSearch(Meals, ids.Select((id, i) => Consumable(id, WellFed, i + 1)).ToList());
        GivenSearch(Medicine, [Consumable(2001, 0, 0)]);
        GivenItemFood(ControlFood(lostBonuses: 0, count: 19));
        await SyncAsync();

        GivenItemFood(ControlFood(lostBonuses: 18, count: 19));
        await SyncAsync();

        StoredRows().Should().HaveCount(1);
    }

    [Fact]
    public async Task An_hq_item_food_row_that_differs_from_the_nq_row_is_logged_and_the_nq_row_is_used()
    {
        SeedItems(1001);
        GivenSearch(Meals, [Consumable(1001, WellFed, 10, hqFoodRow: 11)]);
        GivenSearch(Medicine, [Consumable(2001, 0, 0)]);
        GivenItemFood([
            Food(10, new Slot(Control, 4, 77, 5, 97)),
            Food(11, new Slot(Control, 9, 99, 9, 99)),
        ]);

        await SyncAsync();

        StoredRows().Should().BeEquivalentTo([Row(1001, ConsumableKind.Meal, ConsumableStat.Control, 4, 77, 5, 97)]);
        _logger.Entries.Should().Contain(e =>
            e.Level == LogLevel.Warning && e.Message.Contains("1001") && e.Message.Contains("11"));
    }

    private async Task SyncOneControlMealAsync()
    {
        SeedItems(1001);
        GivenSearch(Meals, [Consumable(1001, WellFed, 1)]);
        GivenSearch(Medicine, [Consumable(2001, 0, 0)]);
        GivenItemFood([Food(1, new Slot(Control, 4, 77, 5, 97))]);
        await SyncAsync();
        StoredRows().Should().HaveCount(1);
    }

    private async Task SyncTwentyFiveControlMealsAsync()
    {
        var ids = Enumerable.Range(1001, 25).ToArray();
        SeedItems(ids);
        GivenSearch(Meals, ids.Select((id, i) => Consumable(id, WellFed, i + 1)).ToList());
        GivenSearch(Medicine, [Consumable(2001, 0, 0)]);
        GivenItemFood(ControlFood(lostBonuses: 0));
        await SyncAsync();
        StoredRows().Should().HaveCount(25);
    }

    private static List<XivApiRow<XivApiItemFoodFields>> ControlFood(int lostBonuses, int count = 25) =>
        Enumerable.Range(1, count)
            .Select(row => row <= lostBonuses
                ? Food(row, new Slot(Hp, 10, 2000, 12, 2400))
                : Food(row, new Slot(Control, 4, 77, 5, 97)))
            .ToList();

    private async Task<int> SyncAsync()
    {
        await using var db = new AppDbContext(_options);
        return await new ConsumableSyncService(db, _xivApi, _logger).SyncConsumablesAsync(CancellationToken.None);
    }

    private List<ConsumableBonus> StoredRows()
    {
        using var db = new AppDbContext(_options);
        return db.ConsumableBonuses.AsNoTracking().ToList()
            .OrderBy(r => r.ItemId).ThenBy(r => r.Stat).ToList();
    }

    private void SeedItems(params int[] ids)
    {
        using var db = new AppDbContext(_options);
        db.Items.AddRange(ids.Select(id => new Item
        {
            ItemId = id,
            Name = $"Item {id}",
            ItemLevel = 700,
            Category = ItemCategory.Other,
            CanBeHQ = true,
        }));
        db.SaveChanges();
    }

    private void GivenSearch(int category, IReadOnlyList<XivApiRow<XivApiConsumableItemFields>> rows, string? next = null)
    {
        _xivApi.SearchAsync<XivApiConsumableItemFields>(
                Arg.Is("Item"), Arg.Is($"ItemUICategory={category}"), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<string?>(4) is null
                ? (rows, next)
                : (Array.Empty<XivApiRow<XivApiConsumableItemFields>>(), (string?)null));
    }

    private void GivenItemFood(IReadOnlyList<XivApiRow<XivApiItemFoodFields>> rows)
    {
        _xivApi.GetSheetAsync<XivApiItemFoodFields>(
                Arg.Is("ItemFood"), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<int?>(3) is null && rows.Count > 0
                ? (rows, (int?)rows[^1].RowId)
                : (Array.Empty<XivApiRow<XivApiItemFoodFields>>(), (int?)null));
    }

    private static (IReadOnlyList<XivApiRow<XivApiConsumableItemFields>> Rows, string? Next) RecordedSearch(string file)
    {
        var response = JsonSerializer.Deserialize<XivApiSearchResponse<XivApiConsumableItemFields>>(
            File.ReadAllText(Path.Combine(RecordingDir, file)), Web)!;
        response.Results.Should().NotBeEmpty();
        return (response.Results.Select(r => new XivApiRow<XivApiConsumableItemFields>(r.RowId, r.Fields)).ToList(), response.Next);
    }

    private static IReadOnlyList<XivApiRow<XivApiItemFoodFields>> RecordedSheet(string file)
    {
        var response = JsonSerializer.Deserialize<XivApiSheetResponse<XivApiItemFoodFields>>(
            File.ReadAllText(Path.Combine(RecordingDir, file)), Web)!;
        response.Rows.Should().NotBeEmpty();
        return response.Rows.Select(r => new XivApiRow<XivApiItemFoodFields>(r.RowId, r.Fields)).ToList();
    }

    private static XivApiRow<XivApiConsumableItemFields> Consumable(int itemId, int status, int foodRow, int? hqFoodRow = null) =>
        new(itemId, ItemAction([status, foodRow, 1800], [status, hqFoodRow ?? foodRow, 1800]));

    private static XivApiConsumableItemFields ItemAction(List<int> data, List<int> dataHq) => new()
    {
        ItemAction = new XivApiRowRef<XivApiItemActionFields>
        {
            Fields = new XivApiItemActionFields { Data = data, DataHQ = dataHq },
        },
    };

    private sealed record Slot(int BaseParam, int Value, int Max, int ValueHQ, int MaxHQ);

    // ItemFood rows always carry three slots; unused ones are zero.
    private static XivApiRow<XivApiItemFoodFields> Food(int rowId, params Slot[] slots)
    {
        var padded = slots.Concat(Enumerable.Repeat(new Slot(0, 0, 0, 0, 0), 3 - slots.Length)).ToList();
        return new(rowId, new XivApiItemFoodFields
        {
            BaseParam = padded.Select(s => s.BaseParam).ToList(),
            IsRelative = padded.Select(s => s.BaseParam != 0).ToList(),
            Value = padded.Select(s => s.Value).ToList(),
            Max = padded.Select(s => s.Max).ToList(),
            ValueHQ = padded.Select(s => s.ValueHQ).ToList(),
            MaxHQ = padded.Select(s => s.MaxHQ).ToList(),
        });
    }

    private static ConsumableBonus Row(int itemId, ConsumableKind kind, ConsumableStat stat, int value, int max, int valueHq, int maxHq) =>
        new()
        {
            ItemId = itemId,
            Kind = kind,
            Stat = stat,
            IsRelative = true,
            Value = value,
            Max = max,
            ValueHQ = valueHq,
            MaxHQ = maxHq,
        };

    public void Dispose() => _connection.Dispose();

    private sealed class CapturingLogger : ILogger<ConsumableSyncService>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => Entries.Add((logLevel, formatter(state, exception)));
    }
}
