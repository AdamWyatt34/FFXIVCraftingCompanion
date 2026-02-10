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
            gearSlots = [];
            foreach (var (slotKey, equipmentId) in profile.CurrentGear)
            {
                var equipment = await _equipmentRepo.GetByIdAsync(equipmentId, cancellationToken);
                if (equipment is not null)
                {
                    gearSlots.Add(ProfileMapper.ToGearSlotDto(slotKey, equipment));
                }
            }
        }

        return ProfileMapper.ToDto(profile, gearSlots);
    }
}
