using FFXIV.Application.Gathering.DTOs;
using MediatR;

namespace FFXIV.Application.Gathering.Queries;

public class GetGatheringGuideQueryHandler : IRequestHandler<GetGatheringGuideQuery, GatheringGuideDto>
{
    private readonly IGatheringGuideService _guideService;

    public GetGatheringGuideQueryHandler(IGatheringGuideService guideService)
    {
        _guideService = guideService;
    }

    public async Task<GatheringGuideDto> Handle(GetGatheringGuideQuery request, CancellationToken cancellationToken)
    {
        var guide = await _guideService.GenerateGuideAsync(request.Materials, cancellationToken);
        return GatheringGuideMapper.ToDto(guide);
    }
}
