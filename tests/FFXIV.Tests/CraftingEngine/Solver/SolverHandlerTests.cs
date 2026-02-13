using FFXIV.Application.Crafting.DTOs;
using FFXIV.Application.CraftingEngine;
using FFXIV.Application.CraftingEngine.Solver;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.CraftingEngine.Solver;

// ── StartSolveCommandHandler Tests ──

public class StartSolveCommandHandlerTests
{
    private readonly ISolverJobService _jobService = Substitute.For<ISolverJobService>();
    private readonly ISolverCacheService _cacheService = Substitute.For<ISolverCacheService>();
    private readonly IRecipeParameterFactory _recipeParamFactory = Substitute.For<IRecipeParameterFactory>();
    private readonly ICharacterProfileRepository _profileRepository = Substitute.For<ICharacterProfileRepository>();

    private readonly StartSolveCommandHandler _handler;

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
        SuggestedCraftsmanship = 3000,
        SuggestedControl = 2800,
    };

    public StartSolveCommandHandlerTests()
    {
        _handler = new StartSolveCommandHandler(
            _jobService, _cacheService, _recipeParamFactory, _profileRepository);

        _recipeParamFactory.CreateAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(DefaultRecipe);

        _jobService.EnqueueSolve(Arg.Any<SolveRequest>(), Arg.Any<string?>())
            .Returns("abc123def456");

        _jobService.GetActiveJobCount(Arg.Any<string>())
            .Returns(0);
    }

    private static SolveRequestDto CreateValidRequest() => new()
    {
        RecipeId = 100,
        Stats = new CrafterStatsInput
        {
            Level = 90,
            Craftsmanship = 3800,
            Control = 3600,
            CP = 600,
        },
    };

    private static StartSolveCommand Cmd(SolveRequestDto req, string? userId = null)
        => new(req, userId, userId ?? "127.0.0.1");

    [Fact]
    public async Task Handle_ReturnsBadRequest_WhenTimeBudgetBelowMinimum()
    {
        var req = CreateValidRequest() with
        {
            Settings = new SolverSettingsDto { TimeBudgetMs = 500 },
        };

        var (_, _, error, statusCode) = await _handler.Handle(Cmd(req), CancellationToken.None);

        statusCode.Should().Be(400);
        error.Should().Contain("TimeBudgetMs");
    }

    [Fact]
    public async Task Handle_ReturnsBadRequest_WhenTimeBudgetAboveMaximum()
    {
        var req = CreateValidRequest() with
        {
            Settings = new SolverSettingsDto { TimeBudgetMs = 70000 },
        };

        var (_, _, error, statusCode) = await _handler.Handle(Cmd(req), CancellationToken.None);

        statusCode.Should().Be(400);
        error.Should().Contain("TimeBudgetMs");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Handle_ReturnsBadRequest_WhenMaxStepsOutOfRange(int maxSteps)
    {
        var req = CreateValidRequest() with
        {
            Constraints = new SolverConstraintsDto { MaxSteps = maxSteps },
        };

        var (_, _, error, statusCode) = await _handler.Handle(Cmd(req), CancellationToken.None);

        statusCode.Should().Be(400);
        error.Should().Contain("MaxSteps");
    }

    [Fact]
    public async Task Handle_ReturnsBadRequest_WhenInvalidAlgorithm()
    {
        var req = CreateValidRequest() with
        {
            Settings = new SolverSettingsDto { Algorithm = "InvalidAlgo" },
        };

        var (_, _, error, statusCode) = await _handler.Handle(Cmd(req), CancellationToken.None);

        statusCode.Should().Be(400);
        error.Should().Contain("InvalidAlgo");
    }

    [Fact]
    public async Task Handle_Returns401_WhenUseProfileStatsWithoutAuth()
    {
        var req = CreateValidRequest() with { UseProfileStats = true, Stats = null };

        var (_, _, error, statusCode) = await _handler.Handle(
            new StartSolveCommand(req, null, "127.0.0.1"), CancellationToken.None);

        statusCode.Should().Be(401);
        error.Should().Contain("authentication");
    }

    [Fact]
    public async Task Handle_ReturnsBadRequest_WhenNoStatsProvided()
    {
        var req = CreateValidRequest() with { Stats = null };

        var (_, _, error, statusCode) = await _handler.Handle(Cmd(req), CancellationToken.None);

        statusCode.Should().Be(400);
        error.Should().Contain("Stats");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Handle_ReturnsBadRequest_WhenLevelOutOfRange(int level)
    {
        var req = CreateValidRequest() with
        {
            Stats = new CrafterStatsInput { Level = level, Craftsmanship = 3800, Control = 3600, CP = 600 },
        };

        var (_, _, error, statusCode) = await _handler.Handle(Cmd(req), CancellationToken.None);

        statusCode.Should().Be(400);
        error.Should().Contain("Level");
    }

    [Fact]
    public async Task Handle_ReturnsBadRequest_WhenRecipeNotFound()
    {
        _recipeParamFactory.CreateAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((RecipeParameters?)null);

        var req = CreateValidRequest();

        var (_, _, error, statusCode) = await _handler.Handle(Cmd(req), CancellationToken.None);

        statusCode.Should().Be(400);
        error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_Returns422_WhenCraftsmanshipBelowMinimum()
    {
        var req = CreateValidRequest() with
        {
            Stats = new CrafterStatsInput { Level = 90, Craftsmanship = 100, Control = 3600, CP = 600 },
        };

        var (_, _, error, statusCode) = await _handler.Handle(Cmd(req), CancellationToken.None);

        statusCode.Should().Be(422);
        error.Should().Contain("Craftsmanship");
    }

    [Fact]
    public async Task Handle_Returns422_WhenControlBelowMinimum()
    {
        var req = CreateValidRequest() with
        {
            Stats = new CrafterStatsInput { Level = 90, Craftsmanship = 3800, Control = 100, CP = 600 },
        };

        var (_, _, error, statusCode) = await _handler.Handle(Cmd(req), CancellationToken.None);

        statusCode.Should().Be(422);
        error.Should().Contain("Control");
    }

    [Fact]
    public async Task Handle_Returns429_WhenConcurrentJobLimitReached()
    {
        _jobService.GetActiveJobCount(Arg.Any<string>()).Returns(5);

        var req = CreateValidRequest();

        var (_, _, error, statusCode) = await _handler.Handle(Cmd(req), CancellationToken.None);

        statusCode.Should().Be(429);
        error.Should().Contain("Concurrent");
    }

    [Fact]
    public async Task Handle_Returns200_WhenCacheHit()
    {
        var cachedResult = new SolverResult
        {
            JobId = "cached",
            Status = SolverStatus.Completed,
            ActionIds = [1, 2, 3],
            Metrics = new SolverMetrics
            {
                AlgorithmUsed = SolverAlgorithm.Greedy,
                SolveTimeMs = 100,
                CraftCompleted = true,
            },
        };

        _cacheService.GetCachedResultAsync(
            Arg.Any<int>(), Arg.Any<CrafterStats>(),
            Arg.Any<SolverConstraints>(), Arg.Any<CancellationToken>())
            .Returns(cachedResult);

        var req = CreateValidRequest();

        var (enqueued, cached, error, statusCode) = await _handler.Handle(Cmd(req), CancellationToken.None);

        statusCode.Should().Be(200);
        cached.Should().NotBeNull();
        cached!.Status.Should().Be("Completed");
        enqueued.Should().BeNull();
        error.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Returns202_WhenEnqueued()
    {
        var req = CreateValidRequest();

        var (enqueued, cached, error, statusCode) = await _handler.Handle(Cmd(req), CancellationToken.None);

        statusCode.Should().Be(202);
        enqueued.Should().NotBeNull();
        enqueued!.JobId.Should().Be("abc123def456");
        enqueued.Status.Should().Be("Queued");
        cached.Should().BeNull();
        error.Should().BeNull();

        _jobService.Received(1).EnqueueSolve(Arg.Any<SolveRequest>(), "127.0.0.1");
    }

    [Fact]
    public async Task Handle_ResolvesProfileStats_WhenUseProfileStats()
    {
        var profile = new CharacterProfile
        {
            UserId = "user1",
            CharacterName = "Test",
            ActiveJob = "CRP",
            ActiveJobLevel = 90,
            Craftsmanship = 4000,
            Control = 3800,
            CP = 650,
        };
        _profileRepository.GetByUserIdAsync("user1", Arg.Any<CancellationToken>())
            .Returns(profile);

        var req = new SolveRequestDto
        {
            RecipeId = 100,
            UseProfileStats = true,
        };

        var (enqueued, _, error, statusCode) = await _handler.Handle(
            new StartSolveCommand(req, "user1", "user1"), CancellationToken.None);

        statusCode.Should().Be(202);
        error.Should().BeNull();
        enqueued.Should().NotBeNull();

        _jobService.Received(1).EnqueueSolve(
            Arg.Is<SolveRequest>(r =>
                r.Stats.Craftsmanship == 4000 &&
                r.Stats.Control == 3800 &&
                r.Stats.CP == 650),
            "user1");
    }

    [Fact]
    public async Task Handle_ReturnsBadRequest_WhenProfileNotFound()
    {
        _profileRepository.GetByUserIdAsync("user1", Arg.Any<CancellationToken>())
            .Returns((CharacterProfile?)null);

        var req = new SolveRequestDto
        {
            RecipeId = 100,
            UseProfileStats = true,
        };

        var (_, _, error, statusCode) = await _handler.Handle(
            new StartSolveCommand(req, "user1", "user1"), CancellationToken.None);

        statusCode.Should().Be(400);
        error.Should().Contain("profile");
    }
}

// ── GetSolverJobQueryHandler Tests ──

public class GetSolverJobQueryHandlerTests
{
    private readonly ISolverJobService _jobService = Substitute.For<ISolverJobService>();
    private readonly GetSolverJobQueryHandler _handler;

    public GetSolverJobQueryHandlerTests()
    {
        _handler = new GetSolverJobQueryHandler(_jobService);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenJobNotFound()
    {
        _jobService.GetJob("nonexistent").Returns((SolverJobInfo?)null);

        var result = await _handler.Handle(new GetSolverJobQuery("nonexistent"), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReturnsSolverJobDto_WhenJobFound()
    {
        var info = new SolverJobInfo
        {
            JobId = "abc123",
            Status = SolverStatus.Running,
            Request = new SolveRequest
            {
                RecipeId = 100,
                Stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 3600, CP = 600 },
            },
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
        };
        _jobService.GetJob("abc123").Returns(info);

        var result = await _handler.Handle(new GetSolverJobQuery("abc123"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.JobId.Should().Be("abc123");
        result.Status.Should().Be("Running");
    }

    [Fact]
    public async Task Handle_ComputesElapsedMs_ForRunningJob()
    {
        var info = new SolverJobInfo
        {
            JobId = "abc123",
            Status = SolverStatus.Running,
            Request = new SolveRequest
            {
                RecipeId = 100,
                Stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 3600, CP = 600 },
            },
            CreatedAt = DateTimeOffset.UtcNow.AddSeconds(-2),
        };
        _jobService.GetJob("abc123").Returns(info);

        var result = await _handler.Handle(new GetSolverJobQuery("abc123"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.ElapsedMs.Should().BeGreaterThanOrEqualTo(1500);
    }

    [Fact]
    public async Task Handle_ComputesElapsedMs_ForCompletedJob()
    {
        var createdAt = DateTimeOffset.UtcNow.AddSeconds(-10);
        var completedAt = createdAt.AddMilliseconds(3500);

        var info = new SolverJobInfo
        {
            JobId = "abc123",
            Status = SolverStatus.Completed,
            Request = new SolveRequest
            {
                RecipeId = 100,
                Stats = new CrafterStats { Level = 90, Craftsmanship = 3800, Control = 3600, CP = 600 },
            },
            Result = new SolverResult
            {
                JobId = "abc123",
                Status = SolverStatus.Completed,
                ActionIds = [1, 2, 3],
                Metrics = new SolverMetrics { AlgorithmUsed = SolverAlgorithm.Greedy },
            },
            CreatedAt = createdAt,
            CompletedAt = completedAt,
        };
        _jobService.GetJob("abc123").Returns(info);

        var result = await _handler.Handle(new GetSolverJobQuery("abc123"), CancellationToken.None);

        result.Should().NotBeNull();
        result!.ElapsedMs.Should().Be(3500);
    }
}

// ── CancelSolverJobCommandHandler Tests ──

public class CancelSolverJobCommandHandlerTests
{
    private readonly ISolverJobService _jobService = Substitute.For<ISolverJobService>();
    private readonly CancelSolverJobCommandHandler _handler;

    public CancelSolverJobCommandHandlerTests()
    {
        _handler = new CancelSolverJobCommandHandler(_jobService);
    }

    [Fact]
    public async Task Handle_ReturnsTrue_WhenCancelled()
    {
        _jobService.CancelJob("abc123").Returns(true);

        var result = await _handler.Handle(new CancelSolverJobCommand("abc123"), CancellationToken.None);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsFalse_WhenNotFound()
    {
        _jobService.CancelJob("nonexistent").Returns(false);

        var result = await _handler.Handle(new CancelSolverJobCommand("nonexistent"), CancellationToken.None);

        result.Should().BeFalse();
    }
}

// ── QuickSolveQueryHandler Tests ──

public class QuickSolveQueryHandlerTests
{
    private readonly ISolverFactory _solverFactory = Substitute.For<ISolverFactory>();
    private readonly IRecipeParameterFactory _recipeParamFactory = Substitute.For<IRecipeParameterFactory>();
    private readonly ICraftingSimulator _simulator = Substitute.For<ICraftingSimulator>();
    private readonly QuickSolveQueryHandler _handler;

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

    public QuickSolveQueryHandlerTests()
    {
        _handler = new QuickSolveQueryHandler(_solverFactory, _recipeParamFactory, _simulator);

        _recipeParamFactory.CreateAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(DefaultRecipe);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task Handle_ReturnsError_WhenLevelOutOfRange(int level)
    {
        var (result, error) = await _handler.Handle(
            new QuickSolveQuery(100, 3800, 3600, 600, level), CancellationToken.None);

        result.Should().BeNull();
        error.Should().Contain("Level");
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenRecipeNotFound()
    {
        _recipeParamFactory.CreateAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns((RecipeParameters?)null);

        var (result, error) = await _handler.Handle(
            new QuickSolveQuery(999, 3800, 3600, 600, 90), CancellationToken.None);

        result.Should().BeNull();
        error.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ReturnsSolverResultDto_OnSuccess()
    {
        var solverResult = new SolverResult
        {
            Status = SolverStatus.Completed,
            ActionIds = [1, 2, 3],
            Metrics = new SolverMetrics
            {
                AlgorithmUsed = SolverAlgorithm.Greedy,
                SolveTimeMs = 50,
            },
        };

        var solver = Substitute.For<IRotationSolver>();
        solver.SolveAsync(
            Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
            Arg.Any<SolverConstraints>(), Arg.Any<SolverSettings>(),
            Arg.Any<CancellationToken>())
            .Returns(solverResult);

        _solverFactory.Create(SolverAlgorithm.Greedy).Returns(solver);

        var simResult = new SimulationResult
        {
            Steps = [],
            FinalState = new CraftingState
            {
                Progress = 3500,
                MaxProgress = 3500,
                Quality = 5000,
                MaxQuality = 7200,
                Durability = 10,
                MaxDurability = 70,
                CP = 100,
                MaxCP = 600,
                Status = CraftStatus.Completed,
            },
        };
        _simulator.Simulate(
            Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
            Arg.Any<IReadOnlyList<int>>(), Arg.Any<SimulationOptions?>())
            .Returns(simResult);

        var (result, error) = await _handler.Handle(
            new QuickSolveQuery(100, 3800, 3600, 600, 90), CancellationToken.None);

        error.Should().BeNull();
        result.Should().NotBeNull();
        result!.ActionIds.Should().HaveCount(3);
        result.Metrics.AlgorithmUsed.Should().Be("Greedy");
    }

    [Fact]
    public async Task Handle_UsesGreedyAlgorithm()
    {
        var solver = Substitute.For<IRotationSolver>();
        solver.SolveAsync(
            Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
            Arg.Any<SolverConstraints>(), Arg.Any<SolverSettings>(),
            Arg.Any<CancellationToken>())
            .Returns(new SolverResult
            {
                Status = SolverStatus.Completed,
                ActionIds = [],
                Metrics = new SolverMetrics { AlgorithmUsed = SolverAlgorithm.Greedy },
            });

        _solverFactory.Create(SolverAlgorithm.Greedy).Returns(solver);

        await _handler.Handle(
            new QuickSolveQuery(100, 3800, 3600, 600, 90), CancellationToken.None);

        _solverFactory.Received(1).Create(SolverAlgorithm.Greedy);
    }
}
