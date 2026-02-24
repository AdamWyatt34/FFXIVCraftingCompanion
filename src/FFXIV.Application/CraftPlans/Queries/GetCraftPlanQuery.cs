using FFXIV.Application.CraftPlans.DTOs;
using MediatR;

namespace FFXIV.Application.CraftPlans.Queries;

public sealed record GetCraftPlanQuery(string UserId, string PlanId) : IRequest<CraftPlanDto?>;
