namespace FFXIV.Application.Feedback.DTOs;

public record SubmitFeedbackRequest(
    string Category,
    string Message,
    string? Email = null,
    string? PageUrl = null);

public record FeedbackResult(int Id, DateTimeOffset CreatedAt);
