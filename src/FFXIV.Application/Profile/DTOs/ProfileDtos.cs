using System.ComponentModel.DataAnnotations;

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

/// <summary>
/// Manual profile creation. Lodestone-sourced fields (the Lodestone id and avatar) are set only by
/// the import flow, so they are not accepted here. Lengths mirror the CharacterProfiles columns.
/// </summary>
public sealed class CreateProfileRequest
{
    [Required(AllowEmptyStrings = false, ErrorMessage = "Character name is required.")]
    [MaxLength(128, ErrorMessage = "Character name must be 128 characters or fewer.")]
    public string CharacterName { get; set; } = string.Empty;

    [MaxLength(64, ErrorMessage = "Server name must be 64 characters or fewer.")]
    public string? ServerName { get; set; }

    [MaxLength(64, ErrorMessage = "Data center must be 64 characters or fewer.")]
    public string? DataCenter { get; set; }

    [Required(AllowEmptyStrings = false, ErrorMessage = "Active job is required.")]
    [MaxLength(8, ErrorMessage = "Active job must be a job abbreviation.")]
    public string ActiveJob { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "Active job level must be between 1 and 100.")]
    public int ActiveJobLevel { get; set; }

    public Dictionary<string, int>? JobLevels { get; set; }

    [Range(0, ProfileLimits.MaxStat, ErrorMessage = "Stats must be between 0 and 99999.")]
    public int Craftsmanship { get; set; }

    [Range(0, ProfileLimits.MaxStat, ErrorMessage = "Stats must be between 0 and 99999.")]
    public int Control { get; set; }

    [Range(0, ProfileLimits.MaxStat, ErrorMessage = "Stats must be between 0 and 99999.")]
    public int CP { get; set; }

    [Range(0, ProfileLimits.MaxStat, ErrorMessage = "Stats must be between 0 and 99999.")]
    public int Gathering { get; set; }

    [Range(0, ProfileLimits.MaxStat, ErrorMessage = "Stats must be between 0 and 99999.")]
    public int Perception { get; set; }

    [Range(0, ProfileLimits.MaxStat, ErrorMessage = "Stats must be between 0 and 99999.")]
    public int GP { get; set; }
}

public sealed class UpdateProfileRequest
{
    [MaxLength(128, ErrorMessage = "Character name must be 128 characters or fewer.")]
    public string? CharacterName { get; set; }

    [MaxLength(64, ErrorMessage = "Server name must be 64 characters or fewer.")]
    public string? ServerName { get; set; }

    [MaxLength(64, ErrorMessage = "Data center must be 64 characters or fewer.")]
    public string? DataCenter { get; set; }

    [MaxLength(8, ErrorMessage = "Active job must be a job abbreviation.")]
    public string? ActiveJob { get; set; }

    [Range(1, 100, ErrorMessage = "Active job level must be between 1 and 100.")]
    public int? ActiveJobLevel { get; set; }

    [Range(0, ProfileLimits.MaxStat, ErrorMessage = "Stats must be between 0 and 99999.")]
    public int? Craftsmanship { get; set; }

    [Range(0, ProfileLimits.MaxStat, ErrorMessage = "Stats must be between 0 and 99999.")]
    public int? Control { get; set; }

    [Range(0, ProfileLimits.MaxStat, ErrorMessage = "Stats must be between 0 and 99999.")]
    public int? CP { get; set; }

    [Range(0, ProfileLimits.MaxStat, ErrorMessage = "Stats must be between 0 and 99999.")]
    public int? Gathering { get; set; }

    [Range(0, ProfileLimits.MaxStat, ErrorMessage = "Stats must be between 0 and 99999.")]
    public int? Perception { get; set; }

    [Range(0, ProfileLimits.MaxStat, ErrorMessage = "Stats must be between 0 and 99999.")]
    public int? GP { get; set; }
}

/// <summary>Bounds shared by the profile request DTOs.</summary>
public static class ProfileLimits
{
    /// <summary>Well above any stat the game can produce, low enough to reject nonsense.</summary>
    public const int MaxStat = 99_999;

    /// <summary>A Lodestone character name: forename, space, surname, each up to 15 letters.</summary>
    public const int MaxCharacterNameLength = 32;
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
    [Required(AllowEmptyStrings = false, ErrorMessage = "Character name is required.")]
    [MaxLength(ProfileLimits.MaxCharacterNameLength, ErrorMessage = "Character name must be 32 characters or fewer.")]
    public string CharacterName { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = "Server name is required.")]
    [MaxLength(64, ErrorMessage = "Server name must be 64 characters or fewer.")]
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
