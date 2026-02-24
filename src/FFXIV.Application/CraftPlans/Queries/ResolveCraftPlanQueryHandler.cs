using FFXIV.Application.CraftPlans.DTOs;
using MediatR;

namespace FFXIV.Application.CraftPlans.Queries;

public sealed class ResolveCraftPlanQueryHandler
    : IRequestHandler<ResolveCraftPlanQuery, CraftPlanResultDto?>
{
    private readonly ICraftPlanResolver _resolver;

    public ResolveCraftPlanQueryHandler(ICraftPlanResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<CraftPlanResultDto?> Handle(
        ResolveCraftPlanQuery request, CancellationToken cancellationToken)
    {
        var inputs = CraftPlanMapper.ToInputs(request.Items);
        var result = await _resolver.ResolveAsync(inputs, cancellationToken);
        return result is null ? null : CraftPlanMapper.ToResultDto(result);
    }
}
