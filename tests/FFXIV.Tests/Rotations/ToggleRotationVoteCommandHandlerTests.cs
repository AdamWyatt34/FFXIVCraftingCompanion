using FFXIV.Application.Rotations.Commands;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.Rotations;

public class ToggleRotationVoteCommandHandlerTests
{
    private readonly ISharedRotationRepository _repo = Substitute.For<ISharedRotationRepository>();
    private readonly ToggleRotationVoteCommandHandler _handler;

    public ToggleRotationVoteCommandHandlerTests()
    {
        _handler = new ToggleRotationVoteCommandHandler(_repo);
        _repo.ExistsAsync(7, Arg.Any<CancellationToken>()).Returns(true);
    }

    [Fact]
    public async Task Unknown_rotation_returns_null_without_touching_votes()
    {
        var result = await _handler.Handle(new ToggleRotationVoteCommand("user-1", 99), CancellationToken.None);

        result.Should().BeNull();
        await _repo.DidNotReceiveWithAnyArgs().AddVoteAsync(default!, default);
        await _repo.DidNotReceiveWithAnyArgs().RecountUpvotesAsync(default, default);
    }

    [Fact]
    public async Task First_toggle_adds_the_vote_and_reports_the_stored_count()
    {
        _repo.HasVotedAsync("user-1", 7, Arg.Any<CancellationToken>()).Returns(false);
        _repo.AddVoteAsync(Arg.Any<RotationVote>(), Arg.Any<CancellationToken>()).Returns(true);
        _repo.RecountUpvotesAsync(7, Arg.Any<CancellationToken>()).Returns(4);

        var result = await _handler.Handle(new ToggleRotationVoteCommand("user-1", 7), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Upvoted.Should().BeTrue();
        result.Upvotes.Should().Be(4);
        await _repo.Received(1).AddVoteAsync(
            Arg.Is<RotationVote>(v => v.UserId == "user-1" && v.RotationId == 7), Arg.Any<CancellationToken>());
        await _repo.DidNotReceiveWithAnyArgs().RemoveVoteAsync(default!, default, default);
    }

    [Fact]
    public async Task Second_toggle_removes_the_vote_and_reports_the_stored_count()
    {
        _repo.HasVotedAsync("user-1", 7, Arg.Any<CancellationToken>()).Returns(true);
        _repo.RemoveVoteAsync("user-1", 7, Arg.Any<CancellationToken>()).Returns(true);
        _repo.RecountUpvotesAsync(7, Arg.Any<CancellationToken>()).Returns(3);

        var result = await _handler.Handle(new ToggleRotationVoteCommand("user-1", 7), CancellationToken.None);

        result!.Upvoted.Should().BeFalse();
        result.Upvotes.Should().Be(3);
        await _repo.DidNotReceiveWithAnyArgs().AddVoteAsync(default!, default);
    }

    [Fact]
    public async Task Concurrent_duplicate_add_is_idempotent()
    {
        _repo.HasVotedAsync("user-1", 7, Arg.Any<CancellationToken>()).Returns(false);
        _repo.AddVoteAsync(Arg.Any<RotationVote>(), Arg.Any<CancellationToken>()).Returns(false);
        _repo.RecountUpvotesAsync(7, Arg.Any<CancellationToken>()).Returns(1);

        var result = await _handler.Handle(new ToggleRotationVoteCommand("user-1", 7), CancellationToken.None);

        result!.Upvoted.Should().BeTrue();
        result.Upvotes.Should().Be(1);
    }
}
