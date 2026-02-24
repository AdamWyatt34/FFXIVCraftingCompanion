using FFXIV.Application.CraftPlans.DTOs;
using MediatR;

namespace FFXIV.Application.CraftPlans.Queries;

public sealed class ExportCraftPlanQueryHandler
    : IRequestHandler<ExportCraftPlanQuery, ExportResultDto?>
{
    private readonly ICraftPlanResolver _resolver;

    public ExportCraftPlanQueryHandler(ICraftPlanResolver resolver)
    {
        _resolver = resolver;
    }

    public async Task<ExportResultDto?> Handle(
        ExportCraftPlanQuery request, CancellationToken cancellationToken)
    {
        var inputs = CraftPlanMapper.ToInputs(request.Items);
        var result = await _resolver.ResolveAsync(inputs, cancellationToken);
        if (result is null)
            return null;

        var content = request.Format.ToLowerInvariant() switch
        {
            "json" => ShoppingListExporter.ExportAsJson(result),
            "teamcraft" => ShoppingListExporter.ExportAsTeamcraft(result),
            _ => ShoppingListExporter.ExportAsText(result)
        };

        return new ExportResultDto(request.Format.ToLowerInvariant(), content);
    }
}
