using MediatR;

namespace FFXIV.Application.Profile.Commands;

public sealed record DeleteProfileCommand(string UserId) : IRequest<bool>;
