using FFXIV.Application.Profile.DTOs;
using MediatR;

namespace FFXIV.Application.Profile.Queries;

public sealed record GetProfileQuery(string UserId) : IRequest<CharacterProfileDto?>;
