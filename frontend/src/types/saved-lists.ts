export interface SavedListSummaryDto {
  id: number;
  name: string;
  type: string;
  isPinned: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface SavedListDto extends SavedListSummaryDto {
  data: unknown;
}

export interface FavoriteRecipeDto {
  recipeId: number;
  itemName: string;
  iconUrl: string;
  craftType: string;
  recipeLevel: number;
  note: string | null;
  createdAt: string;
}

export interface FavoriteStatusDto {
  isFavorited: boolean;
}

// Typed data interfaces for saved list cross-linking
export interface ShoppingListData {
  recipeId: number;
  quantity: number;
  notes?: string;
  items: unknown[];
}

export interface GearSetData {
  job: string;
  level?: number;
  slots: Record<string, number>;
  stats?: unknown;
}

export interface CraftingPlanData {
  items: { recipeId: number; quantity: number }[];
}
