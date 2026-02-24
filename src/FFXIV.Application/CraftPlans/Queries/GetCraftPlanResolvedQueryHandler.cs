using System.Text.Json;
using FFXIV.Application.CraftPlans.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.CraftPlans.Queries;

public sealed class GetCraftPlanResolvedQueryHandler
    : IRequestHandler<GetCraftPlanResolvedQuery, CraftPlanResultDto?>
{
    private readonly ICraftPlanRepository _craftPlanRepo;
    private readonly ICraftPlanResolver _resolver;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GetCraftPlanResolvedQueryHandler(
        ICraftPlanRepository craftPlanRepo,
        ICraftPlanResolver resolver)
    {
        _craftPlanRepo = craftPlanRepo;
        _resolver = resolver;
    }

    public async Task<CraftPlanResultDto?> Handle(
        GetCraftPlanResolvedQuery request, CancellationToken cancellationToken)
    {
        var plan = await _craftPlanRepo.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan is null)
            return null;

        var items = JsonSerializer.Deserialize<List<CraftPlanItemInput>>(plan.Items, JsonOptions);
        if (items is null || items.Count == 0)
            return null;

        var inputs = CraftPlanMapper.ToInputs(items);
        var result = await _resolver.ResolveAsync(inputs, cancellationToken);
        return result is null ? null : CraftPlanMapper.ToResultDto(result);
    }
}
