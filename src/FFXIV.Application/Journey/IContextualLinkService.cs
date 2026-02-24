using FFXIV.Domain.Models;

namespace FFXIV.Application.Journey;

public interface IContextualLinkService
{
    Task<ContextualLinks> GetLinksForItemAsync(int itemId, CancellationToken ct = default);
}
