using FFXIV.Application.Profile.DTOs;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Profile.Commands;

public sealed class UpdateProfileCommandHandler
    : IRequestHandler<UpdateProfileCommand, CharacterProfileDto?>
{
    private readonly ICharacterProfileRepository _profileRepo;

    public UpdateProfileCommandHandler(ICharacterProfileRepository profileRepo)
    {
        _profileRepo = profileRepo;
    }

    public async Task<CharacterProfileDto?> Handle(
        UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profileRepo.GetByUserIdAsync(request.UserId, cancellationToken);
        if (profile is null)
            return null;

        if (request.CharacterName is not null)
            profile.CharacterName = request.CharacterName;

        if (request.ServerName is not null)
            profile.ServerName = request.ServerName;

        if (request.DataCenter is not null)
            profile.DataCenter = request.DataCenter;

        if (request.LodestoneId is not null)
            profile.LodestoneId = request.LodestoneId;

        if (request.AvatarUrl is not null)
            profile.AvatarUrl = request.AvatarUrl;

        if (request.ActiveJob is not null)
            profile.ActiveJob = request.ActiveJob;

        if (request.ActiveJobLevel.HasValue)
            profile.ActiveJobLevel = request.ActiveJobLevel.Value;

        if (request.Craftsmanship.HasValue)
            profile.Craftsmanship = request.Craftsmanship.Value;

        if (request.Control.HasValue)
            profile.Control = request.Control.Value;

        if (request.CP.HasValue)
            profile.CP = request.CP.Value;

        if (request.Gathering.HasValue)
            profile.Gathering = request.Gathering.Value;

        if (request.Perception.HasValue)
            profile.Perception = request.Perception.Value;

        if (request.GP.HasValue)
            profile.GP = request.GP.Value;

        profile.Source = profile.Source == ProfileSource.Lodestone
            ? ProfileSource.Mixed
            : ProfileSource.Manual;

        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await _profileRepo.UpdateAsync(profile, cancellationToken);
        return ProfileMapper.ToDto(profile);
    }
}
