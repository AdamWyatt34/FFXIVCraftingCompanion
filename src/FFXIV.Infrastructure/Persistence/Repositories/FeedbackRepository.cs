using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class FeedbackRepository : IFeedbackRepository
{
    private readonly AppDbContext _db;

    public FeedbackRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddAsync(Feedback feedback, CancellationToken cancellationToken = default)
    {
        _db.Feedbacks.Add(feedback);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
