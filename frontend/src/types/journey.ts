import type { RecipeDto, MaterialTreeResultDto } from "./api";

export interface CraftingJourneyDto {
  recipe: RecipeDto;
  materials: MaterialTreeResultDto;
  sufficiency: StatSufficiencyResultDto | null;
  gearUpgrades: GearUpgradeSuggestionDto[];
  itemContext: ItemContextDto;
}

export interface StatSufficiencyResultDto {
  recipeId: number;
  meetsMinimumRequirements: boolean;
  craftsmanshipSufficient: boolean;
  controlSufficient: boolean;
  levelSufficient: boolean;
  craftsmanshipDeficit: number;
  controlDeficit: number;
  levelDeficit: number;
  estimatedHQPercent: number;
  estimatedQualityPercent: number;
  estimatedSteps: number;
  canComplete: boolean;
  recommendation: StatRecommendation;
  suggestedGearUpgrades: GearUpgradeSuggestionDto[];
}

export type StatRecommendation =
  | "StatsExcellent"
  | "StatsGood"
  | "StatsAdequate"
  | "UpgradeGear";

export interface GearUpgradeSuggestionDto {
  slot: string;
  currentItem: string | null;
  suggestedItem: string;
  suggestedEquipmentId: number;
  craftsmanshipGain: number;
  controlGain: number;
  cpGain: number;
  source: string | null;
  recipeId: number | null;
}

export interface ItemContextDto {
  itemId: number;
  itemName: string;
  iconUrl: string;
  recipes: RecipeLinkDto[];
  canSimulate: boolean;
  canSolve: boolean;
  usedInRecipes: RecipeLinkDto[];
  isEquipment: boolean;
  equipmentInfo: EquipmentLinkDto | null;
  gatheringInfo: GatheringLinkDto[];
  vendorSources: VendorLinkDto[];
}

export interface RecipeLinkDto {
  recipeId: number;
  resultItemName: string;
  iconUrl: string;
  craftType: string;
  level: number;
  stars: number;
}

export interface EquipmentLinkDto {
  equipmentId: number;
  equipSlot: string;
  craftsmanship: number;
  control: number;
  cp: number;
  gathering: number;
  perception: number;
  gp: number;
}

export interface GatheringLinkDto {
  zone: string;
  coordinates: string | null;
  gatheringType: string;
  gatheringClass: string;
  gatheringLevel: number;
  isTimed: boolean;
  spawnTimes: string | null;
}

export interface VendorLinkDto {
  npcName: string;
  zone: string;
  cost: number;
  currency: string;
}
