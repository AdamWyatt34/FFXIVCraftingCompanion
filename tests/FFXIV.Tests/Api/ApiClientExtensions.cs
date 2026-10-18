using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FFXIV.Application.Auth.DTOs;

namespace FFXIV.Tests.Api;

/// <summary>A registered account and the client that carries its bearer token.</summary>
internal sealed record Account(HttpClient Client, string Email, string Password, AuthTokensDto Tokens, string UserId);

internal static class ApiClientExtensions
{
    public const string Password = "Password1!";

    private static int _addresses;

    /// <summary>
    /// A client with its own forwarded address, so the per-client rate limits never couple two
    /// tests. The test server has no connection address, so without the header every anonymous
    /// request would share one bucket.
    /// </summary>
    public static HttpClient CreateApiClient(this ApiFactory factory)
    {
        var client = factory.CreateClient();
        var n = Interlocked.Increment(ref _addresses);
        client.DefaultRequestHeaders.Add("X-Forwarded-For", $"10.{(n >> 16) & 255}.{(n >> 8) & 255}.{n & 255}");
        return client;
    }

    /// <summary>Registers a fresh account through the API and leaves its access token on the client.</summary>
    public static async Task<Account> RegisterAsync(this ApiFactory factory, string? displayName = null)
    {
        var client = factory.CreateApiClient();
        var email = $"{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password, displayName });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var tokens = (await response.Content.ReadFromJsonAsync<AuthTokensDto>())!;
        client.UseBearer(tokens.AccessToken);
        var me = (await client.GetFromJsonAsync<UserInfoDto>("/api/auth/me"))!;
        return new Account(client, email, Password, tokens, me.UserId);
    }

    public static void UseBearer(this HttpClient client, string? accessToken) =>
        client.DefaultRequestHeaders.Authorization =
            accessToken is null ? null : new AuthenticationHeaderValue("Bearer", accessToken);

    /// <summary>The <c>error</c> of the API's error shape, or null when the body has none.</summary>
    public static async Task<string?> ErrorAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("error", out var error) ? error.GetString() : null;
    }

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }
}
