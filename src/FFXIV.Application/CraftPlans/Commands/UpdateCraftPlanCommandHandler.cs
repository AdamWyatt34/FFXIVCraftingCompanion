using FFXIV.Application.CraftPlans.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.CraftPlans.Commands;

public sealed class UpdateCraftPlanCommandHandler
    : IRequestHandler<UpdateCraftPlanCommand, CraftPlanDto?>
{
    private readonly ICraftPlanRepository _craftPlanRepo;

    public UpdateCraftPlanCommandHandler(ICraftPlanRepository craftPlanRepo)
    {
        _craftPlanRepo = craftPlanRepo;
    }

    public async Task<CraftPlanDto?> Handle(
        UpdateCraftPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _craftPlanRepo.GetByIdForUserAsync(request.PlanId, request.UserId, cancellationToken);
        if (plan is null)
            return null;

        if (request.Name is not null)
            plan.Name = request.Name;

        if (request.Items is not null)
            plan.Items = CraftPlanMapper.SerializeItems(request.Items);

        plan.UpdatedAt = DateTimeOffset.UtcNow;

        await _craftPlanRepo.UpdateAsync(plan, cancellationToken);
        return CraftPlanMapper.ToDto(plan);
    }
}
