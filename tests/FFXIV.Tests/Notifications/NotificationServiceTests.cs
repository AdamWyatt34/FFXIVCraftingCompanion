using FFXIV.Application.CraftPlans;
using FFXIV.Application.Notifications;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;
using FFXIV.Domain.Utilities;
using Microsoft.Extensions.Logging;

namespace FFXIV.Tests.Notifications;

public class NotificationServiceTests
{
    private readonly ICraftPlanRepository _planRepo = Substitute.For<ICraftPlanRepository>();
    private readonly ICraftPlanResolver _planResolver = Substitute.For<ICraftPlanResolver>();
    private readonly IGatheringInfoRepository _gatheringRepo = Substitute.For<IGatheringInfoRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly ILogger<NotificationService> _logger = Substitute.For<ILogger<NotificationService>>();
    private readonly NotificationService _service;

    private const string UserId = "user-1";

    public NotificationServiceTests()
    {
        // Cache always misses by default
        _cache.GetAsync<CraftPlanResult>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((CraftPlanResult?)null);
        _cache.GetAsync<NotificationService.DismissedSet>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((NotificationService.DismissedSet?)null);

        _service = new NotificationService(_planRepo, _planResolver, _gatheringRepo, _cache, _logger);
    }

    [Fact]
    public async Task NoPlans_ReturnsEmptyNotifications()
    {
        _planRepo.GetByUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new List<CraftPlan>());

        var result = await _service.GetNotificationsAsync(UserId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task PlanWithTimedNode_NearWindow_ReturnsTimedNodeNotification()
    {
        // Arrange
        var plan = CreatePlan("plan-1", "[{\"RecipeId\":1,\"Quantity\":1}]");
        _planRepo.GetByUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new List<CraftPlan> { plan });

        var resolved = new CraftPlanResult
        {
            Items = [],
            RawMaterials = [new AggregatedMaterial { ItemId = 100, ItemName = "Timed Ore", TotalQuantity = 5, PrimarySourceType = ItemSourceType.Gathered }],
            Crystals = [],
            IntermediateCrafts = [],
            Summary = new CraftPlanSummary()
        };
        _planResolver.ResolveAsync(Arg.Any<List<CraftPlanInput>>(), Arg.Any<CancellationToken>())
            .Returns(resolved);

        // Simulate a timed node whose window is "always active" by giving a 24h window
        var currentHour = Domain.Utilities.EorzeanTime.GetCurrentEorzeanHour();
        var startHour = currentHour;
        var endHour = (currentHour + 2) % 24;

        var gatheringInfo = new GatheringInfo
        {
            GatheringInfoId = 1,
            ItemId = 100,
            IsTimed = true,
            SpawnTimes = SpawnWindows.Serialize([new SpawnWindow(startHour, endHour)]),
            ZoneName = "Eastern La Noscea",
            GatheringType = GatheringType.Mining,
            ClassJob = "Miner",
            Level = 50,
            Item = new Item { ItemId = 100, Name = "Timed Ore" }
        };

        _gatheringRepo.GetByItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, List<GatheringInfo>> { { 100, [gatheringInfo] } });

