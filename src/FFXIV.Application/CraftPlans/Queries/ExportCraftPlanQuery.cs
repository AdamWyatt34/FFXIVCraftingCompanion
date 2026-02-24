using FFXIV.Application.CraftPlans.DTOs;
using MediatR;

namespace FFXIV.Application.CraftPlans.Queries;

public sealed record ExportCraftPlanQuery(
    List<CraftPlanItemInput> Items,
    string Format) : IRequest<ExportResultDto?>;
