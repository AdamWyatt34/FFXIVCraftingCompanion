using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.CraftPlans.Commands;

public sealed class DeleteCraftPlanCommandHandler
    : IRequestHandler<DeleteCraftPlanCommand, bool>
{
    private readonly ICraftPlanRepository _craftPlanRepo;

    public DeleteCraftPlanCommandHandler(ICraftPlanRepository craftPlanRepo)
    {
        _craftPlanRepo = craftPlanRepo;
    }

    public async Task<bool> Handle(
        DeleteCraftPlanCommand request, CancellationToken cancellationToken)
    {
        var plan = await _craftPlanRepo.GetByIdForUserAsync(request.PlanId, request.UserId, cancellationToken);
        if (plan is null)
            return false;

        await _craftPlanRepo.DeleteAsync(plan, cancellationToken);
        return true;
    }
}
