using FFXIV.Application.Recipes;
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

        return services;
    }
}
