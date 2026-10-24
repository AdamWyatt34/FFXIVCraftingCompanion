using System.Net;
using System.Net.Http.Json;

namespace FFXIV.Tests.Api;

public sealed class RotationSubmitTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static Dictionary<string, object> Request() => new()
    {
        ["recipeId"] = ApiFactory.FixtureRecipeId,
        ["actionIds"] = new[] { 100002, 100001 },
        ["stats"] = new { level = 90, craftsmanship = 3000, control = 3000, cp = 500 },
        ["metrics"] = new { qualityPercent = 37.25m, hqPercent = 12, totalSteps = 2, craftCompleted = true },
    };

    [Fact]
    public async Task Create_and_detail_return_reported_metrics_and_action_names()
    {
        var account = await factory.RegisterAsync();
        var response = await account.Client.PostAsJsonAsync("/api/rotations", Request());
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var created = await response.JsonAsync();
        created.GetProperty("qualityPercent").GetDecimal().Should().Be(37.25m);
        created.GetProperty("hqPercent").GetInt32().Should().Be(12);
        created.GetProperty("totalSteps").GetInt32().Should().Be(2);
        created.GetProperty("craftCompleted").GetBoolean().Should().BeTrue();

        var detail = await (await account.Client.GetAsync($"/api/rotations/{created.GetProperty("id").GetInt32()}")).JsonAsync();
        detail.GetProperty("actionNames").EnumerateArray().Select(x => x.GetString())
            .Should().Equal("BasicTouch", "BasicSynthesis");
        detail.GetProperty("macros").GetArrayLength().Should().Be(0);
    }

    [Theory]
    [InlineData("hq", HttpStatusCode.BadRequest)]
    [InlineData("missing", HttpStatusCode.BadRequest)]
    [InlineData("steps", HttpStatusCode.Conflict)]
    public async Task Invalid_metrics_are_rejected(string scenario, HttpStatusCode expected)
    {
        var account = await factory.RegisterAsync();
        var request = Request();
        if (scenario == "missing")
            request.Remove("metrics");
        else
            request["metrics"] = new
            {
                qualityPercent = 50m, hqPercent = scenario == "hq" ? 101 : 12,
                totalSteps = scenario == "steps" ? 3 : 2, craftCompleted = true,
            };

        var response = await account.Client.PostAsJsonAsync("/api/rotations", request);

        response.StatusCode.Should().Be(expected, await response.Content.ReadAsStringAsync());
        if (scenario == "missing")
            (await response.Content.ReadAsStringAsync()).Should().Contain("refresh the page");
    }
}
