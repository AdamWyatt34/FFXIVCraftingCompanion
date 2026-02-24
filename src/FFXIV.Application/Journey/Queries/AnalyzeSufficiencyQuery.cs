using FFXIV.Application.Journey.DTOs;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FFXIV.Application.Journey.Queries;

public sealed record AnalyzeSufficiencyQuery(
    int RecipeId,
    SufficiencyRequestDto Request,
    string? UserId)
    : IRequest<(StatSufficiencyResultDto? Result, string? Error, int StatusCode)>;

public sealed class AnalyzeSufficiencyQueryHandler
    : IRequestHandler<AnalyzeSufficiencyQuery, (StatSufficiencyResultDto? Result, string? Error, int StatusCode)>
{
    private readonly IStatSufficiencyService _sufficiencyService;
    private readonly ICharacterProfileRepository _profileRepo;
    private readonly ILogger<AnalyzeSufficiencyQueryHandler> _logger;

    public AnalyzeSufficiencyQueryHandler(
        IStatSufficiencyService sufficiencyService,
        ICharacterProfileRepository profileRepo,
        ILogger<AnalyzeSufficiencyQueryHandler> logger)
    {
        _sufficiencyService = sufficiencyService;
        _profileRepo = profileRepo;
        _logger = logger;
    }

    public async Task<(StatSufficiencyResultDto? Result, string? Error, int StatusCode)> Handle(
        AnalyzeSufficiencyQuery request, CancellationToken cancellationToken)
    {
        var body = request.Request;

        CrafterStats stats;
        Domain.Entities.CharacterProfile? profile = null;

        if (body.UseProfileStats)
        {
            if (string.IsNullOrEmpty(request.UserId))
                return (null, "Authentication required to use profile stats.", 401);

            profile = await _profileRepo.GetByUserIdAsync(request.UserId, cancellationToken);
            if (profile is null)
                return (null, "No character profile found. Create a profile first.", 404);

            stats = new CrafterStats
            {
                Level = profile.ActiveJobLevel,
                Craftsmanship = profile.Craftsmanship,
                Control = profile.Control,
                CP = profile.CP,
            };
        }
        else
        {
            if (body.Craftsmanship is null || body.Control is null || body.CP is null || body.Level is null)
                return (null, "Provide craftsmanship, control, cp, and level, or set useProfileStats to true.", 400);

            if (body.Level is < 1 or > 100)
                return (null, "Level must be between 1 and 100.", 400);

            if (body.Craftsmanship <= 0 || body.CP <= 0)
                return (null, "Craftsmanship and CP must be positive.", 400);

            stats = new CrafterStats
            {
                Level = body.Level.Value,
                Craftsmanship = body.Craftsmanship.Value,
                Control = body.Control.Value,
                CP = body.CP.Value,
            };
        }

        try
        {
            var result = await _sufficiencyService.AnalyzeAsync(
                request.RecipeId, stats, profile, cancellationToken);

            var dto = JourneyMapper.ToDto(result);
            return (dto, null, 200);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Sufficiency analysis failed for recipe {RecipeId}", request.RecipeId);
            return (null, ex.Message, 404);
        }
    }
}
