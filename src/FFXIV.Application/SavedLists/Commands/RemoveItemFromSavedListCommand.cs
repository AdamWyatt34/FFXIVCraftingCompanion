using FFXIV.Application.SavedLists.DTOs;
using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed record RemoveItemFromSavedListCommand(
    string UserId,
    int ListId,
    int ItemId) : IRequest<SavedListDto?>;
