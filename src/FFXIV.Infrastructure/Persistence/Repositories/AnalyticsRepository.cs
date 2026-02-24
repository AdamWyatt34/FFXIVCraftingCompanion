using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class AnalyticsRepository : IAnalyticsRepository
{
    private readonly AppDbContext _db;

    public AnalyticsRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddEventAsync(AnalyticsEvent analyticsEvent, CancellationToken cancellationToken = default)
    {
        _db.AnalyticsEvents.Add(analyticsEvent);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
