export type GearJob =
  | "CRP" | "BSM" | "ARM" | "GSM" | "LTW" | "WVR" | "ALC" | "CUL"
  | "MIN" | "BTN" | "FSH"
  | "PLD" | "WAR" | "DRK" | "GNB"
  | "WHM" | "SCH" | "AST" | "SGE"
  | "MNK" | "DRG" | "NIN" | "SAM" | "RPR" | "VPR"
  | "BRD" | "MCH" | "DNC"
  | "BLM" | "SMN" | "RDM" | "PCT";

export type JobCategory = "DoH" | "DoL" | "Tank" | "Healer" | "MeleeDPS" | "RangedDPS" | "MagicDPS";

export interface JobInfo {
  abbreviation: GearJob;
  fullName: string;
  category: JobCategory;
  classJobId: number;
  colorClass: string;
  hasOffhand: boolean;
}

export const DOH_JOBS: GearJob[] = ["CRP", "BSM", "ARM", "GSM", "LTW", "WVR", "ALC", "CUL"];
export const DOL_JOBS: GearJob[] = ["MIN", "BTN", "FSH"];
export const TANK_JOBS: GearJob[] = ["PLD", "WAR", "DRK", "GNB"];
export const HEALER_JOBS: GearJob[] = ["WHM", "SCH", "AST", "SGE"];
export const MELEE_DPS_JOBS: GearJob[] = ["MNK", "DRG", "NIN", "SAM", "RPR", "VPR"];
export const RANGED_DPS_JOBS: GearJob[] = ["BRD", "MCH", "DNC"];
export const MAGIC_DPS_JOBS: GearJob[] = ["BLM", "SMN", "RDM", "PCT"];

export const COMBAT_JOBS: GearJob[] = [...TANK_JOBS, ...HEALER_JOBS, ...MELEE_DPS_JOBS, ...RANGED_DPS_JOBS, ...MAGIC_DPS_JOBS];

export const JOB_INFO: Record<GearJob, JobInfo> = {
  // DoH
  CRP: { abbreviation: "CRP", fullName: "Carpenter",     category: "DoH", classJobId: 8,  colorClass: "text-craft-crp", hasOffhand: true },
  BSM: { abbreviation: "BSM", fullName: "Blacksmith",    category: "DoH", classJobId: 9,  colorClass: "text-craft-bsm", hasOffhand: true },
  ARM: { abbreviation: "ARM", fullName: "Armorer",       category: "DoH", classJobId: 10, colorClass: "text-craft-arm", hasOffhand: true },
  GSM: { abbreviation: "GSM", fullName: "Goldsmith",     category: "DoH", classJobId: 11, colorClass: "text-craft-gsm", hasOffhand: true },
  LTW: { abbreviation: "LTW", fullName: "Leatherworker", category: "DoH", classJobId: 12, colorClass: "text-craft-ltw", hasOffhand: true },
  WVR: { abbreviation: "WVR", fullName: "Weaver",        category: "DoH", classJobId: 13, colorClass: "text-craft-wvr", hasOffhand: true },
  ALC: { abbreviation: "ALC", fullName: "Alchemist",     category: "DoH", classJobId: 14, colorClass: "text-craft-alc", hasOffhand: true },
  CUL: { abbreviation: "CUL", fullName: "Culinarian",    category: "DoH", classJobId: 15, colorClass: "text-craft-cul", hasOffhand: false },
  // DoL
  MIN: { abbreviation: "MIN", fullName: "Miner",         category: "DoL", classJobId: 16, colorClass: "text-emerald-400", hasOffhand: true },
  BTN: { abbreviation: "BTN", fullName: "Botanist",      category: "DoL", classJobId: 17, colorClass: "text-emerald-400", hasOffhand: true },
  FSH: { abbreviation: "FSH", fullName: "Fisher",        category: "DoL", classJobId: 18, colorClass: "text-blue-400",    hasOffhand: false },
  // Tank
  PLD: { abbreviation: "PLD", fullName: "Paladin",       category: "Tank", classJobId: 19, colorClass: "text-blue-300", hasOffhand: true },
  WAR: { abbreviation: "WAR", fullName: "Warrior",       category: "Tank", classJobId: 21, colorClass: "text-blue-300", hasOffhand: false },
  DRK: { abbreviation: "DRK", fullName: "Dark Knight",   category: "Tank", classJobId: 32, colorClass: "text-blue-300", hasOffhand: false },
  GNB: { abbreviation: "GNB", fullName: "Gunbreaker",    category: "Tank", classJobId: 37, colorClass: "text-blue-300", hasOffhand: false },
  // Healer
  WHM: { abbreviation: "WHM", fullName: "White Mage",    category: "Healer", classJobId: 24, colorClass: "text-green-300", hasOffhand: false },
  SCH: { abbreviation: "SCH", fullName: "Scholar",       category: "Healer", classJobId: 28, colorClass: "text-green-300", hasOffhand: false },
  AST: { abbreviation: "AST", fullName: "Astrologian",   category: "Healer", classJobId: 33, colorClass: "text-green-300", hasOffhand: false },
  SGE: { abbreviation: "SGE", fullName: "Sage",          category: "Healer", classJobId: 40, colorClass: "text-green-300", hasOffhand: false },
  // Melee DPS
  MNK: { abbreviation: "MNK", fullName: "Monk",          category: "MeleeDPS", classJobId: 20, colorClass: "text-red-300", hasOffhand: false },
  DRG: { abbreviation: "DRG", fullName: "Dragoon",       category: "MeleeDPS", classJobId: 22, colorClass: "text-red-300", hasOffhand: false },
  NIN: { abbreviation: "NIN", fullName: "Ninja",         category: "MeleeDPS", classJobId: 30, colorClass: "text-red-300", hasOffhand: false },
  SAM: { abbreviation: "SAM", fullName: "Samurai",       category: "MeleeDPS", classJobId: 34, colorClass: "text-red-300", hasOffhand: false },
  RPR: { abbreviation: "RPR", fullName: "Reaper",        category: "MeleeDPS", classJobId: 39, colorClass: "text-red-300", hasOffhand: false },
  VPR: { abbreviation: "VPR", fullName: "Viper",         category: "MeleeDPS", classJobId: 41, colorClass: "text-red-300", hasOffhand: false },
  // Ranged DPS
  BRD: { abbreviation: "BRD", fullName: "Bard",          category: "RangedDPS", classJobId: 23, colorClass: "text-red-300", hasOffhand: false },
  MCH: { abbreviation: "MCH", fullName: "Machinist",     category: "RangedDPS", classJobId: 31, colorClass: "text-red-300", hasOffhand: false },
  DNC: { abbreviation: "DNC", fullName: "Dancer",        category: "RangedDPS", classJobId: 38, colorClass: "text-red-300", hasOffhand: false },
  // Magic DPS
  BLM: { abbreviation: "BLM", fullName: "Black Mage",    category: "MagicDPS", classJobId: 25, colorClass: "text-purple-300", hasOffhand: false },
  SMN: { abbreviation: "SMN", fullName: "Summoner",      category: "MagicDPS", classJobId: 27, colorClass: "text-purple-300", hasOffhand: false },
  RDM: { abbreviation: "RDM", fullName: "Red Mage",      category: "MagicDPS", classJobId: 35, colorClass: "text-purple-300", hasOffhand: false },
  PCT: { abbreviation: "PCT", fullName: "Pictomancer",   category: "MagicDPS", classJobId: 42, colorClass: "text-purple-300", hasOffhand: false },
};

