namespace FFXIV.Application.Auth;

public interface ITokenService
{
    Task<AuthTokens> GenerateTokensAsync(string userId, string email, string? displayName, CancellationToken ct = default);
    Task<AuthTokens> RefreshTokensAsync(string refreshToken, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
}
