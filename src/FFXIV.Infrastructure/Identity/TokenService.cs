using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FFXIV.Application.Auth;
using FFXIV.Domain.Entities;
using FFXIV.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FFXIV.Infrastructure.Identity;

/// <summary>
/// Issues JWT access tokens and rotating refresh tokens. Refresh tokens are stored hashed
/// (<see cref="RefreshTokenHasher"/>); a refresh deletes the presented token and issues a new
/// one, and a logout marks the token revoked. Detecting reuse of a rotated-out token (to revoke
/// a whole token family) needs a revocation timestamp column and is left for a schema change.
/// </summary>
public class TokenService : ITokenService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _jwtOptions;
    private readonly TimeProvider _clock;

    public TokenService(AppDbContext db, IOptions<JwtOptions> jwtOptions, TimeProvider clock)
    {
        _db = db;
        _jwtOptions = jwtOptions.Value;
        _clock = clock;
    }

    public async Task<AuthTokens> GenerateTokensAsync(
        string userId, string email, string? displayName, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var expiresAt = now.AddMinutes(_jwtOptions.AccessTokenLifetimeMinutes);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Email, email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (!string.IsNullOrWhiteSpace(displayName))
            claims.Add(new Claim("display_name", displayName));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        // The client receives the random value; only its digest is persisted.
        var refreshTokenValue = GenerateRefreshToken();
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = userId,
            Token = RefreshTokenHasher.Hash(refreshTokenValue),
            ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenLifetimeDays),
            CreatedAt = now
        });
        await _db.SaveChangesAsync(ct);

        return new AuthTokens(accessToken, refreshTokenValue, expiresAt);
    }

    public async Task<AuthTokens> RefreshTokensAsync(string refreshToken, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var hash = RefreshTokenHasher.Hash(refreshToken);
        var storedToken = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == hash, ct);

        if (storedToken is null || storedToken.IsRevoked || storedToken.ExpiresAt <= now)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // Rotation: the presented token is spent and removed before its replacement is issued.
        _db.RefreshTokens.Remove(storedToken);

        var user = await _db.Users.FindAsync([storedToken.UserId], ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        var appUser = (ApplicationUser)user;
        await PruneAsync(storedToken.UserId, now, ct);
        await _db.SaveChangesAsync(ct);

        return await GenerateTokensAsync(appUser.Id, appUser.Email ?? string.Empty, appUser.DisplayName, ct);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = RefreshTokenHasher.Hash(refreshToken);
        var storedToken = await _db.RefreshTokens.FirstOrDefaultAsync(rt => rt.Token == hash, ct);

        if (storedToken is not null && !storedToken.IsRevoked)
        {
            storedToken.IsRevoked = true;
            await _db.SaveChangesAsync(ct);
        }
    }

    /// <summary>
    /// Drops the user's expired and logged-out tokens. A user holds a handful of rows at most,
    /// so the filter runs in memory rather than as a translated DateTimeOffset comparison.
    /// </summary>
    private async Task PruneAsync(string userId, DateTimeOffset now, CancellationToken ct)
    {
        var tokens = await _db.RefreshTokens.Where(rt => rt.UserId == userId).ToListAsync(ct);
        var stale = tokens.Where(rt => rt.ExpiresAt <= now || rt.IsRevoked).ToList();

        if (stale.Count > 0)
            _db.RefreshTokens.RemoveRange(stale);
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
