using MediatR;

namespace FFXIV.Application.CraftingEngine.Solver;

public sealed record CancelSolverJobCommand(string JobId)
    : IRequest<bool>;

public sealed class CancelSolverJobCommandHandler
    : IRequestHandler<CancelSolverJobCommand, bool>
{
    private readonly ISolverJobService _jobService;

    public CancelSolverJobCommandHandler(ISolverJobService jobService)
        => _jobService = jobService;

    public Task<bool> Handle(
        CancelSolverJobCommand request, CancellationToken cancellationToken)
    {
        var cancelled = _jobService.CancelJob(request.JobId);
        return Task.FromResult(cancelled);
    }
}
