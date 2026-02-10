using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Profile.Commands;

public sealed class DeleteProfileCommandHandler : IRequestHandler<DeleteProfileCommand, bool>
{
    private readonly ICharacterProfileRepository _profileRepo;

    public DeleteProfileCommandHandler(ICharacterProfileRepository profileRepo)
    {
        _profileRepo = profileRepo;
    }

    public async Task<bool> Handle(DeleteProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profileRepo.GetByUserIdAsync(request.UserId, cancellationToken);
        if (profile is null)
            return false;

        await _profileRepo.DeleteAsync(profile, cancellationToken);
        return true;
    }
}
