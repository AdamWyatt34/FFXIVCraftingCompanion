using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Domain.Interfaces;
using FFXIV.Infrastructure.Solver;

namespace FFXIV.Tests.CraftingEngine.Solver;

public class SolverCacheServiceTests
{
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly SolverCacheService _service;

    public SolverCacheServiceTests()
    {
        _service = new SolverCacheService(_cache);
    }

    private static CrafterStats CreateStats(
        int craftsmanship = 3800, int control = 3600, int cp = 600, int level = 90)
    {
        return new CrafterStats
        {
            Level = level,
            Craftsmanship = craftsmanship,
            Control = control,
            CP = cp,
        };
    }

    [Fact]
    public void GenerateCacheKey_SameInputs_ProduceSameKey()
    {
        var stats = CreateStats();
        var constraints = new SolverConstraints();

        var key1 = _service.GenerateCacheKey(100, stats, constraints);
        var key2 = _service.GenerateCacheKey(100, stats, constraints);

        key1.Should().Be(key2);
    }

    [Fact]
    public void GenerateCacheKey_DifferentRecipeId_ProduceDifferentKeys()
    {
        var stats = CreateStats();
        var constraints = new SolverConstraints();

        var key1 = _service.GenerateCacheKey(100, stats, constraints);
        var key2 = _service.GenerateCacheKey(200, stats, constraints);

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateCacheKey_DifferentStats_ProduceDifferentKeys()
    {
        var constraints = new SolverConstraints();

        var key1 = _service.GenerateCacheKey(100, CreateStats(craftsmanship: 3800), constraints);
        var key2 = _service.GenerateCacheKey(100, CreateStats(craftsmanship: 4000), constraints);

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateCacheKey_DifferentConstraints_ProduceDifferentKeys()
    {
        var stats = CreateStats();

        var key1 = _service.GenerateCacheKey(100, stats, new SolverConstraints { MaxSteps = 50 });
        var key2 = _service.GenerateCacheKey(100, stats, new SolverConstraints { MaxSteps = 30 });

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateCacheKey_IncludesAllStatFields()
    {
        var stats = CreateStats();
        var constraints = new SolverConstraints();
        var key = _service.GenerateCacheKey(100, stats, constraints);

        key.Should().Contain("100");   // recipeId
        key.Should().Contain("3800");  // craftsmanship
        key.Should().Contain("3600");  // control
        key.Should().Contain("600");   // cp
        key.Should().Contain("90");    // level
        key.Should().StartWith("solve:");
    }

    [Fact]
    public async Task GetCachedResult_DelegatesToCacheService()
    {
        var stats = CreateStats();
        var constraints = new SolverConstraints();
        var expected = new SolverResult { Status = SolverStatus.Completed };

        var key = _service.GenerateCacheKey(100, stats, constraints);
        _cache.GetAsync<SolverResult>(key, Arg.Any<CancellationToken>())
            .Returns(expected);

        var result = await _service.GetCachedResultAsync(100, stats, constraints);

        result.Should().Be(expected);
        await _cache.Received(1).GetAsync<SolverResult>(key, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CacheResult_Uses24HourTtl()
    {
        var stats = CreateStats();
        var constraints = new SolverConstraints();
        var solverResult = new SolverResult { Status = SolverStatus.Completed };

        await _service.CacheResultAsync(100, stats, constraints, solverResult);

        var key = _service.GenerateCacheKey(100, stats, constraints);
        await _cache.Received(1).SetAsync(
            key,
            solverResult,
            TimeSpan.FromHours(24),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void GenerateCacheKey_DifferentCP_ProduceDifferentKeys()
    {
        var constraints = new SolverConstraints();

        var key1 = _service.GenerateCacheKey(100, CreateStats(cp: 600), constraints);
        var key2 = _service.GenerateCacheKey(100, CreateStats(cp: 700), constraints);

        key1.Should().NotBe(key2);
    }

    [Fact]
    public void GenerateCacheKey_DifferentLevel_ProduceDifferentKeys()
    {
        var constraints = new SolverConstraints();

        var key1 = _service.GenerateCacheKey(100, CreateStats(level: 90), constraints);
        var key2 = _service.GenerateCacheKey(100, CreateStats(level: 100), constraints);

        key1.Should().NotBe(key2);
    }
}
