import type { MaterialTreeNodeDto, MaterialTreeResultDto, RecipeDto, RecipeSummaryDto } from "@/types/api";
import type {
  AggregatedIntermediateCraftDto,
  AggregatedMaterialDto,
  CraftPlanItemInput,
  CraftPlanResultDto,
} from "@/types/craft-plan";
import { addRootDemand, compareOrdinalIgnoreCase } from "./materials";

// Port of CraftPlanResolver, CraftPlanMapper and ShoppingListExporter over trees the materials
// port expanded. Proven by goldens/craft-plan.jsonl, exports included.

export interface PlanEntry {
  input: CraftPlanItemInput;
  recipe: RecipeDto;
  tree: MaterialTreeResultDto;
}

export const MAX_PLAN_ITEMS = 20;
export const MAX_PLAN_QUANTITY = 999;

/** CraftPlansController.ValidateItems: the message the API answers 400 with, or null. */
export function validatePlanItems(items: CraftPlanItemInput[] | null | undefined): string | null {
  if (!items || items.length === 0) return "At least one item is required.";
  if (items.length > MAX_PLAN_ITEMS) return `Maximum of ${MAX_PLAN_ITEMS} items per plan.`;
  for (const item of items) {
    if (item.recipeId <= 0) return `Invalid recipe ID: ${item.recipeId}.`;
    if (item.quantity < 1 || item.quantity > MAX_PLAN_QUANTITY)
      return `Quantity must be between 1 and ${MAX_PLAN_QUANTITY} for recipe ${item.recipeId}.`;
  }
  return null;
}

export function aggregateCraftPlan(entries: PlanEntry[]): CraftPlanResultDto {
  const items = entries.map(({ input, recipe, tree }) => ({
    recipeId: recipe.recipeId,
    itemId: recipe.resultItem.itemId,
    itemName: tree.tree.itemName,
    iconUrl: tree.tree.iconUrl,
    quantity: input.quantity,
    craftType: recipe.craftType,
  }));

  // One plan-wide demand map, as CraftPlanResolver: a shared intermediate is crafted for the combined need.
  const intermediateDemand = new Map<number, number>();
  const rawMaterials = new Map<number, AggregatedMaterialDto>();
  const crystals = new Map<number, AggregatedMaterialDto>();
  for (const { recipe, tree } of entries) {
    const rawDemand = addRootDemand(tree.tree, intermediateDemand);
    const added = (itemId: number) => rawDemand.get(itemId) ?? 0;
    for (const item of tree.shoppingList)
      aggregateMaterial(rawMaterials, item, added(item.itemId), recipe.recipeId, tree.tree.itemName);
    for (const item of tree.crystalRequirements)
      aggregateMaterial(crystals, item, added(item.itemId), recipe.recipeId, tree.tree.itemName);
  }

  const recipes = new Map<number, RecipeSummaryDto>();
  const depths = new Map<number, number>();
  for (const { tree } of entries) collectIntermediateRecipes(tree.tree, recipes, depths, true);

  const intermediates = new Map<number, AggregatedIntermediateCraftDto & { maxDepth: number }>();
  for (const { tree } of entries) {
    for (const item of tree.intermediateCrafts) {
      const recipe = recipes.get(item.itemId);
      if (!recipe || intermediates.has(item.itemId)) continue;
      intermediates.set(item.itemId, {
        itemId: item.itemId,
        itemName: item.itemName,
        iconUrl: item.iconUrl,
        totalQuantity: intermediateDemand.get(item.itemId) ?? 0,
        recipeId: recipe.recipeId,
        craftType: recipe.craftType,
        craftTypeName: recipe.craftTypeName,
        recipeLevel: recipe.recipeLevel,
        yieldPerCraft: recipe.yield,
        craftOrder: 0,
        maxDepth: depths.get(item.itemId) ?? 1,
      });
    }
  }

  const sortedIntermediates: AggregatedIntermediateCraftDto[] = [...intermediates.values()]
    .sort((a, b) => b.maxDepth - a.maxDepth || compareOrdinalIgnoreCase(a.itemName, b.itemName))
    .map((craft, i) => ({
      itemId: craft.itemId,
      itemName: craft.itemName,
      iconUrl: craft.iconUrl,
      totalQuantity: craft.totalQuantity,
      recipeId: craft.recipeId,
      craftType: craft.craftType,
      craftTypeName: craft.craftTypeName,
      recipeLevel: craft.recipeLevel,
      yieldPerCraft: craft.yieldPerCraft,
      craftOrder: i + 1,
    }));

  const sortedRaw = [...rawMaterials.values()].sort(
    (a, b) => compareOrdinalIgnoreCase(a.category, b.category) || compareOrdinalIgnoreCase(a.itemName, b.itemName),
  );
  const sortedCrystals = [...crystals.values()].sort((a, b) => compareOrdinalIgnoreCase(a.itemName, b.itemName));
  const maxTreeDepth = Math.max(...entries.map((e) => e.tree.summary.maxTreeDepth));

  return {
    items,
    rawMaterials: sortedRaw,
    crystals: sortedCrystals,
    intermediateCrafts: sortedIntermediates,
    summary: {
      totalPlanItems: items.length,
      totalUniqueRawMaterials: sortedRaw.length,
      totalUniqueCrystals: sortedCrystals.length,
      totalIntermediateCrafts: sortedIntermediates.length,
      maxTreeDepth,
    },
  };
}

