export interface CraftPlanItemInput {
  recipeId: number;
  quantity: number;
}

export interface CraftPlanDto {
  id: string;
  name: string;
  userId: string;
  items: CraftPlanItemInput[];
  createdAt: string;
  updatedAt: string;
}

export interface CraftPlanResultDto {
  items: CraftPlanItemResultDto[];
  rawMaterials: AggregatedMaterialDto[];
  crystals: AggregatedMaterialDto[];
  intermediateCrafts: AggregatedIntermediateCraftDto[];
  summary: CraftPlanSummaryDto;
}

export interface CraftPlanItemResultDto {
  recipeId: number;
  itemId: number;
  itemName: string;
  iconUrl: string;
  quantity: number;
  craftType: string;
}

export interface AggregatedMaterialDto {
  itemId: number;
  itemName: string;
  iconUrl: string;
  totalQuantity: number;
  primarySource: string;
  sources: import("./api").ItemSourceDto[];
  sortCategory: string;
  contributions: MaterialContributionDto[];
}

export interface AggregatedIntermediateCraftDto {
  itemId: number;
  itemName: string;
  iconUrl: string;
  totalQuantity: number;
  recipeId: number;
  craftType: string;
  recipeLevel: number;
  yieldPerCraft: number;
  maxDepth: number;
  craftOrder: number;
}

export interface MaterialContributionDto {
  recipeId: number;
  itemName: string;
  quantity: number;
}

export interface CraftPlanSummaryDto {
  totalPlanItems: number;
  totalUniqueRawMaterials: number;
  totalUniqueCrystals: number;
  totalIntermediateCrafts: number;
  maxTreeDepth: number;
}

export interface ExportResultDto {
  format: string;
  content: string;
}

export interface ResolveCraftPlanRequest {
  items: CraftPlanItemInput[];
}

export interface ExportCraftPlanRequest {
  items: CraftPlanItemInput[];
  format: string;
}

export interface CreateCraftPlanRequest {
  name: string;
  items: CraftPlanItemInput[];
}

export interface UpdateCraftPlanRequest {
  name?: string;
  items?: CraftPlanItemInput[];
}

export interface CraftPlanWithGatheringDto {
  plan: CraftPlanResultDto;
  gatheringGuide: import("./gathering").GatheringGuideDto;
}
