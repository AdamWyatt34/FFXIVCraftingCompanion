using FFXIV.Application.Favorites.Commands;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;

namespace FFXIV.Tests.Favorites;

public class ToggleFavoriteRecipeCommandHandlerTests
{
    private readonly IFavoriteRecipeRepository _favorites = Substitute.For<IFavoriteRecipeRepository>();
    private readonly IRecipeRepository _recipes = Substitute.For<IRecipeRepository>();
    private readonly ToggleFavoriteRecipeCommandHandler _handler;

    public ToggleFavoriteRecipeCommandHandlerTests()
    {
        _handler = new ToggleFavoriteRecipeCommandHandler(_favorites, _recipes);
        _recipes.ExistsAsync(10, Arg.Any<CancellationToken>()).Returns(true);
        _favorites.AddAsync(Arg.Any<FavoriteRecipe>(), Arg.Any<CancellationToken>()).Returns(true);
    }

    [Fact]
    public async Task Unknown_recipe_is_reported_instead_of_hitting_the_foreign_key()
    {
        _favorites.IsFavoritedAsync("user-1", 404, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(new ToggleFavoriteRecipeCommand("user-1", 404, null), CancellationToken.None);

        result.Outcome.Should().Be(ToggleFavoriteOutcome.RecipeNotFound);
        result.Status.Should().BeNull();
        await _favorites.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Adding_a_favorite_stores_it_with_the_note()
    {
        _favorites.IsFavoritedAsync("user-1", 10, Arg.Any<CancellationToken>()).Returns(false);
        _favorites.GetCountByUserAsync("user-1", Arg.Any<CancellationToken>()).Returns(3);

        var result = await _handler.Handle(new ToggleFavoriteRecipeCommand("user-1", 10, "for scrips"), CancellationToken.None);

        result.Outcome.Should().Be(ToggleFavoriteOutcome.Toggled);
        result.Status!.IsFavorited.Should().BeTrue();
        await _favorites.Received(1).AddAsync(
            Arg.Is<FavoriteRecipe>(f => f.UserId == "user-1" && f.RecipeId == 10 && f.Note == "for scrips"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Removing_a_favorite_does_not_need_the_recipe_to_exist()
    {
        _favorites.IsFavoritedAsync("user-1", 404, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(new ToggleFavoriteRecipeCommand("user-1", 404, null), CancellationToken.None);

        result.Outcome.Should().Be(ToggleFavoriteOutcome.Toggled);
        result.Status!.IsFavorited.Should().BeFalse();
        await _favorites.Received(1).RemoveAsync("user-1", 404, Arg.Any<CancellationToken>());
        await _recipes.DidNotReceiveWithAnyArgs().ExistsAsync(default, default);
    }

    [Fact]
    public async Task Quota_is_enforced_before_adding()
    {
        _favorites.IsFavoritedAsync("user-1", 10, Arg.Any<CancellationToken>()).Returns(false);
        _favorites.GetCountByUserAsync("user-1", Arg.Any<CancellationToken>())
            .Returns(ToggleFavoriteRecipeCommandHandler.MaxFavorites);

        var result = await _handler.Handle(new ToggleFavoriteRecipeCommand("user-1", 10, null), CancellationToken.None);

        result.Outcome.Should().Be(ToggleFavoriteOutcome.QuotaExceeded);
        await _favorites.DidNotReceiveWithAnyArgs().AddAsync(default!, default);
    }

    [Fact]
    public async Task Concurrent_duplicate_add_still_reports_favorited()
    {
        _favorites.IsFavoritedAsync("user-1", 10, Arg.Any<CancellationToken>()).Returns(false);
        _favorites.GetCountByUserAsync("user-1", Arg.Any<CancellationToken>()).Returns(0);
        _favorites.AddAsync(Arg.Any<FavoriteRecipe>(), Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(new ToggleFavoriteRecipeCommand("user-1", 10, null), CancellationToken.None);

        result.Outcome.Should().Be(ToggleFavoriteOutcome.Toggled);
        result.Status!.IsFavorited.Should().BeTrue();
    }
}
