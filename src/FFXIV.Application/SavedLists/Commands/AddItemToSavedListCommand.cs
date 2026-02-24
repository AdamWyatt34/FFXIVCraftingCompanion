using FFXIV.Application.SavedLists.DTOs;
using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed record AddItemToSavedListCommand(
    string UserId,
    int ListId,
    int ItemId,
    string ItemName,
    int Quantity,
    string? Category) : IRequest<SavedListDto?>;
