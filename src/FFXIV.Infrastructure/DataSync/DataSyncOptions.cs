namespace FFXIV.Infrastructure.DataSync;

public sealed class DataSyncOptions
{
    public const string SectionName = "DataSync";

    public bool EnableAutoSync { get; set; } = true;
    public string SyncScheduleCron { get; set; } = "0 4 * * *";
    public bool FullSyncOnStartup { get; set; } = true;
    public int BatchSize { get; set; } = 100;
    public int XivApiRequestsPerSecond { get; set; } = 15;
    public bool SyncSources { get; set; } = true;
    public bool SyncGathering { get; set; } = true;
    public bool SyncVendors { get; set; } = true;
    public bool SyncScrips { get; set; } = true;
    public bool SyncEquipment { get; set; } = true;
    public bool EnrichFromGarlandTools { get; set; } = false;
}
