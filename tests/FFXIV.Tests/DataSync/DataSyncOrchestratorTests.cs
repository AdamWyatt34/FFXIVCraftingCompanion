using FFXIV.Domain.Interfaces;
using FFXIV.Infrastructure.DataSync;
using FFXIV.Infrastructure.ExternalApis;
using FFXIV.Infrastructure.ExternalApis.Models;
using FFXIV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FFXIV.Tests.DataSync;

public sealed class DataSyncOrchestratorTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly DbContextOptions<AppDbContext> _options;
    private readonly IXivApiClient _xivApi = Substitute.For<IXivApiClient>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();

    public DataSyncOrchestratorTests()
    {
        _connection.Open();
        _options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        using var db = new AppDbContext(_options);
        db.Database.EnsureCreated();
    }

    [Fact]
    public async Task A_failing_consumable_pass_lets_the_full_sync_store_recipes_and_equipment_and_still_fails_it()
    {
        _xivApi.GetCurrentGameVersionAsync(Arg.Any<CancellationToken>()).Returns(("v7", "7.5"));
        GivenSheet("Item", new XivApiRow<XivApiItemFields>(5001, new XivApiItemFields { Name = "Rroneek Saw" }));
        GivenSheet("Recipe", new XivApiRow<XivApiRecipeFields>(9001, new XivApiRecipeFields
        {
            ItemResult = new XivApiRowRef { RowId = 5001 },
        }));
        GivenSheet("EquipSlotCategory", new XivApiRow<XivApiEquipSlotCategoryFields>(1, new XivApiEquipSlotCategoryFields { MainHand = 1 }));
        GivenSheet("ClassJobCategory", new XivApiRow<XivApiClassJobCategoryFields>(2, new XivApiClassJobCategoryFields { Name = "CRP", CRP = true }));
        GivenSheet("Item", new XivApiRow<XivApiEquipmentItemFields>(5001, new XivApiEquipmentItemFields
        {
            Name = "Rroneek Saw",
            EquipSlotCategoryRowId = 1,
            ClassJobCategoryRowId = 2,
        }));
        // An XIVAPI outage reads as an empty search, which the consumable pass refuses.
        _xivApi.SearchAsync<XivApiConsumableItemFields>(
                Arg.Is("Item"), Arg.Is("ItemUICategory=46"), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((Array.Empty<XivApiRow<XivApiConsumableItemFields>>(), null));

        await using var db = new AppDbContext(_options);
        var orchestrator = Orchestrator(db, new DataSyncOptions { SyncRecipeLevelTable = false, SyncSources = false });

        Func<Task> act = () => orchestrator.RunFullSyncAsync(CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>()).WithMessage("*meals search returned no rows*");
        await using var check = new AppDbContext(_options);
        check.Recipes.Select(r => r.RecipeId).Should().BeEquivalentTo([9001]);
        check.Equipment.Select(e => e.EquipmentId).Should().BeEquivalentTo([5001]);
        check.SyncMetadata.Select(s => s.EntityType).Should().BeEquivalentTo(["Item", "Recipe", "Equipment"]);
        await _cache.Received(1).RemoveByPrefixAsync("recipe:", Arg.Any<CancellationToken>());
    }

    private DataSyncOrchestrator Orchestrator(AppDbContext db, DataSyncOptions syncOptions)
    {
        var options = Options.Create(syncOptions);
        return new DataSyncOrchestrator(
            db,
            _xivApi,
            options,
            new SourceDataSyncService(db, _xivApi, options, NullLogger<SourceDataSyncService>.Instance),
            new EquipmentSyncService(db, _xivApi, options, NullLogger<EquipmentSyncService>.Instance),
            new ConsumableSyncService(db, _xivApi, NullLogger<ConsumableSyncService>.Instance),
            new GarlandDataEnricher(Substitute.For<IGarlandToolsClient>(), db, NullLogger<GarlandDataEnricher>.Instance),
            _cache,
            NullLogger<DataSyncOrchestrator>.Instance);
    }

    private void GivenSheet<T>(string sheet, params XivApiRow<T>[] rows) where T : class
    {
        _xivApi.GetSheetAsync<T>(Arg.Is(sheet), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<int?>(), Arg.Any<CancellationToken>())
            .Returns(call => call.ArgAt<int?>(3) is null
                ? ((IReadOnlyList<XivApiRow<T>>)rows, (int?)rows[^1].RowId)
                : (Array.Empty<XivApiRow<T>>(), (int?)null));
    }

    public void Dispose() => _connection.Dispose();
}
