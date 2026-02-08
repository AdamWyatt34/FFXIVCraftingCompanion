namespace FFXIV.Domain.Interfaces;

public interface IGarlandToolsClient
{
    Task<GarlandItemData?> GetItemAsync(int itemId, CancellationToken ct = default);
}
