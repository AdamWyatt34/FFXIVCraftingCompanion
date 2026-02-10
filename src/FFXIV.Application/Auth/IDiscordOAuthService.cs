namespace FFXIV.Application.Auth;

public interface IDiscordOAuthService
{
    string GetAuthorizationUrl(string state);
    Task<DiscordUserInfo> ExchangeCodeAsync(string code, CancellationToken ct = default);
}
