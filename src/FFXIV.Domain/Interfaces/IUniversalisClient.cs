using FFXIV.Domain.Models;

namespace FFXIV.Domain.Interfaces;

public interface IUniversalisClient
{
    Task<MarketBoardData?> GetItemPricesAsync(string worldOrDc, int itemId, CancellationToken ct = default);
    Task<Dictionary<int, MarketBoardData>> GetItemPricesBatchAsync(string worldOrDc, IEnumerable<int> itemIds, CancellationToken ct = default);
}
