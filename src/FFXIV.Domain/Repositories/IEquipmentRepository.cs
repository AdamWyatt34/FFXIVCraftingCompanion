using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface IEquipmentRepository
{
    Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Equipment>> GetByJobAndLevelAsync(string job, int level, CancellationToken cancellationToken = default);
}
