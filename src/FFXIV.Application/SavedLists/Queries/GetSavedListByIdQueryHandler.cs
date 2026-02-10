using FFXIV.Application.SavedLists.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.SavedLists.Queries;

public sealed class GetSavedListByIdQueryHandler
    : IRequestHandler<GetSavedListByIdQuery, SavedListDto?>
{
    private readonly ISavedListRepository _savedListRepo;

    public GetSavedListByIdQueryHandler(ISavedListRepository savedListRepo)
    {
        _savedListRepo = savedListRepo;
    }

    public async Task<SavedListDto?> Handle(
        GetSavedListByIdQuery request, CancellationToken cancellationToken)
    {
        var savedList = await _savedListRepo.GetByIdAsync(request.ListId, request.UserId, cancellationToken);
        if (savedList is null)
            return null;

        return SavedListMapper.ToDto(savedList);
    }
}
