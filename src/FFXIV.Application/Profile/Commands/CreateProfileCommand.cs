using FFXIV.Application.Profile.DTOs;
using MediatR;

namespace FFXIV.Application.Profile.Commands;

public sealed record CreateProfileCommand(
    string UserId,
    string CharacterName,
    string? ServerName,
    string? DataCenter,
    string ActiveJob,
    int ActiveJobLevel,
    Dictionary<string, int>? JobLevels,
    int Craftsmanship,
    int Control,
    int CP,
    int Gathering,
    int Perception,
    int GP) : IRequest<CharacterProfileDto?>;
