using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class ConsumableRepository(AppDbContext db) : IConsumableRepository
{
    public async Task<IReadOnlyList<ConsumableBonus>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await db.ConsumableBonuses
            .AsNoTracking()
            .Include(c => c.Item)
            .ToListAsync(cancellationToken);

        // Stat is stored as its name, so SQL would sort it by collation; sorting here keeps the
        // enum's declaration order on every provider.
        return rows.OrderBy(c => c.ItemId).ThenBy(c => c.Stat).ToList();
    }
}
