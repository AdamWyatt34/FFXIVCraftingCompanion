using FFXIV.Application.CraftPlans.DTOs;
using MediatR;

namespace FFXIV.Application.CraftPlans.Queries;

public sealed record GetCraftPlansQuery(string UserId) : IRequest<IReadOnlyList<CraftPlanDto>>;
