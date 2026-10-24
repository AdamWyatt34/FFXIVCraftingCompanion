using System.Text.Json;
using FFXIV.Api;
using FFXIV.Application.Auth;
using FFXIV.Application.Profile;
using FFXIV.Domain.Interfaces;
using FFXIV.Export;
using FFXIV.Infrastructure.Persistence;
using FFXIV.Tests.StaticData;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace FFXIV.Tests.Api;

/// <summary>
/// The API on the test server: the real pipeline (forwarded headers, rate limiting, JWT bearer,
/// controllers and middleware) over an in-memory SQLite database seeded with the static-data
/// fixture. The outbound clients (Lodestone, Discord, XIVAPI) are substitutes, so nothing in the
/// suite leaves the process. Accounts are created through the API itself.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<IApiMarker>
{
    public const string AdminKey = "integration-test-admin-key";
    public const string JwtKey = "integration-test-jwt-secret-key-with-plenty-of-length-0123456789";

    /// <summary>Dark Chestnut Lumber (Carpenter, rlvl 133), a root recipe of the static-data fixture.</summary>
    public const int FixtureRecipeId = 2240;
    public const int UnknownRecipeId = 999_999;

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly IReadOnlyDictionary<string, string?> _settings;

    public ApiFactory() : this(new Dictionary<string, string?>()) { }

    /// <param name="settings">Configuration overrides, such as a lower rate limit. Not public: a
    /// class fixture may have one public constructor.</param>
    internal ApiFactory(IReadOnlyDictionary<string, string?> settings)
    {
        _settings = settings;
        _connection.Open();
    }

    public ILodestoneService Lodestone { get; } = Substitute.For<ILodestoneService>();

    public IDiscordOAuthService Discord { get; } = Substitute.For<IDiscordOAuthService>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Settings go in as host configuration, which the factory hands to Program as command-line
        // arguments, so the values Program reads while registering services (the JWT key, the
        // rate limits) already see them. "Testing" skips appsettings.Development.json and its
        // Redis connection.
        builder.UseEnvironment("Testing");
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:SecretKey"] = JwtKey,
            ["Admin:ApiKey"] = AdminKey,
            ["DataSync:FullSyncOnStartup"] = "false",
            ["DataSync:EnableAutoSync"] = "false",
            ["RateLimiting:AuthPerFiveMinutes"] = "1000",
            ["RateLimiting:RegisterPerHour"] = "1000",
            ["RateLimiting:PublicReadPerMinute"] = "1000",
            ["RateLimiting:RotationWritePerHour"] = "1000",
            ["RateLimiting:FeedbackPerHour"] = "1000",
        };
        foreach (var (key, value) in _settings)
            settings[key] = value;
        foreach (var (key, value) in settings)
            builder.UseSetting(key, value);

        builder.ConfigureServices(services =>
        {
            // SQL Server out, one shared in-memory SQLite connection in.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));
            services.AddScoped<AppDbContext>(sp =>
                new SqliteAppDbContext(sp.GetRequiredService<DbContextOptions<AppDbContext>>()));

            services.RemoveAll<ILodestoneService>();
            services.AddSingleton(Lodestone);
            services.RemoveAll<IDiscordOAuthService>();
            services.AddSingleton(Discord);

            // The readiness report asks XIVAPI for the game version; answer it locally.
            var xivApi = Substitute.For<IXivApiClient>();
            xivApi.GetCurrentGameVersionAsync(Arg.Any<CancellationToken>())
                .Returns(("2026.10.17.0000.0000", "7.57"));
            services.RemoveAll<IXivApiClient>();
            services.AddSingleton(xivApi);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        Seed(host.Services);
        return host;
    }

    /// <summary>The static-data fixture: items, recipes, levels, sources and equipment.</summary>
    private static void Seed(IServiceProvider services)
    {
        var fixture = JsonSerializer.Deserialize<FixtureWriter.Fixture>(
                File.ReadAllText(FixtureDatabase.FixturePath), StaticDataExporter.Json)
            ?? throw new InvalidOperationException("fixture.json is empty.");

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        db.Items.AddRange(fixture.Items);
        db.Recipes.AddRange(fixture.Recipes);
        db.RecipeIngredients.AddRange(fixture.RecipeIngredients);
        db.RecipeLevelEntries.AddRange(fixture.RecipeLevels);
        db.ItemSources.AddRange(fixture.ItemSources);
        db.GatheringInfos.AddRange(fixture.GatheringInfos);
        db.VendorInfos.AddRange(fixture.VendorInfos);
        db.Equipment.AddRange(fixture.Equipment);
        db.SaveChanges();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }

    /// <summary>
    /// SQLite cannot order or compare <see cref="DateTimeOffset"/> columns and the repositories
    /// order by CreatedAt and UpdatedAt, so every such property is stored as UTC ticks here.
    /// </summary>
    private sealed class SqliteAppDbContext(DbContextOptions<AppDbContext> options) : AppDbContext(options)
    {
        private static readonly ValueConverter<DateTimeOffset, long> UtcTicks =
            new(value => value.UtcTicks, ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entity.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset) || property.ClrType == typeof(DateTimeOffset?))
                        property.SetValueConverter(UtcTicks);
                }
            }
        }
    }
}
