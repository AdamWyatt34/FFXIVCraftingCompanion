using FFXIV.Application.Rotations.Commands;
using FFXIV.Application.Rotations.DTOs;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using FFXIV.Tests.Recipes;

namespace FFXIV.Tests.Rotations;

public sealed class CreateRotationCommandHandlerTests
{
    private readonly ISharedRotationRepository _rotations = Substitute.For<ISharedRotationRepository>();
    private readonly IRecipeRepository _recipes = Substitute.For<IRecipeRepository>();
    private readonly CreateRotationCommandHandler _handler;
    private SharedRotation? _stored;

    public CreateRotationCommandHandlerTests()
    {
        var recipe = TestDataBuilder.CreateTestSwordRecipe();
        _recipes.GetByIdAsync(1, Arg.Any<CancellationToken>()).Returns(recipe);
        _rotations.CreateAsync(Arg.Any<SharedRotation>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _stored = call.Arg<SharedRotation>();
            _stored.Recipe = recipe;
            return _stored;
        });
        _rotations.GetByIdAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns(_ => _stored);
        _handler = new CreateRotationCommandHandler(_rotations, _recipes);
    }

    private static SubmitRotationRequest Request(int actionId = 100001, int steps = 1) => new()
    {
        RecipeId = 1,
        ActionIds = [actionId, 100002],
        Stats = new CrafterStatsInput { Level = 90, Craftsmanship = 3000, Control = 2800, CP = 500 },
        Metrics = new RotationMetricsInput
        {
            QualityPercent = 37.25m, HQPercent = 12, TotalSteps = steps, CraftCompleted = true,
        },
    };

    [Theory]
    [InlineData(100001)]
    [InlineData(100235)]
    public async Task Stores_reported_metrics_including_for_legacy_actions(int actionId)
    {
        var result = await _handler.Handle(new CreateRotationCommand("author", Request(actionId)), default);

        result.Should().NotBeNull();
        _stored!.QualityPercent.Should().Be(37.25m);
        _stored.HQPercent.Should().Be(12);
        _stored.TotalSteps.Should().Be(1);
        _stored.CraftCompleted.Should().BeTrue();
        result!.Macros.Should().BeEmpty();
    }

    [Theory]
    [InlineData(-1, 1, 0)]
    [InlineData(100001, 3, 0)]
    [InlineData(100001, 1, 200)]
    public async Task Rejects_unknown_actions_excess_steps_and_quota(int actionId, int steps, int count)
    {
        _rotations.GetCountByUserAsync("author", Arg.Any<CancellationToken>()).Returns(count);

        var result = await _handler.Handle(new CreateRotationCommand("author", Request(actionId, steps)), default);

        result.Should().BeNull();
        await _rotations.DidNotReceive().CreateAsync(Arg.Any<SharedRotation>(), Arg.Any<CancellationToken>());
    }
}