export const LEVEL_PRESETS = [50, 60, 70, 80, 90, 100] as const;

export const PROFILE_DISPLAY_NAMES: Record<string, string> = {
  Balanced: "Balanced",
  ProgressFocused: "Progress",
  QualityFocused: "Quality",
  CpFocused: "CP Focus",
  GatheringBalanced: "Balanced",
  PerceptionFocused: "Perception",
  TankBalanced: "Tank Balanced",
  HealerBalanced: "Healer Balanced",
  MeleeDPSBalanced: "Melee DPS",
  RangedDPSBalanced: "Ranged DPS",
  MagicDPSBalanced: "Magic DPS",
};

export const RARITY_COLOR_CLASS: Record<string, string> = {
  Common: "text-rarity-common",
  Uncommon: "text-rarity-uncommon",
  Rare: "text-rarity-rare",
  Relic: "text-rarity-relic",
};

export function getJobIconUrl(classJobId: number): string {
  const padded = String(classJobId).padStart(2, "0");
  return `https://v2.xivapi.com/api/asset/ui/icon/062000/0620${padded}_hr1.tex?format=png`;
}

export function isCombatJob(category: JobCategory): boolean {
  return category === "Tank" || category === "Healer"
    || category === "MeleeDPS" || category === "RangedDPS" || category === "MagicDPS";
}

// Slot grouping for GearGrid layout sections
export type SlotGroup = "Weapons" | "Armor" | "Accessories";

export const SLOT_GROUPS: Record<SlotGroup, string[]> = {
  Weapons: ["MainHand", "OffHand"],
  Armor: ["Head", "Body", "Hands", "Legs", "Feet"],
  Accessories: ["Earring", "Necklace", "Bracelet", "Ring"],
};

const ACCESSORY_KEYS = new Set(["Earring", "Necklace", "Bracelet", "Ring"]);

export function getSlotGroup(slotKey: string): SlotGroup {
  if (slotKey === "MainHand" || slotKey === "OffHand") return "Weapons";
  if (ACCESSORY_KEYS.has(slotKey)) return "Accessories";
  return "Armor";
}
