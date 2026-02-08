using FFXIV.Application.DTOs;
using FFXIV.Application.Items.Queries;
using FFXIV.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Provides endpoints for searching and retrieving FFXIV items.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ItemsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ItemsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Search for items by name, optionally filtered by craft type.
    /// </summary>
    /// <param name="q">Search query (minimum 2 characters). Optional when craftType is specified.</param>
    /// <param name="craftType">Filter by craft type (e.g. CRP, BSM, ARM, GSM, LTW, WVR, ALC, CUL).</param>
    /// <param name="page">Page number (default: 1, minimum: 1).</param>
    /// <param name="pageSize">Items per page (default: 20, range: 1-100).</param>
    /// <response code="200">Returns matching items.</response>
    /// <response code="400">Query too short, or invalid pagination parameters.</response>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ItemSearchResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] string? q = null,
        [FromQuery] CraftType? craftType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var hasQuery = !string.IsNullOrWhiteSpace(q) && q.Length >= 2;
        var hasCraftType = craftType.HasValue;

        if (!hasQuery && !hasCraftType)
            return BadRequest(new { error = "Provide a search query (minimum 2 characters) or a craftType filter." });

        if (!string.IsNullOrWhiteSpace(q) && q.Length < 2)
            return BadRequest(new { error = "Query must be at least 2 characters." });

        if (page < 1)
            return BadRequest(new { error = "Page must be at least 1." });

        if (pageSize < 1 || pageSize > 100)
            return BadRequest(new { error = "PageSize must be between 1 and 100." });

        var result = await _mediator.Send(new SearchItemsQuery(
            hasQuery ? q : null, page, pageSize, craftType));
        return Ok(result);
    }

    /// <summary>
    /// Get an item by its ID.
    /// </summary>
    /// <param name="id">The item ID.</param>
    /// <response code="200">Returns the item.</response>
    /// <response code="404">Item not found.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ItemDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _mediator.Send(new GetItemByIdQuery(id));
        if (result is null)
            return NotFound(new { error = $"Item {id} not found." });

        return Ok(result);
    }
}
