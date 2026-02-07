using FFXIV.Application.DTOs;
using MediatR;

namespace FFXIV.Application.Items.Queries;

public sealed record GetItemByIdQuery(int ItemId) : IRequest<ItemDto?>;
