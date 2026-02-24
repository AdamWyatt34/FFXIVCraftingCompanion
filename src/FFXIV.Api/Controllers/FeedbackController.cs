using FFXIV.Application.Feedback.Commands;
using FFXIV.Application.Feedback.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FFXIV.Api.Controllers;

[ApiController]
[Route("api/feedback")]
public class FeedbackController : ControllerBase
{
    private static readonly HashSet<string> ValidCategories = ["BugReport", "FeatureRequest", "General"];
    private readonly IMediator _mediator;

    public FeedbackController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Submit community feedback.</summary>
    [HttpPost]
    [EnableRateLimiting("feedback")]
    [ProducesResponseType(typeof(FeedbackResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SubmitFeedback([FromBody] SubmitFeedbackRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Category) || !ValidCategories.Contains(request.Category))
            return BadRequest(new { error = $"Category must be one of: {string.Join(", ", ValidCategories)}" });

        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Message is required." });

        if (request.Message.Length > 5000)
            return BadRequest(new { error = "Message must be 5000 characters or less." });

        if (request.Email is not null && request.Email.Length > 320)
            return BadRequest(new { error = "Email must be 320 characters or less." });

        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _mediator.Send(new SubmitFeedbackCommand(
            request.Category,
            request.Message,
            request.Email,
            request.PageUrl,
            userAgent.Length > 512 ? userAgent[..512] : userAgent));

        return StatusCode(StatusCodes.Status201Created, result);
    }
}
