namespace FFXIV.Infrastructure.DataSync;

public interface IDataSyncTrigger
{
    Task TriggerSyncAsync(CancellationToken cancellationToken = default);
    bool IsSyncRunning { get; }
}
