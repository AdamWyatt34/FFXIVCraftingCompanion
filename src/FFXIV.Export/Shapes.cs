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

public sealed record ManifestCounts(int Recipes, int Items, int Equipment, int Jobs, int Consumables);

public sealed record RecipeExport(
    RecipeDto Recipe,
    RecipeCraftingParametersDto CraftingParameters,
    MaterialGraphExport Materials);

// The resolved tree with per-item data factored out: node payloads repeat across occurrences
// and across recipes, the skeleton does not. PerCraft is the ingredient quantity one craft of
// the parent needs (1 at the root), so the tree expands for any requested quantity. Crystals
// (ids 2-19) are omitted from Items and live once in materials/crystals.json.
public sealed record MaterialGraphExport(
    MaterialSkeletonNode Tree,
    IReadOnlyDictionary<int, MaterialItemExport> Items);

public sealed record MaterialSkeletonNode(
    int ItemId,
    int PerCraft,
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

/// <param name="Level">The class job level from the recipe level table (not the rlvl), so the
/// frontend's quick filters can cut by expansion.</param>
public sealed record SearchIndexRecipe(int RecipeId, string CraftType, int Level);

public sealed record SearchIndexEquipment(string Slot, int EquipLevel, string ClassJobCategory);

/// <summary>
/// search/compact.json: the same index as search/items.json as interned tables and typed columns,
/// about a third of the bytes, which the frontend parses and searches in a worker
/// (src/lib/static-data/search-compact.ts rebuilds <see cref="SearchIndexEntry"/> from it).
/// Column i describes item i.
/// </summary>
/// <param name="Recipes">Per item, [recipeId, index into CraftTypes, class job level] triples.</param>
/// <param name="Equipment">Per item, [index into Slots, equip level, index into ClassJobCategories], or null.</param>
public sealed record CompactSearchIndex(
    IReadOnlyList<string> CraftTypes,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> Slots,
    IReadOnlyList<string> ClassJobCategories,
    IReadOnlyList<int> ItemIds,
    IReadOnlyList<string> Names,
    IReadOnlyList<int> Icons,
    IReadOnlyList<int> ItemLevels,
    IReadOnlyList<int> CategoryIndexes,
    IReadOnlyList<bool> CanBeHQ,
    IReadOnlyList<IReadOnlyList<int[]>> Recipes,
    IReadOnlyList<int[]?> Equipment);
