using FFXIV.Application.DTOs;
using MediatR;

namespace FFXIV.Application.Items.Queries;

public sealed record SearchItemsQuery(
    string Query,
    int Page,
    int PageSize) : IRequest<ItemSearchResultDto>;
