using FFXIV.Application.Rotations.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Rotations.Queries;

public sealed record GetRotationByIdQuery(int Id) : IRequest<RotationDetailDto?>;

public sealed class GetRotationByIdQueryHandler
    : IRequestHandler<GetRotationByIdQuery, RotationDetailDto?>
{
    private readonly ISharedRotationRepository _rotationRepo;

    public GetRotationByIdQueryHandler(ISharedRotationRepository rotationRepo)
    {
        _rotationRepo = rotationRepo;
    }

    public async Task<RotationDetailDto?> Handle(
        GetRotationByIdQuery request, CancellationToken cancellationToken)
    {
        var rotation = await _rotationRepo.GetByIdAsync(request.Id, cancellationToken);
        if (rotation is null)
            return null;

        var authorName = await _rotationRepo.GetAuthorNameAsync(rotation.UserId, cancellationToken);
        return RotationMapper.ToDetailDto(rotation, authorName);
    }
}
