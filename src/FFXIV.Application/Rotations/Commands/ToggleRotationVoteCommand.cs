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
        if (!await _rotationRepo.ExistsAsync(request.RotationId, cancellationToken))
            return null;

        var hasVoted = await _rotationRepo.HasVotedAsync(
            request.UserId, request.RotationId, cancellationToken);

        bool upvoted;
        if (hasVoted)
        {
            await _rotationRepo.RemoveVoteAsync(request.UserId, request.RotationId, cancellationToken);
            upvoted = false;
        }
        else
        {
            // A concurrent duplicate makes AddVoteAsync return false; the vote is on record either way.
            await _rotationRepo.AddVoteAsync(new RotationVote
            {
                UserId = request.UserId,
                RotationId = request.RotationId,
                CreatedAt = DateTimeOffset.UtcNow
            }, cancellationToken);
            upvoted = true;
        }

        // The stored count is derived from the votes table, so the response is what every other
        // reader sees, not an estimate from a stale entity.
        var upvotes = await _rotationRepo.RecountUpvotesAsync(request.RotationId, cancellationToken);
        return new UpvoteResultDto(Upvoted: upvoted, Upvotes: upvotes);
    }
}
