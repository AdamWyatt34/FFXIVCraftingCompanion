namespace FFXIV.Application.DTOs;

public sealed record ItemSearchResultDto(
    List<ItemDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
