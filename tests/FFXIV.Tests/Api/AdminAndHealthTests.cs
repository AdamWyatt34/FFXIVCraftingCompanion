using System.Net;

namespace FFXIV.Tests.Api;

public sealed class AdminAndHealthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Liveness_is_open()
    {
        var response = await factory.CreateApiClient().GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Readiness_runs_only_for_the_admin_key()
    {
        var client = factory.CreateApiClient();
        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Add("X-Admin-Key", "wrong");
        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Remove("X-Admin-Key");
        client.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        var report = await client.GetAsync("/health");

        report.StatusCode.Should().Be(HttpStatusCode.OK, await report.Content.ReadAsStringAsync());
        report.Content.Headers.ContentType!.MediaType.Should().Be("application/json");
        (await report.JsonAsync()).GetProperty("status").GetString().Should().Be("Healthy");
    }

    [Fact]
    public async Task Admin_routes_need_the_key_and_a_signed_in_user_is_not_enough()
    {
        var account = await factory.RegisterAsync();
        (await account.Client.GetAsync("/api/admin/sync/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var client = factory.CreateApiClient();
        (await client.GetAsync("/api/admin/sync/status")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.DefaultRequestHeaders.Add("X-Admin-Key", ApiFactory.AdminKey);
        (await client.GetAsync("/api/admin/sync/status")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
