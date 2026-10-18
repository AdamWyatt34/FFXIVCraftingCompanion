using FFXIV.Infrastructure.Caching;

namespace FFXIV.Tests.Caching;

public sealed class InMemoryCacheServiceTests
{
    private sealed record Payload(string Text);

    [Fact]
    public async Task Set_then_get_round_trips_and_remove_clears()
    {
        using var cache = new InMemoryCacheService();

        await cache.SetAsync("k", new Payload("v"), TimeSpan.FromMinutes(5));
        (await cache.GetAsync<Payload>("k")).Should().Be(new Payload("v"));

        await cache.RemoveAsync("k");
        (await cache.GetAsync<Payload>("k")).Should().BeNull();
    }

    [Fact]
    public async Task RemoveByPrefix_removes_only_matching_keys()
    {
        using var cache = new InMemoryCacheService();
        await cache.SetAsync("recipe:1", new Payload("a"));
        await cache.SetAsync("recipe:2", new Payload("b"));
        await cache.SetAsync("item:1", new Payload("c"));

        await cache.RemoveByPrefixAsync("recipe:");

        (await cache.GetAsync<Payload>("recipe:1")).Should().BeNull();
        (await cache.GetAsync<Payload>("recipe:2")).Should().BeNull();
        (await cache.GetAsync<Payload>("item:1")).Should().NotBeNull();
    }

    [Fact]
    public async Task Values_larger_than_the_budget_are_not_cached()
    {
        using var cache = new InMemoryCacheService(sizeLimitBytes: 1024);

        await cache.SetAsync("big", new Payload(new string('x', 4096)));

        (await cache.GetAsync<Payload>("big")).Should().BeNull();
    }

    [Fact]
    public async Task The_budget_bounds_how_much_is_kept()
    {
        // ~600 bytes per entry against a 4 KiB budget: well under ten entries can stay resident,
        // so the cache cannot hold on to everything it was ever given.
        using var cache = new InMemoryCacheService(sizeLimitBytes: 4096);
        for (var i = 0; i < 50; i++)
            await cache.SetAsync($"entry:{i}", new Payload(new string('x', 300)));

        var resident = 0;
        for (var i = 0; i < 50; i++)
        {
            if (await cache.GetAsync<Payload>($"entry:{i}") is not null)
                resident++;
        }

        resident.Should().BeInRange(1, 10);
    }
}
