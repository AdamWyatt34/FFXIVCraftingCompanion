using FFXIV.Domain.Enums;

namespace FFXIV.Domain.Entities;

public class CharacterProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;

    // Character identity
    public string CharacterName { get; set; } = string.Empty;
    public string? ServerName { get; set; }
    public string? DataCenter { get; set; }
    public string? LodestoneId { get; set; }
    public string? AvatarUrl { get; set; }

    // Active job context
    public string ActiveJob { get; set; } = string.Empty;
    public int ActiveJobLevel { get; set; }

    // All job levels (stored as jsonb)
    public Dictionary<string, int> JobLevels { get; set; } = new();

    // Current stats (for the active job's gear)
    public int Craftsmanship { get; set; }
    public int Control { get; set; }
    public int CP { get; set; }
    public int Gathering { get; set; }
    public int Perception { get; set; }
    public int GP { get; set; }

    // Current gear — maps slot key to equipment ID (stored as jsonb)
    public Dictionary<string, int>? CurrentGear { get; set; }

    // Metadata
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? LastLodestoneSync { get; set; }
    public ProfileSource Source { get; set; } = ProfileSource.Manual;
}
