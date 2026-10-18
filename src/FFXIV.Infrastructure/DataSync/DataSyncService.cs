using Cronos;
using FFXIV.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FFXIV.Infrastructure.DataSync;

public sealed class DataSyncService : BackgroundService, IDataSyncTrigger
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<DataSyncService> _logger;
    private readonly DataSyncOptions _options;
    private readonly CronExpression _cronExpression;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private TaskCompletionSource? _manualTrigger;

    public bool IsSyncRunning => _syncLock.CurrentCount == 0;

    public DataSyncService(
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime lifetime,
        IOptions<DataSyncOptions> options,
        ILogger<DataSyncService> logger)
    {
        _scopeFactory = scopeFactory;
        _lifetime = lifetime;
        _logger = logger;
        _options = options.Value;
        _cronExpression = CronExpression.Parse(_options.SyncScheduleCron);
    }

    public bool TriggerSync()
    {
        if (IsSyncRunning)
            return false;

        if (_manualTrigger is { Task.IsCompleted: false })
        {
            _manualTrigger.TrySetResult();
            return true;
        }

        // The loop has not armed a trigger yet (startup delay or startup sync). A sync takes
        // minutes, so it must not run on the caller's request: it would die with the HTTP timeout.
        _ = Task.Run(() => RunSyncWithLockAsync(isManual: true, _lifetime.ApplicationStopping), CancellationToken.None);
        return true;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small delay to let the application fully start
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        if (_options.FullSyncOnStartup)
            await TryRunStartupSyncAsync(stoppingToken);

        if (!_options.EnableAutoSync)
        {
            _logger.LogInformation("Auto sync is disabled. Service will only respond to manual triggers");

            // Still listen for manual triggers
            while (!stoppingToken.IsCancellationRequested)
            {
                _manualTrigger = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                try
                {
                    await _manualTrigger.Task.WaitAsync(stoppingToken);
                    await RunSyncWithLockAsync(isManual: true, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            return;
        }

        // Main scheduling loop
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nextOccurrence = _cronExpression.GetNextOccurrence(DateTimeOffset.UtcNow, TimeZoneInfo.Utc);
                if (nextOccurrence is null)
                {
                    _logger.LogWarning("Cron expression returned no next occurrence. Defaulting to 24-hour delay");
                    nextOccurrence = DateTimeOffset.UtcNow.AddHours(24);
                }

                var delay = nextOccurrence.Value - DateTimeOffset.UtcNow;
                if (delay > TimeSpan.Zero)
                {
                    _logger.LogInformation("Next scheduled sync at {NextRun}", nextOccurrence.Value);

                    _manualTrigger = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    var delayTask = Task.Delay(delay, stoppingToken);
                    var completedTask = await Task.WhenAny(delayTask, _manualTrigger.Task);

                    if (completedTask == _manualTrigger.Task)
                    {
                        await RunSyncWithLockAsync(isManual: true, stoppingToken);
                        continue;
                    }
                }

                await RunSyncWithLockAsync(isManual: false, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in sync service loop. Will retry at next scheduled time");
            }
        }
    }

    private async Task TryRunStartupSyncAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (!await db.Database.CanConnectAsync(ct))
            {
                _logger.LogWarning("Cannot connect to database. Skipping startup sync");
                return;
            }

            var orchestrator = scope.ServiceProvider.GetRequiredService<DataSyncOrchestrator>();

            if (await orchestrator.IsFullSyncNeededAsync(ct))
            {
                _logger.LogInformation("Database is empty. Running initial full sync...");
                await RunSyncCoreAsync(ct);
            }
            else
            {
                _logger.LogInformation("Database already populated. Skipping startup sync");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Startup sync check failed. Will retry at next scheduled time");
        }
    }

    private async Task RunSyncWithLockAsync(bool isManual, CancellationToken ct)
    {
        if (!await _syncLock.WaitAsync(TimeSpan.Zero, ct))
        {
            _logger.LogWarning("Sync already in progress. Skipping");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<DataSyncOrchestrator>();

            if (isManual || await orchestrator.IsVersionChangedAsync(ct))
            {
                await orchestrator.RunFullSyncAsync(ct);
            }
            else
            {
                _logger.LogInformation("Game version unchanged. Skipping sync");
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogInformation("Sync cancelled due to application shutdown");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data sync failed");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task RunSyncCoreAsync(CancellationToken ct)
    {
        if (!await _syncLock.WaitAsync(TimeSpan.Zero, ct))
        {
            _logger.LogWarning("Sync already in progress. Skipping");
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<DataSyncOrchestrator>();
            await orchestrator.RunFullSyncAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data sync failed");
        }
        finally
        {
            _syncLock.Release();
        }
    }
}
