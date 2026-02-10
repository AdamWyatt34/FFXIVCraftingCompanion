using FFXIV.Application.Profile.DTOs;
using MediatR;

namespace FFXIV.Application.Profile.Commands;

public sealed record UpdateCurrentGearCommand(
    string UserId,
    Dictionary<string, int> Slots) : IRequest<CharacterProfileDto?>;
