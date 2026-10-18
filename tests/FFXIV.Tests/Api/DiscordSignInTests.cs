using System.Net;
using System.Net.Http.Json;
using FFXIV.Application.Auth;
using FFXIV.Application.Auth.DTOs;

namespace FFXIV.Tests.Api;

public sealed class DiscordSignInTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    /// <summary>A client holding a state the API issued, as the browser would after the redirect.</summary>
    private async Task<(HttpClient Client, string State)> StartSignInAsync()
    {
        var client = factory.CreateApiClient();
        var start = (await client.GetFromJsonAsync<DiscordAuthUrlDto>("/api/auth/discord/url"))!;
        return (client, start.State);
    }

    [Fact]
    public async Task Callback_rejects_a_state_the_api_did_not_issue()
    {
        var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/discord/callback", new { code = "any", state = "forged" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ErrorAsync()).Should().Contain("expired or is invalid");
        await factory.Discord.DidNotReceive().ExchangeCodeAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Callback_without_a_verified_address_creates_no_account()
    {
        factory.Discord.ExchangeCodeAsync("unverified", Arg.Any<CancellationToken>())
            .Returns(new DiscordUserInfo("discord-unverified", "Unverified", "unverified@example.com", EmailVerified: false, null));
        var (client, state) = await StartSignInAsync();

        var response = await client.PostAsJsonAsync("/api/auth/discord/callback", new { code = "unverified", state });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ErrorAsync()).Should().Contain("verified email");
    }

    [Fact]
    public async Task Callback_with_a_verified_address_creates_the_account_once()
    {
        factory.Discord.ExchangeCodeAsync("verified", Arg.Any<CancellationToken>())
            .Returns(new DiscordUserInfo("discord-verified", "Verified", "verified@example.com", EmailVerified: true,
                "https://cdn.discordapp.com/avatars/verified.png"));
        var (client, state) = await StartSignInAsync();

        var first = await client.PostAsJsonAsync("/api/auth/discord/callback", new { code = "verified", state });

        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());
        client.UseBearer((await first.Content.ReadFromJsonAsync<AuthTokensDto>())!.AccessToken);
        var me = (await client.GetFromJsonAsync<UserInfoDto>("/api/auth/me"))!;
        me.DiscordId.Should().Be("discord-verified");
        me.Email.Should().Be("verified@example.com");

        var (again, secondState) = await StartSignInAsync();
        var second = await again.PostAsJsonAsync("/api/auth/discord/callback", new { code = "verified", state = secondState });
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        again.UseBearer((await second.Content.ReadFromJsonAsync<AuthTokensDto>())!.AccessToken);
        (await again.GetFromJsonAsync<UserInfoDto>("/api/auth/me"))!.UserId.Should().Be(me.UserId,
            "a second Discord sign-in is a login, not a new account");
    }

    [Fact]
    public async Task Callback_never_attaches_discord_to_a_password_account_with_the_same_address()
    {
        var account = await factory.RegisterAsync();
        factory.Discord.ExchangeCodeAsync("same-address", Arg.Any<CancellationToken>())
            .Returns(new DiscordUserInfo("discord-same-address", "Same", account.Email, EmailVerified: true, null));
        var (client, state) = await StartSignInAsync();

        var response = await client.PostAsJsonAsync("/api/auth/discord/callback", new { code = "same-address", state });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await account.Client.GetFromJsonAsync<UserInfoDto>("/api/auth/me"))!.DiscordId.Should().BeNull();
    }
}
