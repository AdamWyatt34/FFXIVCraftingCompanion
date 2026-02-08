namespace FFXIV.Application.Gear.DTOs;

public sealed record GearSetDto(
    string Job,
    string JobName,
    int Level,
    StatWeightProfileDto Profile,
    List<GearSlotDto> Slots,
    GearStatsDto TotalStats);

public sealed record GearSlotDto(
    string SlotName,
    string SlotKey,
    EquipmentDto? Recommendation,
    List<EquipmentDto> Alternatives,
    bool IsEmpty);

public sealed record EquipmentDto(
    int EquipmentId,
    string Name,
    string IconUrl,
    int ItemLevel,
    int EquipLevel,
    GearStatsDto Stats,
    int MateriaSlots,
    bool CanBeHQ,
    string? HQNote,
    EquipmentSourceDto? Source,
    double WeightedScore,
    int Rarity,
    string RarityName,
    string ClassJobCategory);

public sealed record EquipmentSourceDto(
    string Type,
    string Description,
    int? RecipeId,
    bool HasRecipe,
    string? VendorName,
    int? Cost,
    string? Currency);

public sealed record GearStatsDto(
    int Craftsmanship,
    int Control,
    int CP,
    int Gathering,
    int Perception,
    int GP,
    double TotalWeightedScore);

public sealed record StatWeightProfileDto(
    string Name,
    string DisplayName,
    string Description);

public sealed class CalculateGearStatsRequest
{
    public Dictionary<string, int> Slots { get; set; } = new();
}
