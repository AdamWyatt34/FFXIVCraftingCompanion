using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Infrastructure.Identity;
using FFXIV.Infrastructure.Persistence;
using FFXIV.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Tests.Rotations;

/// <summary>
/// Runs the real repository against the real <see cref="AppDbContext"/> on in-memory SQLite, so the
/// set-based recount and the unique-index handling are checked as translated SQL, not as mocks.
/// Foreign keys are off: the rotation rows need no recipe or item fixtures for these paths.
/// </summary>
public sealed class SharedRotationRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:;Foreign Keys=False");
    private readonly AppDbContext _db;
    private readonly SharedRotationRepository _repo;

    public SharedRotationRepositoryTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();

        _db.Users.Add(new ApplicationUser
        {
            Id = "named",
            UserName = "named@example.com",
            Email = "named@example.com",
            DisplayName = "Crafty",
            SecurityStamp = Guid.NewGuid().ToString(),
        });
        _db.Users.Add(new ApplicationUser
        {
            Id = "nameless",
            UserName = "nameless@example.com",
            Email = "nameless@example.com",
            DisplayName = null,
            SecurityStamp = Guid.NewGuid().ToString(),
        });
        _db.SharedRotations.Add(new SharedRotation
        {
            Id = 1,
            UserId = "named",
            RecipeId = 100,
            ActionIds = [1, 2, 3],
            Source = RotationSource.User,
            Upvotes = 42, // deliberately wrong; the recount must correct it
            CreatedAt = DateTimeOffset.UtcNow,
        });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();

        _repo = new SharedRotationRepository(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RecountUpvotes_sets_the_stored_count_from_the_votes_table()
    {
        await _repo.AddVoteAsync(new RotationVote { UserId = "named", RotationId = 1, CreatedAt = DateTimeOffset.UtcNow });
        await _repo.AddVoteAsync(new RotationVote { UserId = "nameless", RotationId = 1, CreatedAt = DateTimeOffset.UtcNow });

        var count = await _repo.RecountUpvotesAsync(1);

        count.Should().Be(2);
        (await _db.SharedRotations.AsNoTracking().SingleAsync(r => r.Id == 1)).Upvotes.Should().Be(2);
    }

    [Fact]
    public async Task RecountUpvotes_for_an_unknown_rotation_returns_zero()
    {
        (await _repo.RecountUpvotesAsync(999)).Should().Be(0);
    }

    [Fact]
    public async Task Duplicate_vote_is_reported_not_thrown_and_leaves_one_row()
    {
        var first = await _repo.AddVoteAsync(new RotationVote { UserId = "named", RotationId = 1, CreatedAt = DateTimeOffset.UtcNow });
        var second = await _repo.AddVoteAsync(new RotationVote { UserId = "named", RotationId = 1, CreatedAt = DateTimeOffset.UtcNow });

        first.Should().BeTrue();
        second.Should().BeFalse();
        (await _db.RotationVotes.AsNoTracking().CountAsync()).Should().Be(1);
        (await _repo.RecountUpvotesAsync(1)).Should().Be(1);
    }

    [Fact]
    public async Task RemoveVote_reports_whether_a_row_was_removed()
    {
        await _repo.AddVoteAsync(new RotationVote { UserId = "named", RotationId = 1, CreatedAt = DateTimeOffset.UtcNow });

        (await _repo.RemoveVoteAsync("named", 1)).Should().BeTrue();
        (await _repo.RemoveVoteAsync("named", 1)).Should().BeFalse();
        (await _repo.RecountUpvotesAsync(1)).Should().Be(0);
    }

    [Fact]
    public async Task Author_names_never_fall_back_to_the_account_email()
    {
        (await _repo.GetAuthorNameAsync("named")).Should().Be("Crafty");
        (await _repo.GetAuthorNameAsync("nameless")).Should().Be(SharedRotationRepository.AnonymousAuthorName);
        (await _repo.GetAuthorNameAsync("missing")).Should().BeNull();

        var names = await _repo.GetAuthorNamesAsync(["named", "nameless", "missing"]);
        names.Should().HaveCount(2);
        names["named"].Should().Be("Crafty");
        names["nameless"].Should().Be(SharedRotationRepository.AnonymousAuthorName);
        names.Values.Should().NotContain(v => v.Contains('@'));
    }
}
