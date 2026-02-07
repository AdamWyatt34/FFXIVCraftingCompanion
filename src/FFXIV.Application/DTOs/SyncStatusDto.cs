namespace FFXIV.Application.DTOs;

public sealed record SyncStatusDto(
    string EntityType,
    DateTimeOffset? LastSyncedAt,
    string? LastPatchVersion,
    int RecordCount,
    bool IsSyncCurrentlyRunning);
