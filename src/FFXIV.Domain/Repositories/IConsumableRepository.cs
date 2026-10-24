using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface IConsumableRepository
{
    Task<IReadOnlyList<ConsumableBonus>> GetAllAsync(CancellationToken cancellationToken = default);
}
