using FFXIV.Application.Profile.DTOs;
using MediatR;

namespace FFXIV.Application.Profile.Commands;

public sealed record UpdateJobLevelsCommand(
    string UserId,
    Dictionary<string, int> JobLevels) : IRequest<CharacterProfileDto?>;
