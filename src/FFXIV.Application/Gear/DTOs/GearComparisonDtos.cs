namespace FFXIV.Application.Gear.DTOs;

public sealed record GearComparisonDto(
    GearSetDto GearSet,
    List<SlotComparisonDto> SlotComparisons,
    UpgradeSummaryDto UpgradeSummary);

public sealed record SlotComparisonDto(
    string SlotKey,
    string SlotName,
    string ProfileSlotKey,
    EquipmentDto? CurrentEquipment,
    EquipmentDto? RecommendedEquipment,
    ComparisonStatusDto? Comparison);

public sealed record ComparisonStatusDto(
    string Status,
    bool IsUpgrade,
    double ScoreDifference,
    List<StatDifferenceDto> StatDifferences);

public sealed record StatDifferenceDto(
    string StatName,
    int CurrentValue,
    int RecommendedValue,
    int Difference);

public sealed record UpgradeSummaryDto(
    int UpgradeCount,
    int EquippedCount,
    int AlternativeCount,
    int EmptySlotCount,
    StatDifferenceTotalDto TotalStatDifference);

public sealed record StatDifferenceTotalDto(
    int Craftsmanship,
    int Control,
    int CP,
    int Gathering,
    int Perception,
    int GP,
    int Vitality = 0,
    int Strength = 0,
    int Dexterity = 0,
    int Intelligence = 0,
    int Mind = 0,
    int PhysicalDamage = 0,
    int CriticalHit = 0,
    int Determination = 0,
    int DirectHitRate = 0,
    int Defense = 0,
    int MagicDefense = 0);
