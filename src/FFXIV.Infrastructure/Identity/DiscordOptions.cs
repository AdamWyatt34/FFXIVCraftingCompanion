namespace FFXIV.Infrastructure.Identity;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = "http://localhost:3000/auth/discord/callback";
}
