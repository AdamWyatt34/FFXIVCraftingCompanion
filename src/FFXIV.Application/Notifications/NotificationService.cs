using System.Text.Json;
using FFXIV.Application.CraftPlans;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Interfaces;
using FFXIV.Domain.Models;
using FFXIV.Domain.Repositories;
using FFXIV.Domain.Utilities;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace FFXIV.Application.Notifications;

public sealed class NotificationService : INotificationService
{
    private static readonly JsonSerializerOptions PlanItemsJson = new() { PropertyNameCaseInsensitive = true };

    private static readonly TimeSpan ResolvedPlanCacheTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan DismissedCacheTtl = TimeSpan.FromHours(24);

    private readonly ICraftPlanRepository _planRepo;
    private readonly ICraftPlanResolver _planResolver;
    private readonly IGatheringInfoRepository _gatheringRepo;
    private readonly ICacheService _cache;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        ICraftPlanRepository planRepo,
        ICraftPlanResolver planResolver,
        IGatheringInfoRepository gatheringRepo,
        ICacheService cache,
        ILogger<NotificationService> logger)
    {
        _planRepo = planRepo;
        _planResolver = planResolver;
        _gatheringRepo = gatheringRepo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Notification>> GetNotificationsAsync(
        string userId, CancellationToken ct = default)
    {
        var plans = await _planRepo.GetByUserAsync(userId, ct);
        if (plans.Count == 0)
            return [];

        var notifications = new List<Notification>();

        // Collect all raw material item IDs across all plans
        var allRawMaterialIds = new HashSet<int>();
        var hasScripMaterials = false;

        foreach (var plan in plans)
        {
            var resolved = await GetResolvedPlanAsync(plan.Id, plan.Items, ct);
            if (resolved is null)
                continue;

            foreach (var mat in resolved.RawMaterials)
            {
                allRawMaterialIds.Add(mat.ItemId);
                if (mat.PrimarySourceType == ItemSourceType.Scrip)
                    hasScripMaterials = true;
            }
        }

        // Batch-lookup gathering info for timed node detection
        if (allRawMaterialIds.Count > 0)
        {
            var gatheringData = await _gatheringRepo.GetByItemIdsAsync(allRawMaterialIds, ct);
            var currentEorzeanHour = EorzeanTime.GetCurrentEorzeanHour();

            foreach (var (itemId, infos) in gatheringData)
            {
                foreach (var info in infos)
                {
                    // Nodes without a real window (including pre-fix rows that carry the 65535
                    // sentinel) parse to nothing and are skipped.
                    foreach (var (startHour, endHour) in SpawnWindows.Parse(info.SpawnTimes))
                    {
                        var isActive = EorzeanTime.IsWindowActive(startHour, endHour, currentEorzeanHour);
                        var timeUntil = EorzeanTime.TimeUntilWindow(startHour, endHour, currentEorzeanHour);

                        NotificationPriority priority;
                        string message;

                        if (isActive)
                        {
                            priority = NotificationPriority.Medium;
                            message = $"Active now in {info.ZoneName}! Gather while you can.";
                        }
                        else if (timeUntil.TotalMinutes < 5)
                        {
                            priority = NotificationPriority.High;
                            message = $"Spawns in {FormatTimeSpan(timeUntil)} in {info.ZoneName}";
                        }
                        else
                        {
                            continue; // Skip distant windows
                        }

                        notifications.Add(new Notification
                        {
                            Id = $"timed:{itemId}:{startHour}",
                            Type = NotificationType.TimedNode,
                            Priority = priority,
                            Title = $"Timed Node: {info.Item?.Name ?? $"Item {itemId}"}",
                            Message = message,
                            ActionUrl = $"/recipes/plan",
                            ExpiresAt = isActive
                                ? DateTime.UtcNow.AddSeconds(RemainingWindowSeconds(startHour, endHour, currentEorzeanHour))
                                : DateTime.UtcNow.Add(timeUntil).AddSeconds(WindowDurationSeconds(startHour, endHour)),
                            Metadata = new Dictionary<string, string>
                            {
                                ["itemId"] = itemId.ToString(CultureInfo.InvariantCulture),
                                ["zoneName"] = info.ZoneName,
                                ["startHour"] = startHour.ToString(CultureInfo.InvariantCulture),
                                ["endHour"] = endHour.ToString(CultureInfo.InvariantCulture),
                                ["isActive"] = isActive.ToString(),
                            }
                        });
                    }
                }
            }
        }

        // Scrip reset notifications (Tuesday reset)
        if (hasScripMaterials)
        {
            var scripNotification = CheckScripReset();
            if (scripNotification is not null)
                notifications.Add(scripNotification);
        }

        // Filter dismissed
        var dismissed = await GetDismissedSetAsync(userId, ct);
        var now = DateTime.UtcNow;

        var filtered = notifications
            .Where(n => !dismissed.Contains(n.Id))
            .Where(n => n.ExpiresAt is null || n.ExpiresAt > now)
            .OrderByDescending(n => n.Priority)
            .ThenBy(n => n.Metadata.TryGetValue("isActive", out var a) && a == "True" ? 0 : 1)
            .ToList();

        return filtered;
    }

    public async Task DismissNotificationAsync(
        string userId, string notificationId, CancellationToken ct = default)
    {
        var dismissed = await GetDismissedSetAsync(userId, ct);
        dismissed.Add(notificationId);
        await SaveDismissedSetAsync(userId, dismissed, ct);
    }

    public async Task DismissAllAsync(string userId, CancellationToken ct = default)
    {
        var notifications = await GetNotificationsAsync(userId, ct);
        var dismissed = await GetDismissedSetAsync(userId, ct);
        foreach (var n in notifications)
            dismissed.Add(n.Id);
        await SaveDismissedSetAsync(userId, dismissed, ct);
    }

    private async Task<CraftPlanResult?> GetResolvedPlanAsync(
        string planId, string itemsJson, CancellationToken ct)
    {
        var cacheKey = $"notifications:resolved:{planId}";
        var cached = await _cache.GetAsync<CraftPlanResult>(cacheKey, ct);
        if (cached is not null)
            return cached;

        try
        {
            var inputs = JsonSerializer.Deserialize<List<CraftPlanInput>>(itemsJson, PlanItemsJson);
            if (inputs is null || inputs.Count == 0)
                return null;

            var resolved = await _planResolver.ResolveAsync(inputs, ct);
            if (resolved is not null)
                await _cache.SetAsync(cacheKey, resolved, ResolvedPlanCacheTtl, ct);

            return resolved;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve plan {PlanId} for notifications", planId);
            return null;
        }
    }

    private async Task<HashSet<string>> GetDismissedSetAsync(string userId, CancellationToken ct)
    {
        var cacheKey = $"notifications:dismissed:{userId}";
        var cached = await _cache.GetAsync<DismissedSet>(cacheKey, ct);
        return cached?.Ids ?? [];
    }

    private async Task SaveDismissedSetAsync(string userId, HashSet<string> ids, CancellationToken ct)
    {
        var cacheKey = $"notifications:dismissed:{userId}";
        await _cache.SetAsync(cacheKey, new DismissedSet { Ids = ids }, DismissedCacheTtl, ct);
    }

    private static Notification? CheckScripReset()
    {
        var utcNow = DateTime.UtcNow;
        var isResetDay = utcNow.DayOfWeek == DayOfWeek.Tuesday && utcNow.Hour < 8;
        var isPreResetDay = utcNow.DayOfWeek == DayOfWeek.Monday && utcNow.Hour >= 20;

        if (!isResetDay && !isPreResetDay)
            return null;

        var resetDate = isResetDay
            ? utcNow.Date
            : utcNow.Date.AddDays(1);

        return new Notification
        {
            Id = $"scrip-reset:{resetDate:yyyy-MM-dd}",
            Type = NotificationType.ScripReset,
            Priority = NotificationPriority.Medium,
            Title = "Weekly Scrip Reset",
            Message = isResetDay
                ? "Scrip vendors reset today at 8:00 UTC. Spend your scrips!"
                : "Scrip vendors reset tomorrow at 8:00 UTC.",
            ActionUrl = "/recipes/plan",
            ExpiresAt = resetDate.AddHours(8),
        };
    }

    private static double RemainingWindowSeconds(int startHour, int endHour, int currentHour)
    {
        var windowLength = (endHour - startHour + 24) % 24;
        var elapsed = (currentHour - startHour + 24) % 24;
        var remaining = windowLength - elapsed;
        return remaining * EorzeanTime.EorzeanHourInSeconds;
    }

    private static double WindowDurationSeconds(int startHour, int endHour)
    {
        var windowLength = (endHour - startHour + 24) % 24;
        return windowLength * EorzeanTime.EorzeanHourInSeconds;
    }

    private static string FormatTimeSpan(TimeSpan ts)
    {
        if (ts.TotalMinutes < 1)
            return $"{(int)ts.TotalSeconds}s";
        if (ts.TotalHours < 1)
            return $"{(int)ts.TotalMinutes}m";
        return $"{(int)ts.TotalHours}h {ts.Minutes}m";
    }

    // Wrapper class for cache serialization (ICacheService requires class constraint)
    internal sealed class DismissedSet
    {
        public HashSet<string> Ids { get; set; } = [];
    }
}
