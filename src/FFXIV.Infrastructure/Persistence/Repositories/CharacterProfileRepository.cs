using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence.Repositories;

public class CharacterProfileRepository : ICharacterProfileRepository
{
    private readonly AppDbContext _db;

    public CharacterProfileRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<CharacterProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.CharacterProfiles
            .FirstOrDefaultAsync(cp => cp.UserId == userId, cancellationToken);
    }

    public async Task<CharacterProfile?> GetByLodestoneIdAsync(string lodestoneId, CancellationToken cancellationToken = default)
    {
        return await _db.CharacterProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(cp => cp.LodestoneId == lodestoneId, cancellationToken);
    }

    public async Task<CharacterProfile> CreateAsync(CharacterProfile profile, CancellationToken cancellationToken = default)
    {
        _db.CharacterProfiles.Add(profile);
        await _db.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task<CharacterProfile> UpdateAsync(CharacterProfile profile, CancellationToken cancellationToken = default)
    {
        var entry = _db.Entry(profile);
        if (entry.State == EntityState.Detached)
            _db.CharacterProfiles.Update(profile);
        // else: already tracked, change tracker handles it
        await _db.SaveChangesAsync(cancellationToken);
        return profile;
    }

    public async Task DeleteAsync(CharacterProfile profile, CancellationToken cancellationToken = default)
    {
        _db.CharacterProfiles.Remove(profile);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _db.CharacterProfiles
            .AnyAsync(cp => cp.UserId == userId, cancellationToken);
    }
}
