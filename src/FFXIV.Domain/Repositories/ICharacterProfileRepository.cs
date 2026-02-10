using FFXIV.Domain.Entities;

namespace FFXIV.Domain.Repositories;

public interface ICharacterProfileRepository
{
    Task<CharacterProfile?> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<CharacterProfile?> GetByLodestoneIdAsync(string lodestoneId, CancellationToken cancellationToken = default);
    Task<CharacterProfile> CreateAsync(CharacterProfile profile, CancellationToken cancellationToken = default);
    Task<CharacterProfile> UpdateAsync(CharacterProfile profile, CancellationToken cancellationToken = default);
    Task DeleteAsync(CharacterProfile profile, CancellationToken cancellationToken = default);
    Task<bool> ExistsForUserAsync(string userId, CancellationToken cancellationToken = default);
}
