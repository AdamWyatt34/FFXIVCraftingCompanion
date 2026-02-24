using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFXIV.Application.Analytics.Commands;

public class TrackEventCommandHandler : IRequestHandler<TrackEventCommand>
{
    private readonly IAnalyticsRepository _analyticsRepo;
    private readonly ILogger<TrackEventCommandHandler> _logger;

    public TrackEventCommandHandler(IAnalyticsRepository analyticsRepo, ILogger<TrackEventCommandHandler> logger)
    {
        _analyticsRepo = analyticsRepo;
        _logger = logger;
    }

    public async Task Handle(TrackEventCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var entity = new AnalyticsEvent
            {
                Event = request.Event,
                Metadata = request.Metadata,
                PageUrl = request.PageUrl,
                Referrer = request.Referrer,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await _analyticsRepo.AddEventAsync(entity, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to track analytics event: {Event}", request.Event);
        }
    }
}
