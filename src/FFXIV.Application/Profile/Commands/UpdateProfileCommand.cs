using FFXIV.Application.Profile.DTOs;
using MediatR;

namespace FFXIV.Application.Profile.Commands;

public sealed record UpdateProfileCommand(
    string UserId,
    string? CharacterName,
    string? ServerName,
    string? DataCenter,
    string? LodestoneId,
    string? AvatarUrl,
    string? ActiveJob,
    int? ActiveJobLevel,
    int? Craftsmanship,
    int? Control,
    int? CP,
    int? Gathering,
    int? Perception,
    int? GP) : IRequest<CharacterProfileDto?>;
