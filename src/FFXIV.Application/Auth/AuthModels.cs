namespace FFXIV.Application.Auth;

public sealed record AuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt);

/// <summary>
/// What Discord reports about the signed-in user. <paramref name="Email"/> is null when the
/// account has no address or Discord withheld it, and <paramref name="EmailVerified"/> is
/// Discord's own verification flag; only a verified address may be matched to an account here.
/// </summary>
public sealed record DiscordUserInfo(
    string DiscordId,
    string Username,
    string? Email,
    bool EmailVerified,
    string? AvatarUrl);
