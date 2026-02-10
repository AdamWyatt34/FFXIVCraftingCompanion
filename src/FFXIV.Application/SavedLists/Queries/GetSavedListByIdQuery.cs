using FFXIV.Application.SavedLists.DTOs;
using MediatR;

namespace FFXIV.Application.SavedLists.Queries;

public sealed record GetSavedListByIdQuery(
    string UserId,
    int ListId) : IRequest<SavedListDto?>;
