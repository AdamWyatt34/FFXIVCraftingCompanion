using System.Text.Json.Serialization;
using FFXIV.Application.DTOs;
using FFXIV.Application.Gear.DTOs;
using FFXIV.Application.Journey.DTOs;
using FFXIV.Application.Recipes.DTOs;
using FFXIV.Domain.Enums;

namespace FFXIV.Export;

// The file shapes the frontend reads. Every DTO inside is the API's own response type, so a
// client written against the API keeps working against the files. Where a shape is new (the
// material skeleton, the search index) the golden tests pin how the API DTO is rebuilt from it.

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
    MaterialGraphExport Materials);

// The resolved tree at quantity 1 with per-item data factored out: node payloads repeat across
// occurrences and across recipes, the skeleton does not. Crystals (ids 2-19) are omitted from
// Items and live once in materials/crystals.json.
public sealed record MaterialGraphExport(
    MaterialSkeletonNode Tree,
    IReadOnlyDictionary<int, MaterialItemExport> Items);

public sealed record MaterialSkeletonNode(
    int ItemId,
    int Quantity,
    IReadOnlyList<MaterialSkeletonNode> Children,
    bool Truncated = false);

public sealed record MaterialItemExport(
    string ItemName,
    string IconUrl,
    bool IsRawMaterial,
    string PrimarySource,
    List<ItemSourceDto> Sources,
    RecipeSummaryDto? Recipe,
    string Category,
    bool AlsoAvailableFromVendor);

public sealed record ItemExport(
    ItemDto Item,
    ItemContextDto Context,
    IReadOnlyList<GatheringNodeExport> GatheringNodes,
    EquipmentDto? Equipment);

public sealed record GatheringNodeExport(
    [property: JsonConverter(typeof(JsonStringEnumConverter))] GatheringType GatheringType,
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
    [property: JsonConverter(typeof(JsonStringEnumConverter))] NodeType NodeType);

// Per job: the stat-weight profiles and, per slot, every piece the recommendation query can
// return for some character level (the top CandidateLimit by item level among pieces equippable
// at that level), in the query's own order.
public sealed record GearJobExport(
    string Job,
    string JobName,
    IReadOnlyList<StatWeightProfileExport> Profiles,
    IReadOnlyDictionary<string, IReadOnlyList<EquipmentDto>> CandidatesBySlot);

public sealed record StatWeightProfileExport(
    string Name,
    string DisplayName,
    string Description,
    IReadOnlyDictionary<string, double> Weights);

public sealed record SearchIndexEntry(
    int ItemId,
    string Name,
    int Icon,
    int ItemLevel,
    string Category,
    bool CanBeHQ,
    IReadOnlyList<SearchIndexRecipe> Recipes,
    SearchIndexEquipment? Equipment);

public sealed record SearchIndexRecipe(int RecipeId, string CraftType);

public sealed record SearchIndexEquipment(string Slot, int EquipLevel, string ClassJobCategory);
