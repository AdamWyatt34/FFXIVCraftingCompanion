using System.Collections.Concurrent;
using System.Diagnostics;
using FFXIV.Application.CraftingEngine;
using FFXIV.Application.CraftingEngine.Solver;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FFXIV.Infrastructure.Solver;

public sealed class SolverJobService : ISolverJobService
{
    private readonly ConcurrentDictionary<string, SolverJob> _jobs = new();
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISolverFactory _solverFactory;
    private readonly ISolverCacheService _cacheService;
    private readonly ICraftingSimulator _simulator;
    private readonly ILogger<SolverJobService> _logger;

    private const int HardTimeoutBufferMs = 5000;
    private const int MaxCompletedJobs = 100;
    private static readonly TimeSpan CompletedJobRetention = TimeSpan.FromHours(1);

    public SolverJobService(
        IServiceScopeFactory scopeFactory,
        ISolverFactory solverFactory,
        ISolverCacheService cacheService,
        ICraftingSimulator simulator,
        ILogger<SolverJobService> logger)
    {
        _scopeFactory = scopeFactory;
        _solverFactory = solverFactory;
        _cacheService = cacheService;
        _simulator = simulator;
        _logger = logger;
        _concurrencyLimiter = new SemaphoreSlim(
            Environment.ProcessorCount, Environment.ProcessorCount);
    }

    public string EnqueueSolve(SolveRequest request, string? clientId = null)
    {
        var job = new SolverJob { Request = request, ClientId = clientId };
        _jobs[job.JobId] = job;

        _ = Task.Run(() => ExecuteJobAsync(job));

        CleanupOldJobs();

        return job.JobId;
    }

    public SolverJobInfo? GetJob(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            return null;

        return new SolverJobInfo
        {
            JobId = job.JobId,
            Status = job.Status,
            Request = job.Request,
            Result = job.Result,
            CreatedAt = job.CreatedAt,
            CompletedAt = job.CompletedAt,
        };
    }

    public int GetActiveJobCount(string clientId)
    {
        return _jobs.Values.Count(j =>
            j.ClientId == clientId &&
            j.Status is SolverStatus.Queued or SolverStatus.Running);
    }

    public bool CancelJob(string jobId)
    {
        if (!_jobs.TryGetValue(jobId, out var job))
            return false;

        var status = job.Status;
        if (status is not (SolverStatus.Queued or SolverStatus.Running))
            return false;

        job.Cts.Cancel();
        job.Result = new SolverResult
        {
            JobId = job.JobId,
            Status = SolverStatus.Failed,
            ErrorMessage = "Job cancelled by user.",
        };
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.Status = SolverStatus.Failed;
        return true;
    }

