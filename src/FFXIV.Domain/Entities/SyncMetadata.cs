namespace FFXIV.Domain.Entities;

public class SyncMetadata
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public DateTimeOffset LastSyncedAt { get; set; }
    public string? LastPatchVersion { get; set; }
    public int RecordCount { get; set; }
}
