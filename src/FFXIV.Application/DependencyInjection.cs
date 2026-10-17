using FFXIV.Application.CraftingEngine;
using FFXIV.Application.CraftPlans;
using FFXIV.Application.Gathering;
using FFXIV.Application.Gear;
using FFXIV.Application.Journey;
using FFXIV.Application.Market;
using FFXIV.Application.Notifications;
using FFXIV.Application.Recipes;
using FFXIV.Domain.CraftingEngine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FFXIV.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        services.AddScoped<IRecipeTreeResolver, RecipeTreeResolver>();
        services.AddScoped<ITreeValidationService, TreeValidationService>();
        services.AddScoped<IGearRecommendationService, GearRecommendationService>();
        services.AddScoped<IRecipeParameterFactory, RecipeParameterFactory>();
        services.AddSingleton<ICraftingSimulator, CraftingSimulator>();
        services.AddScoped<IStatSufficiencyService, StatSufficiencyService>();
        services.AddScoped<IContextualLinkService, ContextualLinkService>();
        services.AddScoped<ICraftPlanResolver, CraftPlanResolver>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICostEstimationService, CostEstimationService>();
        services.AddScoped<IGatheringGuideService, GatheringGuideService>();
        services.AddScoped<INotificationService, NotificationService>();

        return services;
    }
}
