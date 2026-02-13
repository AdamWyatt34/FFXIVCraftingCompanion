using FFXIV.Application.Crafting;
using FFXIV.Application.Crafting.DTOs;
using MediatR;

namespace FFXIV.Application.CraftingEngine.Solver;

public sealed record GetSolverJobQuery(string JobId)
    : IRequest<SolverJobDto?>;

public sealed class GetSolverJobQueryHandler
    : IRequestHandler<GetSolverJobQuery, SolverJobDto?>
{
    private readonly ISolverJobService _jobService;

    public GetSolverJobQueryHandler(ISolverJobService jobService)
        => _jobService = jobService;

    public Task<SolverJobDto?> Handle(
        GetSolverJobQuery request, CancellationToken cancellationToken)
    {
        var info = _jobService.GetJob(request.JobId);
        if (info is null)
            return Task.FromResult<SolverJobDto?>(null);

        var dto = SolverMapper.ToJobDto(info);
        return Task.FromResult<SolverJobDto?>(dto);
    }
}
