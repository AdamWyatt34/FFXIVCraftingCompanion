using FFXIV.Application.Profile.DTOs;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFXIV.Application.Profile.Commands;

public sealed class ImportLodestoneCommandHandler
    : IRequestHandler<ImportLodestoneCommand, ImportLodestoneResult>
{
    private readonly ICharacterProfileRepository _profileRepo;
    private readonly IEquipmentRepository _equipmentRepo;
    private readonly ILodestoneService _lodestoneService;
    private readonly ILogger<ImportLodestoneCommandHandler> _logger;

    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(15);

    public ImportLodestoneCommandHandler(
        ICharacterProfileRepository profileRepo,
        IEquipmentRepository equipmentRepo,
        ILodestoneService lodestoneService,
        ILogger<ImportLodestoneCommandHandler> logger)
    {
        _profileRepo = profileRepo;
        _equipmentRepo = equipmentRepo;
        _lodestoneService = lodestoneService;
        _logger = logger;
    }

    public async Task<ImportLodestoneResult> Handle(
        ImportLodestoneCommand request, CancellationToken cancellationToken)
    {
        if (!FfxivServers.IsValidServer(request.ServerName))
            return new ImportLodestoneResult(null, false, $"'{request.ServerName}' is not a valid FFXIV server.", 400);

        // Check rate limit on existing profile
        var existingProfile = await _profileRepo.GetByUserIdAsync(request.UserId, cancellationToken);
        if (existingProfile?.LastLodestoneSync is not null)
        {
            var elapsed = DateTimeOffset.UtcNow - existingProfile.LastLodestoneSync.Value;
            if (elapsed < RateLimitWindow)
            {
                var waitMinutes = (int)Math.Ceiling((RateLimitWindow - elapsed).TotalMinutes);
                return new ImportLodestoneResult(null, false,
                    $"Please wait {waitMinutes} minute(s) before importing again.", 429);
            }
        }

        // Search and fetch from Lodestone
        var lodestoneData = await _lodestoneService.SearchAndFetchCharacterAsync(
            request.CharacterName, request.ServerName, cancellationToken);

        if (lodestoneData is null)
            return new ImportLodestoneResult(null, false, "Character not found on Lodestone.", 404);

        // Match gear
        var (gearDict, gearSlots) = await MatchGearAsync(lodestoneData, cancellationToken);

        if (existingProfile is not null)
        {
            return await UpdateExistingProfile(existingProfile, lodestoneData, gearDict, gearSlots, cancellationToken);
        }

        return await CreateNewProfile(request.UserId, lodestoneData, gearDict, gearSlots, cancellationToken);
    }

    private async Task<ImportLodestoneResult> CreateNewProfile(
        string userId,
        LodestoneCharacterResult data,
        Dictionary<string, int> gearDict,
        List<ProfileGearSlotDto> gearSlots,
        CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;

        var profile = new CharacterProfile
        {
            UserId = userId,
            CharacterName = data.CharacterName,
            ServerName = data.ServerName,
            DataCenter = data.DataCenter ?? FfxivServers.GetDataCenter(data.ServerName),
            LodestoneId = data.LodestoneId,
            AvatarUrl = data.AvatarUrl,
            ActiveJob = data.ActiveJobAbbreviation ?? "CRP",
            ActiveJobLevel = data.ActiveJobLevel,
            JobLevels = data.JobLevels,
            CurrentGear = gearDict.Count > 0 ? gearDict : null,
            Source = ProfileSource.Lodestone,
            CreatedAt = now,
            UpdatedAt = now,
            LastLodestoneSync = now,
        };

        ApplyStats(profile, data, gearSlots);

        await _profileRepo.CreateAsync(profile, ct);

        _logger.LogInformation("Created profile from Lodestone for user {UserId}, character {Name}",
            userId, data.CharacterName);

        return new ImportLodestoneResult(ProfileMapper.ToDto(profile, gearSlots), true, null, 200);
    }

    private async Task<ImportLodestoneResult> UpdateExistingProfile(
        CharacterProfile profile,
        LodestoneCharacterResult data,
        Dictionary<string, int> gearDict,
        List<ProfileGearSlotDto> gearSlots,
        CancellationToken ct)
    {
        profile.CharacterName = data.CharacterName;
        profile.ServerName = data.ServerName;
        profile.DataCenter = data.DataCenter ?? FfxivServers.GetDataCenter(data.ServerName);
        profile.LodestoneId = data.LodestoneId;
        profile.AvatarUrl = data.AvatarUrl;

        if (data.ActiveJobAbbreviation is not null)
        {
            profile.ActiveJob = data.ActiveJobAbbreviation;
            profile.ActiveJobLevel = data.ActiveJobLevel;
        }

        // Merge job levels — create new dict to trigger EF ValueComparer change detection
        var mergedLevels = new Dictionary<string, int>(profile.JobLevels);
        foreach (var (job, level) in data.JobLevels)
            mergedLevels[job] = level;
        profile.JobLevels = mergedLevels;

        if (gearDict.Count > 0)
            profile.CurrentGear = new Dictionary<string, int>(gearDict);

        ApplyStats(profile, data, gearSlots);

        profile.Source = profile.Source == ProfileSource.Manual
            ? ProfileSource.Lodestone
            : ProfileSource.Mixed;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        profile.LastLodestoneSync = DateTimeOffset.UtcNow;

        await _profileRepo.UpdateAsync(profile, ct);

        _logger.LogInformation("Updated profile from Lodestone for character {Name}", data.CharacterName);

        return new ImportLodestoneResult(ProfileMapper.ToDto(profile, gearSlots), true, null, 200);
    }

    private async Task<(Dictionary<string, int> GearDict, List<ProfileGearSlotDto> GearSlots)> MatchGearAsync(
        LodestoneCharacterResult data, CancellationToken ct)
    {
        var gearDict = new Dictionary<string, int>();
        var gearSlots = new List<ProfileGearSlotDto>();

        if (data.EquippedGear.Count == 0)
            return (gearDict, gearSlots);

        var itemNames = data.EquippedGear.Select(g => g.ItemName).Distinct().ToList();
        var matchedEquipment = await _equipmentRepo.GetByNamesAsync(itemNames, ct);
        var equipmentByName = matchedEquipment
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var gearPiece in data.EquippedGear)
        {
            if (equipmentByName.TryGetValue(gearPiece.ItemName, out var equipment))
            {
                gearDict[gearPiece.SlotName] = equipment.EquipmentId;
                gearSlots.Add(ProfileMapper.ToGearSlotDto(gearPiece.SlotName, equipment));
            }
            else
            {
                _logger.LogWarning("Could not match Lodestone gear '{ItemName}' in slot {Slot}",
                    gearPiece.ItemName, gearPiece.SlotName);
            }
        }

        return (gearDict, gearSlots);
    }

    private static void ApplyStats(
        CharacterProfile profile,
        LodestoneCharacterResult data,
        List<ProfileGearSlotDto> gearSlots)
    {
        if (data.Attributes is not null)
        {
            // Use Lodestone stats directly (reflects materia, food, etc.)
            profile.Craftsmanship = data.Attributes.Craftsmanship;
            profile.Control = data.Attributes.Control;
            profile.CP = data.Attributes.CP;
            profile.Gathering = data.Attributes.Gathering;
            profile.Perception = data.Attributes.Perception;
            profile.GP = data.Attributes.GP;
        }
        // If no attributes from Lodestone and we have gear, stats will be recalculated
        // when the user calls UpdateCurrentGear
    }
}
