using System.ComponentModel.DataAnnotations;

namespace FFXIV.Application.Auth.DTOs;

// Request types carry DataAnnotations; the API answers a failed one with the same { error }
// shape the hand-written checks use (see ConfigureApiBehaviorOptions in Program.cs).

public sealed class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; set; } = string.Empty;

    [MaxLength(128)]
    public string? DisplayName { get; set; }
}

public sealed class LoginRequest
{
    [Required, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}

public sealed class RefreshTokenRequest
{
    [Required, MaxLength(512)]
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class LogoutRequest
{
    [MaxLength(512)]
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class DiscordCallbackRequest
{
    [Required, MaxLength(512)]
    public string Code { get; set; } = string.Empty;

    /// <summary>The <c>state</c> Discord echoed back; it must be the one this API issued.</summary>
    [Required, MaxLength(256)]
    public string State { get; set; } = string.Empty;
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

/// <summary>
/// The Discord authorisation URL plus the <c>state</c> embedded in it, so the browser can store
/// the state before redirecting and compare it with the value Discord returns.
/// </summary>
public sealed record DiscordAuthUrlDto(
    string AuthorizationUrl,
    string State);
