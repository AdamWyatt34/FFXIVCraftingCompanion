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
        // Lengths and the email format are DataAnnotations on the request type.
        if (!ValidCategories.Contains(request.Category))
            return BadRequest(new { error = $"Category must be one of: {string.Join(", ", ValidCategories)}" });

        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _mediator.Send(new SubmitFeedbackCommand(
            request.Category,
            request.Message.Trim(),
            string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            string.IsNullOrWhiteSpace(request.PageUrl) ? null : request.PageUrl.Trim(),
            userAgent.Length > 512 ? userAgent[..512] : userAgent));

        return StatusCode(StatusCodes.Status201Created, result);
    }
}
