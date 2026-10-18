using System.Text.RegularExpressions;
using FFXIV.Application.Profile.DTOs;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFXIV.Application.Profile.Queries;

public sealed partial class PreviewLodestoneQueryHandler
    : IRequestHandler<PreviewLodestoneQuery, LodestonePreviewDto?>
{
    private readonly ILodestoneService _lodestoneService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<PreviewLodestoneQueryHandler> _logger;

    internal static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// A miss is remembered too: the endpoint is anonymous, and every uncached miss is a Lodestone
    /// round trip, so a scripted caller cycling through names would otherwise make the API hammer
    /// Square Enix on their behalf.
    /// </summary>
    internal static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromMinutes(10);

    public PreviewLodestoneQueryHandler(
        ILodestoneService lodestoneService,
        ICacheService cacheService,
        ILogger<PreviewLodestoneQueryHandler> logger)
    {
        _lodestoneService = lodestoneService;
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<LodestonePreviewDto?> Handle(
        PreviewLodestoneQuery request, CancellationToken cancellationToken)
    {
        if (!IsPlausibleCharacterName(request.CharacterName) || !FfxivServers.IsValidServer(request.ServerName))
            return null;

        var cacheKey = $"lodestone:preview:{request.CharacterName.ToLowerInvariant()}:{request.ServerName.ToLowerInvariant()}";

        var cached = await _cacheService.GetAsync<CachedPreview>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached.Preview;

        var data = await _lodestoneService.SearchAndFetchCharacterAsync(
            request.CharacterName, request.ServerName, cancellationToken);

        if (data is null)
        {
            await _cacheService.SetAsync(cacheKey, new CachedPreview(null), NegativeCacheTtl, cancellationToken);
            return null;
        }

        var preview = new LodestonePreviewDto(
            CharacterName: data.CharacterName,
            AvatarUrl: data.AvatarUrl,
            ServerName: data.ServerName,
            DataCenter: data.DataCenter ?? FfxivServers.GetDataCenter(data.ServerName),
            ActiveJob: data.ActiveJobAbbreviation,
            ActiveJobDisplayName: data.ActiveJobAbbreviation is not null
                ? JobClassification.GetJobDisplayName(data.ActiveJobAbbreviation)
                : null,
            ActiveJobLevel: data.ActiveJobLevel,
            JobLevels: ProfileMapper.ToJobLevelDtos(data.JobLevels));

        await _cacheService.SetAsync(cacheKey, new CachedPreview(preview), CacheTtl, cancellationToken);

        _logger.LogInformation("Cached Lodestone preview for {Name} on {Server}",
            data.CharacterName, data.ServerName);

        return preview;
    }

    /// <summary>
    /// Lodestone names are a forename and a surname of letters, apostrophes and hyphens. Anything
    /// else cannot match a character, so it never reaches the Lodestone.
    /// </summary>
    internal static bool IsPlausibleCharacterName(string name) =>
        name.Length <= ProfileLimits.MaxCharacterNameLength && CharacterNamePattern().IsMatch(name);

    [GeneratedRegex(@"^[A-Za-z][A-Za-z'\-]*(?: [A-Za-z][A-Za-z'\-]*)?$")]
    private static partial Regex CharacterNamePattern();

    /// <summary>Cache envelope so a miss can be stored (the cache cannot hold null).</summary>
    public sealed record CachedPreview(LodestonePreviewDto? Preview);
}
