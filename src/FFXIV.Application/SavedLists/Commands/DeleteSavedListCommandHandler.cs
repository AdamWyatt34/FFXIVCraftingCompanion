using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed class DeleteSavedListCommandHandler
    : IRequestHandler<DeleteSavedListCommand, bool>
{
    private readonly ISavedListRepository _savedListRepo;

    public DeleteSavedListCommandHandler(ISavedListRepository savedListRepo)
    {
        _savedListRepo = savedListRepo;
    }

    public async Task<bool> Handle(
        DeleteSavedListCommand request, CancellationToken cancellationToken)
    {
        var savedList = await _savedListRepo.GetByIdAsync(request.ListId, request.UserId, cancellationToken);
        if (savedList is null)
            return false;

        await _savedListRepo.DeleteAsync(savedList, cancellationToken);
        return true;
    }
}
