export interface ItemDto {
  itemId: number;
  name: string;
  iconUrl: string;
  itemLevel: number;
  category: ItemCategory;
  canBeHQ: boolean;
  recipeId: number | null;
  isEquipment: boolean;
}

export interface RecipeDto {
  recipeId: number;
  resultItem: ItemDto;
  craftType: CraftType;
  recipeLevel: number;
  difficulty: number;
  quality: number;
  durability: number;
  requiredCraftsmanship: number;
  requiredControl: number;
  yield: number;
  ingredients: RecipeIngredientDto[];
  materialSummary: MaterialSummaryDto | null;
  equipmentInfo: RecipeEquipmentInfoDto | null;
}

export interface RecipeEquipmentInfoDto {
  equipmentId: number;
  equipSlot: string;
  classJobCategory: string;
  craftsmanship: number;
  control: number;
  cp: number;
  gathering: number;
  perception: number;
  gp: number;
}

export interface RecipeIngredientDto {
  itemId: number;
  itemName: string;
  iconUrl: string;
  quantity: number;
  canBeHQ: boolean;
}

/** The crafting engine's view of a recipe (GET /api/recipes/{id}/crafting-parameters). */
export interface RecipeCraftingParametersDto {
  recipeId: number;
  /** The recipe level (rlvl), not the class job level. */
  recipeLevel: number;
  difficulty: number;
  quality: number;
  durability: number;
  progressDivider: number;
  progressModifier: number;
  qualityDivider: number;
  qualityModifier: number;
  classJobLevel: number;
  stars: number;
  isExpert: boolean;
  conditionsFlag: number;
  suggestedCraftsmanship: number;
  suggestedControl: number;
  materialQualityFactor: number;
  ingredients: RecipeCraftingIngredientDto[];
}

export interface RecipeCraftingIngredientDto {
  itemId: number;
  quantity: number;
  canBeHQ: boolean;
}

export interface ItemSearchResultDto {
  items: ItemDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface SyncStatusDto {
  entityType: string;
  lastSyncedAt: string | null;
  lastPatchVersion: string | null;
  recordCount: number;
  isSyncCurrentlyRunning: boolean;
}

export type ItemCategory =
  | "Material"
  | "Craftable"
  | "Gatherable"
  | "Vendor"
  | "Drop"
  | "Scrip"
  | "Other";

export type CraftType =
  "CRP" | "BSM" | "ARM" | "GSM" | "LTW" | "WVR" | "ALC" | "CUL";

export type EquipSlot =
  | "Head"
  | "Body"
  | "Hands"
  | "Legs"
  | "Feet"
  | "MainHand"
  | "OffHand"
  | "Earring"
  | "Necklace"
  | "Bracelet"
  | "Ring";

export interface ItemSourceDto {
  type: string;
  description: string;
  zone: string | null;
  npc: string | null;
  cost: number | null;
  currency: string | null;
  gatheringLevel: number | null;
  gatheringClass: string | null;
}

export interface RecipeSummaryDto {
  recipeId: number;
  craftType: string;
  craftTypeName: string;
  recipeLevel: number;
  requiredCraftsmanship: number;
  requiredControl: number;
  yield: number;
}

export interface MaterialTreeNodeDto {
  itemId: number;
  itemName: string;
  iconUrl: string;
  quantity: number;
  isRawMaterial: boolean;
  primarySource: string;
  sources: ItemSourceDto[];
  recipe: RecipeSummaryDto | null;
  children: MaterialTreeNodeDto[];
  depth: number;
}

export interface ShoppingListItemDto {
  itemId: number;
  itemName: string;
  iconUrl: string;
  totalQuantity: number;
  primarySource: string;
  sources: ItemSourceDto[];
  category: string;
  alsoAvailableFromVendor: boolean;
}

export interface IntermediateCraftDto {
  itemId: number;
  itemName: string;
  iconUrl: string;
  craftType: string;
  craftTypeName: string;
  recipeId: number;
  quantity: number;
  recipeLevel: number;
  craftOrder: number;
}

export interface TreeSummaryDto {
  totalUniqueRawMaterials: number;
  totalUniqueCrystals: number;
  totalIntermediateCrafts: number;
  maxTreeDepth: number;
  requestedQuantity: number;
}

export interface MaterialTreeResultDto {
  tree: MaterialTreeNodeDto;
  shoppingList: ShoppingListItemDto[];
  intermediateCrafts: IntermediateCraftDto[];
  crystalRequirements: ShoppingListItemDto[];
  summary: TreeSummaryDto;
}

export interface ShoppingListResultDto {
  shoppingList: ShoppingListItemDto[];
  crystalRequirements: ShoppingListItemDto[];
  intermediateCrafts: IntermediateCraftDto[];
  summary: TreeSummaryDto;
}

export interface MaterialSummaryDto {
  rawMaterialCount: number;
  intermediateCraftCount: number;
  crystalTypeCount: number;
  treeDepth: number;
}

export interface ApiError {
  error: string;
  traceId?: string;
  timestamp?: string;
}
