using FFXIV.Application.CraftPlans.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.CraftPlans.Queries;

// A plan's name and items for a shareable link: what GET {id}/resolved already exposes, minus the
// resolution, which the frontend now does from the static game data.
public sealed record GetSharedCraftPlanQuery(string PlanId) : IRequest<CraftPlanDto?>;

public sealed class GetSharedCraftPlanQueryHandler(ICraftPlanRepository craftPlanRepo)
    : IRequestHandler<GetSharedCraftPlanQuery, CraftPlanDto?>
{
    public async Task<CraftPlanDto?> Handle(GetSharedCraftPlanQuery request, CancellationToken cancellationToken)
    {
        var plan = await craftPlanRepo.GetByIdAsync(request.PlanId, cancellationToken);
        return plan is null ? null : CraftPlanMapper.ToDto(plan);
    }
}
