using FFXIV.Domain.Enums;
using FFXIV.Domain.Models;

namespace FFXIV.Application.Notifications.DTOs;

public sealed record NotificationDto(
    string Id,
    string Type,
    string Priority,
    string Title,
    string Message,
    string? ActionUrl,
    string? IconUrl,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    Dictionary<string, string> Metadata)
{
    public static NotificationDto FromDomain(Notification n) => new(
        n.Id,
        n.Type.ToString(),
        n.Priority.ToString(),
        n.Title,
        n.Message,
        n.ActionUrl,
        n.IconUrl,
        n.CreatedAt,
        n.ExpiresAt,
        n.Metadata);
}
