using FFXIV.Domain.Models;

namespace FFXIV.Application.Gathering;

public interface IGatheringGuideService
{
    Task<GatheringGuide> GenerateGuideAsync(List<GatheredMaterialInput> materials, CancellationToken ct = default);
}

public sealed record GatheredMaterialInput(int ItemId, int Quantity);
