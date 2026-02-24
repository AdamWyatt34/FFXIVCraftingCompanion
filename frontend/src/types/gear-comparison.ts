import type { GearSetDto, EquipmentDto } from "./gear";

export interface GearComparisonDto {
  gearSet: GearSetDto;
  slotComparisons: SlotComparisonDto[];
  upgradeSummary: UpgradeSummaryDto;
}

export interface SlotComparisonDto {
  slotKey: string;
  slotName: string;
  profileSlotKey: string;
  currentEquipment: EquipmentDto | null;
  recommendedEquipment: EquipmentDto | null;
  comparison: ComparisonStatusDto | null;
}

export interface ComparisonStatusDto {
  status: "Upgrade" | "Equipped" | "Alternative" | "Empty";
  isUpgrade: boolean;
  scoreDifference: number;
  statDifferences: StatDifferenceDto[];
}

export interface StatDifferenceDto {
  statName: string;
  currentValue: number;
  recommendedValue: number;
  difference: number;
}

export interface UpgradeSummaryDto {
  upgradeCount: number;
  equippedCount: number;
  alternativeCount: number;
  emptySlotCount: number;
  totalStatDifference: StatDifferenceTotalDto;
}

export interface StatDifferenceTotalDto {
  craftsmanship: number;
  control: number;
  cp: number;
  gathering: number;
  perception: number;
  gp: number;
  vitality: number;
  strength: number;
  dexterity: number;
  intelligence: number;
  mind: number;
  physicalDamage: number;
  criticalHit: number;
  determination: number;
  directHitRate: number;
  defense: number;
  magicDefense: number;
}
