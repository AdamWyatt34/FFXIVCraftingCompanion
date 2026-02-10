using FFXIV.Application.SavedLists.DTOs;
using MediatR;

namespace FFXIV.Application.SavedLists.Commands;

public sealed record CreateGearSetCommand(
    string UserId,
    string Name,
    string Job,
    Dictionary<string, int> Slots) : IRequest<SavedListDto?>;
