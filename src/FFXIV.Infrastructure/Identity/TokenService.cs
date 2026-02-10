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

public class TokenService : ITokenService
{
    private readonly AppDbContext _db;
    private readonly JwtOptions _jwtOptions;

    public TokenService(AppDbContext db, IOptions<JwtOptions> jwtOptions)
    {
        _db = db;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthTokens> GenerateTokensAsync(
        string userId, string email, string? displayName, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
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

        var refreshTokenValue = GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = refreshTokenValue,
            ExpiresAt = now.AddDays(_jwtOptions.RefreshTokenLifetimeDays),
            CreatedAt = now
        };

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

        return new AuthTokens(accessToken, refreshTokenValue, expiresAt);
    }

    public async Task<AuthTokens> RefreshTokensAsync(string refreshToken, CancellationToken ct = default)
    {
        var storedToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken && !rt.IsRevoked, ct);

        if (storedToken is null || storedToken.ExpiresAt <= DateTimeOffset.UtcNow)
            throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        storedToken.IsRevoked = true;

        var user = await _db.Users.FindAsync([storedToken.UserId], ct)
            ?? throw new UnauthorizedAccessException("User not found.");

        var appUser = (ApplicationUser)user;
        await _db.SaveChangesAsync(ct);

        return await GenerateTokensAsync(appUser.Id, appUser.Email!, appUser.DisplayName, ct);
    }

    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default)
    {
        var storedToken = await _db.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == refreshToken, ct);

        if (storedToken is not null)
        {
            storedToken.IsRevoked = true;
            await _db.SaveChangesAsync(ct);
        }
    }

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        RandomNumberGenerator.Fill(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
