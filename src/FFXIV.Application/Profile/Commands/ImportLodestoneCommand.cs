using FFXIV.Application.Profile.DTOs;
using MediatR;

namespace FFXIV.Application.Profile.Commands;

public sealed record ImportLodestoneCommand(
    string UserId,
    string CharacterName,
    string ServerName) : IRequest<ImportLodestoneResult>;
