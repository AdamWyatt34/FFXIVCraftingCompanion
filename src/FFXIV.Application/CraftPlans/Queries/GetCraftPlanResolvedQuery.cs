using FFXIV.Application.CraftPlans.DTOs;
using MediatR;

namespace FFXIV.Application.CraftPlans.Queries;

public sealed record GetCraftPlanResolvedQuery(string PlanId) : IRequest<CraftPlanResultDto?>;
