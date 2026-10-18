using System.Net;
using System.Net.Http.Json;
using FFXIV.Application.Auth.DTOs;

namespace FFXIV.Tests.Api;

public sealed class AuthEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string WrongPassword = "Wrong-password-1";

    [Fact]
    public async Task Register_signs_the_account_in()
    {
        var account = await factory.RegisterAsync("Tester");

        var me = (await account.Client.GetFromJsonAsync<UserInfoDto>("/api/auth/me"))!;

        me.Email.Should().Be(account.Email);
        me.DisplayName.Should().Be("Tester");
        account.Tokens.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_rejects_a_duplicate_address()
    {
        var account = await factory.RegisterAsync();
        var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = account.Email, password = ApiClientExtensions.Password });

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await response.ErrorAsync()).Should().Contain("already exists");
    }

    [Fact]
    public async Task Register_answers_a_failed_annotation_with_the_error_shape()
    {
        var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email = "not-an-address", password = ApiClientExtensions.Password });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.ErrorAsync()).Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_returns_tokens_for_the_right_password()
    {
        var account = await factory.RegisterAsync();
        var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { email = account.Email, password = account.Password });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = (await response.Content.ReadFromJsonAsync<AuthTokensDto>())!;
        client.UseBearer(tokens.AccessToken);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_answers_a_wrong_password_and_an_unknown_address_alike()
    {
        var account = await factory.RegisterAsync();
        var client = factory.CreateApiClient();

        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login",
            new { email = account.Email, password = WrongPassword });
        var unknownAddress = await client.PostAsJsonAsync("/api/auth/login",
            new { email = "nobody@example.com", password = account.Password });

        wrongPassword.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknownAddress.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await wrongPassword.ErrorAsync()).Should().Be(await unknownAddress.ErrorAsync());
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_the_account_without_saying_so()
    {
        var account = await factory.RegisterAsync();
        var client = factory.CreateApiClient();
        string? lastError = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failed = await client.PostAsJsonAsync("/api/auth/login",
                new { email = account.Email, password = WrongPassword });
            failed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            lastError = await failed.ErrorAsync();
        }

        var lockedOut = await client.PostAsJsonAsync("/api/auth/login",
            new { email = account.Email, password = account.Password });

        lockedOut.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "the fifth failure locks the account for fifteen minutes");
        (await lockedOut.ErrorAsync()).Should().Be(lastError,
            "a lockout must not confirm that the address is registered");
    }

    [Fact]
    public async Task Refresh_rotates_the_token_and_the_spent_one_stops_working()
    {
        var account = await factory.RegisterAsync();
        var client = factory.CreateApiClient();

        var refreshed = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = account.Tokens.RefreshToken });

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = (await refreshed.Content.ReadFromJsonAsync<AuthTokensDto>())!;
        tokens.RefreshToken.Should().NotBe(account.Tokens.RefreshToken);

        var reused = await client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = account.Tokens.RefreshToken });
        reused.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        client.UseBearer(tokens.AccessToken);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Logout_revokes_the_refresh_token()
    {
        var account = await factory.RegisterAsync();

        var logout = await account.Client.PostAsJsonAsync("/api/auth/logout",
            new { refreshToken = account.Tokens.RefreshToken });
        logout.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var refresh = await factory.CreateApiClient().PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = account.Tokens.RefreshToken });
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("GET", "/api/auth/me")]
    [InlineData("POST", "/api/auth/logout")]
    [InlineData("GET", "/api/profile")]
    [InlineData("GET", "/api/profile/stats")]
    [InlineData("POST", "/api/profile/import-lodestone")]
    [InlineData("GET", "/api/saved-lists")]
    [InlineData("GET", "/api/saved-lists/1")]
    [InlineData("GET", "/api/favorites/recipes")]
    [InlineData("GET", "/api/craft-plans")]
    [InlineData("GET", "/api/craft-plans/some-id")]
    [InlineData("GET", "/api/notifications")]
    [InlineData("POST", "/api/rotations")]
    [InlineData("POST", "/api/rotations/1/upvote")]
    [InlineData("DELETE", "/api/rotations/1")]
    [InlineData("GET", "/api/gear/recommendations/compare?job=CRP")]
    public async Task Account_routes_reject_anonymous_requests(string method, string path)
    {
        var client = factory.CreateApiClient();

        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
