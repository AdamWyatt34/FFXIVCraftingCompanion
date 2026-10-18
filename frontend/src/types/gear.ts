export interface GearStatsDto {
  craftsmanship: number;
  control: number;
  cp: number;
  gathering: number;
  perception: number;
  gp: number;
  // Combat stats
  vitality: number;
  strength: number;
  dexterity: number;
  intelligence: number;
  mind: number;
  piety: number;
  tenacity: number;
  directHitRate: number;
  criticalHit: number;
  determination: number;
  skillSpeed: number;
  spellSpeed: number;
  physicalDamage: number;
  defense: number;
  magicDefense: number;
  totalWeightedScore: number;
}

export interface EquipmentSourceDto {
  type: string;
  description: string;
  recipeId: number | null;
  hasRecipe: boolean;
  vendorName: string | null;
  cost: number | null;
  currency: string | null;
}

export interface EquipmentDto {
  equipmentId: number;
  name: string;
  iconUrl: string;
  itemLevel: number;
  equipLevel: number;
  stats: GearStatsDto;
  materiaSlots: number;
  canBeHQ: boolean;
  hqNote: string | null;
  source: EquipmentSourceDto | null;
  weightedScore: number;
  rarity: number;
  rarityName: string;
  classJobCategory: string;
}

export interface StatWeightProfileDto {
  name: string;
  displayName: string;
  description: string;
}

export interface GearSlotDto {
  slotName: string;
  slotKey: string;
  recommendation: EquipmentDto | null;
  alternatives: EquipmentDto[];
  isEmpty: boolean;
}

export interface GearSetDto {
  job: string;
  jobName: string;
  level: number;
  profile: StatWeightProfileDto;
  slots: GearSlotDto[];
  totalStats: GearStatsDto;
}

