using FFXIV.Application.CraftPlans.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.CraftPlans.Queries;

public sealed class GetCraftPlansQueryHandler
    : IRequestHandler<GetCraftPlansQuery, IReadOnlyList<CraftPlanDto>>
{
    private readonly ICraftPlanRepository _craftPlanRepo;

    public GetCraftPlansQueryHandler(ICraftPlanRepository craftPlanRepo)
    {
        _craftPlanRepo = craftPlanRepo;
    }

    public async Task<IReadOnlyList<CraftPlanDto>> Handle(
        GetCraftPlansQuery request, CancellationToken cancellationToken)
    {
        var plans = await _craftPlanRepo.GetByUserAsync(request.UserId, cancellationToken);
        return plans.Select(CraftPlanMapper.ToDto).ToList();
    }
}
