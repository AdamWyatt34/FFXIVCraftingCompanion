using FFXIV.Application.Market.DTOs;
using FFXIV.Domain.Constants;
using MediatR;

namespace FFXIV.Application.Market.Queries;

public sealed record GetWorldsQuery : IRequest<List<WorldGroupDto>>;

public sealed class GetWorldsQueryHandler : IRequestHandler<GetWorldsQuery, List<WorldGroupDto>>
{
    public Task<List<WorldGroupDto>> Handle(GetWorldsQuery request, CancellationToken cancellationToken)
    {
        var groups = FfxivServers.GetWorldsGrouped();
        return Task.FromResult(MarketMapper.ToWorldGroupDtos(groups));
    }
}
