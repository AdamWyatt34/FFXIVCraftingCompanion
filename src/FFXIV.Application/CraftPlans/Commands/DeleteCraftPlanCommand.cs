using MediatR;

namespace FFXIV.Application.CraftPlans.Commands;

public sealed record DeleteCraftPlanCommand(string UserId, string PlanId) : IRequest<bool>;
