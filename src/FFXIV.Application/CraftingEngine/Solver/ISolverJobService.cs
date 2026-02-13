using FFXIV.Domain.CraftingEngine.Solver.Models;

namespace FFXIV.Application.CraftingEngine.Solver;

public interface ISolverJobService
{
    /// <summary>
    /// Enqueue a solve request. Returns immediately with a job ID.
    /// </summary>
    string EnqueueSolve(SolveRequest request, string? clientId = null);

    /// <summary>
    /// Get the current status and result of a solve job.
    /// </summary>
    SolverJobInfo? GetJob(string jobId);

    /// <summary>
    /// Cancel a running solve job.
    /// </summary>
    bool CancelJob(string jobId);

    /// <summary>
    /// Get the number of active (Queued or Running) jobs for a given client.
    /// </summary>
    int GetActiveJobCount(string clientId);
}

public sealed record SolverJobInfo
{
    public string JobId { get; init; } = string.Empty;
    public SolverStatus Status { get; init; }
    public SolveRequest Request { get; init; } = null!;
    public SolverResult? Result { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
}
