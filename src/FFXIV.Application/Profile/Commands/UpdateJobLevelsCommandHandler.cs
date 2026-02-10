using FFXIV.Application.Profile.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Profile.Commands;

public sealed class UpdateJobLevelsCommandHandler
    : IRequestHandler<UpdateJobLevelsCommand, CharacterProfileDto?>
{
    private readonly ICharacterProfileRepository _profileRepo;

    public UpdateJobLevelsCommandHandler(ICharacterProfileRepository profileRepo)
    {
        _profileRepo = profileRepo;
    }

    public async Task<CharacterProfileDto?> Handle(
        UpdateJobLevelsCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profileRepo.GetByUserIdAsync(request.UserId, cancellationToken);
        if (profile is null)
            return null;

        // Merge: update existing keys and add new ones, don't clear unmentioned jobs
        foreach (var (job, level) in request.JobLevels)
        {
            profile.JobLevels[job.ToUpperInvariant()] = level;
        }

        // If the active job level was updated, sync it
        var activeJobKey = profile.ActiveJob.ToUpperInvariant();
        if (profile.JobLevels.TryGetValue(activeJobKey, out var updatedLevel))
        {
            profile.ActiveJobLevel = updatedLevel;
        }

        profile.UpdatedAt = DateTimeOffset.UtcNow;

        await _profileRepo.UpdateAsync(profile, cancellationToken);
        return ProfileMapper.ToDto(profile);
    }
}
