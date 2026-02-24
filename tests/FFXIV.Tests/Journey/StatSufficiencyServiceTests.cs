using FFXIV.Application.CraftingEngine;
using FFXIV.Application.CraftingEngine.Solver;
using FFXIV.Application.Gear;
using FFXIV.Application.Journey;
using FFXIV.Domain.CraftingEngine;
using FFXIV.Domain.CraftingEngine.Models;
using FFXIV.Domain.CraftingEngine.Solver;
using FFXIV.Domain.CraftingEngine.Solver.Models;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;
using FFXIV.Tests.Gear;
using FFXIV.Tests.Recipes;

namespace FFXIV.Tests.Journey;

public class StatSufficiencyServiceTests
{
    private readonly IRecipeRepository _recipeRepo = Substitute.For<IRecipeRepository>();
    private readonly IRecipeParameterFactory _recipeParamFactory = Substitute.For<IRecipeParameterFactory>();
    private readonly ISolverFactory _solverFactory = Substitute.For<ISolverFactory>();
    private readonly ICraftingSimulator _simulator = Substitute.For<ICraftingSimulator>();
    private readonly IGearRecommendationService _gearService = Substitute.For<IGearRecommendationService>();
    private readonly IEquipmentRepository _equipmentRepo = Substitute.For<IEquipmentRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly StatSufficiencyService _service;

    private static readonly Recipe TestRecipe = TestDataBuilder.CreateTestSwordRecipe();
    private static readonly RecipeParameters TestRecipeParams = new()
    {
        RecipeId = 1,
        RecipeLevel = 50,
        Difficulty = 1000,
        Quality = 2000,
        Durability = 80,
        ProgressDivider = 100,
        ProgressModifier = 100,
        QualityDivider = 100,
        QualityModifier = 100,
        ClassJobLevel = 50,
        SuggestedCraftsmanship = 400,
        SuggestedControl = 350,
    };

    public StatSufficiencyServiceTests()
    {
        _recipeRepo.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(TestRecipe);
        _recipeParamFactory.CreateAsync(1, Arg.Any<CancellationToken>()).Returns(TestRecipeParams);

        _service = new StatSufficiencyService(
            _recipeRepo, _recipeParamFactory, _solverFactory,
            _simulator, _gearService, _equipmentRepo, _cache);
    }

    [Fact]
    public async Task OvergearedStats_ReturnsStatsExcellent()
    {
        // Arrange: stats well above requirements
        var stats = new CrafterStats { Level = 90, Craftsmanship = 4000, Control = 3800, CP = 600 };

        var solver = Substitute.For<IRotationSolver>();
        _solverFactory.Create(SolverAlgorithm.Greedy).Returns(solver);

        var solverResult = new SolverResult
        {
            Status = SolverStatus.Completed,
            ActionIds = [1, 2, 3],
        };
        solver.SolveAsync(Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
            Arg.Any<SolverConstraints>(), Arg.Any<SolverSettings>(), Arg.Any<CancellationToken>())
            .Returns(solverResult);

        var simResult = new SimulationResult
        {
            FinalState = new CraftingState
            {
                Progress = 1000,
                MaxProgress = 1000,
                Quality = 1900,
                MaxQuality = 2000,
            },
            Steps = [
                new StepResult { StepNumber = 1, ActionName = "A", StateAfter = new CraftingState() },
                new StepResult { StepNumber = 2, ActionName = "B", StateAfter = new CraftingState() },
                new StepResult { StepNumber = 3, ActionName = "C", StateAfter = new CraftingState() },
            ],
        };
        _simulator.Simulate(Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
            Arg.Any<IReadOnlyList<int>>(), Arg.Any<SimulationOptions>())
            .Returns(simResult);

        // Act
        var result = await _service.AnalyzeAsync(1, stats);

        // Assert
        result.MeetsMinimumRequirements.Should().BeTrue();
        result.CraftsmanshipSufficient.Should().BeTrue();
        result.ControlSufficient.Should().BeTrue();
        result.LevelSufficient.Should().BeTrue();
        result.CraftsmanshipDeficit.Should().Be(0);
        result.ControlDeficit.Should().Be(0);
        result.CanComplete.Should().BeTrue();
        result.Recommendation.Should().Be(StatRecommendation.StatsExcellent);
    }

