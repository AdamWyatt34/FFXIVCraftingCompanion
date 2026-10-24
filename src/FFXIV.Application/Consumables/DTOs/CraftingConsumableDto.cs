namespace FFXIV.Application.Consumables.DTOs;

public sealed record CraftingConsumableDto(
    int ItemId,
    string Name,
    string IconUrl,
    int ItemLevel,
    bool CanBeHQ,
    string Kind,
    IReadOnlyList<ConsumableBonusDto> Bonuses);

public sealed record ConsumableBonusDto(
    string Stat,
    bool IsRelative,
    int Value,
    int Max,
    int ValueHQ,
    int MaxHQ);
