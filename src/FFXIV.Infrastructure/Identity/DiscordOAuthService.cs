using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using FFXIV.Application.Auth;
using Microsoft.Extensions.Options;

namespace FFXIV.Infrastructure.Identity;

public class DiscordOAuthService : IDiscordOAuthService
{
    private readonly HttpClient _httpClient;
    private readonly DiscordOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public DiscordOAuthService(HttpClient httpClient, IOptions<DiscordOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public string GetAuthorizationUrl(string state)
    {
        var redirectUri = Uri.EscapeDataString(_options.RedirectUri);
        return $"https://discord.com/api/oauth2/authorize" +
               $"?client_id={_options.ClientId}" +
               $"&redirect_uri={redirectUri}" +
               $"&response_type=code" +
               $"&scope=identify%20email" +
               $"&state={Uri.EscapeDataString(state)}";
    }

    public async Task<DiscordUserInfo> ExchangeCodeAsync(string code, CancellationToken ct = default)
    {
        var tokenResponse = await ExchangeCodeForTokenAsync(code, ct);
        return await GetUserInfoAsync(tokenResponse.AccessToken, ct);
    }

    private async Task<DiscordTokenResponse> ExchangeCodeForTokenAsync(string code, CancellationToken ct)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri
        });

        var response = await _httpClient.PostAsync("oauth2/token", content, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<DiscordTokenResponse>(json, JsonOptions)
               ?? throw new InvalidOperationException("Failed to deserialize Discord token response.");
    }

    private async Task<DiscordUserInfo> GetUserInfoAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "users/@me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var discordUser = JsonSerializer.Deserialize<DiscordUserResponse>(json, JsonOptions)
                          ?? throw new InvalidOperationException("Failed to deserialize Discord user response.");

        var avatarUrl = !string.IsNullOrEmpty(discordUser.Avatar)
            ? $"https://cdn.discordapp.com/avatars/{discordUser.Id}/{discordUser.Avatar}.png"
            : null;

        return new DiscordUserInfo(
            DiscordId: discordUser.Id,
            Username: discordUser.Username,
            Email: discordUser.Email ?? string.Empty,
            AvatarUrl: avatarUrl);
    }

    private sealed class DiscordTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
    }

    private sealed class DiscordUserResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("username")]
        public string Username { get; set; } = string.Empty;

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("avatar")]
        public string? Avatar { get; set; }
    }
}
