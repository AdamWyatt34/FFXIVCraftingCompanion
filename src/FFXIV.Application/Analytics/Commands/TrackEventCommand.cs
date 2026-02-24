using MediatR;

namespace FFXIV.Application.Analytics.Commands;

public record TrackEventCommand(
    string Event,
    string? Metadata,
    string? PageUrl,
    string? Referrer) : IRequest;
