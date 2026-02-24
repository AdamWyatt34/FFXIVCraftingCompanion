using FFXIV.Application.Feedback.DTOs;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Feedback.Commands;

public class SubmitFeedbackCommandHandler : IRequestHandler<SubmitFeedbackCommand, FeedbackResult>
{
    private readonly IFeedbackRepository _feedbackRepo;

    public SubmitFeedbackCommandHandler(IFeedbackRepository feedbackRepo)
    {
        _feedbackRepo = feedbackRepo;
    }

    public async Task<FeedbackResult> Handle(SubmitFeedbackCommand request, CancellationToken cancellationToken)
    {
        var entity = new Domain.Entities.Feedback
        {
            Category = request.Category,
            Message = request.Message,
            Email = request.Email,
            PageUrl = request.PageUrl,
            UserAgent = request.UserAgent,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _feedbackRepo.AddAsync(entity, cancellationToken);

        return new FeedbackResult(entity.Id, entity.CreatedAt);
    }
}