    private async Task ExecuteJobAsync(SolverJob job)
    {
        try
        {
            await _concurrencyLimiter.WaitAsync(job.Cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Cancelled while waiting in queue
            job.Result = new SolverResult
            {
                JobId = job.JobId,
                Status = SolverStatus.Failed,
                ErrorMessage = "Job cancelled while queued.",
            };
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.Status = SolverStatus.Failed;
            return;
        }

        try
        {
            job.Status = SolverStatus.Running;

            using var scope = _scopeFactory.CreateScope();
            var recipeParamFactory = scope.ServiceProvider
                .GetRequiredService<IRecipeParameterFactory>();

            var recipe = await recipeParamFactory.CreateAsync(
                job.Request.RecipeId, job.Cts.Token);

            if (recipe is null)
            {
                SetFailed(job, $"Recipe {job.Request.RecipeId} not found.");
                return;
            }

            // Apply HQ ingredient starting quality
            if (job.Request.HQIngredients is { Count: > 0 })
            {
                var startingQuality = HQCalculator.CalculateStartingQuality(
                    recipe.Quality, recipe.MaterialQualityFactor,
                    recipe.Ingredients, job.Request.HQIngredients);

                if (startingQuality > 0)
                    recipe = recipe with { StartingQuality = startingQuality };
            }

            // Check cache first
            var cached = await _cacheService.GetCachedResultAsync(
                job.Request.RecipeId, job.Request.Stats,
                job.Request.Constraints, job.Cts.Token);

            if (cached is not null)
            {
                _logger.LogInformation("Solver cache hit for recipe {RecipeId}", job.Request.RecipeId);
                job.Result = cached with { JobId = job.JobId };
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.Status = SolverStatus.Completed;
                return;
            }

            // Adapt time budget based on recipe difficulty
            var adaptedSettings = _solverFactory.AdaptSettings(
                job.Request.Settings, recipe, job.Request.Stats);

            // Set up hard timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(job.Cts.Token);
            timeoutCts.CancelAfter(adaptedSettings.TimeBudgetMs + HardTimeoutBufferMs);

            var algorithm = _solverFactory.ResolveAlgorithm(
                adaptedSettings.Algorithm, job.Request.Constraints);
            var solver = _solverFactory.Create(algorithm);

            var sw = Stopwatch.StartNew();
            var result = await solver.SolveAsync(
                recipe, job.Request.Stats,
                job.Request.Constraints, adaptedSettings,
                timeoutCts.Token);
            sw.Stop();

            // If solver returned a successful result, validate via simulation and generate macros
            if (result.Status == SolverStatus.Completed && result.ActionIds.Count > 0)
            {
                var simulation = _simulator.Simulate(
                    recipe, job.Request.Stats, result.ActionIds);
                var macros = MacroExporter.ExportToMacro(result.ActionIds);

                result = result with
                {
                    JobId = job.JobId,
                    Simulation = simulation,
                    Macros = macros,
                    Metrics = result.Metrics with
                    {
                        SolveTimeMs = sw.ElapsedMilliseconds,
                        TotalSteps = result.ActionIds.Count,
                        FinalQualityPercent = simulation.FinalQualityPercent,
                        FinalHQPercent = simulation.FinalHQPercent,
                        CraftCompleted = simulation.Success,
                    },
                };

                // Cache successful results
                await _cacheService.CacheResultAsync(
                    job.Request.RecipeId, job.Request.Stats,
                    job.Request.Constraints, result, job.Cts.Token);
            }
            else
            {
                result = result with
                {
                    JobId = job.JobId,
                    Metrics = result.Metrics with { SolveTimeMs = sw.ElapsedMilliseconds },
                };
            }

            // Set Result/CompletedAt BEFORE Status to avoid read races
            job.Result = result;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.Status = result.Status;
        }
        catch (OperationCanceledException)
        {
            var status = job.Cts.IsCancellationRequested
                ? SolverStatus.Failed
                : SolverStatus.TimedOut;
            var message = status == SolverStatus.Failed
                ? "Job cancelled."
                : "Solve timed out.";

            job.Result = new SolverResult
            {
                JobId = job.JobId,
                Status = status,
                ErrorMessage = message,
            };
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.Status = status;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Solver job {JobId} failed", job.JobId);
            SetFailed(job, ex.Message);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }

    private static void SetFailed(SolverJob job, string errorMessage)
    {
        job.Result = new SolverResult
        {
            JobId = job.JobId,
            Status = SolverStatus.Failed,
            ErrorMessage = errorMessage,
        };
        job.CompletedAt = DateTimeOffset.UtcNow;
        job.Status = SolverStatus.Failed;
    }

    private void CleanupOldJobs()
    {
        var cutoff = DateTimeOffset.UtcNow - CompletedJobRetention;
        var completedJobs = _jobs.Values
            .Where(j => j.CompletedAt.HasValue && j.CompletedAt < cutoff)
            .OrderBy(j => j.CompletedAt)
            .ToList();

        foreach (var old in completedJobs)
        {
            _jobs.TryRemove(old.JobId, out _);
            old.Cts.Dispose();
        }

        // If still over the limit, remove oldest completed
        var remaining = _jobs.Values
            .Where(j => j.CompletedAt.HasValue)
            .OrderBy(j => j.CompletedAt)
            .ToList();

        while (remaining.Count > MaxCompletedJobs)
        {
            var oldest = remaining[0];
            remaining.RemoveAt(0);
            _jobs.TryRemove(oldest.JobId, out _);
            oldest.Cts.Dispose();
        }
    }
}
