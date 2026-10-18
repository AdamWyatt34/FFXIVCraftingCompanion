using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FFXIV.Application.Profile;

namespace FFXIV.Tests.Api;

public sealed class ProfileLodestoneTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Server = "Gilgamesh";

    private static LodestoneCharacterResult Character(string lodestoneId, string name) => new()
    {
        LodestoneId = lodestoneId,
        CharacterName = name,
        ServerName = Server,
        DataCenter = "Aether",
        ActiveJobAbbreviation = "CRP",
        ActiveJobLevel = 90,
        JobLevels = new Dictionary<string, int> { ["CRP"] = 90 },
        Attributes = new LodestoneAttributes { Craftsmanship = 3000, Control = 3000, CP = 500 },
    };

    [Fact]
    public async Task Import_rejects_an_unknown_server_before_asking_lodestone()
    {
        var account = await factory.RegisterAsync();

        var response = await account.Client.PostAsJsonAsync("/api/profile/import-lodestone",
            new { characterName = "Some One", serverName = "Nowhere" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ErrorAsync()).Should().Contain("not a valid FFXIV server");
        await factory.Lodestone.DidNotReceive()
            .SearchAndFetchCharacterAsync("Some One", "Nowhere", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Import_refuses_a_malformed_lodestone_id()
    {
        factory.Lodestone.SearchAndFetchCharacterAsync("Mal Formed", Server, Arg.Any<CancellationToken>())
            .Returns(Character("12345; not an id", "Mal Formed"));
        var account = await factory.RegisterAsync();

        var response = await account.Client.PostAsJsonAsync("/api/profile/import-lodestone",
            new { characterName = "Mal Formed", serverName = Server });

        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);
        (await account.Client.GetAsync("/api/profile")).StatusCode.Should().Be(HttpStatusCode.NotFound,
            "nothing is stored from a malformed id");
    }

    [Fact]
    public async Task A_lodestone_character_links_to_one_account()
    {
        factory.Lodestone.SearchAndFetchCharacterAsync("Lin Ked", Server, Arg.Any<CancellationToken>())
            .Returns(Character("12345678", "Lin Ked"));
        var first = await factory.RegisterAsync();
        var second = await factory.RegisterAsync();

        var imported = await first.Client.PostAsJsonAsync("/api/profile/import-lodestone",
            new { characterName = "Lin Ked", serverName = Server });

        imported.StatusCode.Should().Be(HttpStatusCode.OK, await imported.Content.ReadAsStringAsync());
        var profile = await first.Client.GetFromJsonAsync<JsonElement>("/api/profile");
        profile.GetProperty("characterName").GetString().Should().Be("Lin Ked");
        profile.GetProperty("stats").GetProperty("craftsmanship").GetInt32().Should().Be(3000);

        var again = await second.Client.PostAsJsonAsync("/api/profile/import-lodestone",
            new { characterName = "Lin Ked", serverName = Server });
        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await second.Client.GetAsync("/api/profile")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Preview_is_open_and_validates_before_asking_lodestone()
    {
        var client = factory.CreateApiClient();

        var missingServer = await client.GetAsync("/api/profile/lodestone-preview?name=Some%20One");
        var badServer = await client.GetAsync("/api/profile/lodestone-preview?name=Some%20One&server=Nowhere");
        var unknown = await client.GetAsync($"/api/profile/lodestone-preview?name=Some%20One&server={Server}");
        var unknownAgain = await client.GetAsync($"/api/profile/lodestone-preview?name=Some%20One&server={Server}");

        missingServer.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        badServer.StatusCode.Should().Be(HttpStatusCode.NotFound, "an impossible lookup is a miss, not a Lodestone call");
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);
        unknownAgain.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await factory.Lodestone.DidNotReceive().SearchAndFetchCharacterAsync("Some One", "Nowhere", Arg.Any<CancellationToken>());
        await factory.Lodestone.Received(1).SearchAndFetchCharacterAsync("Some One", Server, Arg.Any<CancellationToken>());
    }
}
