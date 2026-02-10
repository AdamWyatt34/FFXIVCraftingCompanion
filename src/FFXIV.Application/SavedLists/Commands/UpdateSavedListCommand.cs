using FFXIV.Application.SavedLists.DTOs;
using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed record UpdateSavedListCommand(
    string UserId,
    int ListId,
    string? Name,
    bool? IsPinned) : IRequest<SavedListDto?>;
