using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class SavedListRepository : ISavedListRepository
{
    private readonly AppDbContext _db;

    public SavedListRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SavedList>> GetByUserAsync(
        string userId, SavedListType? type = null, CancellationToken cancellationToken = default)
    {
        var query = _db.SavedLists
            .AsNoTracking()
            .Where(sl => sl.UserId == userId);

        if (type.HasValue)
            query = query.Where(sl => sl.Type == type.Value);

        return await query
            .OrderByDescending(sl => sl.IsPinned)
            .ThenByDescending(sl => sl.UpdatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<SavedList?> GetByIdAsync(
        int id, string userId, CancellationToken cancellationToken = default)
    {
        return await _db.SavedLists
            .FirstOrDefaultAsync(sl => sl.Id == id && sl.UserId == userId, cancellationToken);
    }

    public async Task<SavedList> CreateAsync(
        SavedList savedList, CancellationToken cancellationToken = default)
    {
        _db.SavedLists.Add(savedList);
        await _db.SaveChangesAsync(cancellationToken);
        return savedList;
    }

    public async Task<SavedList> UpdateAsync(
        SavedList savedList, CancellationToken cancellationToken = default)
    {
        _db.SavedLists.Update(savedList);
        await _db.SaveChangesAsync(cancellationToken);
        return savedList;
    }

    public async Task DeleteAsync(
        SavedList savedList, CancellationToken cancellationToken = default)
    {
        _db.SavedLists.Remove(savedList);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> GetCountByUserAsync(
        string userId, CancellationToken cancellationToken = default)
    {
        return await _db.SavedLists.CountAsync(sl => sl.UserId == userId, cancellationToken);
    }
}
