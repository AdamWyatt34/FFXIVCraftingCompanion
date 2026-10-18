using System.Net;
using System.Net.Http.Json;

namespace FFXIV.Tests.Api;

/// <summary>Its own host, because the budgets are read once when the API starts.</summary>
public sealed class RateLimitTests : IDisposable
{
    private readonly ApiFactory _factory = new(new Dictionary<string, string?>
    {
        ["RateLimiting:AuthPerFiveMinutes"] = "3",
    });

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Login_attempts_are_limited_per_client_address()
    {
        var client = _factory.CreateApiClient();
        var attempt = new { email = "nobody@example.com", password = "Wrong-password-1" };
        for (var i = 0; i < 3; i++)
            (await client.PostAsJsonAsync("/api/auth/login", attempt)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var limited = await client.PostAsJsonAsync("/api/auth/login", attempt);

        limited.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        limited.Headers.RetryAfter!.Delta.Should().BeGreaterThan(TimeSpan.Zero).And.BeLessThanOrEqualTo(TimeSpan.FromMinutes(5),
            "the sliding auth window is five minutes");
        (await limited.ErrorAsync()).Should().Contain("Rate limit");

        var neighbour = await _factory.CreateApiClient().PostAsJsonAsync("/api/auth/login", attempt);
        neighbour.StatusCode.Should().Be(HttpStatusCode.Unauthorized, "the budget is per forwarded address");
    }
}
