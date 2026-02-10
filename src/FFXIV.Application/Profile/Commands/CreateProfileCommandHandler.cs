using FFXIV.Application.Profile.DTOs;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Profile.Commands;

public sealed class CreateProfileCommandHandler
    : IRequestHandler<CreateProfileCommand, CharacterProfileDto?>
{
    private readonly ICharacterProfileRepository _profileRepo;

    public CreateProfileCommandHandler(ICharacterProfileRepository profileRepo)
    {
        _profileRepo = profileRepo;
    }

    public async Task<CharacterProfileDto?> Handle(
        CreateProfileCommand request, CancellationToken cancellationToken)
    {
        if (await _profileRepo.ExistsForUserAsync(request.UserId, cancellationToken))
            return null;

        var now = DateTimeOffset.UtcNow;
        var profile = new CharacterProfile
        {
            UserId = request.UserId,
            CharacterName = request.CharacterName,
            ServerName = request.ServerName,
            DataCenter = request.DataCenter,
            LodestoneId = request.LodestoneId,
            AvatarUrl = request.AvatarUrl,
            ActiveJob = request.ActiveJob,
            ActiveJobLevel = request.ActiveJobLevel,
            JobLevels = request.JobLevels ?? new Dictionary<string, int>(),
            Craftsmanship = request.Craftsmanship,
            Control = request.Control,
            CP = request.CP,
            Gathering = request.Gathering,
            Perception = request.Perception,
            GP = request.GP,
            Source = ProfileSource.Manual,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _profileRepo.CreateAsync(profile, cancellationToken);
        return ProfileMapper.ToDto(profile);
    }
}