function aggregateMaterial(
  lookup: Map<number, AggregatedMaterialDto>,
  item: MaterialTreeResultDto["shoppingList"][number],
  quantity: number,
  recipeId: number,
  recipeItemName: string,
): void {
  const contribution = { recipeId, itemName: recipeItemName, quantity };
  const existing = lookup.get(item.itemId);
  if (existing) {
    existing.totalQuantity += quantity;
    existing.contributions.push(contribution);
  } else {
    lookup.set(item.itemId, {
      itemId: item.itemId,
      itemName: item.itemName,
      iconUrl: item.iconUrl,
      totalQuantity: quantity,
      primarySource: item.primarySource,
      sources: item.sources,
      category: item.category,
      contributions: [contribution],
    });
  }
}

function collectIntermediateRecipes(
  node: MaterialTreeNodeDto,
  recipes: Map<number, RecipeSummaryDto>,
  depths: Map<number, number>,
  isRoot = false,
): void {
  if (!isRoot && !node.isRawMaterial && node.recipe) {
    if (!recipes.has(node.itemId)) recipes.set(node.itemId, node.recipe);
    const existing = depths.get(node.itemId);
    if (existing === undefined || node.depth > existing) depths.set(node.itemId, node.depth);
  }
  for (const child of node.children) collectIntermediateRecipes(child, recipes, depths);
}

export type ExportFormat = "text" | "json" | "teamcraft";

export function exportCraftPlanContent(result: CraftPlanResultDto, format: ExportFormat): string {
  switch (format) {
    case "json":
      return JSON.stringify(result, null, 2);
    case "teamcraft":
      return exportAsTeamcraft(result);
    default:
      return exportAsText(result);
  }
}

function exportAsText(result: CraftPlanResultDto): string {
  const lines: string[] = [];
  lines.push("=== Items to Craft ===");
  for (const item of result.items) lines.push(`  ${item.itemName} x${item.quantity} (${item.craftType})`);
  lines.push("");

  if (result.intermediateCrafts.length > 0) {
    lines.push("=== Intermediate Crafts ===");
    for (const craft of result.intermediateCrafts)
      lines.push(`  ${craft.craftOrder}. ${craft.itemName} x${craft.totalQuantity} (${craft.craftType}, Lv.${craft.recipeLevel})`);
    lines.push("");
  }

  lines.push("=== Raw Materials ===");
  for (const mat of result.rawMaterials) {
    const sourceDesc = mat.sources.length > 0 ? mat.sources[0].description : "";
    lines.push(`  ${mat.itemName} x${mat.totalQuantity}` + (sourceDesc === "" ? "" : ` — ${sourceDesc}`));
  }
  lines.push("");

  if (result.crystals.length > 0) {
    lines.push("=== Crystals ===");
    for (const crystal of result.crystals) lines.push(`  ${crystal.itemName} x${crystal.totalQuantity}`);
    lines.push("");
  }

  lines.push("=== Summary ===");
  lines.push(`  Plan Items: ${result.summary.totalPlanItems}`);
  lines.push(`  Raw Materials: ${result.summary.totalUniqueRawMaterials} types`);
  lines.push(`  Crystals: ${result.summary.totalUniqueCrystals} types`);
  lines.push(`  Intermediate Crafts: ${result.summary.totalIntermediateCrafts}`);

  return lines.join("\n") + "\n";
}

function exportAsTeamcraft(result: CraftPlanResultDto): string {
  const lines: string[] = [];
  for (const item of result.items) lines.push(`${item.itemId},${item.quantity}`);
  for (const craft of result.intermediateCrafts) lines.push(`${craft.itemId},${craft.totalQuantity}`);
  for (const mat of result.rawMaterials) lines.push(`${mat.itemId},${mat.totalQuantity}`);
  for (const crystal of result.crystals) lines.push(`${crystal.itemId},${crystal.totalQuantity}`);
  return lines.length === 0 ? "" : lines.join("\n") + "\n";
}
