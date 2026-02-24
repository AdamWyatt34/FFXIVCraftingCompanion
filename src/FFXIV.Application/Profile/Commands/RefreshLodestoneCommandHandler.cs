using FFXIV.Application.Profile.DTOs;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFXIV.Application.Profile.Commands;

public sealed class RefreshLodestoneCommandHandler
    : IRequestHandler<RefreshLodestoneCommand, ImportLodestoneResult>
{
    private readonly ICharacterProfileRepository _profileRepo;
    private readonly IEquipmentRepository _equipmentRepo;
    private readonly ILodestoneService _lodestoneService;
    private readonly ILogger<RefreshLodestoneCommandHandler> _logger;

    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(15);

    public RefreshLodestoneCommandHandler(
        ICharacterProfileRepository profileRepo,
        IEquipmentRepository equipmentRepo,
        ILodestoneService lodestoneService,
        ILogger<RefreshLodestoneCommandHandler> logger)
    {
        _profileRepo = profileRepo;
        _equipmentRepo = equipmentRepo;
        _lodestoneService = lodestoneService;
        _logger = logger;
    }

    public async Task<ImportLodestoneResult> Handle(
        RefreshLodestoneCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profileRepo.GetByUserIdAsync(request.UserId, cancellationToken);
        if (profile is null)
            return new ImportLodestoneResult(null, false, "No profile found.", 404);

        if (string.IsNullOrEmpty(profile.LodestoneId))
            return new ImportLodestoneResult(null, false,
                "No Lodestone ID linked. Use import first.", 400);

        // Rate limit check
        if (profile.LastLodestoneSync is not null)
        {
            var elapsed = DateTimeOffset.UtcNow - profile.LastLodestoneSync.Value;
            if (elapsed < RateLimitWindow)
            {
                var waitMinutes = (int)Math.Ceiling((RateLimitWindow - elapsed).TotalMinutes);
                return new ImportLodestoneResult(null, false,
                    $"Please wait {waitMinutes} minute(s) before refreshing again.", 429);
            }
        }

        var lodestoneData = await _lodestoneService.FetchCharacterByIdAsync(
            profile.LodestoneId, cancellationToken);

        if (lodestoneData is null)
            return new ImportLodestoneResult(null, false,
                "Lodestone is currently unavailable.", 503);

        // Match gear
        var gearDict = new Dictionary<string, int>();
        var gearSlots = new List<ProfileGearSlotDto>();

        if (lodestoneData.EquippedGear.Count > 0)
        {
            var itemNames = lodestoneData.EquippedGear.Select(g => g.ItemName).Distinct().ToList();
            var matchedEquipment = await _equipmentRepo.GetByNamesAsync(itemNames, cancellationToken);
            var equipmentByName = matchedEquipment
                .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            foreach (var gearPiece in lodestoneData.EquippedGear)
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
        }

        // Update profile
        profile.CharacterName = lodestoneData.CharacterName;
        profile.ServerName = lodestoneData.ServerName;
        profile.DataCenter = lodestoneData.DataCenter ?? FfxivServers.GetDataCenter(lodestoneData.ServerName);
        profile.AvatarUrl = lodestoneData.AvatarUrl;

        if (lodestoneData.ActiveJobAbbreviation is not null)
        {
            profile.ActiveJob = lodestoneData.ActiveJobAbbreviation;
            profile.ActiveJobLevel = lodestoneData.ActiveJobLevel;
        }

        // Merge job levels — create new dict to trigger EF ValueComparer change detection
        var mergedLevels = new Dictionary<string, int>(profile.JobLevels);
        foreach (var (job, level) in lodestoneData.JobLevels)
            mergedLevels[job] = level;
        profile.JobLevels = mergedLevels;

        if (gearDict.Count > 0)
            profile.CurrentGear = new Dictionary<string, int>(gearDict);

        if (lodestoneData.Attributes is not null)
        {
            profile.Craftsmanship = lodestoneData.Attributes.Craftsmanship;
            profile.Control = lodestoneData.Attributes.Control;
            profile.CP = lodestoneData.Attributes.CP;
            profile.Gathering = lodestoneData.Attributes.Gathering;
            profile.Perception = lodestoneData.Attributes.Perception;
            profile.GP = lodestoneData.Attributes.GP;
        }

        profile.Source = ProfileSource.Lodestone;
        profile.UpdatedAt = DateTimeOffset.UtcNow;
        profile.LastLodestoneSync = DateTimeOffset.UtcNow;

        await _profileRepo.UpdateAsync(profile, cancellationToken);

        _logger.LogInformation("Refreshed profile from Lodestone for character {Name}",
            lodestoneData.CharacterName);

        return new ImportLodestoneResult(ProfileMapper.ToDto(profile, gearSlots), true, null, 200);
    }
}
