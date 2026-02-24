using FFXIV.Application.CraftPlans.DTOs;
using MediatR;

namespace FFXIV.Application.CraftPlans.Commands;

public sealed record CreateCraftPlanCommand(
    string UserId,
    string Name,
    List<CraftPlanItemInput> Items) : IRequest<CraftPlanDto?>;
