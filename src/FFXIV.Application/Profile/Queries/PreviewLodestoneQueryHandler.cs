using FFXIV.Application.Profile.DTOs;
using FFXIV.Domain.Constants;
using FFXIV.Domain.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFXIV.Application.Profile.Queries;

public sealed class PreviewLodestoneQueryHandler
    : IRequestHandler<PreviewLodestoneQuery, LodestonePreviewDto?>
{
    private readonly ILodestoneService _lodestoneService;
    private readonly ICacheService _cacheService;
    private readonly ILogger<PreviewLodestoneQueryHandler> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(1);

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
        if (!FfxivServers.IsValidServer(request.ServerName))
            return null;

        var cacheKey = $"lodestone:preview:{request.CharacterName.ToLowerInvariant()}:{request.ServerName.ToLowerInvariant()}";

        var cached = await _cacheService.GetAsync<LodestonePreviewDto>(cacheKey, cancellationToken);
        if (cached is not null)
            return cached;

        var data = await _lodestoneService.SearchAndFetchCharacterAsync(
            request.CharacterName, request.ServerName, cancellationToken);

        if (data is null)
            return null;

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

        await _cacheService.SetAsync(cacheKey, preview, CacheTtl, cancellationToken);

        _logger.LogInformation("Cached Lodestone preview for {Name} on {Server}",
            data.CharacterName, data.ServerName);

        return preview;
    }
}
