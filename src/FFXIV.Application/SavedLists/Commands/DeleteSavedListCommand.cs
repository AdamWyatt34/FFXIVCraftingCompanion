using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed record DeleteSavedListCommand(string UserId, int ListId) : IRequest<bool>;
