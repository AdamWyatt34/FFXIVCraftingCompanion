using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class CraftPlanRepository : ICraftPlanRepository
{
    private readonly AppDbContext _db;

    public CraftPlanRepository(AppDbContext db) => _db = db;

    public async Task<CraftPlan?> GetByIdAsync(
        string id, CancellationToken cancellationToken = default)
    {
        return await _db.CraftPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(cp => cp.Id == id, cancellationToken);
    }

    public async Task<CraftPlan?> GetByIdForUserAsync(
        string id, string userId, CancellationToken cancellationToken = default)
    {
        return await _db.CraftPlans
            .FirstOrDefaultAsync(cp => cp.Id == id && cp.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<CraftPlan>> GetByUserAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        return await _db.CraftPlans
            .AsNoTracking()
            .Where(cp => cp.UserId == userId)
            .OrderByDescending(cp => cp.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<CraftPlan> CreateAsync(
        CraftPlan craftPlan, CancellationToken cancellationToken = default)
    {
        _db.CraftPlans.Add(craftPlan);
        await _db.SaveChangesAsync(cancellationToken);
        return craftPlan;
    }

    public async Task<CraftPlan> UpdateAsync(
        CraftPlan craftPlan, CancellationToken cancellationToken = default)
    {
        _db.CraftPlans.Update(craftPlan);
        await _db.SaveChangesAsync(cancellationToken);
        return craftPlan;
    }

    public async Task DeleteAsync(
        CraftPlan craftPlan, CancellationToken cancellationToken = default)
    {
        _db.CraftPlans.Remove(craftPlan);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetCountByUserAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        return await _db.CraftPlans.CountAsync(cp => cp.UserId == userId, cancellationToken);
    }
}
