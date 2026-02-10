namespace FFXIV.Application.Profile.DTOs;

// Response DTOs

public sealed record CharacterProfileDto(
    int Id,
    string CharacterName,
    string? ServerName,
    string? DataCenter,
    string? LodestoneId,
    string? AvatarUrl,
    string ActiveJob,
    string ActiveJobDisplayName,
    int ActiveJobLevel,
    Dictionary<string, JobLevelDto> JobLevels,
    ProfileStatsDto Stats,
    List<ProfileGearSlotDto>? CurrentGear,
    string Source,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastLodestoneSync);

public sealed record JobLevelDto(
    string Job,
    string JobDisplayName,
    int Level);

public sealed record ProfileStatsDto(
    int Craftsmanship,
    int Control,
    int CP,
    int Gathering,
    int Perception,
    int GP,
    bool IsFromGear);

public sealed record ProfileGearSlotDto(
    string SlotKey,
    string SlotName,
    int EquipmentId,
    string EquipmentName,
    int ItemLevel,
    string IconUrl);

// Request DTOs

public sealed class CreateProfileRequest
{
    public string CharacterName { get; set; } = string.Empty;
    public string? ServerName { get; set; }
    public string? DataCenter { get; set; }
    public string? LodestoneId { get; set; }
    public string? AvatarUrl { get; set; }
    public string ActiveJob { get; set; } = string.Empty;
    public int ActiveJobLevel { get; set; }
    public Dictionary<string, int>? JobLevels { get; set; }
    public int Craftsmanship { get; set; }
    public int Control { get; set; }
    public int CP { get; set; }
    public int Gathering { get; set; }
    public int Perception { get; set; }
    public int GP { get; set; }
}

public sealed class UpdateProfileRequest
{
    public string? CharacterName { get; set; }
    public string? ServerName { get; set; }
    public string? DataCenter { get; set; }
    public string? LodestoneId { get; set; }
    public string? AvatarUrl { get; set; }
    public string? ActiveJob { get; set; }
    public int? ActiveJobLevel { get; set; }
    public int? Craftsmanship { get; set; }
    public int? Control { get; set; }
    public int? CP { get; set; }
    public int? Gathering { get; set; }
    public int? Perception { get; set; }
    public int? GP { get; set; }
}

public sealed class UpdateJobLevelsRequest
{
    public Dictionary<string, int> JobLevels { get; set; } = new();
}

public sealed class UpdateCurrentGearRequest
{
    public Dictionary<string, int> Slots { get; set; } = new();
}

// Lodestone DTOs

public sealed record ImportLodestoneResult(
    CharacterProfileDto? Profile,
    bool Success,
    string? Error,
    int StatusCode);

public sealed class ImportLodestoneRequest
{
    public string CharacterName { get; set; } = string.Empty;
    public string ServerName { get; set; } = string.Empty;
}

public sealed record LodestonePreviewDto(
    string CharacterName,
    string? AvatarUrl,
    string ServerName,
    string? DataCenter,
    string? ActiveJob,
    string? ActiveJobDisplayName,
    int ActiveJobLevel,
    Dictionary<string, JobLevelDto> JobLevels);
