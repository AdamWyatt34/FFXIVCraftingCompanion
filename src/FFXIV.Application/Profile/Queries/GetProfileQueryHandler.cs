using FFXIV.Application.Profile.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Profile.Queries;

public sealed class GetProfileQueryHandler
    : IRequestHandler<GetProfileQuery, CharacterProfileDto?>
{
    private readonly ICharacterProfileRepository _profileRepo;
    private readonly IEquipmentRepository _equipmentRepo;

    public GetProfileQueryHandler(
        ICharacterProfileRepository profileRepo,
        IEquipmentRepository equipmentRepo)
    {
        _profileRepo = profileRepo;
        _equipmentRepo = equipmentRepo;
    }

    public async Task<CharacterProfileDto?> Handle(
        GetProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await _profileRepo.GetByUserIdAsync(request.UserId, cancellationToken);
        if (profile is null)
            return null;

        List<ProfileGearSlotDto>? gearSlots = null;

        if (profile.CurrentGear is { Count: > 0 })
        {
            var equipmentById = (await _equipmentRepo.GetByIdsAsync(profile.CurrentGear.Values, cancellationToken))
                .ToDictionary(e => e.EquipmentId);

            gearSlots = [];
            foreach (var (slotKey, equipmentId) in profile.CurrentGear)
            {
                if (equipmentById.TryGetValue(equipmentId, out var equipment))
                    gearSlots.Add(ProfileMapper.ToGearSlotDto(slotKey, equipment));
            }
        }

        return ProfileMapper.ToDto(profile, gearSlots);
    }
}
