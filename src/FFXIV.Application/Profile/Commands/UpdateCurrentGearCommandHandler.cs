using FFXIV.Application.Profile.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Profile.Commands;

public sealed class UpdateCurrentGearCommandHandler
    : IRequestHandler<UpdateCurrentGearCommand, CharacterProfileDto?>
{
    private readonly ICharacterProfileRepository _profileRepo;
    private readonly IEquipmentRepository _equipmentRepo;

    public UpdateCurrentGearCommandHandler(
        ICharacterProfileRepository profileRepo,
        IEquipmentRepository equipmentRepo)
    {
        _profileRepo = profileRepo;
        _equipmentRepo = equipmentRepo;
    }

    public async Task<CharacterProfileDto?> Handle(
        UpdateCurrentGearCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profileRepo.GetByUserIdAsync(request.UserId, cancellationToken);
        if (profile is null)
            return null;

        profile.CurrentGear = request.Slots;

        // Look up equipment and recalculate stats
        int craftsmanship = 0, control = 0, cp = 0;
        int gathering = 0, perception = 0, gp = 0;
        var gearSlots = new List<ProfileGearSlotDto>();

        var equipmentById = (await _equipmentRepo.GetByIdsAsync(request.Slots.Values, cancellationToken))
            .ToDictionary(e => e.EquipmentId);

        foreach (var (slotKey, equipmentId) in request.Slots)
        {
            if (!equipmentById.TryGetValue(equipmentId, out var equipment))
                continue;

            craftsmanship += equipment.Craftsmanship;
            control += equipment.Control;
            cp += equipment.CP;
            gathering += equipment.Gathering;
            perception += equipment.Perception;
            gp += equipment.GP;

            gearSlots.Add(ProfileMapper.ToGearSlotDto(slotKey, equipment));
        }

        profile.Craftsmanship = craftsmanship;
        profile.Control = control;
        profile.CP = cp;
        profile.Gathering = gathering;
        profile.Perception = perception;
        profile.GP = gp;
        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await _profileRepo.UpdateAsync(profile, cancellationToken);
        return ProfileMapper.ToDto(profile, gearSlots);
    }
}
