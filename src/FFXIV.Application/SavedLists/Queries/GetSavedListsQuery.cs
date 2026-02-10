using FFXIV.Application.SavedLists.DTOs;
using MediatR;

namespace FFXIV.Application.SavedLists.Queries;

public sealed record GetSavedListsQuery(
    string UserId,
    string? Type) : IRequest<IReadOnlyList<SavedListSummaryDto>>;
