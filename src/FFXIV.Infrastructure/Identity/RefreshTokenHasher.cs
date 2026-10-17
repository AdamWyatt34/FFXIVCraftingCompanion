using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace FFXIV.Infrastructure.Identity;

/// <summary>
/// Refresh tokens are stored as SHA-256 digests, so a copy of the RefreshTokens table cannot be
/// replayed as sessions. The digest is base64url (43 characters) and fits the existing unique
/// <c>Token</c> column, so no schema change is involved.
/// </summary>
public static class RefreshTokenHasher
{
    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
