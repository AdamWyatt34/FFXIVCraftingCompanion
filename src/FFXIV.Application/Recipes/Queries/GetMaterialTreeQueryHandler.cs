using System.Diagnostics;
using FFXIV.Application.Mapping;
using FFXIV.Application.Recipes.DTOs;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFXIV.Application.Recipes.Queries;

public sealed class GetMaterialTreeQueryHandler
    : IRequestHandler<GetMaterialTreeQuery, MaterialTreeResultDto?>
{
    private readonly IRecipeRepository _recipeRepo;
    private readonly IRecipeTreeResolver _resolver;
    private readonly ICacheService _cache;
    private readonly ILogger<GetMaterialTreeQueryHandler> _logger;

    public GetMaterialTreeQueryHandler(
        IRecipeRepository recipeRepo,
        IRecipeTreeResolver resolver,
        ICacheService cache,
        ILogger<GetMaterialTreeQueryHandler> logger)
    {
        _recipeRepo = recipeRepo;
        _resolver = resolver;
        _cache = cache;
        _logger = logger;
    }

    public async Task<MaterialTreeResultDto?> Handle(
        GetMaterialTreeQuery request, CancellationToken cancellationToken)
    {
        var cacheKey = $"materialtree:{request.RecipeId}:{request.Quantity}";
        var cached = await _cache.GetAsync<MaterialTreeResultDto>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Material tree cache hit for {CacheKey}", cacheKey);
            return cached;
        }

        var recipe = await _recipeRepo.GetByIdAsync(request.RecipeId, cancellationToken);
        if (recipe is null)
            return null;

        var stopwatch = Stopwatch.StartNew();

        var domainResult = await _resolver.ResolveTreeAsync(
            recipe.ResultItemId, request.Quantity, cancellationToken);

        stopwatch.Stop();

        var dto = MaterialTreeMapper.ToDto(domainResult, request.Quantity);

        var nodeCount = dto.Summary.TotalUniqueRawMaterials
            + dto.Summary.TotalIntermediateCrafts
            + dto.Summary.TotalUniqueCrystals;

        _logger.LogInformation(
            "Material tree resolved: RecipeId={RecipeId}, Quantity={Quantity}, Duration={Duration}ms, NodeCount={NodeCount}",
            request.RecipeId, request.Quantity, stopwatch.ElapsedMilliseconds, nodeCount);

        if (stopwatch.ElapsedMilliseconds > 2000)
        {
            _logger.LogWarning(
                "Slow material tree resolution: RecipeId={RecipeId}, Quantity={Quantity}, Duration={Duration}ms, NodeCount={NodeCount}",
                request.RecipeId, request.Quantity, stopwatch.ElapsedMilliseconds, nodeCount);
        }

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromHours(24), cancellationToken);
        return dto;
    }
}
