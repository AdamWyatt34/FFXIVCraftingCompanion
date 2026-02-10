using FFXIV.Application.Profile.DTOs;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Entities;

namespace FFXIV.Application.Profile;

public static class ProfileMapper
{
    private static readonly Dictionary<string, string> SlotDisplayNames = new()
    {
        ["MainHand"] = "Main Hand",
        ["OffHand"] = "Off Hand",
        ["Ring1"] = "Ring 1",
        ["Ring2"] = "Ring 2"
    };

    public static CharacterProfileDto ToDto(
        CharacterProfile profile,
        List<ProfileGearSlotDto>? gearSlots = null)
    {
        return new CharacterProfileDto(
            Id: profile.Id,
            CharacterName: profile.CharacterName,
            ServerName: profile.ServerName,
            DataCenter: profile.DataCenter,
            LodestoneId: profile.LodestoneId,
            AvatarUrl: profile.AvatarUrl,
            ActiveJob: profile.ActiveJob,
            ActiveJobDisplayName: JobClassification.GetJobDisplayName(profile.ActiveJob),
            ActiveJobLevel: profile.ActiveJobLevel,
            JobLevels: ToJobLevelDtos(profile.JobLevels),
            Stats: new ProfileStatsDto(
                profile.Craftsmanship,
                profile.Control,
                profile.CP,
                profile.Gathering,
                profile.Perception,
                profile.GP,
                IsFromGear: profile.CurrentGear is { Count: > 0 }),
            CurrentGear: gearSlots,
            Source: profile.Source.ToString(),
            CreatedAt: profile.CreatedAt,
            UpdatedAt: profile.UpdatedAt,
            LastLodestoneSync: profile.LastLodestoneSync);
    }

    public static ProfileGearSlotDto ToGearSlotDto(string slotKey, Equipment equipment)
    {
        return new ProfileGearSlotDto(
            SlotKey: slotKey,
            SlotName: GetSlotDisplayName(slotKey),
            EquipmentId: equipment.EquipmentId,
            EquipmentName: equipment.Name,
            ItemLevel: equipment.ItemLevel,
            IconUrl: equipment.IconPath);
    }

    public static Dictionary<string, JobLevelDto> ToJobLevelDtos(Dictionary<string, int> jobLevels)
    {
        return jobLevels.ToDictionary(
            kvp => kvp.Key,
            kvp => new JobLevelDto(
                Job: kvp.Key,
                JobDisplayName: JobClassification.GetJobDisplayName(kvp.Key),
                Level: kvp.Value));
    }

    private static string GetSlotDisplayName(string slotKey)
    {
        return SlotDisplayNames.GetValueOrDefault(slotKey, slotKey);
    }
}
