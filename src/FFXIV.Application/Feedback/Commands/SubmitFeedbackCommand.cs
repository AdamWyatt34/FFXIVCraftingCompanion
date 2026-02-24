using FFXIV.Application.Feedback.DTOs;
using MediatR;

namespace FFXIV.Application.Feedback.Commands;

public record SubmitFeedbackCommand(
    string Category,
    string Message,
    string? Email,
    string? PageUrl,
    string? UserAgent) : IRequest<FeedbackResult>;
