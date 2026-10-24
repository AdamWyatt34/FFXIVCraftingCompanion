using FFXIV.Application.Consumables.DTOs;
using FFXIV.Domain.Entities;
using FFXIV.Domain.Repositories;
using MediatR;

namespace FFXIV.Application.Consumables.Queries;

public sealed class GetCraftingConsumablesQueryHandler(IConsumableRepository consumables)
    : IRequestHandler<GetCraftingConsumablesQuery, IReadOnlyList<CraftingConsumableDto>>
{
    public async Task<IReadOnlyList<CraftingConsumableDto>> Handle(
        GetCraftingConsumablesQuery request,
        CancellationToken cancellationToken)
    {
        var rows = await consumables.GetAllAsync(cancellationToken);

        return rows
            .GroupBy(r => r.ItemId)
            .Select(g => (Item: g.First().Item, Kind: g.First().Kind, Bonuses: g.OrderBy(b => b.Stat).ToList()))
            .OrderBy(c => c.Kind)
            .ThenByDescending(c => c.Item.ItemLevel)
            .ThenBy(c => c.Item.ItemId)
            .Select(c => new CraftingConsumableDto(
                c.Item.ItemId,
                c.Item.Name,
                c.Item.IconPath,
                c.Item.ItemLevel,
                c.Item.CanBeHQ,
                c.Kind.ToString(),
                c.Bonuses.Select(ToDto).ToList()))
            .ToList();
    }

    private static ConsumableBonusDto ToDto(ConsumableBonus bonus) =>
        new(bonus.Stat.ToString(), bonus.IsRelative, bonus.Value, bonus.Max, bonus.ValueHQ, bonus.MaxHQ);
}
