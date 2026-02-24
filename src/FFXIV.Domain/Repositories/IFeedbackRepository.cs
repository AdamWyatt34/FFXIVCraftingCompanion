using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface IFeedbackRepository
{
    Task AddAsync(Feedback feedback, CancellationToken cancellationToken = default);
}
