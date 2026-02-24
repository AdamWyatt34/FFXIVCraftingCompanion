using FFXIV.Application.Analytics.Commands;
using FFXIV.Application.Analytics.DTOs;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FFXIV.Api.Controllers;

[ApiController]
[Route("api/analytics")]
public class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AnalyticsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Track an analytics event.</summary>
    [HttpPost("event")]
    [EnableRateLimiting("analytics")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> TrackEvent([FromBody] TrackEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Event))
            return BadRequest(new { error = "Event name is required." });

        if (request.Event.Length > 128)
            return BadRequest(new { error = "Event name must be 128 characters or less." });

        await _mediator.Send(new TrackEventCommand(
            request.Event,
            request.Metadata,
            request.PageUrl,
            request.Referrer));

        return Accepted();
    }
}
