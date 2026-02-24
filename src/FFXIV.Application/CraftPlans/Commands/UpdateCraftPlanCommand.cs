using FFXIV.Application.CraftPlans.DTOs;
using MediatR;

namespace FFXIV.Application.CraftPlans.Commands;

public sealed record UpdateCraftPlanCommand(
    string UserId,
    string PlanId,
    string? Name,
    List<CraftPlanItemInput>? Items) : IRequest<CraftPlanDto?>;
