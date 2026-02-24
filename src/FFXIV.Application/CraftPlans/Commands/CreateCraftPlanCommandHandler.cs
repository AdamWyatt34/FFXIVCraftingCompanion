using FFXIV.Application.CraftPlans.DTOs;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.CraftPlans.Commands;

public sealed class CreateCraftPlanCommandHandler
    : IRequestHandler<CreateCraftPlanCommand, CraftPlanDto?>
{
    private readonly ICraftPlanRepository _craftPlanRepo;

    public CreateCraftPlanCommandHandler(ICraftPlanRepository craftPlanRepo)
    {
        _craftPlanRepo = craftPlanRepo;
    }

    public async Task<CraftPlanDto?> Handle(
        CreateCraftPlanCommand request, CancellationToken cancellationToken)
    {
        var count = await _craftPlanRepo.GetCountByUserAsync(request.UserId, cancellationToken);
        if (count >= 20)
            return null;

        var now = DateTimeOffset.UtcNow;
        var craftPlan = new CraftPlan
        {
            Id = Guid.NewGuid().ToString(),
            UserId = request.UserId,
            Name = request.Name,
            Items = CraftPlanMapper.SerializeItems(request.Items),
            CreatedAt = now,
            UpdatedAt = now
        };

        await _craftPlanRepo.CreateAsync(craftPlan, cancellationToken);
        return CraftPlanMapper.ToDto(craftPlan);
    }
}
