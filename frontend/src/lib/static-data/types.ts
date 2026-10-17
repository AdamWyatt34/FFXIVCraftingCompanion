import type {
  ItemDto,
  ItemSourceDto,
  RecipeCraftingParametersDto,
  RecipeDto,
  RecipeSummaryDto,
} from "@/types/api";
import type { ItemContextDto } from "@/types/journey";
import type { EquipmentDto } from "@/types/gear";

// Mirrors src/FFXIV.Export/Shapes.cs. The DTOs inside are the API's response types.

export interface Manifest {
  schemaVersion: number;
  version: string;
  gameVersion: string;
  gameVersionKey: string;
  exportedAt: string;
  bucketSize: number;
  counts: { recipes: number; items: number; equipment: number; jobs: number };
}

export interface RecipeExport {
  recipe: RecipeDto;
  craftingParameters: RecipeCraftingParametersDto;
  materials: MaterialGraphExport;
}

export interface MaterialGraphExport {
  tree: MaterialSkeletonNode;
  items: Record<string, MaterialItemExport>;
}

export interface MaterialSkeletonNode {
  itemId: number;
  /** Ingredient quantity one craft of the parent needs; 1 at the root. */
  perCraft: number;
  children: MaterialSkeletonNode[];
  truncated: boolean;
}

export interface MaterialItemExport {
  itemName: string;
  iconUrl: string;
  isRawMaterial: boolean;
  primarySource: string;
  sources: ItemSourceDto[];
  recipe: RecipeSummaryDto | null;
  category: string;
  alsoAvailableFromVendor: boolean;
}

export interface GatheringNodeExport {
  gatheringType: string;
  classJob: string;
  level: number;
  stars: number;
  zoneName: string;
  areaName: string | null;
  coordinateX: number | null;
  coordinateY: number | null;
  isTimed: boolean;
  spawnTimes: string | null;
  isHidden: boolean;
  nodeType: string;
}

export interface ItemExport {
  item: ItemDto;
  context: ItemContextDto;
  gatheringNodes: GatheringNodeExport[];
  equipment: EquipmentDto | null;
}

export interface SearchIndexEntry {
  itemId: number;
  name: string;
  icon: number;
  itemLevel: number;
  category: string;
  canBeHQ: boolean;
  recipes: { recipeId: number; craftType: string }[];
  equipment: { slot: string; equipLevel: number; classJobCategory: string } | null;
}

export type Bucket<T> = Record<string, T>;
