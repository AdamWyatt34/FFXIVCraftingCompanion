using FFXIV.Domain.Models;

namespace FFXIV.Application.Market;

public interface ICostEstimationService
{
    Task<CostEstimate> EstimateCostAsync(
        List<AggregatedMaterial> materials,
        int? finalItemId,
        string worldOrDc,
        CostEstimationOptions options,
        CancellationToken ct = default);
}
