using FFXIV.Application.SavedLists.DTOs;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.SavedLists.Queries;

public sealed class GetSavedListsQueryHandler
    : IRequestHandler<GetSavedListsQuery, IReadOnlyList<SavedListSummaryDto>>
{
    private readonly ISavedListRepository _savedListRepo;

    public GetSavedListsQueryHandler(ISavedListRepository savedListRepo)
    {
        _savedListRepo = savedListRepo;
    }

    public async Task<IReadOnlyList<SavedListSummaryDto>> Handle(
        GetSavedListsQuery request, CancellationToken cancellationToken)
    {
        SavedListType? typeFilter = null;
        if (request.Type is not null && Enum.TryParse<SavedListType>(request.Type, ignoreCase: true, out var parsed))
            typeFilter = parsed;

        var lists = await _savedListRepo.GetByUserAsync(request.UserId, typeFilter, cancellationToken);
        return lists.Select(SavedListMapper.ToSummaryDto).ToList();
    }
}
