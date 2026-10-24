using FFXIV.Application.Consumables.DTOs;
using MediatR;

namespace FFXIV.Application.Consumables.Queries;

public sealed record GetCraftingConsumablesQuery : IRequest<IReadOnlyList<CraftingConsumableDto>>;
