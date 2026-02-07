export interface ItemDto {
  itemId: number;
  name: string;
  iconUrl: string;
  itemLevel: number;
  category: ItemCategory;
  canBeHQ: boolean;
  recipeId: number | null;
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
  ingredients: RecipeIngredientDto[];
}

export interface RecipeIngredientDto {
  itemId: number;
  itemName: string;
  iconUrl: string;
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
  | "CRP"
  | "BSM"
  | "ARM"
  | "GSM"
  | "LTW"
  | "WVR"
  | "ALC"
  | "CUL";

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

export interface ApiError {
  error: string;
  traceId?: string;
  timestamp?: string;
}
