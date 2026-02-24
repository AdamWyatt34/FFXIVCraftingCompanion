using FFXIV.Application.CraftPlans.DTOs;
using MediatR;

namespace FFXIV.Application.CraftPlans.Queries;

public sealed record ResolveCraftPlanQuery(
    List<CraftPlanItemInput> Items) : IRequest<CraftPlanResultDto?>;
