using FFXIV.Application.CraftPlans.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.CraftPlans.Queries;

public sealed class GetCraftPlanQueryHandler
    : IRequestHandler<GetCraftPlanQuery, CraftPlanDto?>
{
    private readonly ICraftPlanRepository _craftPlanRepo;

    public GetCraftPlanQueryHandler(ICraftPlanRepository craftPlanRepo)
    {
        _craftPlanRepo = craftPlanRepo;
    }

    public async Task<CraftPlanDto?> Handle(
        GetCraftPlanQuery request, CancellationToken cancellationToken)
    {
        var plan = await _craftPlanRepo.GetByIdForUserAsync(request.PlanId, request.UserId, cancellationToken);
        return plan is null ? null : CraftPlanMapper.ToDto(plan);
    }
}
