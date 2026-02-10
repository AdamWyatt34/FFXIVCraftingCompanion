using System.Text;
using System.Threading.RateLimiting;
using FFXIV.Application.Auth;
using FFXIV.Application.Profile;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Repositories;
using FFXIV.Infrastructure.Caching;
using FFXIV.Infrastructure.DataSync;
using FFXIV.Infrastructure.ExternalApis;
using FFXIV.Infrastructure.Identity;
using FFXIV.Infrastructure.Persistence;
using FFXIV.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NetStone;
using Polly;
using StackExchange.Redis;

namespace FFXIV.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        AddCaching(services, configuration);
        AddIdentityServices(services, configuration);

        services.AddScoped<IItemRepository, ItemRepository>();
        services.AddScoped<IRecipeRepository, RecipeRepository>();
        services.AddScoped<IEquipmentRepository, EquipmentRepository>();
        services.AddScoped<IItemSourceRepository, ItemSourceRepository>();
        services.AddScoped<IGatheringInfoRepository, GatheringInfoRepository>();
        services.AddScoped<IVendorInfoRepository, VendorInfoRepository>();
        services.AddScoped<ICharacterProfileRepository, CharacterProfileRepository>();
        services.AddScoped<ISavedListRepository, SavedListRepository>();
        services.AddScoped<IFavoriteRecipeRepository, FavoriteRecipeRepository>();

        // Data Sync
        services.Configure<DataSyncOptions>(configuration.GetSection(DataSyncOptions.SectionName));
        services.AddScoped<DataSyncOrchestrator>();
        services.AddScoped<SourceDataSyncService>();
        services.AddScoped<EquipmentSyncService>();
        services.AddSingleton<DataSyncService>();
        services.AddSingleton<IDataSyncTrigger>(sp => sp.GetRequiredService<DataSyncService>());
        services.AddHostedService(sp => sp.GetRequiredService<DataSyncService>());

        AddXivApiClient(services, configuration);
        AddGarlandToolsClient(services, configuration);
        services.AddScoped<GarlandDataEnricher>();

        AddLodestoneServices(services);

        return services;
    }

    private static void AddCaching(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()
            ?? new RedisOptions();

        if (redisOptions.Enabled)
        {
            try
            {
                var multiplexer = ConnectionMultiplexer.Connect(redisOptions.ConnectionString);
                services.AddSingleton<IConnectionMultiplexer>(multiplexer);
                services.AddSingleton<ICacheService, RedisCacheService>();
                return;
            }
            catch (Exception ex)
            {
                // Build a temporary logger to report the fallback
                using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
                var logger = loggerFactory.CreateLogger("FFXIV.Infrastructure");
                logger.LogWarning(ex, "Failed to connect to Redis, falling back to in-memory cache");
            }
        }

        services.AddMemoryCache();
        services.AddSingleton<ICacheService, InMemoryCacheService>();
    }

    private static void AddXivApiClient(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var optionsSection = configuration.GetSection(XivApiOptions.SectionName);
        services.Configure<XivApiOptions>(optionsSection);
        var options = optionsSection.Get<XivApiOptions>() ?? new XivApiOptions();

        services.AddHttpClient<IXivApiClient, XivApiClient>(client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Add("User-Agent", "FFXIVCraftingCompanion/1.0");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .AddResilienceHandler("XivApiPipeline", builder =>
        {
            builder.AddRateLimiter(new SlidingWindowRateLimiter(
                new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = options.RateLimitPermitCount,
                    Window = TimeSpan.FromSeconds(options.RateLimitWindowSeconds),
                    SegmentsPerWindow = 4,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 100
                }));

            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(2)
            });

            builder.AddTimeout(TimeSpan.FromSeconds(options.TimeoutSeconds));
        });
    }

    private static void AddIdentityServices(
        IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<DiscordOptions>(configuration.GetSection(DiscordOptions.SectionName));

        services.AddIdentityCore<ApplicationUser>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = false;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<AppDbContext>()
        .AddDefaultTokenProviders();

        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? new JwtOptions();

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                ClockSkew = TimeSpan.FromMinutes(1)
            };
        });

        services.AddScoped<ITokenService, TokenService>();

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddHttpClient<IDiscordOAuthService, DiscordOAuthService>(client =>
        {
            client.BaseAddress = new Uri("https://discord.com/api/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("User-Agent", "FFXIVCraftingCompanion/1.0");
        });
    }

    private static void AddGarlandToolsClient(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var optionsSection = configuration.GetSection(GarlandToolsOptions.SectionName);
        services.Configure<GarlandToolsOptions>(optionsSection);
        var options = optionsSection.Get<GarlandToolsOptions>() ?? new GarlandToolsOptions();

        services.AddHttpClient<IGarlandToolsClient, GarlandToolsClient>(client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
            client.DefaultRequestHeaders.Add("User-Agent", "FFXIVCraftingCompanion/1.0");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        })
        .AddResilienceHandler("GarlandToolsPipeline", builder =>
        {
            builder.AddRateLimiter(new SlidingWindowRateLimiter(
                new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = options.RateLimitPermitCount,
                    Window = TimeSpan.FromSeconds(options.RateLimitWindowSeconds),
                    SegmentsPerWindow = 2,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 50
                }));

            builder.AddRetry(new HttpRetryStrategyOptions
            {
                MaxRetryAttempts = options.MaxRetryAttempts,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                Delay = TimeSpan.FromSeconds(3)
            });

            builder.AddTimeout(TimeSpan.FromSeconds(options.TimeoutSeconds));
        });
    }

    private static void AddLodestoneServices(IServiceCollection services)
    {
        services.AddSingleton(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<LodestoneService>>();
            logger.LogInformation("Initializing NetStone LodestoneClient...");
            var client = LodestoneClient.GetClientAsync().GetAwaiter().GetResult();
            logger.LogInformation("NetStone LodestoneClient initialized successfully");
            return client;
        });

        services.AddScoped<ILodestoneService, LodestoneService>();
    }
}
