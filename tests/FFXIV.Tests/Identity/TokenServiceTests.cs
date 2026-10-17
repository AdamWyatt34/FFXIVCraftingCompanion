using FFXIV.Domain.Entities;
using FFXIV.Infrastructure.Identity;
using FFXIV.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FFXIV.Tests.Identity;

/// <summary>
/// Runs the real <see cref="TokenService"/> against the real <see cref="AppDbContext"/> on an
/// in-memory SQLite database, so hashing, rotation and pruning are checked end to end.
/// </summary>
public sealed class TokenServiceTests : IDisposable
{
    private const string UserId = "user-1";
    private static readonly DateTimeOffset Start = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly AppDbContext _db;
    private readonly FakeClock _clock = new(Start);
    private readonly JwtOptions _jwt = new()
    {
        SecretKey = "unit-test-jwt-secret-key-with-plenty-of-length-0123456789",
        AccessTokenLifetimeMinutes = 60,
        RefreshTokenLifetimeDays = 30,
    };
    private readonly TokenService _service;

    public TokenServiceTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _db.Users.Add(new ApplicationUser
        {
            Id = UserId,
            UserName = "user@example.com",
            NormalizedUserName = "USER@EXAMPLE.COM",
            Email = "user@example.com",
            NormalizedEmail = "USER@EXAMPLE.COM",
            SecurityStamp = Guid.NewGuid().ToString(),
            CreatedAt = Start,
        });
        _db.SaveChanges();
        _service = new TokenService(_db, Options.Create(_jwt), _clock);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Task<List<RefreshToken>> StoredTokens() =>
        _db.RefreshTokens.AsNoTracking().Where(rt => rt.UserId == UserId).ToListAsync();

    [Fact]
    public async Task GenerateTokens_stores_a_digest_rather_than_the_token()
    {
        var tokens = await _service.GenerateTokensAsync(UserId, "user@example.com", null);

        var stored = (await StoredTokens()).Single();
        stored.Token.Should().NotBe(tokens.RefreshToken);
        stored.Token.Should().Be(RefreshTokenHasher.Hash(tokens.RefreshToken));
        stored.IsRevoked.Should().BeFalse();
        stored.ExpiresAt.Should().Be(Start.AddDays(30));
        tokens.AccessTokenExpiresAt.Should().Be(Start.AddMinutes(60));
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_removes_the_spent_one()
    {
        var first = await _service.GenerateTokensAsync(UserId, "user@example.com", null);

        var second = await _service.RefreshTokensAsync(first.RefreshToken);

        second.RefreshToken.Should().NotBe(first.RefreshToken);
        var stored = await StoredTokens();
        stored.Should().ContainSingle().Which.Token.Should().Be(RefreshTokenHasher.Hash(second.RefreshToken));

        var reuse = () => _service.RefreshTokensAsync(first.RefreshToken);
        await reuse.Should().ThrowAsync<UnauthorizedAccessException>("a rotated-out token must not work twice");
    }

    [Fact]
    public async Task Refresh_rejects_an_expired_token()
    {
        var tokens = await _service.GenerateTokensAsync(UserId, "user@example.com", null);
        _clock.Advance(TimeSpan.FromDays(31));

        var act = () => _service.RefreshTokensAsync(tokens.RefreshToken);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Refresh_rejects_an_unknown_token()
    {
        var act = () => _service.RefreshTokensAsync("never-issued");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Refresh_does_not_accept_a_legacy_plaintext_row()
    {
        // Rows written before hashing hold the raw token; they stop matching, which is the
        // documented one-time re-login after this change ships.
        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = UserId,
            Token = "legacy-raw-token",
            ExpiresAt = Start.AddDays(10),
            CreatedAt = Start,
        });
        await _db.SaveChangesAsync();

        var act = () => _service.RefreshTokensAsync("legacy-raw-token");

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Refresh_prunes_the_users_expired_and_logged_out_tokens()
    {
        var expired = await _service.GenerateTokensAsync(UserId, "user@example.com", null);
        _clock.Advance(TimeSpan.FromDays(20));
        var loggedOut = await _service.GenerateTokensAsync(UserId, "user@example.com", null);
        await _service.RevokeRefreshTokenAsync(loggedOut.RefreshToken);
        var current = await _service.GenerateTokensAsync(UserId, "user@example.com", null);
        _clock.Advance(TimeSpan.FromDays(11)); // "expired" is now past its 30 days

        var next = await _service.RefreshTokensAsync(current.RefreshToken);

        var stored = await StoredTokens();
        stored.Should().ContainSingle("expired, logged-out and spent tokens are all gone")
            .Which.Token.Should().Be(RefreshTokenHasher.Hash(next.RefreshToken));
        stored.Select(rt => rt.Token).Should().NotContain(RefreshTokenHasher.Hash(expired.RefreshToken));
        stored.Select(rt => rt.Token).Should().NotContain(RefreshTokenHasher.Hash(loggedOut.RefreshToken));
    }

    [Fact]
    public async Task Revoke_looks_the_token_up_by_digest()
    {
        var tokens = await _service.GenerateTokensAsync(UserId, "user@example.com", null);

        await _service.RevokeRefreshTokenAsync(tokens.RefreshToken);

        (await StoredTokens()).Single().IsRevoked.Should().BeTrue();
        var act = () => _service.RefreshTokensAsync(tokens.RefreshToken);
        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task Revoke_ignores_an_unknown_token()
    {
        var act = () => _service.RevokeRefreshTokenAsync("never-issued");

        await act.Should().NotThrowAsync();
    }
}
