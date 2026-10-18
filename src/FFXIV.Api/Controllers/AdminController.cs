using System.Runtime.InteropServices;
using FFXIV.Api.Filters;
using FFXIV.Application.DTOs;
using FFXIV.Application.Recipes;
using FFXIV.Application.Recipes.DTOs;
using FFXIV.Domain.Models;
using FFXIV.Infrastructure.DataSync;
using FFXIV.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Api.Controllers;

/// <summary>
/// Administrative endpoints for data synchronization management and tree validation.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[ServiceFilter<AdminApiKeyFilter>]
public class AdminController : ControllerBase
{
    private const int MaxBatchSize = 500;

    private readonly IDataSyncTrigger _syncTrigger;
    private readonly AppDbContext _db;
    private readonly ITreeValidationService _validationService;

    public AdminController(
        IDataSyncTrigger syncTrigger,
        AppDbContext db,
        ITreeValidationService validationService)
    {
        _syncTrigger = syncTrigger;
        _db = db;
        _validationService = validationService;
    }

    /// <summary>
    /// Trigger a manual data sync. The sync runs in the background and outlives this request.
    /// Poll GET /api/admin/sync/status to monitor progress.
    /// </summary>
    /// <response code="202">Sync has been started.</response>
    /// <response code="409">A sync operation is already in progress.</response>
    [HttpPost("sync")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public IActionResult TriggerSync()
    {
        return _syncTrigger.TriggerSync()
            ? Accepted(new { message = "Sync has been started." })
            : Conflict(new { message = "A sync operation is already in progress." });
    }

    /// <summary>
    /// Get sync status for all entity types.
    /// </summary>
    /// <response code="200">Returns sync status for each entity type.</response>
    [HttpGet("sync/status")]
    [ProducesResponseType(typeof(IEnumerable<SyncStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSyncStatus(CancellationToken cancellationToken)
    {
        var metadata = await _db.SyncMetadata
            .AsNoTracking()
            .OrderBy(s => s.EntityType)
            .ToListAsync(cancellationToken);

        var isSyncRunning = _syncTrigger.IsSyncRunning;

        var result = metadata.Select(m => new SyncStatusDto(
            EntityType: m.EntityType,
            LastSyncedAt: m.LastSyncedAt,
            LastPatchVersion: m.LastPatchVersion,
            RecordCount: m.RecordCount,
            IsSyncCurrentlyRunning: isSyncRunning));

        return Ok(result);
    }

    /// <summary>
    /// Validate a recipe's material tree against Garland Tools data.
    /// This is a development/testing aid.
    /// </summary>
    /// <param name="recipeId">The recipe ID to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns validation result.</response>
    /// <response code="404">Recipe not found.</response>
    [HttpPost("validate-tree/{recipeId:int}")]
    [ProducesResponseType(typeof(TreeValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ValidateTree(int recipeId, CancellationToken cancellationToken)
    {
        var result = await _validationService.ValidateTreeAsync(recipeId, 1, cancellationToken);

        if (result.ErrorMessage?.Contains("not found") == true)
            return NotFound(new { error = result.ErrorMessage });

        return Ok(result);
    }

    /// <summary>
    /// Validate a batch of recipe trees against Garland Tools data.
    /// Body can contain a list of recipe IDs or use "random:N" to validate N random recipes.
    /// </summary>
    /// <param name="request">Batch validation request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns batch validation results.</response>
    /// <response code="400">Invalid request.</response>
    [HttpPost("validate-batch")]
    [ProducesResponseType(typeof(BatchValidationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateBatch(
        [FromBody] BatchValidationRequest request, CancellationToken cancellationToken)
    {
        IEnumerable<int> recipeIds;

        if (request.RandomSpec is not null)
        {
            if (!request.RandomSpec.StartsWith("random:", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { error = "Invalid randomSpec format. Use 'random:N'." });

            if (!int.TryParse(request.RandomSpec.AsSpan(7), out var count) || count < 1 || count > MaxBatchSize)
                return BadRequest(new { error = $"Random count must be between 1 and {MaxBatchSize}." });

            recipeIds = await GetRandomRecipeIdsAsync(count, cancellationToken);
        }
        else if (request.RecipeIds is { Count: > 0 })
        {
            if (request.RecipeIds.Count > MaxBatchSize)
                return BadRequest(new { error = $"At most {MaxBatchSize} recipe ids per batch." });

            recipeIds = request.RecipeIds;
        }
        else
        {
            return BadRequest(new { error = "Provide either recipeIds or randomSpec." });
        }

        var result = await _validationService.ValidateBatchAsync(recipeIds, cancellationToken);
        return Ok(result);
    }

    private async Task<List<int>> GetRandomRecipeIdsAsync(int count, CancellationToken ct)
    {
        var allIds = await _db.Recipes
            .AsNoTracking()
            .Select(r => r.RecipeId)
            .ToListAsync(ct);

        Random.Shared.Shuffle(CollectionsMarshal.AsSpan(allIds));
        return allIds.Take(count).ToList();
    }
}
