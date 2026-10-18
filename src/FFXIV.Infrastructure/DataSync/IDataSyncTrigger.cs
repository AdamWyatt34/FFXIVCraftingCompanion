namespace FFXIV.Infrastructure.DataSync;

public interface IDataSyncTrigger
{
    /// <summary>
    /// Starts a full sync in the background. Returns false when one is already running. The run is
    /// tied to the host's lifetime, never to the caller's request.
    /// </summary>
    bool TriggerSync();

    bool IsSyncRunning { get; }
}
