using FFXIV.Application.Notifications;
using FFXIV.Application.Notifications.DTOs;
using FFXIV.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FFXIV.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUser;

    public NotificationsController(
        INotificationService notificationService,
        ICurrentUserService currentUser)
    {
        _notificationService = notificationService;
        _currentUser = currentUser;
    }

    /// <summary>Get current user's notifications.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<NotificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNotifications(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
            return Unauthorized();

        var notifications = await _notificationService.GetNotificationsAsync(userId, ct);
        var dtos = notifications.Select(NotificationDto.FromDomain).ToList();
        return Ok(dtos);
    }

    /// <summary>Dismiss a notification.</summary>
    [HttpPost("{id}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DismissNotification(string id, CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
            return Unauthorized();

        await _notificationService.DismissNotificationAsync(userId, id, ct);
        return NoContent();
    }

    /// <summary>Dismiss all notifications.</summary>
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DismissAllNotifications(CancellationToken ct)
    {
        var userId = _currentUser.UserId;
        if (userId is null)
            return Unauthorized();

        await _notificationService.DismissAllAsync(userId, ct);
        return NoContent();
    }
}
