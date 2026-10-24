using System.Text.Json;
using FFXIV.Application;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Repositories;
using FFXIV.Export;
using FFXIV.Infrastructure.Caching;
using FFXIV.Infrastructure.Persistence;
using FFXIV.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FFXIV.Tests.StaticData;

// The frozen game-data slice written by `FFXIV.Export --fixture-recipes`, loaded into an
// in-memory SQLite AppDbContext behind the real repositories and handlers. Goldens generated
// over it are the reference for every computation ported to the frontend.
public sealed class FixtureDatabase : IDisposable
{
    public static readonly string FixturePath =
        Path.Combine(AppContext.BaseDirectory, "StaticData", "TestData", "fixture.json");

    public static readonly string UniversalisSamplePath =
        Path.Combine(AppContext.BaseDirectory, "StaticData", "TestData", "universalis-sample.json");

    /// The clock every time-dependent golden runs on (cost staleness, generated-at).
    public static readonly DateTimeOffset Now = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _services;

    public FixtureWriter.Fixture Fixture { get; }
    public IServiceProvider Services => _services;

    public FixtureDatabase()
    {
        Fixture = JsonSerializer.Deserialize<FixtureWriter.Fixture>(File.ReadAllText(FixturePath), StaticDataExporter.Json)
            ?? throw new InvalidOperationException("fixture.json is empty.");

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddSingleton<ICacheService, InMemoryCacheService>();
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_connection));
        services.AddScoped<AppDbContext>(sp => new FixtureDbContext(sp.GetRequiredService<DbContextOptions<AppDbContext>>()));
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IItemSourceRepository, ItemSourceRepository>();
        services.AddScoped<IGatheringInfoRepository, GatheringInfoRepository>();
        services.AddScoped<IVendorInfoRepository, VendorInfoRepository>();
        services.AddScoped<IRecipeLevelRepository, RecipeLevelRepository>();
        services.AddApplication();
        services.AddScoped(_ => Substitute.For<ICharacterProfileRepository>());
        services.AddSingleton<TimeProvider>(new FixedClock(Now));
        services.AddSingleton(_ => UniversalisFromSample());
        _services = services.BuildServiceProvider();

        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        db.Items.AddRange(Fixture.Items);
        db.Recipes.AddRange(Fixture.Recipes);
        db.RecipeIngredients.AddRange(Fixture.RecipeIngredients);
        db.RecipeLevelEntries.AddRange(Fixture.RecipeLevels);
        db.ItemSources.AddRange(Fixture.ItemSources);
        db.GatheringInfos.AddRange(Fixture.GatheringInfos);
        db.VendorInfos.AddRange(Fixture.VendorInfos);
        db.Equipment.AddRange(Fixture.Equipment);
        db.SaveChanges();
    }

    public IServiceScope CreateScope() => _services.CreateScope();

    // A recorded Universalis batch response for the fixture's materials (world Gilgamesh),
    // answered through the real response mapping.
    public static Dictionary<int, Domain.Models.MarketBoardData> UniversalisSample()
    {
        var raw = JsonSerializer.Deserialize<Infrastructure.ExternalApis.Models.UniversalisBatchResponse>(
            File.ReadAllText(UniversalisSamplePath))!;
        return raw.Items.ToDictionary(
            kv => int.Parse(kv.Key),
            kv => Infrastructure.ExternalApis.UniversalisClient.MapToMarketBoardData(kv.Value));
    }

    private static Domain.Interfaces.IUniversalisClient UniversalisFromSample()
    {
        var sample = UniversalisSample();
        var client = Substitute.For<Domain.Interfaces.IUniversalisClient>();
        client.GetItemPricesBatchAsync(Arg.Any<string>(), Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IEnumerable<int>>()
                .Where(sample.ContainsKey)
                .ToDictionary(id => id, id => sample[id]));
        return client;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    // Production orders names under SQL Server's case-insensitive collation; SQLite's default is
    // binary. NOCASE (ASCII case folding) is the order the frontend port reproduces.
    private sealed class FixtureDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Domain.Entities.Item>().Property(i => i.Name).UseCollation("NOCASE");
        }
    }

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }
}
