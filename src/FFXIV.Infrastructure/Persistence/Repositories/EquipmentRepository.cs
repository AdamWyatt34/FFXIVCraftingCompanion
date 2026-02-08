using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
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

    public async Task<IReadOnlyList<Equipment>> GetBySlotJobAndLevelAsync(
        EquipSlot slot, string job, int level, CancellationToken cancellationToken = default)
    {
        return await _db.Equipment
            .AsNoTracking()
            .Where(e => e.EquipSlot == slot && e.ClassJobCategory.Contains(job) && e.EquipLevel <= level)
            .OrderByDescending(e => e.ItemLevel)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Equipment>> GetBestBySlotAsync(
        EquipSlot slot, string job, int level, int limit = 5, CancellationToken cancellationToken = default)
    {
        return await _db.Equipment
            .AsNoTracking()
            .Where(e => e.EquipSlot == slot && e.ClassJobCategory.Contains(job) && e.EquipLevel <= level)
            .OrderByDescending(e => e.ItemLevel)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Equipment>> SearchEquipmentAsync(
        string query, string? job = null, EquipSlot? slot = null, int? minLevel = null,
        int? maxLevel = null, int limit = 50, CancellationToken cancellationToken = default)
    {
        var q = _db.Equipment.AsNoTracking().AsQueryable();

        q = q.Where(e => EF.Functions.ILike(e.Name, $"%{query}%"));

        if (!string.IsNullOrEmpty(job))
            q = q.Where(e => e.ClassJobCategory.Contains(job));

        if (slot.HasValue)
            q = q.Where(e => e.EquipSlot == slot.Value);

        if (minLevel.HasValue)
            q = q.Where(e => e.EquipLevel >= minLevel.Value);

        if (maxLevel.HasValue)
            q = q.Where(e => e.EquipLevel <= maxLevel.Value);

        return await q
            .OrderByDescending(e => e.ItemLevel)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<HashSet<int>> GetExistingItemIdsAsync(
        IEnumerable<int> itemIds, CancellationToken cancellationToken = default)
    {
        var ids = itemIds.ToList();
        if (ids.Count == 0) return [];

        var existing = await _db.Equipment
            .AsNoTracking()
            .Where(e => ids.Contains(e.EquipmentId))
            .Select(e => e.EquipmentId)
            .ToListAsync(cancellationToken);

        return existing.ToHashSet();
    }

    public async Task<Equipment?> GetByRecipeIdAsync(int recipeId, CancellationToken cancellationToken = default)
    {
        return await _db.Equipment
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.RecipeId == recipeId, cancellationToken);
    }
}
