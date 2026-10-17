using System.Text.RegularExpressions;
using FFXIV.Infrastructure.Identity;

namespace FFXIV.Tests.Identity;

public class RefreshTokenHasherTests
{
    [Fact]
    public void Hash_is_deterministic()
    {
        RefreshTokenHasher.Hash("token-a").Should().Be(RefreshTokenHasher.Hash("token-a"));
    }

    [Fact]
    public void Hash_differs_for_different_tokens()
    {
        RefreshTokenHasher.Hash("token-a").Should().NotBe(RefreshTokenHasher.Hash("token-b"));
    }

    [Fact]
    public void Hash_is_url_safe_and_fits_the_token_column()
    {
        var hash = RefreshTokenHasher.Hash(Convert.ToBase64String(new byte[64]));

        hash.Should().HaveLength(43, "SHA-256 is 32 bytes, which is 43 base64url characters");
        Regex.IsMatch(hash, "^[A-Za-z0-9_-]+$").Should().BeTrue();
    }

    [Fact]
    public void Hash_rejects_empty_input()
    {
        var act = () => RefreshTokenHasher.Hash("");

        act.Should().Throw<ArgumentException>();
    }
}
