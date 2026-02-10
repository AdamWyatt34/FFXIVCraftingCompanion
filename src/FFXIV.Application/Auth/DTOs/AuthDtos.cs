namespace FFXIV.Application.Auth.DTOs;

public sealed class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

public sealed class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class LogoutRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class DiscordCallbackRequest
{
    public string Code { get; set; } = string.Empty;
}

public sealed record AuthTokensDto(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);

public sealed record UserInfoDto(
    string UserId,
    string Email,
    string? DisplayName,
    string? AvatarUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt,
    string? DiscordId);

public sealed record DiscordAuthUrlDto(
    string AuthorizationUrl);
