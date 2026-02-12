using FFXIV.Application.CraftingEngine;
using FFXIV.Application.Gear;
using FFXIV.Application.Recipes;
using FFXIV.Domain.CraftingEngine;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
