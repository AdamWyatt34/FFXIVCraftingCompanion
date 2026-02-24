using FFXIV.Domain.Models;

namespace FFXIV.Application.Notifications;

public interface INotificationService
{
    Task<IReadOnlyList<Notification>> GetNotificationsAsync(string userId, CancellationToken ct = default);
    Task DismissNotificationAsync(string userId, string notificationId, CancellationToken ct = default);
    Task DismissAllAsync(string userId, CancellationToken ct = default);
}
