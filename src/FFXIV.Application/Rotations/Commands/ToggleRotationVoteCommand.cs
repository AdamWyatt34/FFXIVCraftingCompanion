using FFXIV.Application.Rotations.DTOs;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Rotations.Commands;

public sealed record ToggleRotationVoteCommand(
    string UserId,
    int RotationId) : IRequest<UpvoteResultDto?>;

public sealed class ToggleRotationVoteCommandHandler
    : IRequestHandler<ToggleRotationVoteCommand, UpvoteResultDto?>
{
    private readonly ISharedRotationRepository _rotationRepo;

    public ToggleRotationVoteCommandHandler(ISharedRotationRepository rotationRepo)
    {
        _rotationRepo = rotationRepo;
    }

    public async Task<UpvoteResultDto?> Handle(
        ToggleRotationVoteCommand request, CancellationToken cancellationToken)
    {
        var rotation = await _rotationRepo.GetByIdAsync(request.RotationId, cancellationToken);
        if (rotation is null)
            return null;

        var hasVoted = await _rotationRepo.HasVotedAsync(
            request.UserId, request.RotationId, cancellationToken);

        if (hasVoted)
        {
            await _rotationRepo.RemoveVoteAsync(request.UserId, request.RotationId, cancellationToken);
            await _rotationRepo.IncrementUpvotesAsync(request.RotationId, -1, cancellationToken);
            return new UpvoteResultDto(Upvoted: false, Upvotes: Math.Max(0, rotation.Upvotes - 1));
        }

        var vote = new RotationVote
        {
            UserId = request.UserId,
            RotationId = request.RotationId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _rotationRepo.AddVoteAsync(vote, cancellationToken);
        await _rotationRepo.IncrementUpvotesAsync(request.RotationId, 1, cancellationToken);
        return new UpvoteResultDto(Upvoted: true, Upvotes: rotation.Upvotes + 1);
    }
}
