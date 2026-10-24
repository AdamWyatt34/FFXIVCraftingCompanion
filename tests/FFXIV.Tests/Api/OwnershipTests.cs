using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FFXIV.Tests.Api;

/// <summary>Every <c>{id}</c> route answers another account as if the record did not exist.</summary>
public sealed class OwnershipTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task A_saved_list_is_invisible_to_another_account()
    {
        var owner = await factory.RegisterAsync();
        var other = await factory.RegisterAsync();
        var created = await owner.Client.PostAsJsonAsync("/api/saved-lists/shopping-list",
            new { name = "Lumber run", recipeId = ApiFactory.FixtureRecipeId, quantity = 2 });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await created.JsonAsync()).GetProperty("id").GetInt32();

        (await other.Client.GetAsync($"/api/saved-lists/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.Client.PutAsJsonAsync($"/api/saved-lists/{id}", new { name = "Mine now" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.Client.DeleteAsync($"/api/saved-lists/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.Client.GetFromJsonAsync<JsonElement>("/api/saved-lists")).GetArrayLength().Should().Be(0);

        var mine = await owner.Client.GetAsync($"/api/saved-lists/{id}");
        mine.StatusCode.Should().Be(HttpStatusCode.OK);
        (await mine.JsonAsync()).GetProperty("name").GetString().Should().Be("Lumber run");
        (await owner.Client.DeleteAsync($"/api/saved-lists/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_craft_plan_is_owner_only_except_for_its_shared_items()
    {
        var owner = await factory.RegisterAsync();
        var other = await factory.RegisterAsync();
        var created = await owner.Client.PostAsJsonAsync("/api/craft-plans",
            new { name = "Weekly", items = new[] { new { recipeId = ApiFactory.FixtureRecipeId, quantity = 3 } } });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await created.JsonAsync()).GetProperty("id").GetString();

        (await other.Client.GetAsync($"/api/craft-plans/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.Client.PutAsJsonAsync($"/api/craft-plans/{id}", new { name = "Taken" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.Client.DeleteAsync($"/api/craft-plans/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.Client.GetFromJsonAsync<JsonElement>("/api/craft-plans")).GetArrayLength().Should().Be(0);

        // The id is the share capability: the items are readable without an account.
        var shared = await factory.CreateApiClient().GetAsync($"/api/craft-plans/{id}/items");
        shared.StatusCode.Should().Be(HttpStatusCode.OK);
        (await shared.JsonAsync()).GetProperty("name").GetString().Should().Be("Weekly");

        (await owner.Client.DeleteAsync($"/api/craft-plans/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await factory.CreateApiClient().GetAsync($"/api/craft-plans/{id}/items")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Favourites_belong_to_the_account_and_need_a_real_recipe()
    {
        var owner = await factory.RegisterAsync();
        var other = await factory.RegisterAsync();

        var unknown = await owner.Client.PostAsJsonAsync($"/api/favorites/recipes/{ApiFactory.UnknownRecipeId}", new { });
        unknown.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var added = await owner.Client.PostAsJsonAsync($"/api/favorites/recipes/{ApiFactory.FixtureRecipeId}",
            new { note = "for the house" });
        added.StatusCode.Should().Be(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());
        (await added.JsonAsync()).GetProperty("isFavorited").GetBoolean().Should().BeTrue();

        var otherStatus = await other.Client.GetFromJsonAsync<JsonElement>(
            $"/api/favorites/recipes/{ApiFactory.FixtureRecipeId}/status");
        otherStatus.GetProperty("isFavorited").GetBoolean().Should().BeFalse();
        (await other.Client.GetFromJsonAsync<JsonElement>("/api/favorites/recipes")).GetArrayLength().Should().Be(0);

        var mine = await owner.Client.GetFromJsonAsync<JsonElement>("/api/favorites/recipes");
        mine.EnumerateArray().Should().ContainSingle(f => f.GetProperty("recipeId").GetInt32() == ApiFactory.FixtureRecipeId);

        var removed = await owner.Client.PostAsJsonAsync($"/api/favorites/recipes/{ApiFactory.FixtureRecipeId}", new { });
        (await removed.JsonAsync()).GetProperty("isFavorited").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task Rotation_votes_are_stored_counts_and_only_the_author_deletes()
    {
        var author = await factory.RegisterAsync();
        var voter = await factory.RegisterAsync();
        var created = await author.Client.PostAsJsonAsync("/api/rotations", new
        {
            recipeId = ApiFactory.FixtureRecipeId,
            actionIds = new[] { 100002, 100001 }, // Basic Touch, Basic Synthesis
            stats = new { level = 90, craftsmanship = 3000, control = 3000, cp = 500 },
            metrics = new { qualityPercent = 50m, hqPercent = 15, totalSteps = 2, craftCompleted = true },
            name = "Lumber in two",
        });
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync());
        var id = (await created.JsonAsync()).GetProperty("id").GetInt32();

        var upvoted = await (await voter.Client.PostAsync($"/api/rotations/{id}/upvote", null)).JsonAsync();
        upvoted.GetProperty("upvoted").GetBoolean().Should().BeTrue();
        upvoted.GetProperty("upvotes").GetInt32().Should().Be(1);

        var withdrawn = await (await voter.Client.PostAsync($"/api/rotations/{id}/upvote", null)).JsonAsync();
        withdrawn.GetProperty("upvoted").GetBoolean().Should().BeFalse();
        withdrawn.GetProperty("upvotes").GetInt32().Should().Be(0);

        await voter.Client.PostAsync($"/api/rotations/{id}/upvote", null);
        var detail = await factory.CreateApiClient().GetFromJsonAsync<JsonElement>($"/api/rotations/{id}");
        detail.GetProperty("upvotes").GetInt32().Should().Be(1, "the toggle's answer is the stored count");

        (await voter.Client.PostAsync($"/api/rotations/{ApiFactory.UnknownRecipeId}/upvote", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await voter.Client.DeleteAsync($"/api/rotations/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await author.Client.DeleteAsync($"/api/rotations/{id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await factory.CreateApiClient().GetAsync($"/api/rotations/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Notifications_are_read_and_dismissed_per_account()
    {
        var account = await factory.RegisterAsync();
        var other = await factory.RegisterAsync();

        (await other.Client.PostAsync("/api/notifications/not-an-id/read", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await other.Client.PostAsync("/api/notifications/read-all", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await account.Client.GetFromJsonAsync<JsonElement>("/api/notifications")).GetArrayLength().Should().Be(0);
    }
}
