using FFXIV.Domain.CraftingEngine.Solver.Models;

namespace FFXIV.Infrastructure.Solver;

internal sealed class SolverJob
{
    public string JobId { get; } = Guid.NewGuid().ToString("N")[..12];
    public SolveRequest Request { get; init; } = null!;
    public string? ClientId { get; init; }
    public volatile SolverStatus Status = SolverStatus.Queued;
    public SolverResult? Result;
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt;
    public CancellationTokenSource Cts { get; } = new();
}
