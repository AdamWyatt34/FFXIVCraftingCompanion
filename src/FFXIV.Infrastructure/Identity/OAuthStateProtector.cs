using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using FFXIV.Application.Auth;
using Microsoft.Extensions.Options;

namespace FFXIV.Infrastructure.Identity;

/// <summary>
/// OAuth <c>state</c> as <c>{nonce}.{issuedUnixSeconds}.{signature}</c>, signed with a key
/// derived from the JWT secret (HKDF keeps the two uses of the secret cryptographically apart).
/// A state is valid for ten minutes, which covers a slow Discord consent screen.
/// </summary>
public sealed class OAuthStateProtector : IOAuthStateProtector
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ClockSkew = TimeSpan.FromMinutes(1);
    private const int MaxLength = 256;

    private readonly byte[] _key;
    private readonly TimeProvider _clock;

    public OAuthStateProtector(IOptions<JwtOptions> jwtOptions, TimeProvider clock)
    {
        var secret = jwtOptions.Value.SecretKey;
        if (string.IsNullOrEmpty(secret))
            throw new InvalidOperationException("Jwt:SecretKey must be configured before OAuth state can be signed.");

        _key = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(secret),
            outputLength: 32,
            info: "FFXIVCraftingCompanion.OAuthState.v1"u8.ToArray());
        _clock = clock;
    }

    public string Create()
    {
        Span<byte> nonce = stackalloc byte[16];
        RandomNumberGenerator.Fill(nonce);
        var payload = $"{Base64Url.EncodeToString(nonce)}.{_clock.GetUtcNow().ToUnixTimeSeconds()}";
        return $"{payload}.{Sign(payload)}";
    }

    public bool Validate(string? state)
    {
        if (string.IsNullOrEmpty(state) || state.Length > MaxLength)
            return false;

        var lastDot = state.LastIndexOf('.');
        if (lastDot <= 0 || lastDot == state.Length - 1)
            return false;

        var payload = state[..lastDot];
        var signature = state[(lastDot + 1)..];
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(Sign(payload)),
                Encoding.UTF8.GetBytes(signature)))
            return false;

        var parts = payload.Split('.');
        if (parts.Length != 2 || !long.TryParse(parts[1], out var issuedUnixSeconds))
            return false;

        var issued = DateTimeOffset.FromUnixTimeSeconds(issuedUnixSeconds);
        var now = _clock.GetUtcNow();
        return issued <= now + ClockSkew && now - issued <= Lifetime;
    }

    private string Sign(string payload) =>
        Base64Url.EncodeToString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload)));
}
