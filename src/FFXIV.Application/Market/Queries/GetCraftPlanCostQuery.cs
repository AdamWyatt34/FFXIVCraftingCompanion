using System.Text.Json;
using FFXIV.Application.CraftPlans;
using FFXIV.Application.CraftPlans.DTOs;
using FFXIV.Application.Market.DTOs;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Market.Queries;

public sealed record GetCraftPlanCostQuery(
    string PlanId,
    string WorldOrDc,
    CostEstimationOptions Options) : IRequest<CostEstimateDto?>;

public sealed class GetCraftPlanCostQueryHandler
    : IRequestHandler<GetCraftPlanCostQuery, CostEstimateDto?>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ICraftPlanRepository _craftPlanRepo;
    private readonly ICraftPlanResolver _resolver;
    private readonly ICostEstimationService _costService;

    public GetCraftPlanCostQueryHandler(
        ICraftPlanRepository craftPlanRepo,
        ICraftPlanResolver resolver,
        ICostEstimationService costService)
    {
        _craftPlanRepo = craftPlanRepo;
        _resolver = resolver;
        _costService = costService;
    }

    public async Task<CostEstimateDto?> Handle(
        GetCraftPlanCostQuery request, CancellationToken cancellationToken)
    {
        var plan = await _craftPlanRepo.GetByIdAsync(request.PlanId, cancellationToken);
        if (plan is null)
            return null;

        var items = JsonSerializer.Deserialize<List<CraftPlanItemInput>>(plan.Items, JsonOptions);
        if (items is null || items.Count == 0)
            return null;

        var inputs = CraftPlanMapper.ToInputs(items);
        var result = await _resolver.ResolveAsync(inputs, cancellationToken);
        if (result is null)
            return null;

        // Combine raw materials and crystals for cost estimation
        var materials = result.RawMaterials.Concat(result.Crystals).ToList();

        var estimate = await _costService.EstimateCostAsync(
            materials, null, request.WorldOrDc, request.Options, cancellationToken);

        return MarketMapper.ToDto(estimate);
    }
}
