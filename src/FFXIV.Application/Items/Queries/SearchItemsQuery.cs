using FFXIV.Application.DTOs;
using FFXIV.Domain.Enums;
using MediatR;

namespace FFXIV.Application.Items.Queries;

public sealed record SearchItemsQuery(
    string? Query,
    int Page,
    int PageSize,
    CraftType? CraftType = null) : IRequest<ItemSearchResultDto>;
