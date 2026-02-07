using FFXIV.Application.DTOs;
using FFXIV.Infrastructure.DataSync;
using FFXIV.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Api.Controllers;

// TODO: Secure these endpoints with authentication/authorization (Sprint 4)

/// <summary>
/// Administrative endpoints for data synchronization management.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IDataSyncTrigger _syncTrigger;
    private readonly AppDbContext _db;

    public AdminController(IDataSyncTrigger syncTrigger, AppDbContext db)
    {
        _syncTrigger = syncTrigger;
        _db = db;
    }

    /// <summary>
    /// Trigger a manual data sync. The sync runs in the background.
    /// Poll GET /api/admin/sync/status to monitor progress.
    /// </summary>
    /// <response code="202">Sync has been triggered successfully.</response>
    /// <response code="409">A sync operation is already in progress.</response>
    [HttpPost("sync")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> TriggerSync(CancellationToken cancellationToken)
    {
        if (_syncTrigger.IsSyncRunning)
            return Conflict(new { message = "A sync operation is already in progress." });

        await _syncTrigger.TriggerSyncAsync(cancellationToken);
        return Accepted(new { message = "Sync has been triggered." });
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
}