        // Act
        var result = await _service.GetNotificationsAsync(UserId);

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain(n => n.Type == NotificationType.TimedNode);
        result.First(n => n.Type == NotificationType.TimedNode)
            .Id.Should().StartWith("timed:100:");
    }

    [Fact]
    public async Task DismissedNotifications_FilteredOut()
    {
        // Arrange
        var plan = CreatePlan("plan-1", "[{\"RecipeId\":1,\"Quantity\":1}]");
        _planRepo.GetByUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new List<CraftPlan> { plan });

        var currentHour = Domain.Utilities.EorzeanTime.GetCurrentEorzeanHour();
        var startHour = currentHour;
        var endHour = (currentHour + 2) % 24;

        var resolved = new CraftPlanResult
        {
            Items = [],
            RawMaterials = [new AggregatedMaterial { ItemId = 100, ItemName = "Ore", TotalQuantity = 1, PrimarySourceType = ItemSourceType.Gathered }],
            Crystals = [],
            IntermediateCrafts = [],
            Summary = new CraftPlanSummary()
        };
        _planResolver.ResolveAsync(Arg.Any<List<CraftPlanInput>>(), Arg.Any<CancellationToken>())
            .Returns(resolved);

        var gatheringInfo = new GatheringInfo
        {
            GatheringInfoId = 1,
            ItemId = 100,
            IsTimed = true,
            SpawnTimes = SpawnWindows.Serialize([new SpawnWindow(startHour, endHour)]),
            ZoneName = "Test Zone",
            GatheringType = GatheringType.Mining,
            ClassJob = "Miner",
            Level = 50,
            Item = new Item { ItemId = 100, Name = "Ore" }
        };

        _gatheringRepo.GetByItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, List<GatheringInfo>> { { 100, [gatheringInfo] } });

        // Pre-dismiss the notification
        var dismissedId = $"timed:100:{startHour}";
        _cache.GetAsync<NotificationService.DismissedSet>(
            $"notifications:dismissed:{UserId}", Arg.Any<CancellationToken>())
            .Returns(new NotificationService.DismissedSet { Ids = [dismissedId] });

        // Act
        var result = await _service.GetNotificationsAsync(UserId);

        // Assert
        result.Should().NotContain(n => n.Id == dismissedId);
    }

    [Fact]
    public async Task DuplicateItemsAcrossPlans_SingleNotificationPerItem()
    {
        // Arrange
        var plan1 = CreatePlan("plan-1", "[{\"RecipeId\":1,\"Quantity\":1}]");
        var plan2 = CreatePlan("plan-2", "[{\"RecipeId\":2,\"Quantity\":1}]");
        _planRepo.GetByUserAsync(UserId, Arg.Any<CancellationToken>())
            .Returns(new List<CraftPlan> { plan1, plan2 });

        var currentHour = Domain.Utilities.EorzeanTime.GetCurrentEorzeanHour();
        var startHour = currentHour;
        var endHour = (currentHour + 2) % 24;

        // Both plans resolve to the same raw material
        var resolved = new CraftPlanResult
        {
            Items = [],
            RawMaterials = [new AggregatedMaterial { ItemId = 100, ItemName = "Shared Ore", TotalQuantity = 5, PrimarySourceType = ItemSourceType.Gathered }],
            Crystals = [],
            IntermediateCrafts = [],
            Summary = new CraftPlanSummary()
        };
        _planResolver.ResolveAsync(Arg.Any<List<CraftPlanInput>>(), Arg.Any<CancellationToken>())
            .Returns(resolved);

        var gatheringInfo = new GatheringInfo
        {
            GatheringInfoId = 1,
            ItemId = 100,
            IsTimed = true,
            SpawnTimes = SpawnWindows.Serialize([new SpawnWindow(startHour, endHour)]),
            ZoneName = "Test Zone",
            GatheringType = GatheringType.Mining,
            ClassJob = "Miner",
            Level = 50,
            Item = new Item { ItemId = 100, Name = "Shared Ore" }
        };

        _gatheringRepo.GetByItemIdsAsync(Arg.Any<IEnumerable<int>>(), Arg.Any<CancellationToken>())
            .Returns(new Dictionary<int, List<GatheringInfo>> { { 100, [gatheringInfo] } });

        // Act
        var result = await _service.GetNotificationsAsync(UserId);

        // Assert — same itemId+startHour should produce only one notification
        var timedNotifications = result.Where(n => n.Type == NotificationType.TimedNode).ToList();
        timedNotifications.Select(n => n.Id).Should().OnlyHaveUniqueItems();
    }

    private static CraftPlan CreatePlan(string id, string itemsJson) => new()
    {
        Id = id,
        UserId = UserId,
        Name = $"Test Plan {id}",
        Items = itemsJson,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}
