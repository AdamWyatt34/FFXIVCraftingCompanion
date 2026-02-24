using FFXIV.Application.Gathering;
using FFXIV.Application.Gathering.DTOs;
using FFXIV.Application.Gathering.Queries;
using FFXIV.Domain.Utilities;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Provides endpoints for gathering guides and Eorzean time.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class GatheringController : ControllerBase
{
    private readonly IMediator _mediator;

    public GatheringController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Generate a gathering guide from a list of materials.
    /// </summary>
    /// <param name="request">The items and quantities to gather.</param>
    /// <response code="200">Returns the gathering guide with zone groups, timed nodes, and crystal summary.</response>
    /// <response code="400">Invalid request.</response>
    [HttpPost("guide")]
    [ProducesResponseType(typeof(GatheringGuideDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GenerateGuide([FromBody] GenerateGatheringGuideRequest request)
    {
        if (request.Items is null || request.Items.Count == 0)
            return BadRequest(new { error = "At least one item is required." });

        if (request.Items.Count > 200)
            return BadRequest(new { error = "Maximum of 200 items per guide request." });

        foreach (var item in request.Items)
        {
            if (item.ItemId <= 0)
                return BadRequest(new { error = $"Invalid item ID: {item.ItemId}." });
            if (item.Quantity < 1)
                return BadRequest(new { error = $"Quantity must be at least 1 for item {item.ItemId}." });
        }

        var materials = request.Items
            .Select(i => new GatheredMaterialInput(i.ItemId, i.Quantity))
            .ToList();

        var result = await _mediator.Send(new GetGatheringGuideQuery(materials));
        return Ok(result);
    }

    /// <summary>
    /// Get the current Eorzean time.
    /// </summary>
    /// <response code="200">Returns the current Eorzean hour (0-23).</response>
    [HttpGet("eorzean-time")]
    [ProducesResponseType(typeof(EorzeanTimeResponse), StatusCodes.Status200OK)]
    public IActionResult GetEorzeanTime()
    {
        var hour = EorzeanTime.GetCurrentEorzeanHour();
        return Ok(new EorzeanTimeResponse(hour));
    }
}

public sealed record EorzeanTimeResponse(int CurrentEorzeanHour);