    [Fact]
    public async Task UndergearedStats_ReturnsUpgradeGear()
    {
        // Arrange: stats below requirements
        var stats = new CrafterStats { Level = 50, Craftsmanship = 300, Control = 200, CP = 400 };

        // Act
        var result = await _service.AnalyzeAsync(1, stats);

        // Assert
        result.MeetsMinimumRequirements.Should().BeFalse();
        result.CraftsmanshipSufficient.Should().BeFalse();
        result.ControlSufficient.Should().BeFalse();
        result.CraftsmanshipDeficit.Should().Be(100); // 400 - 300
        result.ControlDeficit.Should().Be(150);         // 350 - 200
        result.Recommendation.Should().Be(StatRecommendation.UpgradeGear);
    }

    [Fact]
    public async Task MeetsMinButLowQuality_ReturnsStatsAdequate()
    {
        // Arrange: meets minimums but low HQ
        var stats = new CrafterStats { Level = 50, Craftsmanship = 420, Control = 360, CP = 300 };

        var solver = Substitute.For<IRotationSolver>();
        _solverFactory.Create(SolverAlgorithm.Greedy).Returns(solver);

        var solverResult = new SolverResult
        {
            Status = SolverStatus.Completed,
            ActionIds = [1, 2, 3, 4, 5],
        };
        solver.SolveAsync(Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
            Arg.Any<SolverConstraints>(), Arg.Any<SolverSettings>(), Arg.Any<CancellationToken>())
            .Returns(solverResult);

        var simResult = new SimulationResult
        {
            FinalState = new CraftingState
            {
                Progress = 1000,
                MaxProgress = 1000,
                Quality = 800,
                MaxQuality = 2000,
            },
            Steps = Enumerable.Range(1, 5).Select(i =>
                new StepResult { StepNumber = i, ActionName = $"A{i}", StateAfter = new CraftingState() }).ToList(),
        };
        _simulator.Simulate(Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
            Arg.Any<IReadOnlyList<int>>(), Arg.Any<SimulationOptions>())
            .Returns(simResult);

        // Act
        var result = await _service.AnalyzeAsync(1, stats);

        // Assert
        result.MeetsMinimumRequirements.Should().BeTrue();
        result.CanComplete.Should().BeTrue();
        result.Recommendation.Should().Be(StatRecommendation.StatsAdequate);
    }

    [Fact]
    public async Task SolverNotCalled_WhenBelowMinimums()
    {
        // Arrange
        var stats = new CrafterStats { Level = 30, Craftsmanship = 200, Control = 150, CP = 200 };

        // Act
        await _service.AnalyzeAsync(1, stats);

        // Assert: solver should never be created
        _solverFactory.DidNotReceive().Create(Arg.Any<SolverAlgorithm>());
    }

