using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class EquipmentRepository : IEquipmentRepository
{
    private readonly AppDbContext _db;

    public EquipmentRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<Equipment?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _db.Equipment
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.EquipmentId == id, cancellationToken);
    }

    public async Task<IReadOnlyList<Equipment>> GetByJobAndLevelAsync(
        string job, int level, CancellationToken cancellationToken = default)
    {
        return await _db.Equipment
            .AsNoTracking()
            .Where(e => e.ClassJobCategory.Contains(job) && e.EquipLevel <= level)
            .OrderByDescending(e => e.ItemLevel)
            .ToListAsync(cancellationToken);
    }
}
