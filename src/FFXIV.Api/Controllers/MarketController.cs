using FFXIV.Application.Market.DTOs;
using FFXIV.Application.Market.Queries;
using FFXIV.Domain.Constants;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Provides endpoints for market board prices and world information.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class MarketController : ControllerBase
{
    private readonly IMediator _mediator;

    public MarketController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Get market board prices for one or more items.
    /// </summary>
    /// <param name="world">The world or data center name (e.g., "Adamantoise" or "Aether").</param>
    /// <param name="items">Comma-separated list of item IDs.</param>
    /// <response code="200">Returns market board data for the requested items.</response>
    /// <response code="400">Invalid world/DC name or item IDs.</response>
    [HttpGet("prices")]
    [ProducesResponseType(typeof(Dictionary<int, MarketBoardDataDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetPrices(
        [FromQuery] string world,
        [FromQuery] string items)
    {
        if (string.IsNullOrWhiteSpace(world) || !FfxivServers.IsValidWorldOrDc(world))
            return BadRequest(new { error = "Invalid or missing world/data center name." });

        if (string.IsNullOrWhiteSpace(items))
            return BadRequest(new { error = "At least one item ID is required." });

        var itemIds = new List<int>();
        foreach (var part in items.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var id) || id <= 0)
                return BadRequest(new { error = $"Invalid item ID: {part}" });
            itemIds.Add(id);
        }

        if (itemIds.Count > 100)
            return BadRequest(new { error = "Maximum 100 items per request." });

        var result = await _mediator.Send(new GetMarketPricesQuery(world, itemIds));
        return Ok(result);
    }

    /// <summary>
    /// Get the list of FFXIV worlds grouped by region and data center.
    /// </summary>
    /// <response code="200">Returns the world list.</response>
    [HttpGet("worlds")]
    [ProducesResponseType(typeof(List<WorldGroupDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorlds()
    {
        var result = await _mediator.Send(new GetWorldsQuery());
        return Ok(result);
    }
}
