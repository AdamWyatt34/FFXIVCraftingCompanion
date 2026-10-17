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
        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IItemSourceRepository, ItemSourceRepository>();
        services.AddScoped<IGatheringInfoRepository, GatheringInfoRepository>();
        services.AddScoped<IVendorInfoRepository, VendorInfoRepository>();
        services.AddScoped<IRecipeLevelRepository, RecipeLevelRepository>();
        services.AddApplication();
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

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }
}
