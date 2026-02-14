using FFXIV.Application.Rotations.DTOs;
using FFXIV.Domain.Enums;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Rotations.Queries;

public sealed record SearchRotationsQuery(
    int? RecipeId,
    string? CraftType,
    int? MinLevel,
    int? MaxLevel,
    string? Source,
    string SortBy,
    int Page,
    int PageSize) : IRequest<RotationListResultDto>;

public sealed class SearchRotationsQueryHandler
    : IRequestHandler<SearchRotationsQuery, RotationListResultDto>
{
    private readonly ISharedRotationRepository _rotationRepo;

    public SearchRotationsQueryHandler(ISharedRotationRepository rotationRepo)
    {
        _rotationRepo = rotationRepo;
    }

    public async Task<RotationListResultDto> Handle(
        SearchRotationsQuery request, CancellationToken cancellationToken)
    {
        var source = !string.IsNullOrWhiteSpace(request.Source) &&
                     Enum.TryParse<RotationSource>(request.Source, true, out var s)
            ? s
            : (RotationSource?)null;

        var (items, totalCount) = await _rotationRepo.SearchAsync(
            recipeId: request.RecipeId,
            craftType: request.CraftType,
            minLevel: request.MinLevel,
            maxLevel: request.MaxLevel,
            source: source,
            sortBy: request.SortBy,
            page: request.Page,
            pageSize: request.PageSize,
            cancellationToken: cancellationToken);

        // Batch-load author names
        var userIds = items.Select(r => r.UserId).Distinct();
        var authorNames = await _rotationRepo.GetAuthorNamesAsync(userIds, cancellationToken);

        var dtos = items
            .Select(r => RotationMapper.ToListItemDto(r, authorNames.GetValueOrDefault(r.UserId)))
            .ToList();

        return new RotationListResultDto(dtos, totalCount, request.Page, request.PageSize);
    }
}
