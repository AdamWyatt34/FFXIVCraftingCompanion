using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Rotations.Commands;

public sealed record DeleteRotationCommand(
    string UserId,
    int RotationId) : IRequest<bool>;

public sealed class DeleteRotationCommandHandler
    : IRequestHandler<DeleteRotationCommand, bool>
{
    private readonly ISharedRotationRepository _rotationRepo;

    public DeleteRotationCommandHandler(ISharedRotationRepository rotationRepo)
    {
        _rotationRepo = rotationRepo;
    }

    public async Task<bool> Handle(DeleteRotationCommand request, CancellationToken cancellationToken)
    {
        var rotation = await _rotationRepo.GetByIdAsync(request.RotationId, cancellationToken);
        if (rotation is null || rotation.UserId != request.UserId)
            return false;

        await _rotationRepo.DeleteAsync(rotation, cancellationToken);
        return true;
    }
}