    [Fact]
    public async Task GearSuggestions_WithProfile_ReturnsUpgrades()
    {
        // Arrange
        var stats = new CrafterStats { Level = 90, Craftsmanship = 3000, Control = 2800, CP = 500 };

        var solver = Substitute.For<IRotationSolver>();
        _solverFactory.Create(SolverAlgorithm.Greedy).Returns(solver);
        solver.SolveAsync(Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
            Arg.Any<SolverConstraints>(), Arg.Any<SolverSettings>(), Arg.Any<CancellationToken>())
            .Returns(new SolverResult
            {
                Status = SolverStatus.Completed,
                ActionIds = [1],
            });
        _simulator.Simulate(Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
            Arg.Any<IReadOnlyList<int>>(), Arg.Any<SimulationOptions>())
            .Returns(new SimulationResult
            {
                FinalState = new CraftingState { Progress = 1000, MaxProgress = 1000, Quality = 1800, MaxQuality = 2000 },
                Steps = [new StepResult { StepNumber = 1, ActionName = "A", StateAfter = new CraftingState() }],
            });

        var profile = new CharacterProfile
        {
            Id = 1,
            UserId = "user1",
            CharacterName = "Test",
            ActiveJob = "BSM",
            ActiveJobLevel = 90,
            Craftsmanship = 3000,
            Control = 2800,
            CP = 500,
            CurrentGear = new Dictionary<string, int> { ["Head"] = 10 },
        };

        var currentHead = EquipmentTestDataBuilder.CreateDoHEquipment(
            10, "Old Hat", EquipSlot.Head, craftsmanship: 80, control: 70, cp: 5);
        _equipmentRepo.GetByIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Equipment> { currentHead }.AsReadOnly());

        var betterHead = EquipmentTestDataBuilder.CreateDoHEquipment(
            20, "Better Hat", EquipSlot.Head, craftsmanship: 120, control: 100, cp: 10);
        var recommendedSet = new GearSet
        {
            Job = "BSM",
            Level = 90,
            Slots =
            [
                new GearSlotRecommendation
                {
                    Slot = EquipSlot.Head,
                    SlotLabel = "Head",
                    RecommendedItem = betterHead,
                    Alternatives = [],
                    Source = new EquipmentSourceInfo
                    {
                        SourceType = ItemSourceType.Crafted,
                        RecipeId = 99,
                        Description = "Crafted",
                    },
                    WeightedScore = 200,
                },
            ],
            TotalStats = new GearStats(),
        };
        _gearService.GetRecommendationsAsync("BSM", 90, "balanced", Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(recommendedSet);

        // Act
        var result = await _service.AnalyzeAsync(1, stats, profile);

        // Assert
        result.SuggestedGearUpgrades.Should().HaveCount(1);
        var upgrade = result.SuggestedGearUpgrades[0];
        upgrade.Slot.Should().Be("Head");
        upgrade.CurrentItem.Should().Be("Old Hat");
        upgrade.SuggestedItem.Should().Be("Better Hat");
        upgrade.CraftsmanshipGain.Should().Be(40);
        upgrade.ControlGain.Should().Be(30);
        upgrade.CPGain.Should().Be(5);
        upgrade.RecipeId.Should().Be(99);
    }

    [Fact]
    public async Task GearSuggestions_WithoutProfile_ReturnsEmpty()
    {
        // Arrange
        var stats = new CrafterStats { Level = 90, Craftsmanship = 4000, Control = 3800, CP = 600 };

        var solver = Substitute.For<IRotationSolver>();
        _solverFactory.Create(SolverAlgorithm.Greedy).Returns(solver);
        solver.SolveAsync(Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
            Arg.Any<SolverConstraints>(), Arg.Any<SolverSettings>(), Arg.Any<CancellationToken>())
            .Returns(new SolverResult { Status = SolverStatus.Completed, ActionIds = [1] });
        _simulator.Simulate(Arg.Any<RecipeParameters>(), Arg.Any<CrafterStats>(),
            Arg.Any<IReadOnlyList<int>>(), Arg.Any<SimulationOptions>())
            .Returns(new SimulationResult
            {
                FinalState = new CraftingState { Progress = 1000, MaxProgress = 1000, Quality = 1900, MaxQuality = 2000 },
                Steps = [new StepResult { StepNumber = 1, ActionName = "A", StateAfter = new CraftingState() }],
            });

        // Act — no profile passed
        var result = await _service.AnalyzeAsync(1, stats);

        // Assert
        result.SuggestedGearUpgrades.Should().BeEmpty();
    }
}
