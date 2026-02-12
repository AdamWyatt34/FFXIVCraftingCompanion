using FFXIV.Application.Crafting.DTOs;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.Interfaces;
using MediatR;

namespace FFXIV.Application.Crafting.Queries;

public sealed record GetCraftingActionsQuery(int? Level) : IRequest<List<ActionInfoDto>>;

public sealed class GetCraftingActionsQueryHandler
    : IRequestHandler<GetCraftingActionsQuery, List<ActionInfoDto>>
{
    private readonly ICacheService _cache;
    private const string CacheKey = "crafting:actions:all";

    public GetCraftingActionsQueryHandler(ICacheService cache)
    {
        _cache = cache;
    }

    public async Task<List<ActionInfoDto>> Handle(
        GetCraftingActionsQuery request, CancellationToken cancellationToken)
    {
        var allActions = await _cache.GetAsync<List<ActionInfoDto>>(CacheKey, cancellationToken);

        if (allActions is null)
        {
            allActions = CraftingActionRegistry.GetAll()
                .Select(CraftingMapper.ToDto)
                .OrderBy(a => a.Category)
                .ThenBy(a => a.LevelRequirement)
                .ToList();

            await _cache.SetAsync(CacheKey, allActions, TimeSpan.FromDays(7), cancellationToken);
        }

        if (request.Level.HasValue)
        {
            return allActions
                .Where(a => a.LevelRequirement <= request.Level.Value)
                .ToList();
        }

        return allActions;
    }
}
