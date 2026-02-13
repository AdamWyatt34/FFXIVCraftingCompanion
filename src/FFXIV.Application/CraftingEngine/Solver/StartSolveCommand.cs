using FFXIV.Application.Crafting;
using FFXIV.Application.Crafting.DTOs;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.CraftingEngine.Solver;

public sealed record StartSolveCommand(
    SolveRequestDto Request, string? UserId, string ClientId)
    : IRequest<(SolveJobResponseDto? Enqueued, SolverJobDto? Cached, string? Error, int StatusCode)>;

public sealed class StartSolveCommandHandler
    : IRequestHandler<StartSolveCommand, (SolveJobResponseDto? Enqueued, SolverJobDto? Cached, string? Error, int StatusCode)>
{
    private readonly ISolverJobService _jobService;
    private readonly ISolverCacheService _cacheService;
    private readonly IRecipeParameterFactory _recipeParamFactory;
    private readonly ICharacterProfileRepository _profileRepository;

    private const int MaxConcurrentJobsPerClient = 5;

    public StartSolveCommandHandler(
        ISolverJobService jobService,
        ISolverCacheService cacheService,
        IRecipeParameterFactory recipeParamFactory,
        ICharacterProfileRepository profileRepository)
    {
        _jobService = jobService;
        _cacheService = cacheService;
        _recipeParamFactory = recipeParamFactory;
        _profileRepository = profileRepository;
    }

    public async Task<(SolveJobResponseDto? Enqueued, SolverJobDto? Cached, string? Error, int StatusCode)> Handle(
        StartSolveCommand command, CancellationToken cancellationToken)
    {
        var req = command.Request;

        // -- Validate settings --
        var timeBudget = req.Settings?.TimeBudgetMs ?? 10000;
        if (timeBudget is < 1000 or > 60000)
            return (null, null, "TimeBudgetMs must be between 1000 and 60000.", 400);

        var maxSteps = req.Constraints?.MaxSteps ?? 50;
        if (maxSteps is < 1 or > 100)
            return (null, null, "MaxSteps must be between 1 and 100.", 400);

        if (req.Settings?.Algorithm is not null)
        {
            if (!Enum.TryParse<SolverAlgorithm>(req.Settings.Algorithm, ignoreCase: true, out _))
                return (null, null, $"Invalid algorithm '{req.Settings.Algorithm}'. Valid values: Auto, Greedy, MCTS.", 400);
        }

        // -- Resolve stats --
        CrafterStats stats;
        if (req.UseProfileStats)
        {
            if (string.IsNullOrEmpty(command.UserId))
                return (null, null, "UseProfileStats requires authentication.", 401);

            var profile = await _profileRepository.GetByUserIdAsync(command.UserId, cancellationToken);
            if (profile is null)
                return (null, null, "No character profile found. Create a profile first.", 400);

            stats = new CrafterStats
            {
                Level = profile.ActiveJobLevel,
                Craftsmanship = profile.Craftsmanship,
                Control = profile.Control,
                CP = profile.CP,
                IsSpecialist = false,
            };
        }
        else
        {
            if (req.Stats is null)
                return (null, null, "Either provide Stats or set UseProfileStats to true.", 400);

            if (req.Stats.Level is < 1 or > 100)
                return (null, null, "Level must be between 1 and 100.", 400);

            if (req.Stats.Craftsmanship <= 0 || req.Stats.CP <= 0)
                return (null, null, "Craftsmanship and CP must be positive.", 400);

            if (req.Stats.Control < 0)
                return (null, null, "Control must be non-negative.", 400);

            stats = CraftingMapper.ToDomain(req.Stats);
        }

        // -- Validate recipe exists --
        var recipe = await _recipeParamFactory.CreateAsync(req.RecipeId, cancellationToken);
        if (recipe is null)
            return (null, null, $"Recipe {req.RecipeId} not found.", 400);

        // -- Validate stats meet recipe minimums --
        if (recipe.SuggestedCraftsmanship > 0 && stats.Craftsmanship < recipe.SuggestedCraftsmanship)
            return (null, null,
                $"Craftsmanship {stats.Craftsmanship} is below the recipe minimum of {recipe.SuggestedCraftsmanship}.",
                422);

        if (recipe.SuggestedControl > 0 && stats.Control < recipe.SuggestedControl)
            return (null, null,
                $"Control {stats.Control} is below the recipe minimum of {recipe.SuggestedControl}.",
                422);

        // -- Concurrent job limit --
        var activeCount = _jobService.GetActiveJobCount(command.ClientId);
        if (activeCount >= MaxConcurrentJobsPerClient)
            return (null, null,
                $"Concurrent job limit reached ({MaxConcurrentJobsPerClient}). Cancel existing jobs or wait for completion.",
                429);

        // -- Check cache --
        var constraints = SolverMapper.ToDomain(req.Constraints);
        var cached = await _cacheService.GetCachedResultAsync(
            req.RecipeId, stats, constraints, cancellationToken);

        if (cached is not null)
        {
            var cachedJobDto = new SolverJobDto
            {
                JobId = "cached",
                Status = SolverStatus.Completed.ToString(),
                Result = SolverMapper.ToDto(cached),
                ElapsedMs = 0,
            };
            return (null, cachedJobDto, null, 200);
        }

        // -- Enqueue --
        var domainRequest = SolverMapper.ToDomain(req, stats);
        var jobId = _jobService.EnqueueSolve(domainRequest, command.ClientId);

        var response = SolverMapper.ToEnqueueResponseDto(jobId, SolverStatus.Queued);
        return (response, null, null, 202);
    }
}
