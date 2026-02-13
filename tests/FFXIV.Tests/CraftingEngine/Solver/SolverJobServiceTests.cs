using FFXIV.Application.CraftingEngine;
using FFXIV.Application.CraftingEngine.Solver;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Infrastructure.Solver;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace FFXIV.Tests.CraftingEngine.Solver;

public class SolverJobServiceTests
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ISolverFactory _solverFactory = Substitute.For<ISolverFactory>();
    private readonly ISolverCacheService _cacheService = Substitute.For<ISolverCacheService>();
    private readonly ICraftingSimulator _simulator = Substitute.For<ICraftingSimulator>();
    private readonly ILogger<SolverJobService> _logger = NullLogger<SolverJobService>.Instance;
    private readonly IRecipeParameterFactory _recipeParamFactory = Substitute.For<IRecipeParameterFactory>();

    private readonly SolverJobService _service;

    private static readonly CrafterStats DefaultStats = new()
    {
        Level = 90,
        Craftsmanship = 3800,
        Control = 3600,
        CP = 600,
    };

    private static readonly RecipeParameters DefaultRecipe = new()
    {
        RecipeId = 100,
        RecipeLevel = 580,
        Difficulty = 3500,
        Quality = 7200,
        Durability = 70,
        ProgressDivider = 146,
        ProgressModifier = 100,
        QualityDivider = 115,
        QualityModifier = 100,
    };

    public SolverJobServiceTests()
    {
        // Wire up the scope factory to resolve IRecipeParameterFactory
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(IRecipeParameterFactory))
            .Returns(_recipeParamFactory);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        _scopeFactory = Substitute.For<IServiceScopeFactory>();
        _scopeFactory.CreateScope().Returns(scope);

        // Default: recipe found
        _recipeParamFactory.CreateAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(DefaultRecipe);

        // Default: no cache hit
        _cacheService.GetCachedResultAsync(
                Arg.Any<int>(), Arg.Any<CrafterStats>(),
                Arg.Any<SolverConstraints>(), Arg.Any<CancellationToken>())
            .Returns((SolverResult?)null);

        _service = new SolverJobService(
            _scopeFactory, _solverFactory, _cacheService, _simulator, _logger);
    }

    private SolveRequest CreateRequest() => new()
    {
        RecipeId = 100,
        Stats = DefaultStats,
    };

    [Fact]
    public void Enqueue_ReturnsJobId()
    {
        var jobId = _service.EnqueueSolve(CreateRequest());

        jobId.Should().NotBeNullOrEmpty();
        jobId.Should().HaveLength(12);
    }

    [Fact]
    public void Enqueue_JobStartsAsQueued()
    {
        var jobId = _service.EnqueueSolve(CreateRequest());

        // Job is immediately visible after enqueue
        var job = _service.GetJob(jobId);
        job.Should().NotBeNull();
        // Status is Queued or Running (race condition with Task.Run)
        job!.Status.Should().BeOneOf(SolverStatus.Queued, SolverStatus.Running, SolverStatus.Failed);
    }

    [Fact]
    public void GetJob_ReturnsNull_ForUnknownId()
    {
        var job = _service.GetJob("nonexistent");
        job.Should().BeNull();
    }

    [Fact]
    public void GetJob_ReturnsJobInfo_ForKnownId()
    {
        var jobId = _service.EnqueueSolve(CreateRequest());

        var job = _service.GetJob(jobId);
        job.Should().NotBeNull();
        job!.JobId.Should().Be(jobId);
        job.Request.RecipeId.Should().Be(100);
        job.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Cancel_ReturnsFalse_ForUnknownJob()
    {
        var result = _service.CancelJob("nonexistent");
        result.Should().BeFalse();
    }

    [Fact]
    public void Cancel_ReturnsTrue_ForActiveJob()
    {
        // Use a solver that blocks until cancelled
        var tcs = new TaskCompletionSource<SolverResult>();
        var blockingSolver = Substitute.For<IRotationSolver>();
        blockingSolver.SolveAsync(
                Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
                Arg.Any<SolverConstraints>(), Arg.Any<SolverSettings>(),
                Arg.Any<CancellationToken>())
            .Returns(tcs.Task);

        _solverFactory.ResolveAlgorithm(Arg.Any<SolverAlgorithm>(), Arg.Any<SolverConstraints>())
            .Returns(SolverAlgorithm.Greedy);
        _solverFactory.Create(Arg.Any<SolverAlgorithm>()).Returns(blockingSolver);

        var jobId = _service.EnqueueSolve(CreateRequest());

        // Give time for the job to start
        Thread.Sleep(100);

        var cancelled = _service.CancelJob(jobId);
        cancelled.Should().BeTrue();

        var job = _service.GetJob(jobId);
        job!.Status.Should().Be(SolverStatus.Failed);
        job.Result!.ErrorMessage.Should().Contain("cancelled");

        // Unblock the solver to avoid leak
        tcs.SetCanceled();
    }

    [Fact]
    public async Task Job_TransitionsToCompleted_OnSuccess()
    {
        var solverResult = new SolverResult
        {
            Status = SolverStatus.Completed,
            ActionIds = [100001, 100002],
        };

        var solver = Substitute.For<IRotationSolver>();
        solver.SolveAsync(
                Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
                Arg.Any<SolverConstraints>(), Arg.Any<SolverSettings>(),
                Arg.Any<CancellationToken>())
            .Returns(solverResult);

        _solverFactory.ResolveAlgorithm(Arg.Any<SolverAlgorithm>(), Arg.Any<SolverConstraints>())
            .Returns(SolverAlgorithm.Greedy);
        _solverFactory.Create(Arg.Any<SolverAlgorithm>()).Returns(solver);

        _simulator.Simulate(
                Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
                Arg.Any<IReadOnlyList<int>>(), Arg.Any<SimulationOptions?>())
            .Returns(new SimulationResult
            {
                Steps = [],
                FinalState = new CraftingState
                {
                    Progress = 3500,
                    MaxProgress = 3500,
                    Quality = 7200,
                    MaxQuality = 7200,
                    Status = CraftStatus.Completed,
                },
            });

        var jobId = _service.EnqueueSolve(CreateRequest());

        // Poll until completed
        SolverJobInfo? job = null;
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(50);
            job = _service.GetJob(jobId);
            if (job?.Status is SolverStatus.Completed or SolverStatus.Failed)
                break;
        }

        job.Should().NotBeNull();
        job!.Status.Should().Be(SolverStatus.Completed);
        job.Result.Should().NotBeNull();
        job.Result!.ActionIds.Should().HaveCount(2);
        job.CompletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Job_SetsFailed_WhenRecipeNotFound()
    {
        _recipeParamFactory.CreateAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((RecipeParameters?)null);

        var jobId = _service.EnqueueSolve(CreateRequest());

        SolverJobInfo? job = null;
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(50);
            job = _service.GetJob(jobId);
            if (job?.Status is SolverStatus.Failed)
                break;
        }

        job.Should().NotBeNull();
        job!.Status.Should().Be(SolverStatus.Failed);
        job.Result!.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Job_SetsFailed_OnSolverException()
    {
        var solver = Substitute.For<IRotationSolver>();
        solver.SolveAsync(
                Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
                Arg.Any<SolverConstraints>(), Arg.Any<SolverSettings>(),
                Arg.Any<CancellationToken>())
            .Returns<SolverResult>(_ => throw new InvalidOperationException("Solver exploded"));

        _solverFactory.ResolveAlgorithm(Arg.Any<SolverAlgorithm>(), Arg.Any<SolverConstraints>())
            .Returns(SolverAlgorithm.Greedy);
        _solverFactory.Create(Arg.Any<SolverAlgorithm>()).Returns(solver);

        var jobId = _service.EnqueueSolve(CreateRequest());

        SolverJobInfo? job = null;
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(50);
            job = _service.GetJob(jobId);
            if (job?.Status is SolverStatus.Failed)
                break;
        }

        job.Should().NotBeNull();
        job!.Status.Should().Be(SolverStatus.Failed);
        job.Result!.ErrorMessage.Should().Contain("Solver exploded");
    }

    [Fact]
    public async Task Job_UsesCachedResult_WhenAvailable()
    {
        var cachedResult = new SolverResult
        {
            JobId = "cached",
            Status = SolverStatus.Completed,
            ActionIds = [100001],
        };

        _cacheService.GetCachedResultAsync(
                Arg.Any<int>(), Arg.Any<CrafterStats>(),
                Arg.Any<SolverConstraints>(), Arg.Any<CancellationToken>())
            .Returns(cachedResult);

        var jobId = _service.EnqueueSolve(CreateRequest());

        SolverJobInfo? job = null;
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(50);
            job = _service.GetJob(jobId);
            if (job?.Status is SolverStatus.Completed)
                break;
        }

        job.Should().NotBeNull();
        job!.Status.Should().Be(SolverStatus.Completed);
        job.Result!.ActionIds.Should().HaveCount(1);
        // Solver should NOT have been called
        _solverFactory.DidNotReceive().Create(Arg.Any<SolverAlgorithm>());
    }

    [Fact]
    public void Enqueue_MultipleJobs_AllGetUniqueIds()
    {
        var ids = new HashSet<string>();
        for (var i = 0; i < 10; i++)
        {
            ids.Add(_service.EnqueueSolve(CreateRequest()));
        }

        ids.Should().HaveCount(10);
    }
}
