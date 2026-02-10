using FFXIV.Application.SavedLists.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed class UpdateSavedListCommandHandler
    : IRequestHandler<UpdateSavedListCommand, SavedListDto?>
{
    private readonly ISavedListRepository _savedListRepo;

    public UpdateSavedListCommandHandler(ISavedListRepository savedListRepo)
    {
        _savedListRepo = savedListRepo;
    }

    public async Task<SavedListDto?> Handle(
        UpdateSavedListCommand request, CancellationToken cancellationToken)
    {
        var savedList = await _savedListRepo.GetByIdAsync(request.ListId, request.UserId, cancellationToken);
        if (savedList is null)
            return null;

        if (request.Name is not null)
            savedList.Name = request.Name;

        if (request.IsPinned.HasValue)
            savedList.IsPinned = request.IsPinned.Value;

        savedList.UpdatedAt = DateTimeOffset.UtcNow;

        await _savedListRepo.UpdateAsync(savedList, cancellationToken);
        return SavedListMapper.ToDto(savedList);
    }
}
