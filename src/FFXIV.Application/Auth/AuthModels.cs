namespace FFXIV.Application.Auth;

public sealed record AuthTokens(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt);

public sealed record DiscordUserInfo(
    string DiscordId,
    string Username,
    string Email,
    string? AvatarUrl);
