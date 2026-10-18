using FFXIV.Application.Profile;
using FFXIV.Application.Profile.Queries;
using FFXIV.Infrastructure.Caching;
using Microsoft.Extensions.Logging;

namespace FFXIV.Tests.Profile;

public sealed class PreviewLodestoneQueryHandlerTests : IDisposable
{
    private readonly ILodestoneService _lodestone = Substitute.For<ILodestoneService>();
    private readonly InMemoryCacheService _cache = new();
    private readonly PreviewLodestoneQueryHandler _handler;

    public PreviewLodestoneQueryHandlerTests()
    {
        _handler = new PreviewLodestoneQueryHandler(
            _lodestone, _cache, Substitute.For<ILogger<PreviewLodestoneQueryHandler>>());
    }

    public void Dispose() => _cache.Dispose();

    [Theory]
    [InlineData("Robert'); DROP TABLE", "Cactuar")]
    [InlineData("<script>alert(1)</script>", "Cactuar")]
    [InlineData("Nameless Hero", "Nowhere")]
    [InlineData("A Name That Is Far Too Long For The Lodestone To Ever Accept", "Cactuar")]
    public async Task Implausible_requests_never_reach_the_lodestone(string name, string server)
    {
        var result = await _handler.Handle(new PreviewLodestoneQuery(name, server), CancellationToken.None);

        result.Should().BeNull();
        await _lodestone.DidNotReceiveWithAnyArgs().SearchAndFetchCharacterAsync(default!, default!, default);
    }

    [Theory]
    [InlineData("Y'shtola Rhul")]
    [InlineData("Jean-Luc Picard")]
    [InlineData("Alphinaud")]
    public void Real_name_shapes_are_accepted(string name)
    {
        PreviewLodestoneQueryHandler.IsPlausibleCharacterName(name).Should().BeTrue();
    }

    [Fact]
    public async Task A_miss_is_cached_so_repeats_do_not_hit_the_lodestone()
    {
        _lodestone.SearchAndFetchCharacterAsync("Nameless Hero", "Cactuar", Arg.Any<CancellationToken>())
            .Returns((LodestoneCharacterResult?)null);

        var first = await _handler.Handle(new PreviewLodestoneQuery("Nameless Hero", "Cactuar"), CancellationToken.None);
        var second = await _handler.Handle(new PreviewLodestoneQuery("Nameless Hero", "Cactuar"), CancellationToken.None);

        first.Should().BeNull();
        second.Should().BeNull();
        await _lodestone.Received(1).SearchAndFetchCharacterAsync("Nameless Hero", "Cactuar", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_hit_is_cached_and_mapped()
    {
        _lodestone.SearchAndFetchCharacterAsync("Test Character", "Cactuar", Arg.Any<CancellationToken>())
            .Returns(new LodestoneCharacterResult
            {
                LodestoneId = "12345",
                CharacterName = "Test Character",
                ServerName = "Cactuar",
                ActiveJobAbbreviation = "CRP",
                ActiveJobLevel = 90,
                JobLevels = new Dictionary<string, int> { ["CRP"] = 90 },
            });

        var first = await _handler.Handle(new PreviewLodestoneQuery("Test Character", "Cactuar"), CancellationToken.None);
        var second = await _handler.Handle(new PreviewLodestoneQuery("Test Character", "Cactuar"), CancellationToken.None);

        first.Should().NotBeNull();
        first!.DataCenter.Should().Be("Aether");
        first.ActiveJobDisplayName.Should().Be("Carpenter");
        second.Should().BeEquivalentTo(first);
        await _lodestone.Received(1).SearchAndFetchCharacterAsync("Test Character", "Cactuar", Arg.Any<CancellationToken>());
    }
}
