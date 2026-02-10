using FFXIV.Application.Profile.DTOs;
using MediatR;

namespace FFXIV.Application.Profile.Commands;

public sealed record RefreshLodestoneCommand(string UserId) : IRequest<ImportLodestoneResult>;
