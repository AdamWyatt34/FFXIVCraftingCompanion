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

// Typed data interfaces for saved list data

export interface ShoppingListMaterial {
  itemId: number;
  itemName: string;
  quantity: number;
  category: string;
}

export interface ShoppingListData {
  sourceRecipeId: number;
  sourceItemName: string;
  quantity: number;
  materials: ShoppingListMaterial[];
  crystals: ShoppingListMaterial[];
  savedAt: string;
  notes?: string;
}

export interface GearSetData {
  job: string;
  level?: number;
  slots: Record<string, number>;
  stats?: {
    craftsmanship?: number;
    control?: number;
    cp?: number;
    gathering?: number;
    perception?: number;
    gp?: number;
  };
}

export interface CraftingPlanData {
  items: { recipeId: number; quantity: number }[];
}
