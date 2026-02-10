namespace FFXIV.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "FFXIVCraftingCompanion";
    public string Audience { get; set; } = "FFXIVCraftingCompanion";
    public string SecretKey { get; set; } = string.Empty;
    public int AccessTokenLifetimeMinutes { get; set; } = 60;
    public int RefreshTokenLifetimeDays { get; set; } = 30;
}
