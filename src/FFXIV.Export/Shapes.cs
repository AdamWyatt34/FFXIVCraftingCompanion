using FFXIV.Application.DTOs;
using FFXIV.Application.Gear.DTOs;
using FFXIV.Application.Journey.DTOs;
using FFXIV.Application.Recipes.DTOs;
using FFXIV.Domain.Enums;

namespace FFXIV.Export;

// The file shapes the frontend reads. Every DTO inside is the API's own response type, so a
// client written against the API keeps working against the files.

public sealed record Manifest(
    int SchemaVersion,
    string Version,
    string GameVersion,
    string GameVersionKey,
    DateTimeOffset ExportedAt,
    int BucketSize,
    ManifestCounts Counts);

public sealed record ManifestCounts(int Recipes, int Items, int Equipment, int Jobs);

public sealed record RecipeExport(
    RecipeDto Recipe,
    RecipeCraftingParametersDto CraftingParameters,
    MaterialTreeResultDto Materials);

public sealed record ItemExport(
    ItemDto Item,
    ItemContextDto Context,
    IReadOnlyList<GatheringNodeExport> GatheringNodes);

public sealed record GatheringNodeExport(
    GatheringType GatheringType,
    string ClassJob,
    int Level,
    int Stars,
    string ZoneName,
    string? AreaName,
    float? CoordinateX,
    float? CoordinateY,
    bool IsTimed,
    string? SpawnTimes,
    bool IsHidden,
    NodeType NodeType);

public sealed record GearJobExport(
    string Job,
    string JobName,
    IReadOnlyList<StatWeightProfileExport> Profiles,
    IReadOnlyList<EquipmentDto> Equipment);

public sealed record StatWeightProfileExport(
    string Name,
    string DisplayName,
    string Description,
    IReadOnlyDictionary<string, double> Weights);

public sealed record SearchIndexEntry(
    int ItemId,
    string Name,
    string IconUrl,
    int ItemLevel,
    string Category,
    bool CanBeHQ,
    bool IsEquipment,
    IReadOnlyList<SearchIndexRecipe> Recipes);

public sealed record SearchIndexRecipe(int RecipeId, string CraftType);
