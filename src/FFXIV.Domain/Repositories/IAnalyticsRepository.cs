using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface IAnalyticsRepository
{
    Task AddEventAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken = default);
}
